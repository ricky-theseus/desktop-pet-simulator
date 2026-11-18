using System.Reflection;
using System.Runtime.Loader;

namespace DesktopPet.Core;

public sealed record CodeModPackage(string PackageDirectory, ContentModManifest Manifest);

/// <summary>代码 Mod 环境 v1：资源目录按只读约定使用，独立数据目录可写。</summary>
public interface IModEnvironment
{
    string ApiVersion { get; }
    string ModDirectory { get; }
    string DataDirectory { get; }
}

/// <summary>主线程逐帧服务 v1；delta 单位为秒，订阅必须随 Mod 停用释放。</summary>
public interface IModUpdateService
{
    string ApiVersion { get; }
    IDisposable Subscribe(Action<double> update);
}

/// <summary>可选运行错误查询 v1，错误在成功重试后清除。</summary>
public interface IModRuntimeErrorService
{
    string ApiVersion { get; }
    string? GetError(string id);
}

/// <summary>完全信任代码包代理 v1；同进程执行，每包单独解析依赖，首次启用时加载。</summary>
public sealed class ExternalCodeMod : IMod
{
    private readonly CodeModPackage package;
    private readonly string dataDirectory;
    private readonly Func<ServiceRegistry, IDisposable>? configureServices;
    private readonly Action<Assembly>? onAssemblyLoaded;
    private PackageLoadContext? loadContext;
    private IMod? implementation;
    private ModContext? scopedContext;
    private IDisposable? configuredServices;

    public ExternalCodeMod(CodeModPackage package, string dataDirectory,
        Func<ServiceRegistry, IDisposable>? configureServices = null, Action<Assembly>? onAssemblyLoaded = null)
    {
        this.package = package;
        this.dataDirectory = Path.GetFullPath(dataDirectory);
        this.configureServices = configureServices;
        this.onAssemblyLoaded = onAssemblyLoaded;
        Manifest = new(package.Manifest.Id, package.Manifest.Name, package.Manifest.Version, package.Manifest.ApiVersion);
    }

    public ModManifest Manifest { get; }

    public void Start(ModContext context)
    {
        if (scopedContext is not null) return;
        if (configuredServices is not null) Stop(context);
        var manifest = ModManagementService.ReadManifest(package.PackageDirectory);
        if (manifest.Kind != "code" || manifest.Id != Manifest.Id || manifest.Version != Manifest.Version || manifest.Entry != package.Manifest.Entry || manifest.EntryType != package.Manifest.EntryType)
            throw new InvalidOperationException("代码包已变更，请重启程序后加载。");
        if (manifest.Dependencies.Length > 0)
        {
            var runtime = context.Services.GetRequired<IModRuntimeService>();
            foreach (var dependency in manifest.Dependencies)
                if (runtime.Contains(dependency) && !runtime.IsRunning(dependency))
                    throw new InvalidOperationException($"请先启动代码依赖：{dependency}");
        }
        var services = new ServiceRegistry(context.Services);
        Directory.CreateDirectory(dataDirectory);
        services.Add<IModEnvironment>(new EnvironmentInfo(Path.GetFullPath(package.PackageDirectory), dataDirectory));
        scopedContext = new ModContext(services, context.LifecycleLog);
        try
        {
            configuredServices = configureServices?.Invoke(services);
            if (implementation is null)
            {
                loadContext ??= new PackageLoadContext(package.PackageDirectory, onAssemblyLoaded);
                var assembly = loadContext.LoadManaged(ModManagementService.PackagePath(package.PackageDirectory, manifest.Entry));
                var type = assembly.GetType(manifest.EntryType, throwOnError: true)!;
                if (!type.IsVisible || type.IsAbstract || !typeof(IMod).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) is null)
                    throw new InvalidOperationException("entryType 必须是公开、非抽象且具有无参构造函数的 IMod 实现。");
                var candidate = (IMod)Activator.CreateInstance(type)!;
                if (candidate.Manifest != Manifest) throw new InvalidOperationException("代码实现的 manifest 与包声明不一致。");
                implementation = candidate;
            }
            implementation.Start(scopedContext);
        }
        catch (Exception startError)
        {
            try { Stop(context); }
            catch (Exception cleanupError) { throw new AggregateException("代码 Mod 启动与清理均失败。", startError, cleanupError); }
            throw;
        }
    }

    public void Stop(ModContext context)
    {
        if (scopedContext is null && configuredServices is null) return;
        try
        {
            if (scopedContext is not null)
            {
                implementation?.Stop(scopedContext);
                scopedContext = null;
            }
        }
        finally
        {
            // 只在释放成功后清除引用；失败时保留待重试资源，不重复停止已成功停止的实现。
            configuredServices?.Dispose();
            configuredServices = null;
        }
    }

    private sealed record EnvironmentInfo(string ModDirectory, string DataDirectory) : IModEnvironment
    {
        public string ApiVersion => "1.0";
    }

    private sealed class PackageLoadContext(string directory, Action<Assembly>? onLoaded) : AssemblyLoadContext(isCollectible: false)
    {
        private static bool IsShared(string? name) => name is "DesktopPet.Core" or "DesktopPet.Godot" or "GodotSharp" or "GodotSharpEditor";

        public Assembly LoadManaged(string path)
        {
            var identity = AssemblyName.GetAssemblyName(path);
            var existing = Assemblies.FirstOrDefault(a => a.GetName().Name == identity.Name);
            if (existing is not null) return existing;
            using var stream = File.OpenRead(path);
            var assembly = LoadFromStream(stream);
            onLoaded?.Invoke(assembly);
            return assembly;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == typeof(IMod).Assembly.GetName().Name) return typeof(IMod).Assembly;
            if (IsShared(assemblyName.Name))
                return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => AssemblyName.ReferenceMatchesDefinition(assemblyName, assembly.GetName()))
                    ?? GetLoadContext(typeof(IMod).Assembly)!.LoadFromAssemblyName(assemblyName);
            var path = ModManagementService.PackagePath(directory, assemblyName.Name + ".dll");
            if (File.Exists(path)) return LoadManaged(path);
            // 框架程序集由运行时统一解析；其他依赖必须随包提供，避免意外绑定宿主内部类型。
            if (assemblyName.Name is "netstandard" or "mscorlib" or "System" || assemblyName.Name?.StartsWith("System.", StringComparison.Ordinal) == true || assemblyName.Name?.StartsWith("Microsoft.", StringComparison.Ordinal) == true)
                return null;
            throw new FileNotFoundException($"代码包缺少依赖：{assemblyName.Name}");
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            var candidates = OperatingSystem.IsWindows() ? new[] { unmanagedDllName, unmanagedDllName + ".dll" } :
                OperatingSystem.IsMacOS() ? new[] { unmanagedDllName, "lib" + unmanagedDllName + ".dylib" } :
                new[] { unmanagedDllName, "lib" + unmanagedDllName + ".so" };
            foreach (var candidate in candidates)
            {
                var path = ModManagementService.PackagePath(directory, candidate);
                if (File.Exists(path)) return LoadUnmanagedDllFromPath(path);
            }
            return nint.Zero;
        }
    }
}

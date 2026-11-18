using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopPet.Core;

public sealed record BuiltInMod(string Id, string Name, string Version, string Description, bool Required, string? AppearancePath = null);
public sealed record ManagedMod(string Id, string Name, string Version, string Description, string Source,
    bool Enabled, bool Running, bool Required, string? Error, string? AppearancePath, bool RestartRequired = false);

public interface IModManagementService
{
    string ApiVersion { get; }
    string ModsDirectory { get; }
    string? SelectedAppearancePath { get; }
    string? SelectedAppearanceId { get; }
    IReadOnlyList<ManagedMod> List();
    string ImportZip(string path, CancellationToken cancellationToken = default);
    void SetEnabled(string id, bool enabled);
    void SelectAppearance(string? id);
    void Remove(string id);
}

public sealed class ModManagementException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ContentModManifest
{
    public int FormatVersion { get; init; }
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string ApiVersion { get; init; } = "";
    public string Description { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Entry { get; init; } = "";
    public string EntryType { get; init; } = "";
    public string[]? Platforms { get; init; }
    public string[] Dependencies { get; init; } = [];
    public string[] Permissions { get; init; } = [];
}

/// <summary>本地内容包管理 v1。调用方串行调用；支持外观与完全信任代码包，代码生命周期由宿主运行服务管理。</summary>
public sealed class ModManagementService : IModManagementService
{
    private const long PackageLimit = 64 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private readonly string root;
    private readonly string statePath;
    private readonly BuiltInMod[] builtIns;
    private readonly HashSet<string> initiallyEnabled;
    private readonly IModRuntimeService? runtime;
    private Settings settings;
    private readonly string packageDirectory;
    private readonly string legacyDirectory;
    public string ApiVersion => "1.0";
    public string ModsDirectory { get; }
    public string? SelectedAppearanceId => List().FirstOrDefault(mod => mod.AppearancePath is not null && mod.Running)?.Id;
    public string? SelectedAppearancePath => List().FirstOrDefault(mod => mod.AppearancePath is not null && mod.Running)?.AppearancePath;

    public ModManagementService(string dataDirectory, IEnumerable<BuiltInMod> builtIns, IModRuntimeService? runtime = null, string? packageDirectory = null)
    {
        root = Path.GetFullPath(dataDirectory);
        legacyDirectory = Path.Combine(root, "mods");
        this.packageDirectory = Path.GetFullPath(packageDirectory ?? legacyDirectory);
        ModsDirectory = this.packageDirectory;
        try
        {
            Directory.CreateDirectory(ModsDirectory);
            // 已存在的发行目录也可能不可写，先探测实际安装权限，避免界面指向无法安装的位置。
            var probe = Path.Combine(ModsDirectory, ".write-probe-" + Guid.NewGuid().ToString("N"));
            using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        { ModsDirectory = legacyDirectory; }
        statePath = Path.Combine(root, "mods-state.json");
        this.builtIns = builtIns.ToArray();
        this.runtime = runtime;
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(ModsDirectory);
        try
        {
            settings = File.Exists(statePath)
                ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(statePath), JsonOptions) ?? new()
                : new();
            settings.Disabled ??= [];
        }
        catch (JsonException)
        {
            File.Copy(statePath, statePath + ".invalid-" + Guid.NewGuid().ToString("N"));
            settings = new();
        }
        initiallyEnabled = this.builtIns.Where(mod => mod.Required || !settings.Disabled.Contains(mod.Id)).Select(mod => mod.Id).ToHashSet();
    }

    private sealed record PackageInfo(string Directory, string Id, ContentModManifest? Manifest, string? Error);

    private IReadOnlyList<PackageInfo> Scan()
    {
        var packages = new List<PackageInfo>();
        foreach (var source in new[] { packageDirectory, legacyDirectory }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(source)) continue;
            foreach (var directory in Directory.GetDirectories(source).Order(StringComparer.Ordinal))
            {
                var id = Path.GetFileName(directory);
                if (id is ".staging" or ".removed") continue;
                try
                {
                    var manifest = ReadManifest(directory);
                    if (manifest.Id != id) throw Error("package.id", "文件夹名称与 Mod ID 不一致。");
                    if (id.StartsWith("official-", StringComparison.Ordinal) && source != packageDirectory)
                        throw Error("package.id", "官方保留 ID 只能从预装目录加载。");
                    packages.Add(new(directory, id, manifest, null));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or ModManagementException)
                { packages.Add(new(directory, id, null, exception.Message)); }
            }
        }
        var duplicateIds = packages.GroupBy(p => p.Id).Where(g => g.Count() > 1 || builtIns.Any(b => b.Id == g.Key)).Select(g => g.Key).ToHashSet();
        return packages.Select(p => duplicateIds.Contains(p.Id) ? p with { Error = "Mod ID 重复，所有同名包均未加载。" } : p).ToArray();
    }

    private string? DependencyError(PackageInfo package, IReadOnlyList<PackageInfo> packages, bool requireEnabled, HashSet<string> visiting)
    {
        if (package.Error is not null) return package.Error;
        if (!visiting.Add(package.Id)) return $"依赖存在循环：{package.Id}";
        try
        {
            foreach (var id in package.Manifest!.Dependencies)
            {
                if (requireEnabled && settings.Disabled.Contains(id)) return $"请先安装并启用有效的依赖：{id}";
                if (requireEnabled && (runtime as IModRuntimeErrorService)?.GetError(id) is { } runtimeError)
                    return $"依赖 {id} 运行失败：{runtimeError}";
                if (builtIns.Any(b => b.Id == id)) continue;
                var matches = packages.Where(p => p.Id == id).ToArray();
                if (matches.Length != 1) return $"请先安装有效的依赖：{id}";
                var error = DependencyError(matches[0], packages, requireEnabled, visiting);
                if (error is not null) return error;
            }
            return null;
        }
        finally { visiting.Remove(package.Id); }
    }

    public IReadOnlyList<CodeModPackage> GetCodePackages()
    {
        var packages = Scan();
        var ordered = new List<CodeModPackage>();
        var visited = new HashSet<string>();
        void Visit(PackageInfo package)
        {
            if (!visited.Add(package.Id)) return;
            foreach (var id in package.Manifest!.Dependencies)
            {
                var dependency = packages.FirstOrDefault(p => p.Id == id);
                if (dependency is not null) Visit(dependency);
            }
            if (package.Manifest.Kind == "code") ordered.Add(new(package.Directory, package.Manifest));
        }
        foreach (var package in packages.OrderBy(p => p.Id, StringComparer.Ordinal))
            if (DependencyError(package, packages, false, []) is null) Visit(package);
        return ordered;
    }

    public IReadOnlyList<ManagedMod> List()
    {
        var packages = Scan();
        var result = builtIns.Select(mod => new ManagedMod(mod.Id, mod.Name, mod.Version, mod.Description, "内置",
            mod.Required || !settings.Disabled.Contains(mod.Id), runtime?.IsRunning(mod.Id) ?? initiallyEnabled.Contains(mod.Id), mod.Required,
            (runtime as IModRuntimeErrorService)?.GetError(mod.Id), mod.AppearancePath)).ToList();
        foreach (var package in packages)
        {
            var manifest = package.Manifest;
            var error = DependencyError(package, packages, true, []);
            var code = manifest?.Kind == "code";
            result.Add(new(package.Id, manifest?.Name ?? package.Id, manifest?.Version ?? "—", manifest?.Description ?? "", "本地",
                error is null && !settings.Disabled.Contains(package.Id), error is null && code && runtime?.IsRunning(package.Id) == true, false,
                error ?? (runtime as IModRuntimeErrorService)?.GetError(package.Id),
                manifest?.Kind == "appearance" && error is null ? PackagePath(package.Directory, manifest.Entry) : null,
                code && runtime is not null && !runtime.Contains(package.Id)));
        }
        var available = result.Where(mod => mod.Enabled && mod.Error is null && mod.AppearancePath is not null).ToArray();
        var active = available.FirstOrDefault(mod => mod.Id == settings.SelectedAppearance) ?? available.FirstOrDefault();
        return result.Select(mod => mod.AppearancePath is null ? mod : mod with { Running = mod.Id == active?.Id }).ToArray();
    }

    public string ImportZip(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path)) throw Error("package.missing", "找不到选择的 Mod 包。");
        var stagingRoot = PackagePath(ModsDirectory, ".staging");
        Directory.CreateDirectory(stagingRoot);
        var staging = PackagePath(stagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            using var archive = ZipFile.OpenRead(path);
            if (archive.Entries.Count is 0 or > 1024) throw Error("package.limit", "Mod 包文件数必须为 1–1024。");
            long total = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = entry.FullName.TrimEnd('/');
                var destination = PackagePath(staging, name);
                if ((entry.ExternalAttributes >> 16 & 0xF000) == 0xA000) throw Error("package.link", "Mod 包不能包含链接。");
                if (entry.FullName.EndsWith('/')) continue;
                if (!seen.Add(name)) throw Error("package.duplicate-file", "Mod 包包含重复文件名。");
                total += entry.Length;
                if (entry.Length > 8 * 1024 * 1024 || total > PackageLimit) throw Error("package.limit", "单文件不得超过 8 MB，包内容不得超过 64 MB。");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var input = entry.Open();
                using var output = File.Create(destination);
                var buffer = new byte[81920];
                long copied = 0;
                int count;
                while ((count = input.Read(buffer)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    copied += count;
                    if (copied > entry.Length) throw Error("package.limit", "压缩包大小信息不一致。");
                    output.Write(buffer, 0, count);
                }
            }
            var manifest = ReadManifest(staging);
            if (manifest.Kind == "appearance" && seen.Any(name => Path.GetExtension(name).ToLowerInvariant() is not (".json" or ".png" or ".md")))
                throw Error("package.kind", "外观包只支持 JSON、PNG 和说明文档。");
            if (runtime?.Contains(manifest.Id) == true)
                throw Error("mod.restart-required", "已注册的代码 Mod 需退出程序后才能替换，请关闭程序后替换包目录再重新启动。");
            if (manifest.Id.StartsWith("official-", StringComparison.Ordinal) || List().Any(mod => mod.Id == manifest.Id))
                throw Error("mod.duplicate", "该 Mod ID 已存在或为官方保留 ID，请先卸载旧版本。");
            var destinationDirectory = PackagePath(ModsDirectory, manifest.Id);
            cancellationToken.ThrowIfCancellationRequested();
            var installed = List();
            var missingDependency = manifest.Dependencies.Any(id => !installed.Any(mod => mod.Id == id && mod.Enabled && mod.Error is null));
            Directory.Move(staging, destinationDirectory);
            try
            {
                var disabled = new HashSet<string>(settings.Disabled);
                if (missingDependency) disabled.Add(manifest.Id); else disabled.Remove(manifest.Id);
                Save(disabled, settings.SelectedAppearance);
            }
            catch
            {
                Directory.Move(destinationDirectory, staging);
                throw;
            }
            return manifest.Id;
        }
        catch (InvalidDataException exception)
        {
            throw Error("package.invalid", "无法读取 Mod 压缩包：" + exception.Message);
        }
        finally
        {
            // staging 位于安装目标所在磁盘，确保最终重命名原子完成且不跨卷移动。
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    public void SetEnabled(string id, bool enabled)
    {
        var target = Find(id);
        if (target.Required && !enabled) throw Error("mod.required", "管理入口需要保持启用。");
        if (enabled && target.Source == "本地")
        {
            var packages = Scan();
            var package = packages.Single(item => item.Id == id);
            var structuralError = DependencyError(package, packages, true, []);
            if (structuralError is not null) throw Error("mod.invalid", structuralError);
        }
        if (enabled && target.Source == "本地")
        {
            var dependencies = ReadManifest(FindPackageDirectory(id)).Dependencies;
            foreach (var dependency in dependencies)
                if (dependency == id || !List().Any(mod => mod.Id == dependency && mod.Enabled && mod.Error is null))
                    throw Error("mod.dependency", $"请先安装并启用依赖：{dependency}");
        }
        if (!enabled) EnsureNoDependents(id);
        var disabled = new HashSet<string>(settings.Disabled);
        if (enabled) disabled.Remove(id); else disabled.Add(id);
        var controlsRuntime = runtime?.Contains(id) == true;
        var wasRunning = controlsRuntime && runtime!.IsRunning(id);
        if (controlsRuntime)
        {
            try { runtime!.SetEnabled(id, enabled); }
            catch (Exception exception) { throw Error("mod.lifecycle", exception.Message); }
        }
        try
        {
            Save(disabled, enabled && target.AppearancePath is not null ? id :
                !enabled && settings.SelectedAppearance == id ? null : settings.SelectedAppearance);
        }
        catch (Exception saveError)
        {
            if (controlsRuntime)
            {
                try { runtime!.SetEnabled(id, wasRunning); }
                catch (Exception rollbackError)
                {
                    throw Error("mod.rollback", $"保存 Mod“{target.Name}”的设置失败：{saveError.Message}；恢复原运行状态也失败：{rollbackError.Message}");
                }
            }
            throw;
        }
    }

    public void SelectAppearance(string? id)
    {
        if (id is not null)
        {
            var mod = Find(id);
            if (!mod.Enabled || mod.Error is not null || mod.AppearancePath is null)
                throw Error("mod.appearance", "请选择已启用且有效的外观 Mod。");
        }
        Save(new(settings.Disabled), id);
    }

    public void Remove(string id)
    {
        var mod = Find(id);
        if (mod.Source == "内置") throw Error("mod.required", "内置 Mod 不能卸载。");
        EnsureNoDependents(id);
        if (runtime?.Contains(id) == true) throw Error("mod.restart-required", "已注册的代码 Mod 需退出程序后才能卸载或替换。请关闭程序后替换包目录，再重新启动。");
        var source = FindPackageDirectory(id);
        RejectLink(source);
        var removed = PackagePath(Path.GetDirectoryName(source)!, ".removed");
        Directory.CreateDirectory(removed);
        var target = Path.Combine(removed, id + "-" + Guid.NewGuid().ToString("N"));
        Directory.Move(source, target);
        try
        {
            var disabled = new HashSet<string>(settings.Disabled);
            disabled.Remove(id);
            Save(disabled, settings.SelectedAppearance == id ? null : settings.SelectedAppearance);
        }
        catch { Directory.Move(target, source); throw; }
    }

    private void EnsureNoDependents(string id)
    {
        foreach (var mod in List().Where(mod => mod.Source == "本地" && mod.Enabled && mod.Error is null && mod.Id != id))
            if (ReadManifest(FindPackageDirectory(mod.Id)).Dependencies.Contains(id))
                throw Error("mod.dependency", $"请先禁用依赖它的 Mod：{mod.Name}");
    }

    private string FindPackageDirectory(string id)
    {
        var matches = Scan().Where(p => p.Id == id).ToArray();
        if (matches.Length != 1) throw Error("mod.duplicate", "找不到唯一的包目录，请先处理重复 ID。");
        return matches[0].Directory;
    }

    private ManagedMod Find(string id)
    {
        var matches = List().Where(mod => mod.Id == id).ToArray();
        if (matches.Length > 1) throw Error("mod.duplicate", "Mod ID 重复，请先处理同名包。");
        return matches.FirstOrDefault() ?? throw Error("mod.missing", "找不到该 Mod。");
    }

    internal static ContentModManifest ReadManifest(string directory)
    {
        RejectLink(directory);
        var path = PackagePath(directory, "manifest.json");
        if (!File.Exists(path)) throw Error("package.manifest", "包根目录缺少 manifest.json。");
        if (new FileInfo(path).Length > 65536) throw Error("package.manifest", "manifest.json 超过大小限制。");
        ContentModManifest manifest;
        try { manifest = JsonSerializer.Deserialize<ContentModManifest>(File.ReadAllText(path), JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Error("package.manifest", "manifest.json 格式无效。"); }
        if (manifest.FormatVersion != 1 || manifest.ApiVersion != "1.0") throw Error("package.version", "此内容包与当前 API 1.0 不兼容。");
        if (manifest.Permissions is null ||
            (manifest.Kind == "appearance" ? manifest.Permissions.Length != 0 :
            manifest.Kind != "code" || !manifest.Permissions.SequenceEqual(new[] { "full-trust" })))
            throw Error("package.permission", "appearance 不申请权限；code 必须显式声明 full-trust 权限。");
        if (!Regex.IsMatch(manifest.Id ?? "", "^[a-z0-9][a-z0-9._-]{1,79}$") || !IsPortableSegment(manifest.Id!) || string.IsNullOrWhiteSpace(manifest.Name) ||
            manifest.Name.Length > 64 || manifest.Description is null || manifest.Description.Length > 2048 ||
            !Version.TryParse(manifest.Version, out _) || manifest.Dependencies is null || manifest.Dependencies.Length > 64 ||
            manifest.Dependencies.Any(id => id == manifest.Id || !Regex.IsMatch(id ?? "", "^[a-z0-9][a-z0-9._-]{1,79}$")))
            throw Error("package.manifest", "请检查 Mod ID、名称、版本和依赖列表。");
        if (manifest.Kind == "code")
        {
            var entry = PackagePath(directory, manifest.Entry);
            if (!File.Exists(entry) || !string.Equals(Path.GetExtension(entry), ".dll", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(manifest.EntryType))
                throw Error("package.entry", "代码包需要包内 DLL 和公开 IMod 类型 entryType。");
            if (manifest.Platforms is not null && (manifest.Platforms.Length == 0 || manifest.Platforms.Any(p => p is not ("windows" or "macos" or "linux"))))
                throw Error("package.platform", "platforms 必须是 windows、macos 或 linux。");
            var platform = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";
            if (manifest.Platforms is not null && !manifest.Platforms.Contains(platform))
                throw Error("package.platform", "代码包不支持当前平台。");
            return manifest;
        }
        var appearancePath = PackagePath(directory, manifest.Entry);
        if (!File.Exists(appearancePath) || new FileInfo(appearancePath).Length > 1024 * 1024) throw Error("package.entry", "找不到外观配置或配置过大。");
        var appearance = PetAppearanceDefinition.FromJson(File.ReadAllText(appearancePath));
        if ((long)appearance.Frames.Length * appearance.FrameSize * appearance.FrameSize * 4 > PackageLimit)
            throw Error("package.limit", "外观帧的解码内存不得超过 64 MB。");
        foreach (var frame in appearance.Frames)
        {
            var framePath = PackagePath(Path.GetDirectoryName(appearancePath)!, frame);
            if (!File.Exists(framePath)) throw Error("package.frame", $"缺少动作帧：{frame}");
            using var stream = File.OpenRead(framePath);
            var header = new byte[24];
            if (stream.Read(header) != 24 || !header.AsSpan(0, 8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }) ||
                !header.AsSpan(8, 8).SequenceEqual(new byte[] { 0,0,0,13,73,72,68,82 }) ||
                System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16,4)) != appearance.FrameSize ||
                System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20,4)) != appearance.FrameSize)
                throw Error("package.frame", $"图片尺寸或 PNG 头无效：{frame}");
        }
        return manifest;
    }

    internal static string PackagePath(string directory, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains(':') || relative.Contains('\\') ||
            relative.Split('/').Any(part => part is "" or "." or ".." || !IsPortableSegment(part))) throw Error("package.path", "内容包包含无效路径。");
        var path = Path.GetFullPath(Path.Combine(directory, relative));
        if (!path.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw Error("package.path", "内容包路径超出安装目录。");
        var current = directory;
        RejectLink(current);
        foreach (var part in relative.Split('/')) { current = Path.Combine(current, part); RejectLink(current); }
        return path;
    }

    private static void RejectLink(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw Error("package.link", "内容包不能包含文件系统链接。");
    }

    private static bool IsPortableSegment(string name) => !name.EndsWith('.') && !name.EndsWith(' ') &&
        name.IndexOfAny(['<', '>', '"', '|', '?', '*']) < 0 &&
        !Regex.IsMatch(name, "^(con|prn|aux|nul|com[0-9]|lpt[0-9])(?:\\.|$)", RegexOptions.IgnoreCase);

    private void Save(HashSet<string> disabled, string? selected)
    {
        var updated = new Settings { Disabled = disabled, SelectedAppearance = selected };
        var temporary = statePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(updated, JsonOptions));
        File.Move(temporary, statePath, true);
        settings = updated;
    }

    private static ModManagementException Error(string code, string message) => new(code, message);
    private sealed class Settings
    {
        public HashSet<string> Disabled { get; set; } = [];
        public string? SelectedAppearance { get; set; }
    }
}

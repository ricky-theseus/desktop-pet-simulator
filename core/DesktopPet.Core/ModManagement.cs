using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopPet.Core;

public sealed record BuiltInMod(string Id, string Name, string Version, string Description, bool Required, string? AppearancePath = null);
public sealed record ManagedMod(string Id, string Name, string Version, string Description, string Source,
    bool Enabled, bool Running, bool Required, string? Error, string? AppearancePath);

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
    public string[] Dependencies { get; init; } = [];
    public string[] Permissions { get; init; } = [];
}

/// <summary>本地内容包管理 v1。调用方串行调用；仅导入数据外观包，代码 Mod 由宿主运行服务管理。</summary>
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
    public string ApiVersion => "1.0";
    public string ModsDirectory { get; }
    public string? SelectedAppearanceId => List().FirstOrDefault(mod => mod.AppearancePath is not null && mod.Running)?.Id;
    public string? SelectedAppearancePath => List().FirstOrDefault(mod => mod.AppearancePath is not null && mod.Running)?.AppearancePath;

    public ModManagementService(string dataDirectory, IEnumerable<BuiltInMod> builtIns, IModRuntimeService? runtime = null)
    {
        root = Path.GetFullPath(dataDirectory);
        ModsDirectory = Path.Combine(root, "mods");
        statePath = Path.Combine(root, "mods-state.json");
        this.builtIns = builtIns.ToArray();
        this.runtime = runtime;
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

    public IReadOnlyList<ManagedMod> List()
    {
        var manifests = new Dictionary<string, ContentModManifest>();
        var result = builtIns.Select(mod => new ManagedMod(mod.Id, mod.Name, mod.Version, mod.Description, "内置",
            mod.Required || !settings.Disabled.Contains(mod.Id), runtime?.IsRunning(mod.Id) ?? initiallyEnabled.Contains(mod.Id), mod.Required, null, mod.AppearancePath)).ToList();
        foreach (var directory in Directory.GetDirectories(ModsDirectory).Order())
        {
            try
            {
                var manifest = ReadManifest(directory);
                if (manifest.Id != Path.GetFileName(directory)) throw Error("package.id", "文件夹名称与 Mod ID 不一致。");
                if (manifest.Id.StartsWith("official-", StringComparison.Ordinal)) throw Error("package.id", "本地内容包不能使用官方保留 ID。");
                manifests.Add(manifest.Id, manifest);
                result.Add(new(manifest.Id, manifest.Name, manifest.Version, manifest.Description, "本地",
                    !settings.Disabled.Contains(manifest.Id), settings.SelectedAppearance == manifest.Id, false, null,
                    PackagePath(directory, manifest.Entry)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or ModManagementException)
            {
                result.Add(new(Path.GetFileName(directory), Path.GetFileName(directory), "—", "", "本地", false, false, false, exception.Message, null));
            }
        }
        var byId = result.GroupBy(mod => mod.Id).ToDictionary(group => group.Key, group => group.First());
        string? DependencyError(string id, HashSet<string> visiting)
        {
            if (!visiting.Add(id)) return $"依赖存在循环：{id}";
            try
            {
                if (!manifests.TryGetValue(id, out var manifest)) return null;
                foreach (var dependency in manifest.Dependencies)
                {
                    if (!byId.TryGetValue(dependency, out var target) || !target.Enabled || target.Error is not null)
                        return $"请先安装并启用有效的依赖：{dependency}";
                    var error = DependencyError(dependency, visiting);
                    if (error is not null) return error;
                }
                return null;
            }
            finally { visiting.Remove(id); }
        }
        var validated = result.Select(mod =>
        {
            var error = mod.Error ?? (mod.Source == "本地" ? DependencyError(mod.Id, []) : null);
            return error is null ? mod : mod with { Error = error, Enabled = false, Running = false };
        }).ToArray();
        var available = validated.Where(mod => mod.Enabled && mod.Error is null && mod.AppearancePath is not null).ToArray();
        var active = available.FirstOrDefault(mod => mod.Id == settings.SelectedAppearance) ?? available.FirstOrDefault();
        return validated.Select(mod => mod.AppearancePath is null ? mod : mod with { Running = mod.Id == active?.Id }).ToArray();
    }

    public string ImportZip(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path)) throw Error("package.missing", "找不到选择的 Mod 包。");
        var staging = Path.Combine(root, "staging-" + Guid.NewGuid().ToString("N"));
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
                if (Path.GetExtension(name).ToLowerInvariant() is not (".json" or ".png" or ".md"))
                    throw Error("package.kind", "当前仅支持包含 JSON、PNG 和说明文档的外观内容包。");
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
            // staging 由本次导入在固定数据目录内创建，不接受来自包内容的清理路径。
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    public void SetEnabled(string id, bool enabled)
    {
        var target = Find(id);
        if (target.Required && !enabled) throw Error("mod.required", "管理入口需要保持启用。");
        if (enabled && target.Error is not null) throw Error("mod.invalid", target.Error);
        if (enabled && target.Source == "本地")
        {
            var dependencies = ReadManifest(PackagePath(ModsDirectory, id)).Dependencies;
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
        var source = PackagePath(ModsDirectory, id);
        RejectLink(source);
        var removed = Path.Combine(root, "removed");
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
            if (ReadManifest(PackagePath(ModsDirectory, mod.Id)).Dependencies.Contains(id))
                throw Error("mod.dependency", $"请先禁用依赖它的 Mod：{mod.Name}");
    }

    private ManagedMod Find(string id) => List().FirstOrDefault(mod => mod.Id == id) ?? throw Error("mod.missing", "找不到该 Mod。");

    private static ContentModManifest ReadManifest(string directory)
    {
        RejectLink(directory);
        var path = PackagePath(directory, "manifest.json");
        if (!File.Exists(path)) throw Error("package.manifest", "包根目录缺少 manifest.json。");
        if (new FileInfo(path).Length > 65536) throw Error("package.manifest", "manifest.json 超过大小限制。");
        ContentModManifest manifest;
        try { manifest = JsonSerializer.Deserialize<ContentModManifest>(File.ReadAllText(path), JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw Error("package.manifest", "manifest.json 格式无效。"); }
        if (manifest.FormatVersion != 1 || manifest.ApiVersion != "1.0") throw Error("package.version", "此内容包与当前 API 1.0 不兼容。");
        if (manifest.Kind != "appearance" || manifest.Permissions is null || manifest.Permissions.Length != 0)
            throw Error("package.permission", "当前只接受不申请额外权限的 appearance 内容包。");
        if (!Regex.IsMatch(manifest.Id ?? "", "^[a-z0-9][a-z0-9._-]{1,79}$") || !IsPortableSegment(manifest.Id!) || string.IsNullOrWhiteSpace(manifest.Name) ||
            manifest.Name.Length > 64 || manifest.Description is null || manifest.Description.Length > 2048 ||
            !Version.TryParse(manifest.Version, out _) || manifest.Dependencies is null || manifest.Dependencies.Length > 64 ||
            manifest.Dependencies.Any(id => id == manifest.Id || !Regex.IsMatch(id ?? "", "^[a-z0-9][a-z0-9._-]{1,79}$")))
            throw Error("package.manifest", "请检查 Mod ID、名称、版本和依赖列表。");
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

    private static string PackagePath(string directory, string relative)
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

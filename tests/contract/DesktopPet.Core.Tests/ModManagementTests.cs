using System.IO.Compression;
using System.Text.Json;
using DesktopPet.Core;
using Xunit;

namespace DesktopPet.Core.Tests;

public sealed class ModManagementTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "dps-mod-test-" + Guid.NewGuid());
    private ModManagementService Service() => new(root, [
        new BuiltInMod("official-mod-manager", "管理器", "0.1.0", "", true),
        new BuiltInMod("official-steam", "Steam", "0.1.0", "", false)
    ]);

    private string Package(string id = "test-pet", string api = "1.0", string extraPath = "", string[]? dependencies = null)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, Guid.NewGuid() + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string name, string content)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(content);
        }
        Add("manifest.json", JsonSerializer.Serialize(new { formatVersion = 1, id, name = "测试宠物", version = "1.0.0",
            apiVersion = api, kind = "appearance", entry = "appearance.json", dependencies = dependencies ?? [] }));
        Add("appearance.json", """
            {"formatVersion":1,"id":"test","displayName":"测试宠物","frameSize":8,"frames":["idle.png"],
             "actions":{"idle":{"frames":[0],"fps":1,"loop":true}}}
            """);
        using (var file = archive.CreateEntry("idle.png").Open())
            file.Write(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAD0lEQVQYV2P8z8AARMAAABYAAf4ZJucAAAAASUVORK5CYII="));
        if (extraPath.Length > 0) Add(extraPath, "rejected");
        return path;
    }

    [Fact]
    public void ImportSelectionDisableAndRemovePersistAcrossSessions()
    {
        var service = Service();
        service.ImportZip(Package());
        Assert.Contains(service.List(), item => item.Id == "test-pet" && item.Enabled);
        service.SelectAppearance("test-pet");
        Assert.EndsWith("appearance.json", Service().SelectedAppearancePath);
        service.SetEnabled("test-pet", false);
        Assert.Null(Service().SelectedAppearancePath);
        service.Remove("test-pet");
        Assert.DoesNotContain(Service().List(), item => item.Id == "test-pet");
    }

    [Theory]
    [InlineData("../escape.json")]
    [InlineData("/absolute.json")]
    [InlineData("code.dll")]
    [InlineData("frames/../../escape.json")]
    public void InvalidArchivesCannotInstall(string path)
    {
        var service = Service();
        Assert.Throws<ModManagementException>(() => service.ImportZip(Package(extraPath: path)));
        Assert.DoesNotContain(service.List(), item => item.Id == "test-pet");
    }

    [Fact]
    public void RequiredModsAndReservedIdsAreProtected()
    {
        var service = Service();
        Assert.Throws<ModManagementException>(() => service.SetEnabled("official-mod-manager", false));
        Assert.Throws<ModManagementException>(() => service.Remove("official-mod-manager"));
        Assert.Throws<ModManagementException>(() => service.ImportZip(Package("official-mod-manager")));
    }

    [Fact]
    public void DuplicateIdAndIncompatibleApiAreRejected()
    {
        var service = Service();
        Assert.Throws<ModManagementException>(() => service.ImportZip(Package(api: "99.0")));
        service.ImportZip(Package());
        Assert.Throws<ModManagementException>(() => service.ImportZip(Package()));
    }

    [Fact]
    public void MissingDependencyPreventsEnableAndDisablingDependencyIsRejected()
    {
        var service = Service();
        service.ImportZip(Package("dependent", dependencies: ["base"]));
        Assert.Throws<ModManagementException>(() => service.SetEnabled("dependent", true));
        service.ImportZip(Package("base"));
        service.SetEnabled("dependent", true);
        Assert.Throws<ModManagementException>(() => service.SetEnabled("base", false));
    }

    [Fact]
    public void UnknownTargetAndCancellationDoNotChangeInstalledMods()
    {
        var service = Service();
        Assert.Throws<ModManagementException>(() => service.Remove("unknown"));
        Assert.Throws<OperationCanceledException>(() => service.ImportZip(Package(), new CancellationToken(true)));
        Assert.Equal(2, service.List().Count);
    }

    [Fact]
    public void MissingDependencyIsReportedAndExternallyBrokenSelectionFallsBack()
    {
        var service = Service();
        service.ImportZip(Package("dependent", dependencies: ["base"]));
        Assert.Contains("base", service.List().Single(mod => mod.Id == "dependent").Error);
        service.ImportZip(Package("base"));
        service.SetEnabled("dependent", true);
        service.SelectAppearance("dependent");
        File.Delete(Path.Combine(service.ModsDirectory, "base", "idle.png"));
        Assert.Null(service.SelectedAppearancePath);
        Assert.NotNull(service.List().Single(mod => mod.Id == "dependent").Error);
    }

    [Fact]
    public void PermissionRequestAndSelfDependencyAreRejected()
    {
        var service = Service();
        Assert.Throws<ModManagementException>(() => service.ImportZip(Package(dependencies: ["test-pet"])));
        var path = Package();
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("manifest.json")!;
            string json;
            using (var reader = new StreamReader(entry.Open())) json = reader.ReadToEnd();
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("manifest.json").Open());
            writer.Write(json.Replace("\"kind\":\"appearance\"", "\"kind\":\"appearance\",\"permissions\":[\"shell\"]"));
        }
        Assert.Throws<ModManagementException>(() => service.ImportZip(path));
        Assert.Equal(2, service.List().Count);
    }

    [Fact]
    public void InvalidStateRecoversAndCancelledImportLeavesNoStagingDirectory()
    {
        var service = Service();
        File.WriteAllText(Path.Combine(root, "mods-state.json"), "{invalid");
        Assert.Null(Service().SelectedAppearancePath);
        Assert.Single(Directory.GetFiles(root, "mods-state.json.invalid-*"));
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();
        Assert.Throws<OperationCanceledException>(() => service.ImportZip(Package(), timeout.Token));
        Assert.Empty(Directory.GetDirectories(root, "staging-*"));
    }

    [Theory]
    [InlineData("test-pet.")]
    [InlineData("con")]
    [InlineData("nul.pet")]
    public void NonPortableIdsAreRejectedBeforeInstall(string id)
    {
        var service = Service();
        Assert.Throws<ModManagementException>(() => service.ImportZip(Package(id)));
        Assert.Equal(2, service.List().Count);
    }

    [Fact]
    public void BuiltInAppearanceCanBeReplacedAndAllAppearancesCanBeDisabled()
    {
        ModManagementService Create() => new(root, [
            new BuiltInMod("official-mod-manager", "管理器", "0.1.0", "", true),
            new BuiltInMod("official-mushroom", "蘑菇", "0.1.0", "", false, "res://mushroom.json")
        ]);
        var service = Create();
        Assert.Equal("official-mushroom", service.SelectedAppearanceId);
        service.ImportZip(Package());
        service.SetEnabled("official-mushroom", false);
        Assert.Equal("test-pet", service.SelectedAppearanceId);
        service.SetEnabled("test-pet", false);
        Assert.Null(service.SelectedAppearancePath);
        Assert.Null(Create().SelectedAppearancePath);
        service.SetEnabled("official-mushroom", true);
        Assert.Equal("res://mushroom.json", Create().SelectedAppearancePath);
    }

    [Fact]
    public void RuntimeStateTracksImmediateStopAndRestart()
    {
        var (service, host, mod) = ManagedRuntime(true);
        Assert.True(service.List().Single().Running);

        service.SetEnabled("test-code", false);
        Assert.False(mod.Active);
        Assert.False(service.List().Single().Running);
        Assert.False(RuntimeService().List().Single().Enabled);
        service.SetEnabled("test-code", true);
        Assert.True(mod.Active);
        Assert.True(service.List().Single().Running);
        Assert.True(RuntimeService().List().Single().Enabled);
        host.StopAll();
        Assert.True(service.List().Single().Enabled);
        Assert.False(service.List().Single().Running);
    }

    [Fact]
    public void DisabledCodeModIsNotStartedInNextSession()
    {
        RuntimeService().SetEnabled("test-code", false);
        var host = new ModHost(new ModHostOptions("1.0"));
        var mod = new RuntimeMod();
        host.Register(mod);
        var service = RuntimeService(host);
        host.StartAll(id => service.List().Single(item => item.Id == id).Enabled);

        Assert.False(mod.Active);
        Assert.False(service.List().Single().Running);
        service.SetEnabled("test-code", true);
        Assert.True(mod.Active);
        host.StopAll();
    }

    [Fact]
    public void FailedStartCleansResourcesWithoutPersistingEnable()
    {
        var (service, host, mod) = ManagedRuntime(false);
        mod.FailStart = true;

        var error = Assert.Throws<ModManagementException>(() => service.SetEnabled("test-code", true));

        Assert.Equal("mod.lifecycle", error.Code);
        Assert.Contains("start failure", error.Message);
        Assert.False(mod.Active);
        Assert.False(service.List().Single().Enabled);
        Assert.False(RuntimeService().List().Single().Enabled);
        Assert.False(host.IsRunning("test-code"));
        host.StopAll();
    }

    [Fact]
    public void FailedStopDoesNotPersistDisable()
    {
        var (service, host, mod) = ManagedRuntime(true);
        mod.FailStop = true;

        var error = Assert.Throws<ModManagementException>(() => service.SetEnabled("test-code", false));

        Assert.Equal("mod.lifecycle", error.Code);
        Assert.Contains("stop failure", error.Message);
        Assert.True(service.List().Single().Enabled);
        Assert.True(RuntimeService().List().Single().Enabled);
        Assert.True(host.IsRunning("test-code"));
        mod.FailStop = false;
        host.StopAll();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaveFailureRestoresPreviousRuntimeAndSettings(bool initiallyEnabled)
    {
        var (service, host, mod) = ManagedRuntime(initiallyEnabled);
        var originalState = File.ReadAllText(Path.Combine(root, "mods-state.json"));
        Directory.CreateDirectory(Path.Combine(root, "mods-state.json.tmp"));

        var error = Record.Exception(() => service.SetEnabled("test-code", !initiallyEnabled));

        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Equal(initiallyEnabled, mod.Active);
        Assert.Equal(initiallyEnabled, host.IsRunning("test-code"));
        Assert.Equal(initiallyEnabled, service.List().Single().Enabled);
        Assert.Equal(initiallyEnabled, RuntimeService().List().Single().Enabled);
        Assert.Equal(originalState, File.ReadAllText(Path.Combine(root, "mods-state.json")));
        Assert.Equal(initiallyEnabled ? ["start", "stop", "start"] : ["start", "stop"], mod.Events);
        host.StopAll();
    }

    private ModManagementService RuntimeService(IModRuntimeService? runtime = null) => new(root,
        [new BuiltInMod("test-code", "测试代码 Mod", "1.0", "", false)], runtime);

    private (ModManagementService Service, ModHost Host, RuntimeMod Mod) ManagedRuntime(bool enabled)
    {
        RuntimeService().SetEnabled("test-code", enabled);
        var host = new ModHost(new ModHostOptions("1.0"));
        var mod = new RuntimeMod();
        host.Register(mod);
        var service = RuntimeService(host);
        Assert.False(service.List().Single().Running);
        host.StartAll(id => service.List().Single(item => item.Id == id).Enabled);
        return (service, host, mod);
    }

    private sealed class RuntimeMod : IMod
    {
        public ModManifest Manifest { get; } = new("test-code", "测试代码 Mod", "1.0", "1.0");
        public List<string> Events { get; } = [];
        public bool Active { get; private set; }
        public bool FailStart { get; set; }
        public bool FailStop { get; set; }

        public void Start(ModContext context)
        {
            Events.Add("start");
            Active = true;
            if (FailStart) throw new InvalidOperationException("start failure");
        }

        public void Stop(ModContext context)
        {
            Events.Add("stop");
            if (FailStop) throw new InvalidOperationException("stop failure");
            Active = false;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}

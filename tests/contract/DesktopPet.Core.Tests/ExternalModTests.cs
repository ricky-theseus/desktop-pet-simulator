using DesktopPet.Core;
using Xunit;
using System.Text.Json;
using System.IO.Compression;

namespace DesktopPet.Core.Tests;

public sealed class ExternalModTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "external-mod-tests-" + Guid.NewGuid().ToString("N"));
    private string Packages => Path.Combine(root, "packages");
    private string Data => Path.Combine(root, "data");
    private ModManagementService Service(IModRuntimeService? runtime = null) => new(Data, [], runtime, Packages);

    [Fact]
    public void ScansPrimaryAndLegacyPackagesInDependencyOrder()
    {
        WritePackage(Packages, "dependent", ["base"]);
        WritePackage(Path.Combine(Data, "mods"), "base");
        WritePackage(Packages, "official-test");
        var service = Service();
        Assert.Equal(["base", "dependent", "official-test"], service.GetCodePackages().Select(p => p.Manifest.Id));
        Assert.All(service.List(), mod => Assert.Null(mod.AppearancePath));
        Assert.All(service.List(), mod => Assert.False(mod.Running));
    }

    [Fact]
    public void DuplicateIdsInBothRootsCannotLoad()
    {
        WritePackage(Packages, "duplicate");
        WritePackage(Path.Combine(Data, "mods"), "duplicate");
        var service = Service();
        Assert.Empty(service.GetCodePackages());
        Assert.Equal(2, service.List().Count);
        Assert.All(service.List(), mod => Assert.NotNull(mod.Error));
    }

    [Theory]
    [InlineData("99.0", "full-trust", "plugin.dll")]
    [InlineData("1.0", "", "plugin.dll")]
    [InlineData("1.0", "full-trust", "../plugin.dll")]
    public void InvalidCodeManifestDoesNotLoad(string api, string permission, string entry)
    {
        WritePackage(Packages, "invalid", api: api, permission: permission, entry: entry);
        Assert.Empty(Service().GetCodePackages());
        Assert.NotNull(Service().List().Single().Error);
    }

    [Fact]
    public void NewImportRequiresRestartAndRegisteredPackageCannotBeRemoved()
    {
        Directory.CreateDirectory(root);
        var source = WritePackage(Path.Combine(root, "source"), "new-code");
        var zip = Path.Combine(root, "package.zip");
        ZipFile.CreateFromDirectory(source, zip);
        var host = new ModHost(new("1.0"));
        var service = Service(host);
        service.ImportZip(zip);
        Assert.True(service.List().Single().RestartRequired);
        Assert.False(service.List().Single().Running);
        Assert.Throws<ModManagementException>(() => service.ImportZip(zip));
        host.Register(new ExternalCodeMod(service.GetCodePackages().Single(), Data));
        Assert.Equal("mod.restart-required", Assert.Throws<ModManagementException>(() => service.Remove("new-code")).Code);
    }

    [Fact]
    public void StartFailuresDoNotPreventLaterModsAndAreVisible()
    {
        WritePackage(Packages, "invalid-binary");
        var host = new ModHost(new("1.0"));
        var service = Service(host);
        host.Register(new ExternalCodeMod(service.GetCodePackages().Single(), Data));
        host.Register(new HealthyMod());
        Assert.ThrowsAny<Exception>(() => host.StartAll());
        Assert.True(host.IsRunning("healthy"));
        Assert.NotNull(service.List().Single().Error);
        host.StopAll();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndependentAssemblyUsesScopedServicesAndReleasesResources(bool failStart)
    {
        var packagePath = FixturePackage();
        if (failStart) File.WriteAllText(Path.Combine(packagePath, "fail-start"), "");
        var parent = new ServiceRegistry();
        parent.Add<IDesktopProvider>(new Provider());
        var updates = new Updates();
        var scopeDisposals = 0;
        var loadedAssemblies = new List<System.Reflection.Assembly>();
        var mod = new ExternalCodeMod(Service().GetCodePackages().Single(), Data, registry => {
            registry.Add<IModUpdateService>(updates);
            return new Cleanup(() => { updates.Clear(); scopeDisposals++; });
        }, assembly => loadedAssemblies.Add(assembly));
        var host = new ModHost(new("1.0"), parent);
        host.Register(mod);
        Assert.Empty(loadedAssemblies);
        if (failStart) Assert.Throws<InvalidOperationException>(() => host.StartAll());
        else
        {
            host.StartAll();
            Assert.Equal(1, updates.Count);
            updates.Tick(0.25);
        }
        host.StopAll();
        Assert.Equal(0, updates.Count);
        Assert.Equal(1, scopeDisposals);
        Assert.Throws<InvalidOperationException>(() => parent.GetRequired<IModEnvironment>());
        Assert.Contains(loadedAssemblies, assembly => assembly.GetName().Name == "Fixture.Dependency");
        Assert.All(loadedAssemblies, assembly => Assert.NotSame(System.Runtime.Loader.AssemblyLoadContext.Default, System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(assembly)));
        var events = File.ReadAllText(Path.Combine(Data, "events.txt"));
        Assert.Contains("dependency-loaded:test:1.0", events);
        Assert.Contains("stopped", events);
        if (!failStart) Assert.Contains("tick:0.25", events);
        File.Move(Path.Combine(packagePath, "Fixture.Plugin.dll"), Path.Combine(packagePath, "moved.dll"));
    }

    [Fact]
    public void StopFailureStillDisposesScopeAndCanRetryImplementationCleanup()
    {
        var path = FixturePackage();
        File.WriteAllText(Path.Combine(path, "fail-stop"), "");
        var registry = new ServiceRegistry();
        registry.Add<IDesktopProvider>(new Provider());
        var updates = new Updates();
        var disposals = 0;
        var host = new ModHost(new("1.0"), registry);
        host.Register(new ExternalCodeMod(Service().GetCodePackages().Single(), Data, services => {
            services.Add<IModUpdateService>(updates);
            return new Cleanup(() => { updates.Clear(); disposals++; });
        }));
        host.StartAll();
        Assert.Throws<AggregateException>(() => host.StopAll());
        Assert.Equal(0, updates.Count);
        Assert.Equal(1, disposals);
        File.Delete(Path.Combine(path, "fail-stop"));
        host.StopAll();
        Assert.Equal(2, File.ReadAllLines(Path.Combine(Data, "events.txt")).Count(line => line == "stopped"));
        Assert.Equal(1, disposals);
    }

    [Fact]
    public void DisabledCodePackagesRemainDiscoverableAndCanEnableLater()
    {
        WritePackage(Packages, "base");
        WritePackage(Packages, "dependent", ["base"]);
        var service = Service();
        service.SetEnabled("dependent", false);
        service.SetEnabled("base", false);
        Assert.Equal(["base", "dependent"], service.GetCodePackages().Select(package => package.Manifest.Id));
    }

    [Fact]
    public void PrimaryDirectoryReceivesImportsAndOfficialZipCannotImpersonatePreinstall()
    {
        Directory.CreateDirectory(root);
        var service = Service();
        var source = WritePackage(Path.Combine(root, "source"), "imported");
        var zip = Path.Combine(root, "package.zip");
        ZipFile.CreateFromDirectory(source, zip);
        service.ImportZip(zip);
        Assert.Equal(Packages, service.ModsDirectory);
        Assert.True(File.Exists(Path.Combine(Packages, "imported", "manifest.json")));
        source = WritePackage(Path.Combine(root, "source"), "official-impostor");
        var officialZip = Path.Combine(root, "official.zip");
        ZipFile.CreateFromDirectory(source, officialZip);
        Assert.Throws<ModManagementException>(() => service.ImportZip(officialZip));
    }

    [Fact]
    public void MissingManagedDependencyFailsWithoutBindingHostAssemblies()
    {
        var path = FixturePackage();
        File.Delete(Path.Combine(path, "Fixture.Dependency.dll"));
        var parent = new ServiceRegistry();
        parent.Add<IDesktopProvider>(new Provider());
        var updates = new Updates();
        var host = new ModHost(new("1.0"), parent);
        host.Register(new ExternalCodeMod(Service().GetCodePackages().Single(), Data, services => {
            services.Add<IModUpdateService>(updates);
            return new Cleanup(updates.Clear);
        }));
        Assert.ThrowsAny<Exception>(() => host.StartAll());
        Assert.False(host.IsRunning("fixture-code"));
        Assert.Contains("Fixture.Dependency", ((IModRuntimeErrorService)host).GetError("fixture-code"));
    }

    [Fact]
    public void RuntimeFailureDoesNotBlockSuccessfulRetryFromManager()
    {
        var path = FixturePackage();
        File.WriteAllText(Path.Combine(path, "fail-start"), "");
        var parent = new ServiceRegistry();
        parent.Add<IDesktopProvider>(new Provider());
        var updates = new Updates();
        var host = new ModHost(new("1.0"), parent);
        var service = Service(host);
        host.Register(new ExternalCodeMod(service.GetCodePackages().Single(), Data, services => {
            services.Add<IModUpdateService>(updates);
            return new Cleanup(updates.Clear);
        }));
        Assert.ThrowsAny<Exception>(() => host.StartAll());
        File.Delete(Path.Combine(path, "fail-start"));
        service.SetEnabled("fixture-code", true);
        Assert.True(service.List().Single().Running);
        Assert.Null(service.List().Single().Error);
        host.StopAll();
    }

    [Fact]
    public void FullTrustCodeZipCanCarrySceneAudioAndOtherPackageResources()
    {
        Directory.CreateDirectory(root);
        var source = WritePackage(Path.Combine(root, "source"), "resource-code");
        File.WriteAllText(Path.Combine(source, "scene.tscn"), "[gd_scene format=3]");
        File.WriteAllBytes(Path.Combine(source, "sound.wav"), [1, 2, 3]);
        var zip = Path.Combine(root, "resources.zip");
        ZipFile.CreateFromDirectory(source, zip);
        var service = Service();
        service.ImportZip(zip);
        Assert.True(File.Exists(Path.Combine(Packages, "resource-code", "scene.tscn")));
    }

    [Fact]
    public void CyclicAndMissingDependenciesAreNotDiscoverable()
    {
        WritePackage(Packages, "cycle-a", ["cycle-b"]);
        WritePackage(Packages, "cycle-b", ["cycle-a"]);
        WritePackage(Packages, "missing", ["absent"]);
        Assert.Empty(Service().GetCodePackages());
        Assert.All(Service().List(), mod => Assert.NotNull(mod.Error));
    }

    [Fact]
    public void CrossVolumeImportAndRemoveKeepInternalDirectoriesOutOfList()
    {
        if (!OperatingSystem.IsWindows() || !Directory.Exists("C:/") || !Directory.Exists("D:/")) return;
        var targetRoot = Path.Combine("D:/", "external-mod-cross-volume-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.NotEqual(Path.GetPathRoot(Data), Path.GetPathRoot(targetRoot));
            var service = new ModManagementService(Data, [], packageDirectory: targetRoot);
            var source = WritePackage(Path.Combine(root, "source"), "cross-volume");
            var zip = Path.Combine(root, "cross.zip");
            ZipFile.CreateFromDirectory(source, zip);
            service.ImportZip(zip);
            Assert.Single(service.List());
            service.Remove("cross-volume");
            Assert.Empty(service.List());
            Assert.Single(Directory.GetDirectories(Path.Combine(targetRoot, ".removed")));
            Directory.CreateDirectory(Path.Combine(targetRoot, ".staging", "partial"));
            Assert.Empty(service.List());
        }
        finally { if (Directory.Exists(targetRoot)) Directory.Delete(targetRoot, true); }
    }

    [Fact]
    public void FailedCodeDependencyBlocksChildStartupAndManagerEnableUntilRecovered()
    {
        WritePackage(Packages, "broken-base");
        var path = FixturePackage();
        var manifestPath = Path.Combine(path, "manifest.json");
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
        json["dependencies"] = new System.Text.Json.Nodes.JsonArray("broken-base");
        File.WriteAllText(manifestPath, json.ToJsonString());
        var registry = new ServiceRegistry();
        registry.Add<IDesktopProvider>(new Provider());
        var updates = new Updates();
        var host = new ModHost(new("1.0"), registry);
        var service = Service(host);
        foreach (var package in service.GetCodePackages())
            host.Register(new ExternalCodeMod(package, Data, services => {
                services.Add<IModUpdateService>(updates);
                return new Cleanup(updates.Clear);
            }));
        Assert.ThrowsAny<Exception>(() => host.StartAll());
        Assert.False(host.IsRunning("fixture-code"));
        Assert.False(File.Exists(Path.Combine(Data, "events.txt")));
        var child = service.List().Single(mod => mod.Id == "fixture-code");
        Assert.False(child.Enabled);
        Assert.Contains("broken-base", child.Error);
        Assert.Throws<ModManagementException>(() => service.SetEnabled("fixture-code", true));
    }

    [Fact]
    public void ManifestMismatchIsRejectedOnEveryRetry()
    {
        var path = FixturePackage();
        var manifestPath = Path.Combine(path, "manifest.json");
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
        json["name"] = "different name";
        File.WriteAllText(manifestPath, json.ToJsonString());
        var registry = new ServiceRegistry();
        registry.Add<IDesktopProvider>(new Provider());
        var host = new ModHost(new("1.0"), registry);
        host.Register(new ExternalCodeMod(Service().GetCodePackages().Single(), Data));
        Assert.ThrowsAny<Exception>(() => host.StartAll());
        Assert.ThrowsAny<Exception>(() => host.SetEnabled("fixture-code", true));
        Assert.False(File.Exists(Path.Combine(Data, "events.txt")));
        Assert.Contains("manifest", host.GetError("fixture-code"));
    }

    [Fact]
    public void ConstructorFailureDisposesConfiguredServicesOnEveryAttempt()
    {
        var path = FixturePackage();
        var manifestPath = Path.Combine(path, "manifest.json");
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
        json["entryType"] = "Fixture.ThrowingConstructorPlugin";
        File.WriteAllText(manifestPath, json.ToJsonString());
        var disposals = 0;
        var host = new ModHost(new("1.0"));
        host.Register(new ExternalCodeMod(Service().GetCodePackages().Single(), Data, _ => new Cleanup(() => disposals++)));
        Assert.ThrowsAny<Exception>(() => host.StartAll());
        Assert.Equal(1, disposals);
        Assert.ThrowsAny<Exception>(() => host.SetEnabled("fixture-code", true));
        Assert.Equal(2, disposals);
        host.StopAll();
        Assert.Equal(2, disposals);
        Assert.False(host.IsRunning("fixture-code"));
    }

    [Fact]
    public void FailedStartAndFailedStopRetryReleasesActualResourcesBeforeRestart()
    {
        var path = FixturePackage();
        File.WriteAllText(Path.Combine(path, "fail-start"), "");
        File.WriteAllText(Path.Combine(path, "fail-stop"), "");
        var registry = new ServiceRegistry();
        registry.Add<IDesktopProvider>(new Provider());
        var updates = new Updates();
        var disposals = 0;
        var host = new ModHost(new("1.0"), registry);
        host.Register(new ExternalCodeMod(Service().GetCodePackages().Single(), Data, services => {
            services.Add<IModUpdateService>(updates);
            return new Cleanup(() => { updates.Clear(); disposals++; });
        }));
        Assert.Throws<AggregateException>(() => host.StartAll());
        Assert.Equal(0, updates.Count);
        Assert.Equal(1, disposals);
        Assert.True(File.Exists(Path.Combine(Data, "active-resource")));
        Assert.Throws<AggregateException>(() => host.StopAll());
        File.Delete(Path.Combine(path, "fail-stop"));
        File.Delete(Path.Combine(path, "fail-start"));
        host.SetEnabled("fixture-code", true);
        Assert.Equal(1, updates.Count);
        Assert.True(host.IsRunning("fixture-code"));
        host.StopAll();
        Assert.Equal(0, updates.Count);
        Assert.Equal(2, disposals);
        Assert.False(File.Exists(Path.Combine(Data, "active-resource")));
    }

    [Fact]
    public void FailedServiceScopeDisposalRemainsPendingAndRetriesWithoutStoppingImplementationAgain()
    {
        FixturePackage();
        var registry = new ServiceRegistry();
        registry.Add<IDesktopProvider>(new Provider());
        var updates = new Updates();
        var attempts = 0;
        var resourcePath = Path.Combine(Data, "scope-resource");
        var host = new ModHost(new("1.0"), registry);
        host.Register(new ExternalCodeMod(Service().GetCodePackages().Single(), Data, services => {
            services.Add<IModUpdateService>(updates);
            File.WriteAllText(resourcePath, "active");
            return new Cleanup(() => {
                attempts++;
                if (attempts == 1) throw new InvalidOperationException("scope disposal failed");
                File.Delete(resourcePath);
            });
        }));
        host.StartAll();
        Assert.Throws<AggregateException>(() => host.StopAll());
        Assert.Equal(1, attempts);
        Assert.True(File.Exists(resourcePath));
        Assert.True(host.IsRunning("fixture-code"));
        Assert.Contains("scope disposal failed", host.GetError("fixture-code"));
        host.StopAll();
        Assert.Equal(2, attempts);
        Assert.False(File.Exists(resourcePath));
        Assert.False(host.IsRunning("fixture-code"));
        Assert.Null(host.GetError("fixture-code"));
        Assert.Equal(1, File.ReadAllLines(Path.Combine(Data, "events.txt")).Count(line => line == "stopped"));
        host.StopAll();
        Assert.Equal(2, attempts);
    }

    private string FixturePackage()
    {
        var path = WritePackage(Packages, "fixture-code", entry: "Fixture.Plugin.dll");
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures"), "*.dll"))
            File.Copy(file, Path.Combine(path, Path.GetFileName(file)));
        return path;
    }

    private sealed class Provider : IDesktopProvider { public string PlatformId => "test"; }
    private sealed class Cleanup(Action cleanup) : IDisposable { public void Dispose() => cleanup(); }
    private sealed class Updates : IModUpdateService
    {
        public string ApiVersion => "1.0";
        private readonly List<Action<double>> callbacks = [];
        public int Count => callbacks.Count;
        public IDisposable Subscribe(Action<double> update) { callbacks.Add(update); return new Cleanup(() => callbacks.Remove(update)); }
        public void Clear() => callbacks.Clear();
        public void Tick(double delta) { foreach (var callback in callbacks.ToArray()) callback(delta); }
    }

    private string WritePackage(string directory, string id, string[]? dependencies = null, string api = "1.0", string permission = "full-trust", string entry = "plugin.dll")
    {
        var path = Path.Combine(directory, id);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "plugin.dll"), "not a managed assembly");
        File.WriteAllText(Path.Combine(path, "manifest.json"), JsonSerializer.Serialize(new {
            formatVersion = 1, id, name = id, version = "1.0", apiVersion = api, kind = "code", entry,
            entryType = "Fixture.Plugin", permissions = new[] { permission }, dependencies = dependencies ?? []
        }));
        return path;
    }

    private sealed class HealthyMod : IMod
    {
        public ModManifest Manifest => new("healthy", "healthy", "1.0", "1.0");
        public void Start(ModContext context) { }
        public void Stop(ModContext context) { }
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

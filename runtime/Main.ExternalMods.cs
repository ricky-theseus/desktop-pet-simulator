using DesktopPet.Core;
using DesktopPet.Godot;
using Godot;

namespace DesktopPet.Runtime;

public partial class Main
{
    private readonly List<GodotModScope> externalModScopes = [];

    private static string ResolveModsDirectory(string[] arguments)
    {
        var configured = arguments.FirstOrDefault(arg => arg.StartsWith("--mods-dir="))?[11..];
        if (!string.IsNullOrWhiteSpace(configured)) return System.IO.Path.GetFullPath(configured);
        return !OS.HasFeature("editor")
            ? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(OS.GetExecutablePath())!, "mods")
            : ProjectSettings.GlobalizePath("res://mods");
    }

    private void RegisterExternalMods(ModManagementService management, string dataDirectory)
    {
        foreach (var package in management.GetCodePackages())
        {
            var id = package.Manifest.Id;
            host!.Register(new ExternalCodeMod(package, System.IO.Path.Combine(dataDirectory, "mod-data", id),
                services =>
                {
                    var scope = new GodotModScope(this, id);
                    services.Add<IModSceneService>(scope);
                    services.Add<IModUpdateService>(scope);
                    externalModScopes.Add(scope);
                    return scope;
                }, global::Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly));
        }
    }

    // 每次启用创建独立容器；即使 Mod 的 Stop 抛错，宿主也能释放其窗口和逐帧订阅。
    private sealed class GodotModScope : IModSceneService, IModUpdateService, IDisposable
    {
        private readonly Main owner;
        private readonly string modId;
        private readonly List<UpdateSubscription> subscriptions = [];
        private bool disposed;

        public string ApiVersion => "1.0";
        public Node Root { get; }

        public GodotModScope(Main owner, string modId)
        {
            this.owner = owner;
            this.modId = modId;
            Root = new Node { Name = "Mod_" + modId };
            owner.AddChild(Root);
        }

        public void Present(Window window)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentNullException.ThrowIfNull(window);
            if (window.GetParent() is null) Root.AddChild(window);
            if (!Root.IsAncestorOf(window)) throw new InvalidOperationException("Mod 窗口必须属于自己的场景容器。");
            if (!window.Visible) window.Show();
            if (window.Mode == Window.ModeEnum.Minimized) window.Mode = Window.ModeEnum.Windowed;
            PresentModWindow(window);
            window.GrabFocus();
        }

        public IDisposable Subscribe(Action<double> callback)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentNullException.ThrowIfNull(callback);
            var subscription = new UpdateSubscription(this, callback);
            subscriptions.Add(subscription);
            return subscription;
        }

        public void Tick(double delta)
        {
            if (disposed) return;
            foreach (var subscription in subscriptions.ToArray())
            {
                if (disposed) break;
                try { subscription.Invoke(delta); }
                catch (Exception exception)
                {
                    subscription.Dispose();
                    GD.PushWarning($"Mod {modId} 的逐帧回调已停止：{exception.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var subscription in subscriptions.ToArray()) subscription.Dispose();
            owner.externalModScopes.Remove(this);
            if (GodotObject.IsInstanceValid(Root) && !Root.IsQueuedForDeletion())
            {
                Root.ProcessMode = ProcessModeEnum.Disabled;
                // 先隐藏原生窗口，避免排队释放期间残留一帧可交互界面。
                HideWindows(Root);
                Root.QueueFree();
            }
        }

        private static void HideWindows(Node node)
        {
            if (node is Window window) window.Hide();
            foreach (var child in node.GetChildren()) HideWindows(child);
        }

        private sealed class UpdateSubscription(GodotModScope scope, Action<double> callback) : IDisposable
        {
            private Action<double>? action = callback;
            public void Invoke(double delta) => action?.Invoke(delta);
            public void Dispose()
            {
                action = null;
                scope.subscriptions.Remove(this);
            }
        }
    }
}

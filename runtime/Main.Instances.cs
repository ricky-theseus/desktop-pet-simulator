using DesktopPet.Core;
using Godot;
using System.Text.Json;

namespace DesktopPet.Runtime;

public partial class Main : IPetInstanceService
{
    private sealed record InstanceOptions(string AppearancePath, ServiceRegistry Services,
        Action OpenManager, PetMenuService SharedMenu, Action<Vector2I> PositionChanged);
    private InstanceOptions? instanceOptions;
    private Exception? instanceStartupError;
    private string instanceDataDirectory = "";
    private bool instancesShuttingDown;
    private readonly Dictionary<(string Owner, string Key), InstanceLease> petInstances = [];
    private sealed record SavedPosition(int X, int Y);

    public IPetInstance Create(string owner, string key, string appearancePath)
    {
        if (!IsInsideTree() || instanceOptions is not null || instancesShuttingDown)
            throw new InvalidOperationException("宠物实例只能由正在运行的宿主创建。");
        ValidateInstanceName(owner, 128);
        ValidateInstanceName(key, 64);
        ArgumentException.ThrowIfNullOrWhiteSpace(appearancePath);
        if (petInstances.ContainsKey((owner, key))) throw new InvalidOperationException("此 Mod 的宠物实例已经存在。");
        if (DisplayServer.GetName() == "headless") throw new NotSupportedException("独立桌宠需要原生显示窗口。");
        using (var file = global::Godot.FileAccess.Open(appearancePath.Replace('\\', '/'), global::Godot.FileAccess.ModeFlags.Read)
            ?? throw new FileNotFoundException("无法读取宠物实例的外观定义。"))
            PetAppearanceDefinition.FromJson(file.GetAsText());

        var services = new ServiceRegistry();
        var window = new Window
        {
            Name = "Pet_" + owner + "_" + key, Title = "桌面宠物模拟器", Size = new Vector2I(192, 256),
            Borderless = true, Transparent = true, TransparentBg = true, AlwaysOnTop = true,
            Unresizable = true, Transient = false, Visible = false, GuiEmbedSubwindows = false
        };
        var controller = new Main();
        var lease = new InstanceLease(this, owner, key, window, controller, services);
        controller.instanceOptions = new(appearancePath, services, () => modManager?.Show(), modMenu, lease.SavePosition);
        try
        {
            AddChild(window);
            window.Position = RestoreInstancePosition(owner, key, window.Size);
            window.Show();
            window.AddChild(controller);
            if (controller.instanceStartupError is { } failure)
                throw new InvalidOperationException("宠物实例初始化失败。", failure);
            window.Title = controller.pet!.DisplayName;
            window.CloseRequested += lease.Hide;
            window.TreeExiting += lease.OnTreeExiting;
            petInstances.Add((owner, key), lease);
            return lease;
        }
        catch
        {
            window.Hide();
            window.QueueFree();
            throw;
        }
    }

    public IReadOnlyList<PetInstanceInfo> List() => petInstances.Values.Where(item => item.IsAlive)
        .Select(item => new PetInstanceInfo(item.Owner, item.Key, item.Controller.pet?.DisplayName ?? "宠物", item.Window.Visible)).ToArray();

    private static void ValidateInstanceName(string value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit || !(char.IsAsciiLetterLower(value[0]) || char.IsAsciiDigit(value[0])) ||
            value.Any(c => !(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c)) && c is not '-' and not '_' and not '.'))
            throw new ArgumentException("实例 owner/key 必须是以小写字母或数字开头的可移植标识。");
        if (value.EndsWith('.') || new[] { "CON", "PRN", "AUX", "NUL" }.Contains(value.Split('.')[0].ToUpperInvariant()) ||
            System.Text.RegularExpressions.Regex.IsMatch(value, "^(?i:com|lpt)[0-9](?:\\.|$)"))
            throw new ArgumentException("实例 owner/key 不能使用文件系统保留名称。");
    }

    private string InstancePositionPath(string owner, string key) => System.IO.Path.Combine(instanceDataDirectory, owner, key + ".json");

    private Vector2I RestoreInstancePosition(string owner, string key, Vector2I size)
    {
        var area = DisplayServer.ScreenGetUsableRect(GetWindow().CurrentScreen);
        var position = GetWindow().Position + new Vector2I(GetWindow().Size.X + 24 + petInstances.Count * 36, 0);
        if (position.X + size.X > area.End.X) position.X = GetWindow().Position.X - size.X - 24;
        try
        {
            var path = InstancePositionPath(owner, key);
            if (System.IO.File.Exists(path) && JsonSerializer.Deserialize<SavedPosition>(System.IO.File.ReadAllText(path)) is { } saved)
                position = new(saved.X, saved.Y);
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or JsonException) { }
        for (var screen = 0; screen < DisplayServer.GetScreenCount(); screen++)
        {
            var candidate = DisplayServer.ScreenGetUsableRect(screen);
            if (candidate.HasPoint(position + size / 2)) { area = candidate; break; }
        }
        return new(Math.Clamp(position.X, area.Position.X, Math.Max(area.Position.X, area.End.X - size.X)),
            Math.Clamp(position.Y, area.Position.Y, Math.Max(area.Position.Y, area.End.Y - size.Y)));
    }

    private sealed class InstanceLease(Main host, string owner, string key, Window window,
        Main controller, ServiceRegistry services) : IPetInstance
    {
        private bool disposed;
        public string Owner => owner;
        public string Key => key;
        public Window Window => window;
        public Main Controller => controller;
        public bool IsAlive => !disposed && GodotObject.IsInstanceValid(window) && window.IsInsideTree() && !window.IsQueuedForDeletion();
        public ServiceRegistry Services
        {
            get { ObjectDisposedException.ThrowIf(!IsAlive, this); return services; }
        }

        public void Show()
        {
            ObjectDisposedException.ThrowIf(!IsAlive, this);
            var wasVisible = window.Visible;
            window.Show();
            // 隐藏独立 Window 会销毁其原生句柄，恢复时需对新句柄重新应用任务栏样式。
            if (!wasVisible) controller.ApplyTaskbarStyle();
            controller.RefreshMousePassthrough();
        }

        public void Hide()
        {
            ObjectDisposedException.ThrowIf(!IsAlive, this);
            SavePosition(window.Position);
            controller.CancelPointer();
            controller.menu?.Hide();
            if (controller.feedback is not null) controller.feedback.Visible = false;
            window.Hide();
            controller.pet?.RefreshAnimationPresentation();
        }

        public void SavePosition(Vector2I position)
        {
            if (disposed || host.instanceDataDirectory.Length == 0) return;
            // 以默认尺寸的脚底保存位置，避免重启后先恢复窗口、再应用缩放造成漂移。
            if (controller.pet is { } pet)
                position += (Vector2I)(pet.Position + new Vector2(96, 152) * pet.Scale - PetOffset - new Vector2(96, 152));
            try
            {
                var path = host.InstancePositionPath(owner, key);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                var temporary = path + ".tmp";
                System.IO.File.WriteAllText(temporary, JsonSerializer.Serialize(new SavedPosition(position.X, position.Y)));
                System.IO.File.Move(temporary, path, true);
            }
            catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
            {
                GD.PushWarning("宠物位置暂时无法保存：" + error.Message);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            if (IsAlive) SavePosition(window.Position);
            disposed = true;
            host.petInstances.Remove((owner, key));
            if (GodotObject.IsInstanceValid(window) && window.IsInsideTree() && !window.IsQueuedForDeletion())
            {
                controller.ClearAppearance();
                window.Hide();
                window.QueueFree();
            }
        }

        public void OnTreeExiting()
        {
            if (disposed) return;
            SavePosition(window.Position);
            disposed = true;
            host.petInstances.Remove((owner, key));
        }
    }
}

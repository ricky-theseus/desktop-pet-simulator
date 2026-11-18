using DesktopPet.Core;
using DesktopPet.Godot;
using Godot;

namespace Example.DeskLamp;

/// <summary>只使用公共 SDK 的独立窗口示例；所有 Godot 操作在宿主主线程执行。</summary>
public sealed class LampMod : IMod
{
    public ModManifest Manifest { get; } = new("example-desk-lamp", "桌面小灯 · 代码示例", "1.0.0", "1.0");
    private Window? window;
    private ColorRect? glow;
    private Label? status;
    private Button? toggle;
    private HSlider? brightness;
    private IDisposable? updates;
    private IPetMenuCommand? menu;
    private string? statePath;
    private bool lit = true;
    private double elapsed;

    public void Start(ModContext context)
    {
        var environment = context.Services.GetRequired<IModEnvironment>();
        var scenes = context.Services.GetRequired<IModSceneService>();
        var updateService = context.Services.GetRequired<IModUpdateService>();
        if (environment.ApiVersion != "1.0" || scenes.ApiVersion != "1.0" || updateService.ApiVersion != "1.0")
            throw new NotSupportedException("桌面小灯需要 SDK API 1.0。");

        Directory.CreateDirectory(environment.DataDirectory);
        statePath = Path.Combine(environment.DataDirectory, "lamp.txt");
        lit = !File.Exists(statePath) || File.ReadAllText(statePath).Trim() != "off";
        elapsed = 0;
        window = new Window { Title = "桌面小灯 / SDK 示例", Size = new Vector2I(340, 280), Unresizable = true };
        window.CloseRequested += Hide;
        var panel = new VBoxContainer { Position = new Vector2(20, 18), Size = new Vector2(300, 244) };
        panel.AddThemeConstantOverride("separation", 10);
        window.AddChild(panel);
        panel.AddChild(new Label { Text = "DESK LAMP", HorizontalAlignment = HorizontalAlignment.Center });
        glow = new ColorRect { CustomMinimumSize = new Vector2(300, 68), MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(glow);
        status = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        panel.AddChild(status);
        brightness = new HSlider { MinValue = 15, MaxValue = 100, Value = 80, Step = 1 };
        panel.AddChild(brightness);
        toggle = new Button();
        toggle.Pressed += Toggle;
        panel.AddChild(toggle);
        panel.AddChild(new Label { Text = "关闭窗口后，可从菜单重新打开。", HorizontalAlignment = HorizontalAlignment.Center });
        // Present 将窗口挂到当前 Mod 的专属容器，宿主停用时也会兜底清理。
        scenes.Present(window);
        menu = context.Services.GetRequired<IPetMenuService>().Register(
            Manifest.Id, "show", "打开桌面小灯", Show, PetMenuScope.Shared);
        updates = updateService.Subscribe(Update);
        Update(0);
    }

    private void Update(double delta)
    {
        elapsed += delta;
        if (glow is null || status is null || toggle is null || brightness is null) return;
        var intensity = lit ? (float)(brightness.Value / 100 * (0.85 + 0.15 * Math.Sin(elapsed * 2))) : 0.06f;
        glow.Color = new Color(intensity, intensity * 0.78f, intensity * 0.22f);
        status.Text = lit ? $"呼吸灯亮着 · {elapsed:0} 秒" : "小灯已关闭";
        toggle.Text = lit ? "关灯" : "开灯";
    }

    private void Toggle()
    {
        lit = !lit;
        if (statePath is not null) File.WriteAllText(statePath, lit ? "on" : "off");
        Update(0);
    }

    private void Show() { window?.Show(); window?.GrabFocus(); }
    private void Hide() => window?.Hide();

    public void Stop(ModContext context)
    {
        // 先断开外部回调，再释放窗口；Start 部分失败时也允许重复清理。
        updates?.Dispose();
        updates = null;
        menu?.Dispose();
        menu = null;
        if (toggle is not null && GodotObject.IsInstanceValid(toggle)) toggle.Pressed -= Toggle;
        if (window is not null && GodotObject.IsInstanceValid(window))
        {
            window.CloseRequested -= Hide;
            window.Hide();
            window.QueueFree();
        }
        window = null;
        glow = null;
        status = null;
        toggle = null;
        brightness = null;
        statePath = null;
    }
}

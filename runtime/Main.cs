using DesktopPet.Core;
using DesktopPet.OfficialMushroom;
using DesktopPet.OfficialModManager;
using Godot;

namespace DesktopPet.Runtime;

public partial class Main : Node2D, IPetAppearanceService, IPetVisualService
{
    public string ApiVersion => "1.0";
    bool IPetVisualService.IsVisible => pet?.Visible == true;
    public bool CanPose => pet?.Visible == true && pet.Action == "idle" && !pointerDown && menu?.Visible != true;

    public void SetPose(PetVisualPose pose)
    {
        if (!pose.IsValid) throw new ArgumentOutOfRangeException(nameof(pose));
        if (pet?.SetPose(CanPose ? pose : PetVisualPose.Neutral) == true) RefreshMousePassthrough();
    }
    private const int FeedAction = 1;
    private const int PetAction = 2;
    private const int CenterAction = 3;
    private const int QuitAction = 4;
    private const int ModsAction = 5;
    private ModManagerMod? modManager;
    private Action? tickDistribution = null;
    private StatusIndicator? tray;
    private bool wholeWindowPassthrough;
    private const string DefaultAppearance = "res://assets/mushroom/appearance.json";

    private PetView? pet;
    private readonly MushroomPetState petState = new();
    private ModHost? host;
    private bool pointerDown;
    private bool dragging;
    private bool doubleClick;
    private Vector2I pressScreen;
    private Vector2I grabOffset;
    private PopupMenu? menu;
    private SpeechBubble? feedback;
    private float feedbackSeconds;
    private static readonly Vector2 PetOffset = new(0f, 64f);

    public override void _Ready()
    {
        GetViewport().TransparentBg = true;
        GetViewport().GuiEmbedSubwindows = false;
        GetWindow().Transparent = true;
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Transparent, true);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.AlwaysOnTop, true);

        if (OperatingSystem.IsWindows() && DisplayServer.GetName() != "headless" &&
            !OS.GetCmdlineUserArgs().Contains("--show-taskbar"))
        {
            WindowsDesktopWindow.HideTaskbarButton((nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle));
        }
        GetWindow().FocusExited += CancelPointer;

        var arguments = OS.GetCmdlineUserArgs();
        var safeMode = arguments.Contains("--safe-mode");
        var dataDirectory = arguments.FirstOrDefault(arg => arg.StartsWith("--mod-data-dir="))?[15..]
            ?? System.IO.Path.Combine(OS.GetUserDataDir(), "mod-platform");
        var builtIns = new List<BuiltInMod> {
            new("official-mushroom", "默认蘑菇", "0.1.0", "初始蘑菇形象，带有待机、摸摸、进食、提起和落地动作，可关闭或替换。", false, DefaultAppearance),
            new("official-mod-manager", "Mod 管理器", "0.1.0", "导入、启用和管理本地 Mod。此管理入口始终保留，方便恢复桌宠。", true)
        };
        var services = new ServiceRegistry();
        services.Add<IPetAppearanceService>(this);
        services.Add<IPetVisualService>(this);
        host = new ModHost(new ModHostOptions("1.0"), services);
        services.Add<IModRuntimeService>(host);
        ConfigureLocalMods(builtIns, services, host, safeMode);
        var management = new ModManagementService(dataDirectory, builtIns, host);
        services.Add<IModManagementService>(management);
        var mushroom = new MushroomMod();
        host.Register(mushroom);
        services.Add<ISteamService>(new OfflineSteamService());
        ConfigureDistribution(services, host, safeMode);

        pet = new PetView { Position = PetOffset };
        AddChild(pet);
        var failedAppearance = false;
        try
        {
            if (safeMode) ApplyAppearance(null);
            else if (management.SelectedAppearancePath is { } path) ApplyAppearance(path);
            else ClearAppearance();
        }
        catch (Exception exception)
        {
            GD.PushWarning("外观加载失败，请在管理器中切换或关闭该 Mod：" + exception.Message);
            ClearAppearance();
            failedAppearance = true;
        }

        menu = new PopupMenu { Theme = CreateMenuTheme() };
        menu.AddItem($"喂{pet.DisplayName}", FeedAction);
        menu.AddItem($"摸摸{pet.DisplayName}", PetAction);
        menu.AddSeparator();
        menu.AddItem("Mod 管理器…", ModsAction);
        menu.AddSeparator();
        menu.AddItem("回到屏幕中央", CenterAction);
        menu.AddItem("退出桌宠", QuitAction);
        menu.IdPressed += OnMenuItemPressed;
        AddChild(menu);

        feedback = new SpeechBubble
        {
            Position = new Vector2(8f, 4f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(feedback);
        modManager = new ModManagerMod(this, PresentModWindow);
        host.Register(modManager);
        var enabledMods = management.List().ToDictionary(mod => mod.Id, mod => mod.Enabled);
        host.StartAll(id => !enabledMods.TryGetValue(id, out var enabled) || enabled);
        CreateTray();
        if (arguments.Contains("--open-mod-manager") || failedAppearance) modManager.Show();
        RefreshMousePassthrough();
    }

    private void CreateTray()
    {
        if (!DisplayServer.HasFeature(DisplayServer.Feature.StatusIndicator)) return;
        var trayMenu = new PopupMenu { Name = "TrayMenu", PreferNativeMenu = true, Theme = CreateMenuTheme() };
        trayMenu.AddItem("打开 Mod 管理器", ModsAction);
        trayMenu.AddSeparator();
        trayMenu.AddItem("退出桌宠", QuitAction);
        trayMenu.IdPressed += OnMenuItemPressed;
        AddChild(trayMenu);
        tray = new StatusIndicator
        {
            Name = "DesktopTray", Tooltip = "桌面宠物模拟器 · 点击打开 Mod 管理器",
            Icon = GD.Load<Texture2D>("res://assets/mushroom/actions-v1/01.png"), Visible = true
        };
        tray.Pressed += (button, position) =>
        {
            if (button == (long)MouseButton.Left) modManager?.Show();
            else if (button == (long)MouseButton.Right)
            {
                trayMenu.Position = position;
                trayMenu.Popup();
            }
        };
        AddChild(tray);
    }

    // 发行实现由显式构建选项接入；公共构建没有实现时，此调用在编译时移除。
    partial void ConfigureDistribution(ServiceRegistry services, ModHost modHost, bool safeMode);
    partial void ConfigureLocalMods(List<BuiltInMod> builtIns, ServiceRegistry services, ModHost modHost, bool safeMode);

    private static void PresentModWindow(Window window)
    {
        if (!OperatingSystem.IsWindows() || DisplayServer.GetName() == "headless") return;
        WindowsDesktopWindow.ShowOwnedWindow(
            (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, window.GetWindowId()),
            window.Transient
                ? (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, window.GetParent().GetWindow().GetWindowId())
                : 0);
    }

    public void ApplyAppearance(string? path)
    {
        pet?.LoadAppearance(path ?? DefaultAppearance);
        if (pet is not null) pet.Visible = true;
        if (menu is not null && pet is not null)
        {
            menu.SetItemText(menu.GetItemIndex(FeedAction), $"喂{pet.DisplayName}");
            menu.SetItemText(menu.GetItemIndex(PetAction), $"摸摸{pet.DisplayName}");
        }
        RefreshMousePassthrough();
    }

    public void ClearAppearance()
    {
        if (pet is null) return;
        pet.ClearAppearance();
        pet.Visible = false;
        if (feedback is not null) feedback.Visible = false;
        menu?.Hide();
        CancelPointer();
        RefreshMousePassthrough();
    }

    public override void _Process(double delta)
    {
        tickDistribution?.Invoke();
        if (pet?.Visible == true) petState.Tick((float)delta);
        if (feedback is null || !feedback.Visible)
        {
            return;
        }

        feedbackSeconds -= (float)delta;
        if (feedbackSeconds <= 0f)
        {
            feedback.Visible = false;
            RefreshMousePassthrough();
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (pet is null || !pet.Visible) return;

        if (inputEvent is InputEventMouseMotion motion && pointerDown)
        {
            var pointer = DisplayServer.WindowGetPosition() + (Vector2I)motion.Position;
            if (!dragging && ((Vector2)(pointer - pressScreen)).Length() >= 6f)
            {
                dragging = true;
                pet.Play("held");
                if (feedback is not null) feedback.Visible = false;
                RefreshMousePassthrough();
            }
            if (dragging) DisplayServer.WindowSetPosition(pointer - grabOffset);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent is not InputEventMouseButton mouseButton) return;
        if (mouseButton.ButtonIndex == MouseButton.Left && !mouseButton.Pressed && pointerDown)
        {
            if (dragging)
            {
                pet.Play("land");
                var area = DisplayServer.ScreenGetUsableRect();
                var size = DisplayServer.WindowGetSize();
                var position = DisplayServer.WindowGetPosition();
                DisplayServer.WindowSetPosition(new Vector2I(
                    Math.Clamp(position.X, area.Position.X, Math.Max(area.Position.X, area.End.X - size.X)),
                    Math.Clamp(position.Y, area.Position.Y, Math.Max(area.Position.Y, area.End.Y - size.Y))));
            }
            else if (pet.InteractionRect.HasPoint(mouseButton.Position - pet.Position))
            {
                petState.Interact();
                pet.Play("pet");
                ShowFeedback(doubleClick ? "超开心！" : "摸摸！");
            }
            pointerDown = false;
            dragging = false;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!mouseButton.Pressed || !pet.InteractionRect.HasPoint(mouseButton.Position - pet.Position)) return;
        if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            if (menu is not null)
            {
                menu.ResetSize();
                PositionMenuBesidePet();
                menu.Popup();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (mouseButton.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        pointerDown = true;
        doubleClick = mouseButton.DoubleClick;
        grabOffset = (Vector2I)mouseButton.Position;
        pressScreen = DisplayServer.WindowGetPosition() + grabOffset;
        GetViewport().SetInputAsHandled();
    }

    private void CancelPointer()
    {
        if (dragging) pet?.Play("land");
        pointerDown = false;
        dragging = false;
    }

    public override void _ExitTree()
    {
        if (tray is not null) tray.Visible = false;
        GetWindow().FocusExited -= CancelPointer;
        host?.StopAll();
    }

    private void OnMenuItemPressed(long action)
    {
        if (pet is null)
        {
            return;
        }

        switch (action)
        {
            case ModsAction:
                modManager?.Show();
                break;
            case FeedAction:
                petState.Feed();
                pet.Play("eat");
                ShowFeedback("好吃！");
                break;
            case PetAction:
                petState.Interact();
                pet.Play("pet");
                ShowFeedback("摸摸！");
                break;
            case CenterAction:
                CenterWindow();
                ShowFeedback("回到这里啦！");
                break;
            case QuitAction:
                GetTree().Quit();
                break;
        }
    }

    private void CenterWindow()
    {
        var workArea = DisplayServer.ScreenGetUsableRect();
        var windowSize = DisplayServer.WindowGetSize();
        DisplayServer.WindowSetPosition(workArea.Position + (workArea.Size - windowSize) / 2);
    }

    private void RefreshMousePassthrough()
    {
        if (pet is null) return;
        if (wholeWindowPassthrough == pet.Visible)
        {
            wholeWindowPassthrough = !pet.Visible;
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.MousePassthrough, wholeWindowPassthrough);
            // Godot 刷新窗口标记时会重建 Windows 扩展样式，需要重新保留无任务栏行为。
            if (OperatingSystem.IsWindows() && DisplayServer.GetName() != "headless" && !OS.GetCmdlineUserArgs().Contains("--show-taskbar"))
                WindowsDesktopWindow.HideTaskbarButton((nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle));
        }
        if (!pet.Visible) return;
        var region = pet.HitPolygon.Select(point => point + PetOffset).ToArray();
        if (feedback?.Visible == true)
        {
            // Windows 会同时裁剪穿透区域之外的画面，气泡显示时须包含其边界。
            var rect = feedback.GetRect();
            region = Geometry2D.ConvexHull([
                ..region, rect.Position, new Vector2(rect.End.X, rect.Position.Y),
                rect.End, new Vector2(rect.Position.X, rect.End.Y)
            ]);
        }

        DisplayServer.WindowSetMousePassthrough(region);
    }

    private void PositionMenuBesidePet()
    {
        if (menu is null)
        {
            return;
        }

        var windowPosition = DisplayServer.WindowGetPosition();
        var windowSize = DisplayServer.WindowGetSize();
        var workArea = DisplayServer.ScreenGetUsableRect();
        var menuSize = menu.Size;
        const int gap = 8;

        var rightX = windowPosition.X + windowSize.X + gap;
        var leftX = windowPosition.X - menuSize.X - gap;
        var x = rightX + menuSize.X <= workArea.End.X
            ? rightX
            : leftX;
        x = Math.Clamp(x, workArea.Position.X, Math.Max(workArea.Position.X, workArea.End.X - menuSize.X));
        var y = Math.Clamp(windowPosition.Y + (int)PetOffset.Y, workArea.Position.Y,
            Math.Max(workArea.Position.Y, workArea.End.Y - menuSize.Y));
        menu.Position = new Vector2I(x, y);
    }

    private static Theme CreateMenuTheme()
    {
        var theme = new Theme();
        theme.SetFontSize("font_size", "PopupMenu", 20);
        theme.SetConstant("v_separation", "PopupMenu", 12);
        theme.SetColor("font_color", "PopupMenu", new Color("#302339"));
        theme.SetColor("font_hover_color", "PopupMenu", new Color("#302339"));
        var panel = SpeechBubble.CreatePanelStyle();
        panel.ContentMarginTop = 10f;
        panel.ContentMarginBottom = 10f;
        panel.ContentMarginLeft = 14f;
        panel.ContentMarginRight = 14f;
        theme.SetStylebox("panel", "PopupMenu", panel);
        theme.SetStylebox("hover", "PopupMenu", new StyleBoxFlat { BgColor = new Color("#f5d2bc") });
        theme.SetStylebox("separator", "PopupMenu", new StyleBoxLine
        {
            Color = new Color("#b29ba3"), Thickness = 1, GrowBegin = -4, GrowEnd = -4
        });
        return theme;
    }

    private void ShowFeedback(string message)
    {
        if (feedback is null)
        {
            return;
        }

        feedback.MessageLabel.Text = message;
        feedback.Visible = true;
        feedbackSeconds = 3f;
        RefreshMousePassthrough();
    }
}

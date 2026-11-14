using DesktopPet.Core;
using Godot;

namespace DesktopPet.OfficialModManager;

/// <summary>统一 Mod 列表；安装和外观切换通过公共服务完成，窗口及后台导入随 Mod 释放。</summary>
public sealed class ModManagerMod(Node parent, Action<Window>? presentNativeWindow = null) : IMod
{
    public ModManifest Manifest { get; } = new("official-mod-manager", "Mod 管理器", "0.1.0", "1.0");
    private IModManagementService management = null!;
    private IPetAppearanceService appearance = null!;
    private Window window = null!;
    private FileDialog picker = null!;
    private ConfirmationDialog confirmation = null!;
    private VBoxContainer rows = null!;
    private LineEdit search = null!;
    private Label error = null!, empty = null!;
    private Button import = null!;
    private readonly List<MenuButton> rowMenus = [];
    private readonly List<CheckButton> switches = [];
    private readonly List<Texture2D> icons = [];
    private Texture2D moreIcon = null!;
    private ManagedMod[] installed = [];
    private CancellationTokenSource? importCancellation;
    private Task<string>? importTask;
    private bool stopped, busy;
    private string? removalId;

    public void Start(ModContext context)
    {
        management = context.Services.GetRequired<IModManagementService>();
        appearance = context.Services.GetRequired<IPetAppearanceService>();
        window = new Window
        {
            Name = "ModManager", Title = "Mod 管理器", Visible = false,
            Size = new Vector2I(700, 410), MinSize = new Vector2I(500, 280),
            Borderless = false, Transient = false, AlwaysOnTop = false, Theme = CreateTheme(), Transparent = false
        };
        window.CloseRequested += Close;
        window.WindowInput += input => { if (input is InputEventKey { Pressed: true, Keycode: Key.Escape }) Close(); };
        parent.AddChild(window);
        var on = SwitchIcon(true);
        var off = SwitchIcon(false);
        window.Theme.SetIcon("checked", "CheckButton", on);
        window.Theme.SetIcon("unchecked", "CheckButton", off);
        window.Theme.SetIcon("checked_disabled", "CheckButton", SwitchIcon(true, true));
        window.Theme.SetIcon("unchecked_disabled", "CheckButton", SwitchIcon(false, true));
        var background = new PanelContainer();
        background.AddThemeStyleboxOverride("panel", Panel("#faf7f2", 0));
        window.AddChild(background);
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var margin = new MarginContainer();
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 20);
        background.AddChild(margin);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 12);
        margin.AddChild(body);
        var toolbar = new HBoxContainer { Name = "Toolbar" };
        toolbar.AddThemeConstantOverride("separation", 12);
        body.AddChild(toolbar);
        search = new LineEdit
        {
            Name = "SearchMods", PlaceholderText = "搜索 Mod", ClearButtonEnabled = true,
            CustomMinimumSize = new Vector2(0, 44), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        search.AddThemeIconOverride("clear", Icon("<path d='M7 7l10 10M17 7L7 17'/>"));
        toolbar.AddChild(search);
        search.TextChanged += _ => { if (!busy) DrawRows(); };
        import = new Button { Name = "ImportMod", Text = "导入 ZIP", CustomMinimumSize = new Vector2(112, 44) };
        import.AddThemeStyleboxOverride("normal", Panel("#654d70", 10, 6));
        import.AddThemeStyleboxOverride("hover", Panel("#745980", 10, 6));
        import.AddThemeStyleboxOverride("pressed", Panel("#513c5b", 10, 6));
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            import.AddThemeColorOverride(state, Colors.White);
        import.Pressed += () => picker.PopupCentered();
        toolbar.AddChild(import);
        error = Text(body, "", 17, "#9c334c");
        error.Name = "ImportError";
        error.Visible = false;
        error.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        var scroll = new ScrollContainer
        {
            Name = "ModScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        body.AddChild(scroll);
        var listBody = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(listBody);
        rows = new VBoxContainer { Name = "ModList" };
        rows.AddThemeConstantOverride("separation", 0);
        listBody.AddChild(rows);
        empty = Text(listBody, "没有找到匹配的 Mod", 18, "#716575");
        empty.Name = "EmptyState";
        empty.CustomMinimumSize = new Vector2(0, 80);
        empty.HorizontalAlignment = HorizontalAlignment.Center;
        moreIcon = Icon("<circle cx='5' cy='12' r='1' fill='#716575'/><circle cx='12' cy='12' r='1' fill='#716575'/><circle cx='19' cy='12' r='1' fill='#716575'/>");
        picker = new FileDialog
        {
            Name = "ImportPicker", Title = "导入 Mod", Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenFile, UseNativeDialog = true,
            Filters = ["*.zip;Mod 内容包"], Size = new Vector2I(760, 500)
        };
        picker.FileSelected += BeginImport;
        window.AddChild(picker);
        confirmation = new ConfirmationDialog { Name = "RemoveConfirmation", Title = "卸载 Mod", Transient = true, OkButtonText = "卸载", CancelButtonText = "取消" };
        confirmation.Confirmed += () => Run(() =>
        {
            if (removalId is null) return;
            ChangeAppearance(AfterRemovingAppearance(removalId), () => management.Remove(removalId));
        });
        window.AddChild(confirmation);
        Refresh();
    }

    public void Show()
    {
        if (stopped) return;
        if (!busy) Run(Refresh);
        if (!window.Visible)
        {
            var area = DisplayServer.ScreenGetUsableRect();
            window.Position = area.Position + (area.Size - window.Size) / 2;
            window.Show();
        }
        if (window.Mode == Window.ModeEnum.Minimized) window.Mode = Window.ModeEnum.Windowed;
        presentNativeWindow?.Invoke(window);
        window.GrabFocus();
    }

    private void Close()
    {
        importCancellation?.Cancel();
        window.Hide();
    }

    private void Refresh()
    {
        installed = management.List().ToArray();
        DrawRows();
    }

    private void DrawRows()
    {
        foreach (var child in rows.GetChildren()) { rows.RemoveChild(child); child.QueueFree(); }
        rowMenus.Clear();
        switches.Clear();
        foreach (var mod in installed.Where(mod => (mod.Name + mod.Id).Contains(search.Text, StringComparison.OrdinalIgnoreCase)))
        {
            var current = mod.Running && mod.AppearancePath is not null;
            var row = new PanelContainer { TooltipText = mod.Error ?? mod.Description };
            row.SetMeta("mod_id", mod.Id);
            var surface = Panel(current ? "#f2eaf4" : "#ffffff", 14);
            surface.BorderWidthBottom = 1;
            surface.BorderColor = new Color("#e7e0e8");
            row.AddThemeStyleboxOverride("panel", surface);
            rows.AddChild(row);
            var line = new HBoxContainer();
            line.AddThemeConstantOverride("separation", 16);
            row.AddChild(line);
            var name = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            name.AddThemeConstantOverride("separation", 4);
            line.AddChild(name);
            var label = Text(name, mod.Name, 20);
            label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            label.ClipText = true;
            var detail = "v" + mod.Version + (mod.Error is not null ? " · 异常" : current ? " · 当前外观" :
                mod.Source == "内置" && mod.AppearancePath is null && mod.Enabled != mod.Running ? " · 重启生效" : "");
            Text(name, detail, 16, mod.Error is not null ? "#9c334c" : "#716575");
            var toggle = new CheckButton
            {
                Name = "EnableToggle", ButtonPressed = mod.Enabled, Disabled = busy || mod.Required || mod.Error is not null,
                CustomMinimumSize = new Vector2(52, 40), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                TooltipText = mod.Required ? "管理入口默认开启" : mod.Enabled ? "关闭" + mod.Name : "启动" + mod.Name
            };
            toggle.Toggled += enabled => Run(() => SetEnabled(mod.Id, enabled));
            line.AddChild(toggle);
            switches.Add(toggle);
            var hasAppearance = mod.AppearancePath is not null;
            if (mod.Source == "内置" && (!hasAppearance || current))
            {
                line.AddChild(new Control { CustomMinimumSize = new Vector2(40, 40) });
                continue;
            }
            var actions = new MenuButton
            {
                Name = "Actions", Icon = moreIcon, TooltipText = "管理" + mod.Name,
                CustomMinimumSize = new Vector2(40, 40), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, Disabled = busy
            };
            line.AddChild(actions);
            rowMenus.Add(actions);
            var popup = actions.GetPopup();
            if (hasAppearance)
            {
                popup.AddItem("使用此外观", 1);
                popup.SetItemDisabled(0, current || !mod.Enabled || mod.Error is not null);
            }
            if (mod.Source != "内置") { popup.AddSeparator(); popup.AddItem("卸载…", 3); }
            popup.IdPressed += action => Run(() => Act(mod.Id, action));
        }
        empty.Visible = rows.GetChildCount() == 0;
    }

    private void Act(string id, long action)
    {
        var mod = management.List().FirstOrDefault(item => item.Id == id) ?? throw new InvalidOperationException("找不到该 Mod，请重新打开管理器。");
        if (action == 1)
        {
            ChangeAppearance(mod.AppearancePath, () => management.SelectAppearance(id));
        }
        else if (action == 3)
        {
            removalId = id;
            confirmation.DialogText = $"卸载“{mod.Name}”？";
            confirmation.PopupCentered(new Vector2I(420, 160));
            presentNativeWindow?.Invoke(confirmation);
        }
    }

    private void SetEnabled(string id, bool enabled)
    {
        var mod = management.List().First(item => item.Id == id);
        var next = enabled ? mod.AppearancePath ?? management.SelectedAppearancePath : AfterRemovingAppearance(id);
        ChangeAppearance(next, () => management.SetEnabled(id, enabled));
    }

    private string? AfterRemovingAppearance(string id)
    {
        if (management.SelectedAppearanceId != id) return management.SelectedAppearancePath;
        return management.List().FirstOrDefault(mod => mod.Id != id && mod.Enabled && mod.Error is null && mod.AppearancePath is not null)?.AppearancePath;
    }

    private void ApplyAppearance(string? path)
    {
        if (path is null) appearance.ClearAppearance(); else appearance.ApplyAppearance(path);
    }

    private void ChangeAppearance(string? next, Action save)
    {
        var previous = management.SelectedAppearancePath;
        var changed = next != previous;
        // 先解码和验证新外观，失败时不写入启用状态；保存失败则恢复原画面。
        if (changed) ApplyAppearance(next);
        try { save(); }
        catch { if (changed) ApplyAppearance(previous); throw; }
        finally { if (window.Visible) presentNativeWindow?.Invoke(window); }
    }

    private async void BeginImport(string path)
    {
        if (busy || stopped) return;
        busy = true;
        error.Visible = false;
        importCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellation = importCancellation;
        import.Disabled = true;
        import.Text = "导入中…";
        search.Editable = false;
        foreach (var menu in rowMenus) menu.Disabled = true;
        foreach (var toggle in switches) toggle.Disabled = true;
        string? failure = null;
        string? importedId = null;
        var previousAppearance = management.SelectedAppearancePath;
        try
        {
            importTask = Task.Run(() => management.ImportZip(path, cancellation.Token));
            importedId = await importTask;
        }
        catch (OperationCanceledException) { failure = "导入已取消或超时，请重试。"; }
        catch (Exception exception) { failure = "导入失败：" + exception.Message; }
        finally { importTask = null; importCancellation = null; cancellation.Dispose(); }
        if (stopped) return;
        Callable.From(() =>
        {
            if (stopped) return;
            busy = false;
            import.Disabled = false;
            import.Text = "导入 ZIP";
            search.Editable = true;
            if (failure is null) search.Text = "";
            Run(() =>
            {
                var selected = management.SelectedAppearancePath;
                if (selected == previousAppearance) return;
                try { ApplyAppearance(selected); }
                catch
                {
                    // 无外观时导入会成为候选；无法显示的包保留在列表中但关闭，供玩家卸载。
                    if (management.SelectedAppearanceId == importedId && importedId is not null) management.SetEnabled(importedId, false);
                    throw;
                }
                finally { if (window.Visible) presentNativeWindow?.Invoke(window); }
            });
            if (failure is not null) ShowError(failure);
        }).CallDeferred();
    }

    private void Run(Action action)
    {
        if (busy || stopped) return;
        error.Visible = false;
        try { action(); if (action != Refresh) Refresh(); }
        catch (Exception exception) { Refresh(); ShowError(exception.Message); }
    }

    private void ShowError(string message) { error.Text = message; error.Visible = true; }

    public void Stop(ModContext context)
    {
        stopped = true;
        importCancellation?.Cancel();
        // 导入任务只操作文件，退出时可等待它完成取消清理，不依赖 Godot 主线程。
        try { importTask?.GetAwaiter().GetResult(); }
        catch (Exception exception) { if (exception is not OperationCanceledException) GD.PushWarning(exception.Message); }
        importCancellation?.Dispose();
        importCancellation = null;
        window.QueueFree();
        foreach (var icon in icons) icon.Dispose();
        icons.Clear();
    }

    private Texture2D Icon(string geometry)
    {
        using var image = new Image();
        image.LoadSvgFromString($"<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24' fill='none' stroke='#716575' stroke-width='1.8' stroke-linecap='round'>{geometry}</svg>");
        var texture = ImageTexture.CreateFromImage(image);
        icons.Add(texture);
        return texture;
    }

    private Texture2D SwitchIcon(bool enabled, bool disabled = false)
    {
        using var image = new Image();
        var color = disabled ? "#b6a5be" : enabled ? "#654d70" : "#b7afb9";
        image.LoadSvgFromString($"<svg xmlns='http://www.w3.org/2000/svg' width='44' height='26'><rect width='44' height='26' rx='13' fill='{color}'/><circle cx='{(enabled ? 31 : 13)}' cy='13' r='9' fill='white'/></svg>");
        var texture = ImageTexture.CreateFromImage(image);
        icons.Add(texture);
        return texture;
    }

    private static Label Text(Node owner, string text, int size, string color = "#302339")
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        owner.AddChild(label);
        return label;
    }

    private static StyleBoxFlat Panel(string color, int padding, int radius = 0) => new()
    {
        BgColor = new Color(color), ContentMarginLeft = padding, ContentMarginRight = padding,
        ContentMarginTop = padding, ContentMarginBottom = padding,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius
    };

    private static Theme CreateTheme()
    {
        var theme = new Theme { DefaultFontSize = 18 };
        foreach (var type in new[] { "Button", "MenuButton", "LineEdit", "PopupMenu" })
        {
            foreach (var color in new[] { "font_color", "font_selected_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
                theme.SetColor(color, type, new Color("#302339"));
            theme.SetColor("font_disabled_color", type, new Color("#8b8189"));
            theme.SetStylebox("normal", type, Panel("#ffffff00", 8, 6));
            theme.SetStylebox("hover", type, Panel("#eee5f0", 8, 6));
            theme.SetStylebox("pressed", type, Panel("#e4d6e9", 8, 6));
            theme.SetStylebox("disabled", type, Panel("#f0ecee", 8, 6));
            var focus = Panel("#ffffff00", 0, 6);
            focus.BorderColor = new Color("#9776a5");
            focus.SetBorderWidthAll(2);
            theme.SetStylebox("focus", type, focus);
        }
        var input = Panel("#ffffff", 12, 6);
        input.BorderColor = new Color("#ddd4df");
        input.SetBorderWidthAll(1);
        theme.SetStylebox("normal", "LineEdit", input);
        theme.SetColor("font_placeholder_color", "LineEdit", new Color("#716575"));
        theme.SetColor("caret_color", "LineEdit", new Color("#654d70"));
        theme.SetStylebox("panel", "PopupMenu", Panel("#fffdf9", 12, 6));
        theme.SetConstant("v_separation", "PopupMenu", 12);
        theme.SetStylebox("panel", "AcceptDialog", Panel("#faf7f2", 16, 6));
        return theme;
    }
}

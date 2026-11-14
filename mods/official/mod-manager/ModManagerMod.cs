using DesktopPet.Core;
using Godot;

namespace DesktopPet.OfficialModManager;

/// <summary>官方管理界面；通过公开服务管理内容，窗口随 Mod 生命周期释放。</summary>
public sealed class ModManagerMod(Node parent, Action<Window>? presentNativeWindow = null) : IMod
{
    public ModManifest Manifest { get; } = new("official-mod-manager", "Mod 管理器", "0.1.0", "1.0");
    private IModManagementService management = null!;
    private IPetAppearanceService appearance = null!;
    private ISteamService steam = null!;
    private Window window = null!;
    private FileDialog picker = null!;
    private ConfirmationDialog confirmation = null!;
    private ItemList list = null!;
    private LineEdit search = null!;
    private Label title = null!, metadata = null!, description = null!, state = null!, notice = null!, steamStatus = null!;
    private Button toggle = null!, use = null!, remove = null!;
    private ItemList subscriptions = null!;
    private Button importSubscription = null!;
    private readonly List<Button> controls = [];
    private ManagedMod[] visibleMods = [];
    private IReadOnlyList<WorkshopSubscription> workshopItems = [];
    private CancellationTokenSource? importCancellation;
    private Task<string>? importTask;
    private bool stopped, busy;
    private string? selectedId, removalId;

    public void Start(ModContext context)
    {
        management = context.Services.GetRequired<IModManagementService>();
        appearance = context.Services.GetRequired<IPetAppearanceService>();
        steam = context.Services.GetRequired<ISteamService>();
        window = new Window
        {
            Name = "ModManager", Title = "Mod 管理器 · 桌面宠物模拟器", Visible = false,
            Size = new Vector2I(920, 650), MinSize = new Vector2I(780, 560),
            Transient = true, Theme = CreateTheme(), Transparent = false
        };
        window.CloseRequested += () => { importCancellation?.Cancel(); window.Hide(); };
        parent.AddChild(window);
        var background = new PanelContainer();
        background.AddThemeStyleboxOverride("panel", Panel("#faf7f2", 0, 0));
        window.AddChild(background);
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var margin = new MarginContainer();
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 24);
        background.AddChild(margin);
        var body = Column(margin, 16);
        var heading = new HBoxContainer();
        body.AddChild(heading);
        var headingText = Column(heading, 4);
        headingText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        Text(headingText, "给桌宠添点新玩法", 28);
        Text(headingText, "管理你的 Mod，发现不同的陪伴方式。", 18, "#716575");
        var close = Button(heading, "完成", "CloseManager", () => { importCancellation?.Cancel(); window.Hide(); });
        close.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        controls.Remove(close);
        var tabs = new TabContainer { Name = "ModTabs", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddChild(tabs);
        BuildInstalled(tabs);
        BuildSteam(tabs);
        notice = Text(body, "本地外观包无需 Steam，导入后选择“使用此外观”即可切换。", 17, "#716575");
        notice.Name = "Notice";
        notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        picker = new FileDialog
        {
            Name = "ImportPicker", Title = "导入外观 Mod", Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenFile, UseNativeDialog = true,
            Filters = ["*.zip;Mod 内容包"], Size = new Vector2I(760, 500)
        };
        picker.FileSelected += path => BeginImport(path);
        window.AddChild(picker);
        confirmation = new ConfirmationDialog { Name = "RemoveConfirmation", Title = "卸载 Mod", OkButtonText = "卸载", CancelButtonText = "取消" };
        confirmation.Confirmed += () => Run(() =>
        {
            if (removalId is null) return;
            management.Remove(removalId);
            appearance.ApplyAppearance(management.SelectedAppearancePath);
            SetNotice("已卸载。原内容保留在本地恢复目录中。");
        });
        window.AddChild(confirmation);
        Refresh();
        Run(RefreshSteam);
    }

    private void BuildInstalled(TabContainer tabs)
    {
        var page = new VBoxContainer { Name = "已安装", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 16);
        tabs.AddChild(page);
        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 10);
        page.AddChild(toolbar);
        Button(toolbar, "导入 ZIP", "ImportMod", () => picker.PopupCentered());
        Button(toolbar, "打开 Mod 文件夹", "OpenMods", () => OS.ShellOpen(management.ModsDirectory));
        Button(toolbar, "刷新", "RefreshMods", () => Run(Refresh));
        search = new LineEdit { Name = "SearchMods", PlaceholderText = "搜索名称或 ID", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        toolbar.AddChild(search);
        search.TextChanged += _ => { if (!busy) Refresh(); };
        var split = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        page.AddChild(split);
        list = new ItemList { Name = "ModList", CustomMinimumSize = new Vector2(280, 0), SizeFlagsHorizontal = Control.SizeFlags.Fill };
        list.AddThemeConstantOverride("v_separation", 18);
        split.AddChild(list);
        list.ItemSelected += index => { selectedId = visibleMods[index].Id; ShowDetails(); };
        var detailPanel = new PanelContainer { CustomMinimumSize = new Vector2(360, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        detailPanel.AddThemeStyleboxOverride("panel", Panel("#ffffff", 16, 10));
        split.AddChild(detailPanel);
        var details = Column(detailPanel, 12);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 130), SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        details.AddChild(scroll);
        var information = Column(scroll, 12);
        information.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title = Text(information, "", 24);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        metadata = Text(information, "", 16, "#716575");
        metadata.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        state = Text(information, "", 18, "#6e486f");
        description = Text(information, "", 18);
        description.Name = "ModDescription";
        description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        description.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        use = Button(details, "使用此外观", "UseAppearance", () => Run(SelectAppearance));
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 12);
        details.AddChild(actions);
        toggle = Button(actions, "禁用", "ToggleMod", () => Run(() =>
        {
            var mod = Selected();
            if (mod is null) return;
            management.SetEnabled(mod.Id, !mod.Enabled);
            appearance.ApplyAppearance(management.SelectedAppearancePath);
            SetNotice(mod.Source == "内置" ? "已保存；重新启动桌宠后生效。" : "已更新启用状态。");
        }));
        toggle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        remove = Button(actions, "卸载…", "RemoveMod", () =>
        {
            var mod = Selected();
            if (mod is null) return;
            removalId = mod.Id;
            confirmation.DialogText = $"卸载“{mod.Name}”？\n若正在使用此外观，桌宠将恢复默认蘑菇。";
            confirmation.PopupCentered(new Vector2I(480, 180));
        });
        remove.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    }

    private void BuildSteam(TabContainer tabs)
    {
        var page = new VBoxContainer { Name = "Steam 创意工坊" };
        page.AddThemeConstantOverride("separation", 16);
        tabs.AddChild(page);
        Text(page, "从创意工坊带回新朋友", 24);
        steamStatus = Text(page, "", 18);
        steamStatus.Name = "SteamStatus";
        steamStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 12);
        page.AddChild(toolbar);
        Button(toolbar, "刷新状态", "RefreshSteam", () => Run(RefreshSteam));
        var browse = Button(toolbar, "浏览创意工坊 ↗", "BrowseWorkshop", () =>
        {
            if (steam.Status.AppId != 0) OS.ShellOpen($"https://steamcommunity.com/app/{steam.Status.AppId}/workshop/");
        });
        browse.Disabled = steam.Status.AppId == 0;
        // 未配置 App ID 的入口不会在导入完成后被统一恢复启用。
        controls.Remove(browse);
        subscriptions = new ItemList { Name = "Subscriptions", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        page.AddChild(subscriptions);
        subscriptions.ItemSelected += _ => UpdateSubscriptionAction();
        importSubscription = Button(page, "导入所选订阅", "ImportSubscription", () =>
        {
            var selected = subscriptions.GetSelectedItems();
            if (selected.Length == 0) return;
            var item = workshopItems[selected[0]];
            if (item.Installed && !item.NeedsUpdate && item.Directory is not null)
                BeginImport(System.IO.Path.Combine(item.Directory, "mod.zip"));
        });
        var note = Text(page, "订阅内容由 Steam 下载。下载完成后导入到本地，更新时不会自动覆盖你的当前外观。", 17, "#716575");
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
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

    private void Refresh()
    {
        visibleMods = management.List().Where(mod => (mod.Name + mod.Id).Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToArray();
        list.Clear();
        foreach (var mod in visibleMods)
            list.AddItem($"{mod.Name}  ·  {(mod.Error is not null ? "异常" : mod.Running && mod.AppearancePath is not null ? "使用中" : mod.Enabled ? "已启用" : "已禁用")}");
        var index = Array.FindIndex(visibleMods, mod => mod.Id == selectedId);
        if (index < 0 && visibleMods.Length > 0) index = 0;
        selectedId = index >= 0 ? visibleMods[index].Id : null;
        if (index >= 0) list.Select(index);
        ShowDetails();
    }

    private ManagedMod? Selected() => visibleMods.FirstOrDefault(mod => mod.Id == selectedId);

    private void ShowDetails()
    {
        var mod = Selected();
        title.Text = mod?.Name ?? "没有找到 Mod";
        metadata.Text = mod is null ? "试试其他关键词，或导入一个外观包。" : $"{mod.Source}  /  v{mod.Version}\n{mod.Id}";
        state.Text = mod is null ? "" : mod.Error is not null ? "需要处理" : mod.Required ? "内置基础能力 · 保持启用" :
            mod.Source == "内置" && mod.Enabled != mod.Running ? "重新启动后生效" : mod.Running && mod.AppearancePath is not null ? "当前使用中" : mod.Enabled ? "已启用" : "已禁用";
        description.Text = mod?.Error ?? mod?.Description ?? "";
        use.Visible = mod?.AppearancePath is not null || mod?.Id == "official-mushroom";
        use.Text = mod?.Id == "official-mushroom" ? "恢复默认蘑菇" : "使用此外观";
        use.Disabled = busy || mod is null || !mod.Enabled || mod.Error is not null;
        toggle.Text = mod?.Enabled == true ? "禁用" : "启用";
        toggle.Disabled = busy || mod is null || mod.Required || mod.Error is not null;
        remove.Disabled = busy || mod is null || mod.Source == "内置";
    }

    private void SelectAppearance()
    {
        var mod = Selected();
        if (mod is null) return;
        var previous = management.SelectedAppearancePath;
        // 先解码并显示，保存失败时恢复旧画面，避免坏图片成为下次启动的默认选择。
        appearance.ApplyAppearance(mod.AppearancePath);
        try { management.SelectAppearance(mod.Id == "official-mushroom" ? null : mod.Id); }
        catch { appearance.ApplyAppearance(previous); throw; }
        SetNotice($"已切换为{mod.Name}。");
    }

    private async void BeginImport(string path)
    {
        if (busy || stopped) return;
        busy = true;
        importCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellation = importCancellation;
        foreach (var button in controls) button.Disabled = true;
        search.Editable = false;
        list.MouseFilter = Control.MouseFilterEnum.Ignore;
        SetNotice("正在校验并导入… 关闭窗口可取消。");
        string? id = null;
        string message;
        var failed = false;
        try
        {
            importTask = Task.Run(() => management.ImportZip(path, cancellation.Token));
            id = await importTask;
            message = "导入完成。选择“使用此外观”来切换；缺少依赖的 Mod 会保持禁用。";
        }
        catch (OperationCanceledException) { message = "导入已取消或超过 30 秒，未完成的内容已清理。"; }
        catch (Exception exception) { message = "导入失败：" + exception.Message; failed = true; }
        finally { importTask = null; importCancellation = null; cancellation.Dispose(); }
        // Godot 对象只在主线程更新；退出后不再投递 UI 回调。
        if (stopped) return;
        Callable.From(() =>
        {
            if (stopped) return;
            importCancellation = null;
            busy = false;
            foreach (var button in controls) button.Disabled = false;
            search.Editable = true;
            list.MouseFilter = Control.MouseFilterEnum.Stop;
            selectedId = id ?? selectedId;
            Run(Refresh);
            UpdateSubscriptionAction();
            SetNotice(message, failed);
        }).CallDeferred();
    }

    private void RefreshSteam()
    {
        workshopItems = steam.GetSubscriptions();
        steamStatus.Text = steam.Status.Message + (steam.Status.PlayerName is { } name ? $" · {name}" : "");
        subscriptions.Clear();
        foreach (var item in workshopItems)
            subscriptions.AddItem($"{item.Id}  ·  {(item.NeedsUpdate ? "等待 Steam 更新" : item.Installed ? "已下载" : "等待 Steam 下载")}");
        if (workshopItems.Count == 0)
        {
            subscriptions.AddItem(steam.Status.Available ? "还没有订阅内容" : "连接 Steam 后，这里会显示你订阅的 Mod。");
            subscriptions.SetItemSelectable(0, false);
        }
        UpdateSubscriptionAction();
    }

    private void UpdateSubscriptionAction()
    {
        var selection = subscriptions.GetSelectedItems();
        importSubscription.Disabled = busy || selection.Length == 0 || selection[0] >= workshopItems.Count ||
            !workshopItems[selection[0]].Installed || workshopItems[selection[0]].NeedsUpdate;
    }

    private void Run(Action action)
    {
        if (busy || stopped) return;
        try { action(); Refresh(); }
        catch (Exception exception) { SetNotice(exception.Message, true); }
    }

    private void SetNotice(string message, bool error = false)
    {
        notice.Text = message;
        notice.AddThemeColorOverride("font_color", new Color(error ? "#9c334c" : "#716575"));
    }

    public void Stop(ModContext context)
    {
        stopped = true;
        importCancellation?.Cancel();
        // 等待纯文件任务结束，确保暂存目录已清理；任务不依赖 Godot 主线程。
        try { importTask?.GetAwaiter().GetResult(); }
        catch (Exception exception)
        {
            if (exception is not OperationCanceledException) GD.PushWarning("退出时导入已结束：" + exception.Message);
        }
        importCancellation?.Dispose();
        importCancellation = null;
        window.QueueFree();
    }

    private Button Button(Node owner, string text, string name, Action action)
    {
        var button = new Button { Name = name, Text = text, CustomMinimumSize = new Vector2(0, 42) };
        button.Pressed += action;
        owner.AddChild(button);
        controls.Add(button);
        return button;
    }

    private static VBoxContainer Column(Node owner, int gap)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", gap);
        owner.AddChild(column);
        return column;
    }

    private static Label Text(Node owner, string text, int size, string color = "#302339")
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        owner.AddChild(label);
        return label;
    }

    private static StyleBoxFlat Panel(string color, int padding, int radius) => new()
    {
        BgColor = new Color(color), ContentMarginLeft = padding, ContentMarginRight = padding,
        ContentMarginTop = padding, ContentMarginBottom = padding,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius
    };

    private static Theme CreateTheme()
    {
        var theme = new Theme { DefaultFontSize = 18 };
        foreach (var type in new[] { "Button", "LineEdit", "ItemList", "TabContainer" })
        {
            foreach (var color in new[] { "font_color", "font_selected_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
                theme.SetColor(color, type, new Color("#302339"));
            theme.SetColor("font_disabled_color", type, new Color("#8b8189"));
        }
        theme.SetStylebox("normal", "Button", Panel("#ede3ec", 10, 7));
        theme.SetStylebox("hover", "Button", Panel("#e5d4e5", 10, 7));
        theme.SetStylebox("pressed", "Button", Panel("#d7bed8", 10, 7));
        theme.SetStylebox("disabled", "Button", Panel("#f0ecee", 10, 7));
        theme.SetStylebox("normal", "LineEdit", Panel("#ffffff", 10, 7));
        theme.SetColor("font_placeholder_color", "LineEdit", new Color("#827583"));
        theme.SetColor("caret_color", "LineEdit", new Color("#4a3154"));
        theme.SetStylebox("panel", "ItemList", Panel("#ffffff", 12, 10));
        theme.SetStylebox("selected", "ItemList", Panel("#ecdbea", 8, 6));
        theme.SetStylebox("selected_focus", "ItemList", Panel("#ecdbea", 8, 6));
        theme.SetStylebox("panel", "TabContainer", Panel("#faf7f2", 12, 0));
        theme.SetStylebox("tab_selected", "TabContainer", Panel("#ecdbea", 12, 7));
        theme.SetStylebox("tab_unselected", "TabContainer", Panel("#f2ecef", 12, 7));
        theme.SetColor("font_unselected_color", "TabContainer", new Color("#716575"));
        return theme;
    }
}

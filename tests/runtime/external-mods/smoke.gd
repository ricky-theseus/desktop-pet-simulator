extends SceneTree

var failures := 0
var checks := 0
var options := {}

func _initialize() -> void:
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--qa-") and "=" in arg:
            var pair = arg.split("=", true, 1)
            options[pair[0].trim_prefix("--qa-")] = pair[1]
    call_deferred("run")

func check(value: bool, label: String) -> void:
    checks += 1
    if not value:
        failures += 1
        push_error("EXTERNAL MOD QA: " + label)

func find_toggle(manager: Node, id: String) -> CheckButton:
    var list = manager.find_child("ModList", true, false)
    if list:
        for row in list.get_children():
            if row.has_meta("mod_id") and str(row.get_meta("mod_id")) == id:
                return row.find_child("EnableToggle", true, false)
    return null

func invoke(menu: PopupMenu, label: String) -> bool:
    for index in menu.item_count:
        if menu.get_item_text(index) == label:
            menu.id_pressed.emit(menu.get_item_id(index))
            return true
    return false

func lamp_window(main: Node) -> Window:
    var scope = main.get_node_or_null("Mod_example-desk-lamp")
    if scope:
        for child in scope.get_children():
            if child is Window: return child
    return null

func run() -> void:
    var main = load("res://Main.tscn").instantiate()
    root.add_child(main)
    await create_timer(.5).timeout
    var menu: PopupMenu
    for child in main.get_children():
        if child is PopupMenu and child.name != "TrayMenu": menu = child
    check(menu != null, "主程序独立启动")
    if menu == null:
        finish(main)
        return
    check(invoke(menu, "Mod 管理器…"), "管理入口可恢复")
    await process_frame
    var manager = main.get_node_or_null("ModManager")
    check(manager is Window and manager.visible, "管理器可显示")
    if manager == null:
        finish(main)
        return
    if options.has("bad-packages"):
        var rows = manager.find_child("ModList", true, false).get_children()
        for id in ["bad-json", "broken-code", "duplicate-package"]:
            var found_error = false
            for row in rows:
                if row.has_meta("mod_id") and str(row.get_meta("mod_id")) == id:
                    for label in row.find_children("*", "Label", true, false):
                        if "异常" in label.text: found_error = true
            check(found_error, "异常包被隔离并可查看原因：" + id)
        check(main.get_node_or_null("Mod_broken-code") == null, "坏 DLL 启动失败后释放场景容器")
        check(main.get_node_or_null("Mod_duplicate-package") == null, "重复 ID 包没有执行代码")
        check(main.get_node_or_null("Mod_official-mushroom") == null, "碰撞内置 ID 不会替换内置入口")
    if options.get("mode", "run") == "install":
        check(find_toggle(manager, "example-desk-lamp") == null, "启动时尚未安装新玩法")
        var picker: FileDialog
        for child in manager.get_children():
            if child is FileDialog: picker = child
        check(picker != null, "导入对话框存在")
        if picker:
            picker.file_selected.emit(options["zip"])
            for attempt in 200:
                if find_toggle(manager, "example-desk-lamp") != null: break
                await create_timer(.05).timeout
        var toggle = find_toggle(manager, "example-desk-lamp")
        check(toggle != null and toggle.button_pressed, "ZIP 导入后出现在统一列表")
        check(lamp_window(main) == null, "新代码包等待重启，不虚报运行中")
        check(FileAccess.file_exists(options["installed-manifest"]), "跨盘导入落在指定 mods 目录")
    else:
        var window = lamp_window(main)
        check(window != null and window.visible, "未知玩法的独立桌面窗口已出现")
        check(find_toggle(manager, "example-desk-lamp") != null, "新玩法无需内置注册即可管理")
        if window:
            var glow = window.find_children("*", "ColorRect", true, false)[0] as ColorRect
            var buttons = window.find_children("*", "Button", true, false)
            # 重复运行会恢复上次的关灯存档，先打开灯再验证更新回调。
            if buttons[0].text == "开灯": buttons[0].pressed.emit()
            var color_before = glow.color
            await create_timer(.4).timeout
            check(glow.color != color_before, "逐帧公开服务驱动呼吸动画")
            buttons[0].pressed.emit()
            check(FileAccess.file_exists(options["state"]), "独立存档写入用户目录")
            window.close_requested.emit()
            check(not window.visible, "窗口关闭只收起")
            check(invoke(menu, "打开桌面小灯") and window.visible, "共享菜单可重新打开窗口")
            if options.has("capture"):
                await RenderingServer.frame_post_draw
                window.get_texture().get_image().save_png(options["capture"])
        var toggle = find_toggle(manager, "example-desk-lamp")
        if toggle:
            toggle.button_pressed = false
            await process_frame
            await process_frame
            check(lamp_window(main) == null, "停用释放窗口与容器")
            check(not invoke(menu, "打开桌面小灯"), "停用释放共享菜单命令")
            find_toggle(manager, "example-desk-lamp").button_pressed = true
            await process_frame
            window = lamp_window(main)
            check(window != null and window.visible, "同一代码包可以再次启用")
    finish(main)

func finish(main: Node) -> void:
    if options.has("report"):
        var file = FileAccess.open(options["report"], FileAccess.WRITE)
        file.store_string(JSON.stringify({"checks": checks, "failures": failures}, "  "))
    main.queue_free()
    await process_frame
    print("External mod QA: ", "PASS" if failures == 0 else "FAIL", " (", checks, " checks)")
    quit(0 if failures == 0 else 1)

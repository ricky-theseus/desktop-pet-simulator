extends SceneTree

var failures := 0
var package_path := ""
var capture_dir := ""
var data_dir := ""

func _initialize():
    for argument in OS.get_cmdline_user_args():
        if argument.begins_with("--test-package="):
            package_path = argument.trim_prefix("--test-package=")
        if argument.begins_with("--capture-dir="):
            capture_dir = argument.trim_prefix("--capture-dir=")
        if argument.begins_with("--mod-data-dir="):
            data_dir = argument.trim_prefix("--mod-data-dir=")
    call_deferred("run_checks")

func check(condition: bool, message: String):
    if not condition:
        failures += 1
        push_error(message)

func find_control(manager: Window, name: String):
    return manager.find_child(name, true, false)

func row_for(manager: Window, id: String):
    for row in find_control(manager, "ModList").get_children():
        if row.get_meta("mod_id") == id:
            return row
    return null

func act(manager: Window, id: String, action: int):
    var row = row_for(manager, id)
    check(row != null, "找不到 Mod：" + id)
    if row == null:
        return
    var actions: MenuButton = row.find_child("Actions", true, false)
    check(actions != null, "应有行内操作菜单")
    if actions != null:
        actions.get_popup().emit_signal("id_pressed", action)

func filter_mods(manager: Window, text: String):
    var search: LineEdit = find_control(manager, "SearchMods")
    search.text = text
    search.emit_signal("text_changed", text)

func set_enabled(manager: Window, id: String, enabled: bool):
    var toggle: CheckButton = row_for(manager, id).find_child("EnableToggle", true, false)
    check(not toggle.disabled, "可选 Mod 应能操作开关：" + id)
    toggle.set_pressed_no_signal(enabled)
    toggle.emit_signal("toggled", enabled)

func wait_import(manager: Window, path: String):
    find_control(manager, "ImportPicker").emit_signal("file_selected", path)
    var deadline := Time.get_ticks_msec() + 10000
    while find_control(manager, "ImportMod").disabled and Time.get_ticks_msec() < deadline:
        await process_frame
    check(not find_control(manager, "ImportMod").disabled, "导入应在有界时间内完成")

func transparent_package() -> String:
    var path := data_dir.path_join("transparent.zip")
    var source := ZIPReader.new()
    check(source.open(package_path) == OK, "应能读取测试包")
    var output := ZIPPacker.new()
    check(output.open(path) == OK, "应能创建透明图片测试包")
    for file in source.get_files():
        var content := source.read_file(file)
        if file == "manifest.json":
            var manifest = JSON.parse_string(content.get_string_from_utf8())
            manifest.id = "transparent-pet"
            manifest.formatVersion = int(manifest.formatVersion)
            content = JSON.stringify(manifest).to_utf8_buffer()
        elif file.ends_with(".png"):
            var pixels := Image.new()
            check(pixels.load_png_from_buffer(content) == OK, "测试帧应为有效 PNG")
            pixels.fill(Color.TRANSPARENT)
            content = pixels.save_png_to_buffer()
        output.start_file(file)
        output.write_file(content)
        output.close_file()
    output.close()
    source.close()
    return path

func capture(manager: Window, name: String):
    if capture_dir.is_empty() or DisplayServer.get_name() == "headless":
        return
    await process_frame
    await process_frame
    await RenderingServer.frame_post_draw
    manager.get_texture().get_image().save_png(capture_dir.path_join(name + ".png"))

func run_checks():
    var main = load("res://Main.tscn").instantiate()
    root.add_child(main)
    await process_frame
    var manager: Window = main.get_node("ModManager")
    check(not manager.visible, "管理器初始应隐藏")
    main.get_child(1).emit_signal("id_pressed", 5)
    await process_frame
    check(manager.visible, "右键菜单应打开管理器")
    check(find_control(manager, "ModList").get_child_count() == 3, "新档应包含三个官方 Mod")
    if DisplayServer.get_name() != "headless":
        check(manager.borderless, "管理器不应显示冗余系统标题栏")
    check(find_control(manager, "ModTabs") == null, "应只有统一列表，不再划分工坊分页")
    check(not find_control(manager, "ImportError").visible, "默认不应有常驻提示")
    var default_toggle: CheckButton = row_for(manager, "official-mushroom").find_child("EnableToggle", true, false)
    check(default_toggle.button_pressed and not default_toggle.disabled, "默认外观应可关闭")
    var manager_toggle: CheckButton = row_for(manager, "official-mod-manager").find_child("EnableToggle", true, false)
    check(manager_toggle.button_pressed and manager_toggle.disabled, "管理入口应默认开启并保留")
    await capture(manager, "manager-installed")
    filter_mods(manager, "STEAM")
    check(find_control(manager, "ModList").get_child_count() == 1, "搜索应忽略大小写")
    filter_mods(manager, "没有这个mod")
    check(find_control(manager, "EmptyState").visible, "搜索无匹配应显示空状态")
    await capture(manager, "manager-empty")
    filter_mods(manager, "")
    manager.size = Vector2i(500, 280)
    await process_frame
    await process_frame
    check(find_control(manager, "Toolbar").get_global_rect().end.x <= manager.size.x, "窄窗口工具栏不能溢出")
    await capture(manager, "manager-compact")
    manager.size = Vector2i(700, 410)
    if not package_path.is_empty():
        find_control(manager, "ImportPicker").emit_signal("file_selected", package_path)
        # 导入异步执行，等待有界时间并检查实际 UI 结果。
        var deadline := Time.get_ticks_msec() + 10000
        while find_control(manager, "ImportMod").disabled and Time.get_ticks_msec() < deadline:
            await process_frame
        check(find_control(manager, "ModList").get_child_count() == 4, "ZIP 导入后应出现第四个 Mod")
        if row_for(manager, "example-sleepy-mushroom") != null:
            act(manager, "example-sleepy-mushroom", 1)
            check(main.get_child(0).get("DisplayName") == "瞌睡蘑菇", "导入的原始 PNG 应能即时显示")
            await capture(manager, "manager-imported")
            await process_frame
            await process_frame
            check(not find_control(manager, "ImportError").visible, "成功操作不应留下常驻提示")
            set_enabled(manager, "example-sleepy-mushroom", false)
            check(main.get_child(0).get("DisplayName") == "蘑菇", "禁用当前外观应恢复默认")
            set_enabled(manager, "example-sleepy-mushroom", true)
            find_control(manager, "CloseManager").emit_signal("pressed")
            check(not manager.visible, "关闭管理器不应退出桌宠")
            main.queue_free()
            await process_frame
            main = load("res://Main.tscn").instantiate()
            root.add_child(main)
            await process_frame
            check(main.get_child(0).get("DisplayName") == "瞌睡蘑菇", "重新创建宿主后应恢复选择")
            manager = main.get_node("ModManager")
            main.get_child(1).emit_signal("id_pressed", 5)
            act(manager, "example-sleepy-mushroom", 3)
            check(find_control(manager, "RemoveConfirmation").visible, "卸载前应显示确认")
            find_control(manager, "RemoveConfirmation").emit_signal("confirmed")
            find_control(manager, "RemoveConfirmation").hide()
            check(find_control(manager, "ModList").get_child_count() == 3, "确认卸载后应移出列表")
            check(main.get_child(0).get("DisplayName") == "蘑菇", "卸载当前外观应恢复默认")
        find_control(manager, "ImportPicker").emit_signal("file_selected", package_path + ".missing")
        deadline = Time.get_ticks_msec() + 10000
        while find_control(manager, "ImportMod").disabled and Time.get_ticks_msec() < deadline:
            await process_frame
        check(find_control(manager, "ImportError").visible, "导入失败必须显示原因")
        check(find_control(manager, "ModList").get_child_count() == 3, "失败导入不能改变列表")
    if not package_path.is_empty() and not data_dir.is_empty():
        var transparent := transparent_package()
        await wait_import(manager, transparent)
        set_enabled(manager, "official-mushroom", false)
        check(find_control(manager, "ImportError").visible, "回退到不可显示的外观应显示失败原因")
        check(row_for(manager, "official-mushroom").find_child("EnableToggle", true, false).button_pressed, "回退失败不能保存默认外观关闭状态")
        check(main.get_child(0).visible, "回退失败应保留原画面")
        set_enabled(manager, "transparent-pet", false)
        set_enabled(manager, "transparent-pet", true)
        check(find_control(manager, "ImportError").visible, "无法显示的外观不应启用成功")
        check(not row_for(manager, "transparent-pet").find_child("EnableToggle", true, false).button_pressed, "启用失败应保留原关闭状态")
        var state = JSON.parse_string(FileAccess.get_file_as_string(data_dir.path_join("mods-state.json")))
        check(state.Disabled.has("transparent-pet") and state.SelectedAppearance != "transparent-pet", "失败选择不能写入配置")
        act(manager, "transparent-pet", 3)
        find_control(manager, "RemoveConfirmation").emit_signal("confirmed")
        find_control(manager, "RemoveConfirmation").hide()
        set_enabled(manager, "official-mushroom", false)
        await wait_import(manager, transparent)
        check(row_for(manager, "transparent-pet") != null, "失败显示的新包仍应出现在列表中供管理")
        check(not row_for(manager, "transparent-pet").find_child("EnableToggle", true, false).button_pressed, "全关闭时导入不可显示的包应自动停用该包")
        check(not main.get_child(0).visible, "导入失败不能恢复已关闭的默认外观")
    else:
        set_enabled(manager, "official-mushroom", false)
    check(not main.get_child(0).visible, "全部外观关闭后桌宠应消失")
    find_control(manager, "CloseManager").emit_signal("pressed")
    await process_frame
    check(not manager.visible and is_instance_valid(main), "叉掉窗口应继续在后台运行")
    if DisplayServer.has_feature(DisplayServer.FEATURE_STATUS_INDICATOR):
        var tray: StatusIndicator = main.get_node("DesktopTray")
        check(tray.visible, "没有宠物时应保留托盘入口")
        tray.emit_signal("pressed", MOUSE_BUTTON_LEFT, Vector2i.ZERO)
    else:
        main.get_child(1).emit_signal("id_pressed", 5)
    check(manager.visible, "关闭全部外观后仍应能打开管理器")
    main.queue_free()
    await process_frame
    main = load("res://Main.tscn").instantiate()
    root.add_child(main)
    await process_frame
    check(not main.get_child(0).visible, "重启后不能擅自恢复已关闭的默认外观")
    manager = main.get_node("ModManager")
    main.get_child(1).emit_signal("id_pressed", 5)
    set_enabled(manager, "official-mushroom", true)
    check(main.get_child(0).visible, "重新打开外观开关应恢复宠物")
    if not package_path.is_empty() and not data_dir.is_empty():
        find_control(manager, "ImportPicker").emit_signal("file_selected", package_path)
        main.queue_free()
        await process_frame
        await process_frame
        var directory := DirAccess.open(data_dir)
        check(directory != null, "独立测试档应存在")
        if directory != null:
            for child in directory.get_directories():
                check(not child.begins_with("staging-"), "卸载管理器必须等待导入清理完成")
    print("Mod manager smoke: ", "PASS" if failures == 0 else "FAIL", " (", failures, " failures)")
    quit(0 if failures == 0 else 1)

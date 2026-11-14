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

func select_mod(manager: Window, text: String):
    var list: ItemList = find_control(manager, "ModList")
    for index in list.item_count:
        if text in list.get_item_text(index):
            list.select(index)
            list.emit_signal("item_selected", index)
            return true
    check(false, "找不到 Mod：" + text)
    return false

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
    check(find_control(manager, "ModList").item_count == 3, "新档应包含三个官方 Mod")
    check(find_control(manager, "ToggleMod").disabled, "默认外观不可禁用")
    check("App ID" in find_control(manager, "SteamStatus").text, "Steam 应说明未配置 App ID")
    check(find_control(manager, "BrowseWorkshop").disabled, "无 App ID 时不能打开错误的工坊页面")
    await capture(manager, "manager-installed")
    if not package_path.is_empty():
        find_control(manager, "ImportPicker").emit_signal("file_selected", package_path)
        # 导入异步执行，等待有界时间并检查实际 UI 结果。
        var deadline := Time.get_ticks_msec() + 10000
        while find_control(manager, "ImportMod").disabled and Time.get_ticks_msec() < deadline:
            await process_frame
        check(find_control(manager, "ModList").item_count == 4, "ZIP 导入后应出现第四个 Mod")
        if select_mod(manager, "瞌睡蘑菇"):
            find_control(manager, "UseAppearance").emit_signal("pressed")
            check(main.get_child(0).get("DisplayName") == "瞌睡蘑菇", "导入的原始 PNG 应能即时显示")
            await capture(manager, "manager-imported")
            await process_frame
            await process_frame
            var notice: Label = find_control(manager, "Notice")
            var description: Label = find_control(manager, "ModDescription")
            check(notice.get_global_rect().end.y <= manager.size.y, "底部反馈必须在窗口范围内")
            check(description.size.y >= 24, "外观描述不能被压成零高度")
            find_control(manager, "ToggleMod").emit_signal("pressed")
            check(main.get_child(0).get("DisplayName") == "蘑菇", "禁用当前外观应恢复默认")
            find_control(manager, "ToggleMod").emit_signal("pressed")
            find_control(manager, "UseAppearance").emit_signal("pressed")
            manager.emit_signal("close_requested")
            check(not manager.visible, "关闭管理器不应退出桌宠")
            main.queue_free()
            await process_frame
            main = load("res://Main.tscn").instantiate()
            root.add_child(main)
            await process_frame
            check(main.get_child(0).get("DisplayName") == "瞌睡蘑菇", "重新创建宿主后应恢复选择")
            manager = main.get_node("ModManager")
            main.get_child(1).emit_signal("id_pressed", 5)
            select_mod(manager, "瞌睡蘑菇")
            find_control(manager, "RemoveMod").emit_signal("pressed")
            find_control(manager, "RemoveConfirmation").emit_signal("confirmed")
            check(find_control(manager, "ModList").item_count == 3, "确认卸载后应移出列表")
            check(main.get_child(0).get("DisplayName") == "蘑菇", "卸载当前外观应恢复默认")
    find_control(manager, "ModTabs").current_tab = 1
    await process_frame
    await capture(manager, "manager-steam")
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

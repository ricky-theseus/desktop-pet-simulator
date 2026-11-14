extends SceneTree

var failures := 0
var capture_dir := ""

func _initialize():
    for argument in OS.get_cmdline_user_args():
        if argument.begins_with("--capture-dir="):
            capture_dir = argument.trim_prefix("--capture-dir=")
    call_deferred("run_checks")

func check(condition: bool, message: String):
    if not condition:
        failures += 1
        push_error(message)

func button(main: Node, pressed: bool, position := Vector2(96, 145)):
    var event := InputEventMouseButton.new()
    event.button_index = MOUSE_BUTTON_LEFT
    event.pressed = pressed
    event.position = position
    main.call("_UnhandledInput", event)

func motion(main: Node, position: Vector2):
    var event := InputEventMouseMotion.new()
    event.position = position
    event.button_mask = MOUSE_BUTTON_MASK_LEFT
    main.call("_UnhandledInput", event)

func capture(name: String):
    if capture_dir.is_empty() or DisplayServer.get_name() == "headless":
        return
    await RenderingServer.frame_post_draw
    root.get_texture().get_image().save_png(capture_dir.path_join(name + ".png"))

func run_checks():
    var main = load("res://Main.tscn").instantiate()
    root.add_child(main)
    await process_frame
    var pet = main.get_child(0)
    check(pet.get("Action") == "idle", "初始动作应为待机")
    await capture("idle")

    button(main, true)
    motion(main, Vector2(98, 145))
    check(pet.get("Action") == "idle", "轻微抖动不应触发提起")
    button(main, false)
    check(pet.get("Action") == "pet", "点击松开后应触发摸摸")
    await capture("pet")

    main.get_child(1).emit_signal("id_pressed", 1)
    check(pet.get("Action") == "eat", "喂食应触发进食")
    await capture("eat")
    button(main, true)
    motion(main, Vector2(126, 160))
    check(pet.get("Action") == "held", "拖动应打断进食并触发提起")
    await create_timer(0.4).timeout
    check(pet.get("Action") == "held", "持续拖动应保持提起")
    await capture("held")
    button(main, false)
    check(pet.get("Action") == "land", "松开拖动应触发落地")
    await capture("land")
    await create_timer(0.6).timeout
    check(pet.get("Action") == "idle", "落地结束后应回到待机")

    button(main, true)
    motion(main, Vector2(126, 160))
    root.emit_signal("focus_exited")
    check(pet.get("Action") == "land", "失去焦点应结束拖动")
    pet.call("Play", "unsupported-action")
    check(pet.get("Action") == "idle", "未知动作应回退到待机")
    print("Interaction smoke: ", "PASS" if failures == 0 else "FAIL", " (", failures, " failures)")
    quit(0 if failures == 0 else 1)

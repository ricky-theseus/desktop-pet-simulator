using DesktopPet.Core;
using DesktopPet.Runtime;
using Godot;
using System.Text.Json;

public partial class AnimationLayerSmoke : Node
{
    private int failures;
    private string temporary = "";

    public override async void _Ready()
    {
        try
        {
            temporary = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pet-layer-" + Guid.NewGuid());
            System.IO.Directory.CreateDirectory(temporary);
            MakeFrame("pose.png", 64, new Rect2I(32, 16, 28, 48));
            MakeFrame("wide.png", 64, new Rect2I(0, 0, 64, 64));
            MakeFrame("wrong.png", 32, new Rect2I(0, 0, 32, 32));
            MakeFrame("empty.png", 64, default);
            MakeFrame("large.png", 128, new Rect2I(0, 0, 128, 128));
            var main = new Main();
            AddChild(main);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            main.SetProcess(false);
            var pet = main.GetChildren().OfType<PetView>().Single();
            pet.SetProcess(false);
            var sprite = pet.GetChildren().OfType<Sprite2D>().Single();
            var service = (IPetAnimationService)main;
            var original = sprite.Texture;
            var originalId = service.AppearanceId;
            var definition = WriteDefinition(originalId!);
            var regionChanges = 0;
            pet.HitRegionChanged += () => regionChanges++;

            Throws<ArgumentException>(() => service.CreateLayer("../invalid", definition), "拒绝非法 owner");
            using var layer = service.CreateLayer("example-animation", definition);
            Check(!layer.IsPresented, "选帧前不显示覆盖层");
            Throws<InvalidOperationException>(() => service.CreateLayer("second-owner", definition), "租约独占");
            layer.SetFrame("pose_0");
            Check(layer.IsPresented && sprite.Texture != original, "显示临时帧");
            Check(regionChanges > 0, "切帧同步通知穿透区域更新");
            var cached = sprite.Texture;
            Check(layer.TryGetAnchor("grip", out var anchor), "返回当前挂点");
            CheckAnchor(anchor, 142, 176, "普通挂点包含宠物节点偏移");
            Check(!layer.TryGetAnchor("missing", out _), "未知挂点返回 false");
            var bounds = pet.InteractionRect;
            layer.SetFrame("pose_0", true);
            Check(sprite.FlipH, "设置镜像");
            layer.TryGetAnchor("grip", out anchor);
            CheckAnchor(anchor, 50, 176, "镜像挂点与实际图片一致");
            Check(pet.InteractionRect.Position.X < bounds.Position.X, "镜像同时更新命中区");
            var changes = regionChanges;
            layer.SetFrame("wide");
            Check(pet.InteractionRect.Size.X > bounds.Size.X && regionChanges > changes, "扩展帧轮廓立即刷新");
            layer.SetFrame("pose_0");
            Check(ReferenceEquals(cached, sprite.Texture), "切帧复用纹理");
            Throws<ArgumentException>(() => layer.SetFrame("unknown"), "未知帧明确报错");
            Check(layer.IsPresented && ReferenceEquals(cached, sprite.Texture), "未知帧不破坏当前显示");

            main.SetPose(new PetVisualPose(.1f, -.1f, 1.2f, .8f, .25f, 0));
            layer.TryGetAnchor("grip", out anchor);
            CheckAnchor(anchor, 108.8f + MathF.Cos(.25f) * 55.2f + MathF.Sin(.25f) * 32,
                203.2f + MathF.Sin(.25f) * 55.2f - MathF.Cos(.25f) * 32, "挂点应用缩放旋转和位移");
            main.SetPose(PetVisualPose.Neutral);

            var menu = main.GetChildren().OfType<PopupMenu>().First();
            menu.EmitSignal(PopupMenu.SignalName.IdPressed, 1L);
            Check(pet.Action == "eat" && !layer.IsPresented && !layer.TryGetAnchor("grip", out _), "喂食抢占临时层");
            Check(sprite.Texture != cached, "喂食恢复基础动画帧");
            pet._Process(10);
            Check(layer.IsPresented && sprite.Texture == cached, "回到 idle 恢复临时层");
            pet.Play("pet");
            Check(!layer.IsPresented, "摸摸抢占临时层");
            pet._Process(10);
            main._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(110, 160) });
            Check(!layer.IsPresented, "按下鼠标立即隐藏临时层");
            main._UnhandledInput(new InputEventMouseMotion { Position = new Vector2(130, 160) });
            Check(pet.Action == "held" && !layer.IsPresented, "拖动抢占临时层");
            main._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = new Vector2(130, 160) });
            pet._Process(10);
            Check(layer.IsPresented, "落地后恢复临时层");
            menu.Show();
            Check(!layer.IsPresented, "菜单抢占临时层");
            menu.Hide();
            Check(layer.IsPresented, "菜单关闭后恢复临时层");

            main.ClearAppearance();
            Check(service.AppearanceId is null && !layer.IsPresented, "关闭外观安全隐藏临时层");
            main.ApplyAppearance(null);
            Check(layer.IsPresented && service.AppearanceId == originalId, "重开兼容外观恢复临时层");
            var other = WriteAppearance("other-example");
            main.ApplyAppearance(other);
            Check(!layer.IsPresented && service.AppearanceId == "other-example", "换外观不强制恢复原外观");
            main.ApplyAppearance(null);
            Check(layer.IsPresented, "切回兼容外观恢复临时层");
            bounds = pet.InteractionRect;
            main.ApplyAppearance(WriteAppearance(originalId!, "large.png", 128));
            Check(layer.IsPresented && pet.InteractionRect == bounds, "兼容外观尺寸变化不改变临时层命中区");
            layer.TryGetAnchor("grip", out anchor);
            CheckAnchor(anchor, 142, 176, "兼容外观尺寸变化不改变挂点");
            main.ApplyAppearance(null);

            pet._Process(.2);
            layer.Dispose();
            Check(!layer.IsPresented && !layer.TryGetAnchor("grip", out _) && !sprite.FlipH, "释放清除覆盖和挂点");
            Check(sprite.Texture != cached && service.AppearanceId == originalId, "释放后显示原时间线和原外观");
            Throws<ObjectDisposedException>(() => layer.SetFrame("pose_0"), "释放后的句柄不可写");
            Throws<ArgumentException>(() => service.CreateLayer("bad-frame", WriteDefinition(originalId!, "wrong.png")), "加载失败不占用租约");
            Throws<ArgumentException>(() => service.CreateLayer("empty-frame", WriteDefinition(originalId!, "empty.png")), "拒绝全透明帧");
            Throws<ArgumentException>(() => service.CreateLayer("bad-second-frame", WriteDefinition(originalId!, "pose.png", "wrong.png")), "全部帧验证后才安装");
            using (var incompatible = service.CreateLayer("incompatible-owner", WriteDefinition("other-appearance")))
            {
                incompatible.SetFrame("pose_0");
                Check(!incompatible.IsPresented && !incompatible.TryGetAnchor("grip", out _), "不兼容的定义可持有但不覆盖当前外观");
            }
            using var next = service.CreateLayer("next-owner", definition);
            next.SetFrame("pose_0");
            layer.Dispose();
            Check(next.IsPresented, "旧租约重复释放不影响新租约");
            next.Dispose();
            TestPriorityLayers(main, pet, sprite, service, originalId!);
            var timelineAppearance = System.IO.Path.Combine(temporary, "timeline.json");
            System.IO.File.WriteAllText(timelineAppearance, JsonSerializer.Serialize(new {
                formatVersion = 1, id = "timeline", displayName = "时间线检查", frameSize = 64,
                frames = new[] { "pose.png", "wide.png" },
                actions = new { idle = new { frames = new[] { 0, 1 }, fps = 2, loop = true } }
            }));
            main.ApplyAppearance(timelineAppearance);
            using (var advancing = service.CreateLayer("timeline-owner", WriteDefinition("timeline")))
            {
                advancing.SetFrame("pose_0");
                pet._Process(.6);
                advancing.Dispose();
                using var currentPixels = sprite.Texture.GetImage();
                Check(currentPixels.GetPixel(0, 0).A == 1, "临时层释放恢复持续推进后的基础帧");
            }
            pet._Process(.5);
            using (var currentPixels = sprite.Texture.GetImage())
                Check(currentPixels.GetPixel(0, 0).A == 0, "基础时间线继续循环");
            var exitLow = service.CreateLayer("exit-background", WriteDefinition("timeline"), -100);
            var exitHigh = service.CreateLayer("exit-foreground", WriteDefinition("timeline"));
            exitLow.SetFrame("pose_0");
            exitHigh.SetFrame("pose_0");
            main.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!exitLow.IsPresented && !exitHigh.IsPresented, "退出宿主释放全部优先级租约");
            exitLow.Dispose();
            exitHigh.Dispose();
            Throws<ObjectDisposedException>(() => exitLow.SetFrame("wide"), "退出后低层句柄不可写");
            Throws<ObjectDisposedException>(() => exitHigh.SetFrame("wide"), "退出后高层句柄不可写");
        }
        catch (Exception error)
        {
            failures++;
            GD.PushError(error.ToString());
        }
        finally
        {
            if (temporary.Length > 0) System.IO.Directory.Delete(temporary, true);
        }
        GD.Print($"Animation layer smoke: {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        GetTree().Quit(failures == 0 ? 0 : 1);
    }

    private void TestPriorityLayers(Main main, PetView pet, Sprite2D sprite, IPetAnimationService service, string appearance)
    {
        var definition = WriteDefinition(appearance);
        var backgroundDefinition = WriteDefinition(appearance, "large.png", "large.png", 128, 32, 96);
        Check(!service.HasActiveLayer, "没有租约时不报告活动层");
        Throws<ArgumentOutOfRangeException>(() => service.CreateLayer("invalid-low", definition, -1001), "拒绝过低优先级");
        Throws<ArgumentOutOfRangeException>(() => service.CreateLayer("invalid-high", definition, 1001), "拒绝过高优先级");
        using var background = service.CreateLayer("background", backgroundDefinition, -100);
        Check(!service.HasActiveLayer, "未选帧租约不报告活动层");
        background.SetFrame("pose_0");
        Check(background.IsPresented && service.HasActiveLayer, "低优先级层可以独立显示");
        Throws<InvalidOperationException>(() => service.CreateLayer("other-background", definition, -100), "同优先级仍独占");

        foreach (var mirror in new[] { false, true })
        {
            using var foreground = service.CreateLayer("foreground", definition);
            Check(background.IsPresented && !foreground.IsPresented, "高层未选帧不遮挡低层");
            foreground.SetFrame("pose_0", mirror);
            Check(foreground.IsPresented && !background.IsPresented, "优先级高者独占呈现");
            Check(!background.TryGetAnchor("grip", out _), "隐藏低层不返回高层挂点");
            Check(foreground.TryGetAnchor("grip", out var anchor), "高层返回自己的挂点");
            CheckAnchor(anchor, mirror ? 50 : 142, 176, "高层使用自己的源尺寸和镜像");
            var visibleTexture = sprite.Texture;
            var visibleBounds = pet.InteractionRect;
            background.SetFrame("wide", !mirror);
            Check(sprite.Texture == visibleTexture && pet.InteractionRect == visibleBounds, "隐藏低层可以推进帧而不影响高层纹理和轮廓");
            foreground.Dispose();
            Check(background.IsPresented && sprite.Texture != visibleTexture && sprite.FlipH == !mirror, "高层释放立即恢复低层最新帧和镜像");
            Check(!background.TryGetAnchor("grip", out _), "恢复隐藏期间选中的 wide 帧，不沿用先前 pose 帧的挂点");
            background.SetFrame("pose_0", !mirror);
            Check(background.TryGetAnchor("grip", out anchor), "恢复后低层挂点可用");
            CheckAnchor(anchor, !mirror ? 128 : 64, 184, "恢复后挂点使用低层源尺寸");
            Check(Math.Abs(pet.InteractionRect.Size.X - 128) < .01f, "恢复后按低层尺寸缩放轮廓");
        }

        using (var incompatible = service.CreateLayer("other-appearance", WriteDefinition("other-priority")))
        {
            incompatible.SetFrame("pose_0");
            Check(background.IsPresented && !incompatible.IsPresented, "不兼容高层不遮挡兼容低层");
            main.ApplyAppearance(WriteAppearance("other-priority"));
            Check(incompatible.IsPresented && !background.IsPresented, "切换外观按各层兼容性重新选择");
            main.ClearAppearance();
            Check(!service.HasActiveLayer && !incompatible.IsPresented, "关闭外观不报告活动层");
            main.ApplyAppearance(null);
            Check(background.IsPresented, "重新启用兼容外观恢复低层");
        }
        using (var foreground = service.CreateLayer("interactive-foreground", definition))
        {
            foreground.SetFrame("pose_0");
            var menu = main.GetChildren().OfType<PopupMenu>().First();
            menu.EmitSignal(PopupMenu.SignalName.IdPressed, 1L);
            Check(service.HasActiveLayer && !foreground.IsPresented && !background.IsPresented, "喂食暂停呈现但仍报告动作请求");
            pet._Process(10);
            Check(foreground.IsPresented && service.HasActiveLayer, "喂食结束恢复高层");
            menu.Show();
            Check(service.HasActiveLayer && !foreground.IsPresented, "菜单暂停不清除动作请求");
            menu.Hide();
            pet.Visible = false;
            Check(!service.HasActiveLayer && !foreground.IsPresented, "宠物隐藏时不报告活动层");
            pet.Visible = true;
            Check(foreground.IsPresented, "宠物重新显示恢复最高层");
            background.Dispose();
            background.Dispose();
            Check(foreground.IsPresented, "低层重复释放不影响高层");
        }
        Check(!service.HasActiveLayer, "所有层释放后活动请求结束");
        using var minimum = service.CreateLayer("minimum", definition, -1000);
        minimum.SetFrame("pose_0");
        Throws<ArgumentException>(() => service.CreateLayer("bad-priority-frame", WriteDefinition(appearance, "pose.png", "wrong.png"), 1000), "全部资源加载失败不会占用优先级");
        Check(minimum.IsPresented, "另一层加载失败不影响现有层");
        using var maximum = service.CreateLayer("maximum", definition, 1000);
        maximum.SetFrame("wide");
        Check(maximum.IsPresented && !minimum.IsPresented, "优先级边界值可用且高值优先");
    }

    private void Check(bool condition, string message)
    {
        if (condition) return;
        failures++;
        GD.PushError(message);
    }

    private void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); Check(false, message); }
        catch (T) { }
    }

    private void CheckAnchor(PetAnchorPosition actual, float x, float y, string message)
    {
        var window = DisplayServer.WindowGetPosition();
        Check(Math.Abs(actual.X - window.X - x) < .01f && Math.Abs(actual.Y - window.Y - y) < .01f,
            $"{message}: ({actual.X - window.X}, {actual.Y - window.Y}), expected ({x}, {y})");
    }

    private void MakeFrame(string name, int size, Rect2I opaque)
    {
        using var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);
        if (opaque.HasArea()) image.FillRect(opaque, Colors.Coral);
        if (image.SavePng(System.IO.Path.Combine(temporary, name)) != Error.Ok)
            throw new InvalidOperationException("测试图片无法写入。");
    }

    private string WriteDefinition(string appearance, string firstImage = "pose.png", string secondImage = "wide.png",
        int frameSize = 64, int anchorX = 55, int anchorY = 44)
    {
        var path = System.IO.Path.Combine(temporary, Guid.NewGuid() + ".json");
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(new {
            formatVersion = 1, compatibleAppearanceId = appearance, frameSize,
            frames = new {
                pose_0 = new { image = firstImage, anchors = new { grip = new { x = anchorX, y = anchorY } } },
                wide = new { image = secondImage }
            }
        }));
        return path;
    }

    private string WriteAppearance(string id, string image = "wide.png", int size = 64)
    {
        var path = System.IO.Path.Combine(temporary, id + ".json");
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(new {
            formatVersion = 1, id, displayName = "测试外观", frameSize = size, frames = new[] { image },
            actions = new { idle = new { frames = new[] { 0 }, fps = 1, loop = true } }
        }));
        return path;
    }
}

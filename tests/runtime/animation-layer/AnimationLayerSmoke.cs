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
            main.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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

    private string WriteDefinition(string appearance, string firstImage = "pose.png", string secondImage = "wide.png")
    {
        var path = System.IO.Path.Combine(temporary, Guid.NewGuid() + ".json");
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(new {
            formatVersion = 1, compatibleAppearanceId = appearance, frameSize = 64,
            frames = new {
                pose_0 = new { image = firstImage, anchors = new { grip = new { x = 55, y = 44 } } },
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

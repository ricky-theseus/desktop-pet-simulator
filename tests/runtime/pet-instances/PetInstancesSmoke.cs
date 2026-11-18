using DesktopPet.Core;
using DesktopPet.Runtime;
using Godot;
using System.Runtime.InteropServices;

public partial class PetInstancesSmoke : Node
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowStyle(nint handle, int index);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint handle);

    private void CheckTaskbar(Window window)
    {
        if (!OperatingSystem.IsWindows()) return;
        var handle = (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, window.GetWindowId());
        var style = GetWindowStyle(handle, -20);
        Check((style & 0x40000) == 0 && (style & 0x80) != 0, "独立窗口不产生任务栏图标");
        Check(IsWindowVisible(handle), "独立窗口在 Windows 中实际可见");
    }
    private int checks;
    private void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        checks++;
    }
    private void Throws<T>(Action action, string label) where T : Exception
    {
        try { action(); } catch (T) { checks++; return; }
        throw new Exception(label);
    }
    public override async void _Ready()
    {
        try
        {
            const string appearance = "res://assets/mushroom/appearance.json";
            var main = new Main();
            AddChild(main);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            main.SetProcess(false);
            GetWindow().Position = new Vector2I(40, 40);
            GetWindow().AlwaysOnTop = false;
            var primaryPet = main.GetChildren().OfType<PetView>().Single();
            primaryPet.SetProcess(false);
            var instances = (IPetInstanceService)main;
            Throws<ArgumentException>(() => instances.Create("../escape", "pet", appearance), "拒绝非法 owner");
            Throws<ArgumentException>(() => instances.Create("example", "CON", appearance), "拒绝保留文件名");
            Throws<FileNotFoundException>(() => instances.Create("example", "missing", "res://missing.json"), "缺少定义不创建实例");
            Check(instances.List().Count == 0, "失败不会留下实例");
            var malformed = File.ReadAllText(ProjectSettings.GlobalizePath(appearance))
                .Replace("\"frameSize\": 64", "\"frameSize\": 128")
                .Replace("\"actions-v1/", "\"assets/mushroom/actions-v1/");
            File.WriteAllText(ProjectSettings.GlobalizePath("res://bad-appearance.json"), malformed);
            Throws<InvalidOperationException>(() => instances.Create("example", "bad-frames", "res://bad-appearance.json"), "帧尺寸错误清理部分初始化的窗口");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(instances.List().Count == 0 && !main.GetChildren().OfType<Window>().Any(window => window.Name.ToString().StartsWith("Pet_")), "初始化失败后没有残留窗口");
            var first = instances.Create("example-one", "pet", appearance);
            var second = instances.Create("example-two", "pet", appearance);
            var windows = main.GetChildren().OfType<Window>().Where(window => window.Name.ToString().StartsWith("Pet_")).ToArray();
            var one = windows[0].GetChildren().OfType<Main>().Single();
            var two = windows[1].GetChildren().OfType<Main>().Single();
            var firstPet = one.GetChildren().OfType<PetView>().Single();
            var secondPet = two.GetChildren().OfType<PetView>().Single();
            firstPet.SetProcess(false);
            secondPet.SetProcess(false);
            Check(first.IsAlive && second.IsAlive && instances.List().Count == 2, "两个实例同时存在");
            Check(windows[0].GetWindowId() != windows[1].GetWindowId() && windows.All(window => window.GetWindowId() != GetWindow().GetWindowId()), "各自具有原生窗口");
            Check(first.Services != second.Services, "每只宠物使用独立服务域");
            CheckTaskbar(windows[0]);
            CheckTaskbar(windows[1]);
            var display = first.Services.GetRequired<IPetDisplayService>();
            foreach (var options in new[] {
                new PetDisplayOptions(), new PetDisplayOptions(),
                new PetDisplayOptions(Scale: 1.5, Opacity: .7, SmoothScaling: true),
                new PetDisplayOptions(AlwaysOnTop: false), new PetDisplayOptions(AlwaysOnTop: false),
                new PetDisplayOptions()
            })
            {
                display.Apply(options);
                CheckTaskbar(windows[0]);
            }
            Check(!windows.SelectMany(window => window.GetChildren().OfType<Main>()).Any(controller => controller.GetChildren().OfType<StatusIndicator>().Any()), "附加实例不重复创建托盘");
            Throws<InvalidOperationException>(() => instances.Create("example-one", "pet", appearance), "同 owner/key 不能重复创建");
            var menu1 = (PetMenuService)first.Services.GetRequired<IPetMenuService>();
            var menu2 = (PetMenuService)second.Services.GetRequired<IPetMenuService>();
            var clicked = 0;
            using var command = menu1.Register("example-one", "hello", "测试动作", () => clicked++);
            Check(menu1.Items.Count == 1 && menu2.Items.Count == 0, "菜单命令不串到其他宠物");
            menu1.Invoke(menu1.Items[0].Id);
            Check(clicked == 1, "独立菜单可以执行");
            using (first.Services.GetRequired<IPetActivityService>().TryAcquire("example-one"))
                Check(!second.Services.GetRequired<IPetActivityService>().IsBusy, "活动租约互不抢占");

            var originalPosition = GetWindow().Position;
            var otherPosition = windows[1].Position;
            for (var repeat = 0; repeat < 2; repeat++)
            {
                var before = windows[0].Position;
                var hit = firstPet.InteractionRect.GetCenter() + firstPet.Position;
                one._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = hit });
                one._UnhandledInput(new InputEventMouseMotion { Position = hit + new Vector2(20, 12) });
                Check(firstPet.Action == "held", "拖动独立宠物进入提起动作");
                one._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = hit });
                Check(windows[0].Position == before + new Vector2I(20, 12) && firstPet.Action == "land", "连续拖动后正确落地");
                Check(GetWindow().Position == originalPosition && windows[1].Position == otherPosition, "拖动不会移动主宠物或其他实例");
                firstPet._Process(10);
            }
            one.GetChildren().OfType<PopupMenu>().Single().EmitSignal(PopupMenu.SignalName.IdPressed, 1L);
            Check(firstPet.Action == "eat" && primaryPet.Action == "idle" && secondPet.Action == "idle", "喂食只作用于当前宠物");
            firstPet._Process(10);
            var firstState = first.Services.GetRequired<IPetScreenService>().GetState();
            Check(Math.Abs(firstState.CenterX - windows[0].Position.X - 96) < .01, "位置服务返回对应窗口的屏幕坐标");

            var layerPath = ProjectSettings.GlobalizePath("res://layer.json");
            File.WriteAllText(layerPath, "{\"formatVersion\":1,\"compatibleAppearanceId\":\"official-mushroom\",\"frameSize\":64,\"frames\":{\"pose\":{\"image\":\"assets/mushroom/actions-v1/01.png\",\"anchors\":{\"center\":{\"x\":32,\"y\":32}}}}}");
            using var layer1 = first.Services.GetRequired<IPetAnimationService>().CreateLayer("example-one", layerPath);
            using var layer2 = second.Services.GetRequired<IPetAnimationService>().CreateLayer("example-two", layerPath);
            layer1.SetFrame("pose");
            layer2.SetFrame("pose");
            Check(layer1.IsPresented && layer2.IsPresented, "两只宠物可同时持有同优先级动画层");
            layer1.TryGetAnchor("center", out var anchor1);
            layer2.TryGetAnchor("center", out var anchor2);
            Check(Math.Abs(anchor1.X - anchor2.X - windows[0].Position.X + windows[1].Position.X) < .01, "挂点跟随各自窗口");
            main.ClearAppearance();
            Check(layer1.IsPresented && layer2.IsPresented && first.Services.GetRequired<IPetVisualService>().IsVisible, "关闭主宠物不关闭独立宠物");
            main.ApplyAppearance(null);
            first.Hide();
            Check(!windows[0].Visible && windows[1].Visible && !layer1.IsPresented && layer2.IsPresented, "收起只影响指定实例");
            first.Show();
            first.Show();
            CheckTaskbar(windows[0]);
            Check(windows[0].Visible && instances.List().Count == 2 && layer1.IsPresented, "重复恢复不会创建副本");
            first.Services.GetRequired<IPetAppearanceService>().ClearAppearance();
            first.Hide();
            first.Show();
            Check(DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.MousePassthrough, windows[0].GetWindowId()), "恢复无外观实例仍保持全窗口穿透");
            first.Services.GetRequired<IPetAppearanceService>().ApplyAppearance(null);
            Check(!DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.MousePassthrough, windows[0].GetWindowId()), "恢复外观后重新接受局部鼠标交互");
            windows[0].EmitSignal(Window.SignalName.CloseRequested);
            Check(first.IsAlive && !windows[0].Visible && main.IsInsideTree(), "系统关闭只收起窗口，不退出进程");
            first.Show();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var captures = OS.GetCmdlineUserArgs().First(value => value.StartsWith("--captures="))[11..];
            Directory.CreateDirectory(captures);
            using (var capture = windows[0].GetTexture().GetImage()) capture.SavePng(Path.Combine(captures, "instance-one.png"));
            using (var capture = windows[1].GetTexture().GetImage()) capture.SavePng(Path.Combine(captures, "instance-two.png"));
            var savedPosition = windows[0].Position;
            first.Dispose();
            first.Dispose();
            Check(!first.IsAlive && instances.List().Count == 1 && second.IsAlive, "释放一个实例不影响另一个");
            Throws<ObjectDisposedException>(() => first.Show(), "释放后的实例不能恢复");
            Throws<ObjectDisposedException>(() => _ = first.Services, "释放后不能取得服务");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!layer1.IsPresented && layer2.IsPresented, "窗口释放清理其动画层");
            using var replacement = instances.Create("example-one", "pet", appearance);
            var restored = main.GetChildren().OfType<Window>().Last(window => window.Name.ToString().StartsWith("Pet_"));
            Check(restored.Position == savedPosition, "重新启用恢复独立位置");
            second.Dispose();
            replacement.Dispose();
            Check(instances.List().Count == 0, "所有附加实例可清理");
            var exitInstance = instances.Create("example-exit", "pet", appearance);
            main.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!exitInstance.IsAlive, "宿主退出自动销毁实例");
            exitInstance.Dispose();
            GD.Print($"Pet instances GPU smoke: PASS ({checks} checks)");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}

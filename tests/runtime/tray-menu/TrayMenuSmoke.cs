using DesktopPet.Core;
using DesktopPet.Runtime;
using Godot;
using System.Reflection;

public partial class TrayMenuSmoke : Node
{
    private int failures;
    private int checks;
    public override async void _Ready()
    {
        try
        {
            var main = new Main();
            var service = (PetMenuService)typeof(Main).GetField("modMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
            var startupCalls = 0;
            using var startupCommand = service.Register("example-test", "startup", "启动时的活动", () => startupCalls++);
            var startupId = service.Items.Single().Id;
            AddChild(main);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            main.ClearAppearance();
            var petMenu = (PopupMenu)typeof(Main).GetField("menu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
            var tray = main.GetNodeOrNull<PopupMenu>("TrayMenu");
            Check(tray is not null, "实际显示服务器提供托盘菜单");
            if (tray is null) { GetTree().Quit(1); return; }
            Check(tray.GetItemIndex(startupId) >= 0, "创建托盘时补齐启动前已注册的命令");
            Check(petMenu.GetItemIndex(startupId) >= 0, "启动前已注册的命令同步到宠物右键菜单");
            tray.EmitSignal(PopupMenu.SignalName.IdPressed, (long)startupId);
            Check(startupCalls == 1, "关闭外观后仍可从托盘执行启动前注册的命令");
            var called = 0;
            using var command = service.Register("example-test", "open", "测试活动", () => called++);
            var id = service.Items.Single(item => item.Key == "open").Id;
            Check(tray.GetItemIndex(id) >= 0, "外观关闭时注册命令仍同步到托盘");
            Check(petMenu.GetItemIndex(id) >= 0, "运行中注册命令同步到宠物右键菜单");
            if (tray.GetItemIndex(id) >= 0 && petMenu.GetItemIndex(id) >= 0)
            {
                command.Text = "恢复活动";
                Check(tray.GetItemText(tray.GetItemIndex(id)) == "恢复活动", "托盘标题同步");
                Check(petMenu.GetItemText(petMenu.GetItemIndex(id)) == "恢复活动", "宠物右键标题同步");
                command.Enabled = false;
                Check(tray.IsItemDisabled(tray.GetItemIndex(id)), "托盘禁用状态同步");
                Check(petMenu.IsItemDisabled(petMenu.GetItemIndex(id)), "宠物右键禁用状态同步");
                tray.EmitSignal(PopupMenu.SignalName.IdPressed, (long)id);
                petMenu.EmitSignal(PopupMenu.SignalName.IdPressed, (long)id);
                Check(called == 0, "两处菜单的禁用命令均不执行");
                command.Enabled = true;
                Check(!tray.IsItemDisabled(tray.GetItemIndex(id)), "托盘恢复启用状态");
                Check(!petMenu.IsItemDisabled(petMenu.GetItemIndex(id)), "宠物右键恢复启用状态");
                tray.EmitSignal(PopupMenu.SignalName.IdPressed, (long)id);
                Check(called == 1, "托盘使用同一个命令回调");
                petMenu.EmitSignal(PopupMenu.SignalName.IdPressed, (long)id);
                Check(called == 2, "宠物右键使用同一个命令回调");
            }
            command.Dispose();
            Check(tray.GetItemIndex(id) < 0, "停用释放后移除托盘入口");
            Check(petMenu.GetItemIndex(id) < 0, "停用释放后移除宠物右键入口");
            Check(tray.GetItemIndex(startupId) >= 0 && petMenu.GetItemIndex(startupId) >= 0, "释放运行中命令不影响启动前注册的命令");
            using var second = service.Register("example-test", "open", "再次打开", () => called++);
            var secondId = service.Items.Single(item => item.Key == "open").Id;
            command.Dispose();
            Check(tray.GetItemIndex(secondId) >= 0, "旧句柄不删除重新注册的托盘命令");
            Check(petMenu.GetItemIndex(secondId) >= 0, "旧句柄不删除重新注册的宠物右键命令");
            second.Dispose();
            startupCommand.Dispose();
            Check(tray.GetItemIndex(startupId) < 0 && petMenu.GetItemIndex(startupId) < 0, "启动前注册的命令也从两处菜单释放");
            Check(tray.ItemCount == 3, "无活动命令时只保留管理器、分隔符与退出");
            Check(petMenu.ItemCount == 7, "无活动命令时宠物右键菜单恢复全部内置入口");
            main.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        catch (Exception e) { failures++; GD.PrintErr(e); }
        GD.Print($"Tray menu smoke: {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures, {checks} checks)");
        GetTree().Quit(failures == 0 ? 0 : 1);
    }
    private void Check(bool ok, string reason) { checks++; if (!ok) { failures++; GD.PrintErr("FAIL: " + reason); } }
}

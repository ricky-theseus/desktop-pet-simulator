using DesktopPet.Core;
using Godot;

namespace DesktopPet.Runtime;

public partial class Main : IPetScreenService
{
    private readonly PetMenuService modMenu = new();
    private readonly PetActivityService activities = new();
    private readonly List<int> modMenuIds = [];
    private readonly Dictionary<int, (PetMenuService Service, int CommandId)> menuBindings = [];
    private const int ModSeparatorId = 999;

    public PetScreenState GetState()
    {
        var position = GetWindow().Position;
        var scale = pet?.Scale.X ?? 1;
        var offset = pet?.Position ?? PetOffset;
        return new(pet?.Visible == true && GetWindow().Visible, position.X + offset.X + 96 * scale,
            position.Y + offset.Y + 88 * scale, position.Y + offset.Y + 152 * scale, 128 * scale);
    }

    private void ConfigureModServices(ServiceRegistry services)
    {
        services.Add<IPetMenuService>(modMenu);
        services.Add<IPetActivityService>(activities);
        services.Add<IPetScreenService>(this);
        services.Add<IPetAnimationService>(this);
        services.Add<IPetDisplayService>(this);
        modMenu.Changed += RefreshModMenu;
        if (instanceOptions is not null) instanceOptions.SharedMenu.Changed += RefreshModMenu;
        RefreshModMenu();
    }

    private void RefreshModMenu()
    {
        menuBindings.Clear();
        var items = modMenu.Items.Select(item => (Id: item.Id, Command: item)).ToList();
        foreach (var item in modMenu.Items) menuBindings[item.Id] = (modMenu, item.Id);
        if (instanceOptions is not null)
        {
            var nextId = items.Count == 0 ? 1000 : items.Max(item => item.Id) + 1;
            foreach (var item in instanceOptions.SharedMenu.Items.Where(item => item.Scope == PetMenuScope.Shared))
            {
                items.Add((nextId, item));
                menuBindings[nextId++] = (instanceOptions.SharedMenu, item.Id);
            }
        }
        RefreshModCommands(menu, items.Where(item => item.Command.Scope != PetMenuScope.TrayOnly).ToArray());
        RefreshModCommands(trayMenu, items);
        modMenuIds.Clear();
        modMenuIds.AddRange(items.Select(item => item.Id));
    }

    private bool InvokeModCommand(int id) => menuBindings.TryGetValue(id, out var binding) && binding.Service.Invoke(binding.CommandId);

    private void RefreshModCommands(PopupMenu? target, IReadOnlyList<(int Id, PetMenuService.Command Command)> items)
    {
        if (target is null || !GodotObject.IsInstanceValid(target) || !target.IsInsideTree()) return;
        foreach (var id in modMenuIds)
        {
            var index = target.GetItemIndex(id);
            if (index >= 0) target.RemoveItem(index);
        }
        var separator = target.GetItemIndex(ModSeparatorId);
        if (separator >= 0) target.RemoveItem(separator);
        var quit = target.GetItemIndex(QuitAction);
        if (quit >= 0) target.RemoveItem(quit);
        var exitSeparator = target.GetItemIndex(998);
        if (exitSeparator >= 0) target.RemoveItem(exitSeparator);
        if (items.Count > 0) target.AddSeparator("", ModSeparatorId);
        foreach (var (id, command) in items)
        {
            target.AddItem(command.Text, id);
            target.SetItemDisabled(target.GetItemIndex(id), !command.Enabled);
        }
        target.AddSeparator("", 998);
        target.AddItem("退出程序", QuitAction);
    }
}

using DesktopPet.Core;
using Godot;

namespace DesktopPet.Runtime;

public partial class Main : IPetScreenService
{
    private readonly PetMenuService modMenu = new();
    private readonly PetActivityService activities = new();
    private readonly List<int> modMenuIds = [];
    private const int ModSeparatorId = 999;

    public PetScreenState GetState()
    {
        var position = DisplayServer.WindowGetPosition();
        return new(pet?.Visible == true, position.X + 96, position.Y + PetOffset.Y + 88,
            position.Y + PetOffset.Y + 152, 128);
    }

    private void ConfigureModServices(ServiceRegistry services)
    {
        services.Add<IPetMenuService>(modMenu);
        services.Add<IPetActivityService>(activities);
        services.Add<IPetScreenService>(this);
        services.Add<IPetAnimationService>(this);
        modMenu.Changed += RefreshModMenu;
    }

    private void RefreshModMenu()
    {
        RefreshModCommands(menu);
        RefreshModCommands(trayMenu);
        modMenuIds.Clear();
        modMenuIds.AddRange(modMenu.Items.Select(command => command.Id));
    }

    private void RefreshModCommands(PopupMenu? target)
    {
        if (target is null) return;
        foreach (var id in modMenuIds)
        {
            var index = target.GetItemIndex(id);
            if (index >= 0) target.RemoveItem(index);
        }
        var separator = target.GetItemIndex(ModSeparatorId);
        if (separator >= 0) target.RemoveItem(separator);
        if (modMenu.Items.Count == 0) return;
        target.AddSeparator("", ModSeparatorId);
        foreach (var command in modMenu.Items)
        {
            target.AddItem(command.Text, command.Id);
            target.SetItemDisabled(target.GetItemIndex(command.Id), !command.Enabled);
        }
    }
}

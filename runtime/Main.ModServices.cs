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
        modMenu.Changed += RefreshModMenu;
    }

    private void RefreshModMenu()
    {
        if (menu is null) return;
        foreach (var id in modMenuIds)
        {
            var index = menu.GetItemIndex(id);
            if (index >= 0) menu.RemoveItem(index);
        }
        modMenuIds.Clear();
        var separator = menu.GetItemIndex(ModSeparatorId);
        if (separator >= 0) menu.RemoveItem(separator);
        if (modMenu.Items.Count == 0) return;
        menu.AddSeparator("", ModSeparatorId);
        foreach (var command in modMenu.Items)
        {
            menu.AddItem(command.Text, command.Id);
            menu.SetItemDisabled(menu.GetItemIndex(command.Id), !command.Enabled);
            modMenuIds.Add(command.Id);
        }
    }
}

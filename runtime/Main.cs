using DesktopPet.Core;
using DesktopPet.OfficialMushroom;
using Godot;

namespace DesktopPet.Runtime;

public partial class Main : Node2D
{
    private MushroomPet? pet;

    public override void _Ready()
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.AlwaysOnTop, true);

        var host = new ModHost(new ModHostOptions("1.0"));
        var mushroom = new MushroomMod();
        host.Register(mushroom);
        host.StartAll();

        pet = new MushroomPet();
        AddChild(pet);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton mouseButton ||
            mouseButton.ButtonIndex != MouseButton.Left ||
            !mouseButton.Pressed ||
            pet is null ||
            !pet.InteractionRect.HasPoint(mouseButton.Position))
        {
            return;
        }

        pet.State.Interact();
        DisplayServer.WindowStartDrag();
        GetViewport().SetInputAsHandled();
    }
}

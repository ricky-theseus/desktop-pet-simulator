using DesktopPet.Core;
using DesktopPet.OfficialMushroom;
using Godot;

namespace DesktopPet.Runtime;

public partial class Main : Node2D
{
    public override void _Ready()
    {
        var host = new ModHost(new ModHostOptions("1.0"));
        var mushroom = new MushroomMod();
        host.Register(mushroom);
        host.StartAll();

        var title = new Label
        {
            Text = $"{mushroom.PetDefinition.DisplayName} · Desktop Pet Simulator",
            Position = new Vector2(24, 24)
        };
        AddChild(title);
    }
}

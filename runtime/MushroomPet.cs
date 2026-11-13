using DesktopPet.OfficialMushroom;
using Godot;

namespace DesktopPet.Runtime;

public partial class MushroomPet : Node2D
{
    private static readonly Color Shadow = new("#302339");
    private Sprite2D? sprite;

    public MushroomPetState State { get; } = new();

    public Rect2 InteractionRect => new(18f, 16f, 156f, 160f);

    public override void _Ready()
    {
        var texture = GD.Load<Texture2D>("res://assets/mushroom/mushroom-idle.png");
        if (texture is null)
        {
            GD.PushError("The official mushroom sprite could not be loaded.");
            return;
        }

        sprite = new Sprite2D
        {
            Texture = texture,
            Position = new Vector2(96f, 88f),
            Scale = new Vector2(0.14f, 0.14f),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        AddChild(sprite);
    }

    public override void _Process(double delta)
    {
        State.Tick((float)delta);
        if (sprite is not null)
        {
            var bob = Mathf.Round(Mathf.Sin(State.IdleTime * 3.2f) * 2f - State.BounceEnergy * 3f);
            sprite.Position = new Vector2(96f, 88f + bob);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(42f, 165f, 108f, 7f), new Color(Shadow, 0.25f));
    }
}

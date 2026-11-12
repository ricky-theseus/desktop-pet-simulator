using DesktopPet.OfficialMushroom;
using Godot;

namespace DesktopPet.Runtime;

public partial class MushroomPet : Node2D
{
    private static readonly Color Outline = new("#302339");
    private static readonly Color Cap = new("#d94b68");
    private static readonly Color CapLight = new("#f47d77");
    private static readonly Color Stem = new("#f6d49b");
    private static readonly Color StemShadow = new("#d99b72");
    private static readonly Color Eye = new("#302339");

    public MushroomPetState State { get; } = new();

    public Rect2 InteractionRect => new(24f, 28f, 144f, 140f);

    public override void _Process(double delta)
    {
        State.Tick((float)delta);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var bob = Mathf.Round(Mathf.Sin(State.IdleTime * 3.2f) * 2f - State.BounceEnergy * 3f);

        DrawRect(new Rect2(42f, 165f, 108f, 7f), new Color(Outline, 0.25f));
        DrawMushroom(new Vector2(0f, bob));
    }

    private void DrawMushroom(Vector2 offset)
    {
        DrawPixelRect(new Rect2(60f, 92f, 72f, 58f), offset, Outline);
        DrawPixelRect(new Rect2(68f, 100f, 56f, 50f), offset, Stem);
        DrawPixelRect(new Rect2(68f, 128f, 12f, 22f), offset, StemShadow);
        DrawPixelRect(new Rect2(80f, 140f, 44f, 10f), offset, StemShadow);
        DrawPixelRect(new Rect2(56f, 146f, 80f, 10f), offset, Outline);
        DrawPixelRect(new Rect2(64f, 146f, 64f, 6f), offset, Stem);

        DrawPixelRect(new Rect2(40f, 48f, 112f, 8f), offset, Outline);
        DrawPixelRect(new Rect2(32f, 56f, 128f, 40f), offset, Outline);
        DrawPixelRect(new Rect2(40f, 96f, 112f, 8f), offset, Outline);
        DrawPixelRect(new Rect2(48f, 40f, 96f, 8f), offset, Outline);
        DrawPixelRect(new Rect2(64f, 32f, 64f, 8f), offset, Outline);

        DrawPixelRect(new Rect2(48f, 48f, 96f, 8f), offset, CapLight);
        DrawPixelRect(new Rect2(40f, 56f, 112f, 32f), offset, Cap);
        DrawPixelRect(new Rect2(48f, 88f, 96f, 8f), offset, Cap);

        DrawPixelRect(new Rect2(60f, 48f, 12f, 12f), offset, new Color("#fff0c2"));
        DrawPixelRect(new Rect2(112f, 56f, 16f, 12f), offset, new Color("#fff0c2"));
        DrawPixelRect(new Rect2(80f, 76f, 12f, 8f), offset, new Color("#fff0c2"));

        DrawPixelRect(new Rect2(64f, 92f, 16f, 8f), offset, Eye);
        DrawPixelRect(new Rect2(112f, 92f, 16f, 8f), offset, Eye);
        DrawPixelRect(new Rect2(84f, 108f, 24f, 6f), offset, Eye);
    }

    private void DrawPixelRect(Rect2 rect, Vector2 offset, Color color)
    {
        DrawRect(new Rect2(rect.Position + offset, rect.Size), color);
    }
}

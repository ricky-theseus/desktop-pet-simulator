using DesktopPet.Core;
using Godot;

namespace DesktopPet.Runtime;

/// <summary>通用帧动画显示；角色素材和动作映射由外观包提供。</summary>
public partial class PetView : Node2D
{
    private readonly Sprite2D sprite = new();
    private Texture2D[] textures = [];
    private PetAnimationTimeline? timeline;
    public string Action => timeline?.Action ?? "idle";
    public string DisplayName { get; private set; } = "宠物";
    public Vector2[] HitPolygon { get; private set; } = [];
    public Rect2 InteractionRect => new(30f, 20f, 132f, 148f);

    public override void _Ready()
    {
        sprite.Position = new Vector2(96, 152);
        sprite.Centered = false;
        sprite.TextureFilter = TextureFilterEnum.Nearest;
        AddChild(sprite);
    }

    public void LoadAppearance(string path)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read)
            ?? throw new InvalidOperationException($"Cannot open appearance: {path}");
        var definition = PetAppearanceDefinition.FromJson(file.GetAsText());
        var directory = path[..(path.LastIndexOf('/') + 1)];
        var loaded = definition.Frames.Select(frame =>
            GD.Load<Texture2D>(directory + frame) ?? throw new InvalidOperationException($"Missing frame: {frame}")).ToArray();
        if (loaded.Any(texture => texture.GetWidth() != definition.FrameSize || texture.GetHeight() != definition.FrameSize))
            throw new InvalidOperationException("All appearance frames must match frameSize.");

        var offset = new Vector2(-definition.FrameSize / 2f, -definition.FrameSize);
        var scale = 128f / definition.FrameSize;
        var outline = new List<Vector2>();
        foreach (var texture in loaded)
        {
            using var bitmap = new Bitmap();
            bitmap.CreateFromImageAlpha(texture.GetImage(), 0.5f);
            foreach (var polygon in bitmap.OpaqueToPolygons(new Rect2I(0, 0, definition.FrameSize, definition.FrameSize)))
                outline.AddRange(polygon.Select(point => sprite.Position + (point + offset) * scale));
        }
        if (outline.Count == 0) throw new InvalidOperationException("Appearance contains no visible pixels.");

        // 全部资源验证后再替换当前外观，加载失败时原角色继续可用。
        textures = loaded;
        HitPolygon = Geometry2D.ConvexHull(outline.ToArray());
        DisplayName = definition.DisplayName;
        timeline = new PetAnimationTimeline(definition);
        sprite.Offset = offset;
        sprite.Scale = Vector2.One * scale;
        sprite.Texture = textures[timeline.FrameIndex];
    }

    public void Play(string action)
    {
        timeline?.Play(action);
        if (timeline is not null) sprite.Texture = textures[timeline.FrameIndex];
    }

    public override void _Process(double delta)
    {
        if (timeline is null) return;
        timeline.Tick(delta);
        sprite.Texture = textures[timeline.FrameIndex];
    }
}

using DesktopPet.Core;
using Godot;

namespace DesktopPet.Runtime;

/// <summary>通用帧动画显示；角色素材和动作映射由外观包提供。</summary>
public partial class PetView : Node2D
{
    private readonly Sprite2D sprite = new();
    private Texture2D[] textures = [];
    private bool ownsTextures;
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
        path = path.Replace('\\', '/');
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read)
            ?? throw new InvalidOperationException($"Cannot open appearance: {path}");
        var definition = PetAppearanceDefinition.FromJson(file.GetAsText());
        var directory = path[..(path.LastIndexOf('/') + 1)];
        var external = !path.StartsWith("res://");
        var loaded = new List<Texture2D>();
        try
        {
            foreach (var frame in definition.Frames) loaded.Add(LoadTexture(directory + frame));
            if (loaded.Any(texture => texture.GetWidth() != definition.FrameSize || texture.GetHeight() != definition.FrameSize))
                throw new InvalidOperationException("All appearance frames must match frameSize.");

            var offset = new Vector2(-definition.FrameSize / 2f, -definition.FrameSize);
            var scale = 128f / definition.FrameSize;
            var outline = new List<Vector2>();
            foreach (var texture in loaded)
            {
                using var bitmap = new Bitmap();
                using var pixels = texture.GetImage();
                bitmap.CreateFromImageAlpha(pixels, 0.5f);
                foreach (var polygon in bitmap.OpaqueToPolygons(new Rect2I(0, 0, definition.FrameSize, definition.FrameSize)))
                    outline.AddRange(polygon.Select(point => sprite.Position + (point + offset) * scale));
            }
            if (outline.Count == 0) throw new InvalidOperationException("Appearance contains no visible pixels.");

            // 全部资源验证后再替换；仅释放自己创建的纹理，不释放 Godot 共享导入资源。
            var previous = textures;
            var releasePrevious = ownsTextures;
            textures = loaded.ToArray();
            ownsTextures = external;
            HitPolygon = Geometry2D.ConvexHull(outline.ToArray());
            DisplayName = definition.DisplayName;
            timeline = new PetAnimationTimeline(definition);
            sprite.Offset = offset;
            sprite.Scale = Vector2.One * scale;
            sprite.Texture = textures[timeline.FrameIndex];
            if (releasePrevious)
                foreach (var texture in previous) texture.Dispose();
        }
        catch
        {
            if (external)
                foreach (var texture in loaded) texture.Dispose();
            throw;
        }
    }

    public override void _ExitTree()
    {
        if (ownsTextures)
            foreach (var texture in textures) texture.Dispose();
        textures = [];
    }

    public void ClearAppearance()
    {
        sprite.Texture = null;
        timeline = null;
        HitPolygon = [];
        if (ownsTextures)
            foreach (var texture in textures) texture.Dispose();
        textures = [];
        ownsTextures = false;
    }

    private static Texture2D LoadTexture(string path)
    {
        if (path.StartsWith("res://"))
            return GD.Load<Texture2D>(path) ?? throw new InvalidOperationException("无法加载内置动作帧。");
        // 用户导入的 PNG 没有 Godot 导入缓存，直接解码为运行时纹理。
        using var image = new Image();
        if (image.Load(path) != Error.Ok || image.IsEmpty()) throw new InvalidOperationException("外观图片无法解码，请检查内容包。");
        return ImageTexture.CreateFromImage(image);
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

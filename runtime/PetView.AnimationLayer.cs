using DesktopPet.Core;
using Godot;

namespace DesktopPet.Runtime;

public partial class PetView
{
    private readonly Dictionary<int, AnimationLayer> animationLayers = [];
    private AnimationLayer? presentedLayer;
    private LayerFrame? presentedFrame;

    internal bool HasActiveAnimationLayer => animationLayers.Values.Any(layer => layer.IsActive);

    internal IPetAnimationLayer CreateAnimationLayer(string owner, string definitionPath, int priority, Func<bool> canPresent)
    {
        if (string.IsNullOrWhiteSpace(owner) || owner.Length > 128 ||
            !char.IsAsciiLetterOrDigit(owner[0]) || owner.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new ArgumentException("Owner must be a Mod identifier (letters, digits, '-', '_' or '.').", nameof(owner));
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionPath);
        if (priority is < -1000 or > 1000) throw new ArgumentOutOfRangeException(nameof(priority));
        if (animationLayers.ContainsKey(priority)) throw new InvalidOperationException("This pet animation priority is already leased.");
        var loaded = AnimationLayer.Load(this, definitionPath, priority, canPresent);
        animationLayers.Add(priority, loaded);
        return loaded;
    }

    internal void RefreshAnimationPresentation()
    {
        AnimationLayer? nextLayer = null;
        foreach (var layer in animationLayers.Values)
            if (layer.IsActive && layer.CanPresent() && (nextLayer is null || layer.Priority > nextLayer.Priority))
                nextLayer = layer;
        var next = nextLayer?.CurrentFrame;
        var mirror = nextLayer?.MirrorX ?? false;
        var changed = presentedLayer != nextLayer || presentedFrame != next || sprite.FlipH != mirror;
        presentedLayer = nextLayer;
        presentedFrame = next;
        var size = nextLayer?.Definition.FrameSize ?? baseFrameSize;
        sprite.Offset = new Vector2(-size / 2f, -size);
        sprite.FlipH = mirror;
        sprite.Texture = next?.Texture ?? (timeline is null ? null : textures[timeline.FrameIndex]);
        if (!changed) return;
        ApplyVisualTransform();
        HitRegionChanged?.Invoke();
    }

    // FlipH 翻转图片内容，不改变绘制矩形；挂点和轮廓须先在源图片宽度内镜像。
    private Vector2 FramePoint(Vector2 point, int frameSize, bool mirrorX) => sprite.Offset +
        new Vector2(mirrorX ? frameSize - point.X : point.X, point.Y);

    private sealed record LayerFrame(Texture2D Texture, Vector2[] Outline, Dictionary<string, PetAnimationAnchor> Anchors);

    private sealed class AnimationLayer(PetView view, PetAnimationLayerDefinition definition, int priority, Func<bool> canPresent,
        Dictionary<string, LayerFrame> frames, List<Texture2D> ownedTextures) : IPetAnimationLayer
    {
        public PetAnimationLayerDefinition Definition { get; } = definition;
        public int Priority { get; } = priority;
        public Func<bool> CanPresent { get; } = canPresent;
        public LayerFrame? CurrentFrame { get; private set; }
        public bool MirrorX { get; private set; }
        private bool disposed;
        public bool IsActive => !disposed && CurrentFrame is not null && view.Visible &&
            view.AppearanceId == Definition.CompatibleAppearanceId;

        public bool IsPresented
        {
            get
            {
                if (disposed) return false;
                view.RefreshAnimationPresentation();
                return view.presentedLayer == this;
            }
        }

        public void SetFrame(string frame, bool mirrorX = false)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (frame is null || !frames.TryGetValue(frame, out var selected))
                throw new ArgumentException($"Unknown animation layer frame: {frame}", nameof(frame));
            CurrentFrame = selected;
            MirrorX = mirrorX;
            view.RefreshAnimationPresentation();
        }

        public bool TryGetAnchor(string name, out PetAnchorPosition position)
        {
            position = default;
            if (!IsPresented || name is null || !CurrentFrame!.Anchors.TryGetValue(name, out var anchor)) return false;
            var local = view.FramePoint(new Vector2(anchor.X, anchor.Y), Definition.FrameSize, MirrorX);
            var point = view.sprite.GetViewportTransform() * view.sprite.GlobalTransform * local;
            point += (Vector2)DisplayServer.WindowGetPosition(view.GetWindow().GetWindowId());
            position = new PetAnchorPosition(point.X, point.Y);
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            view.animationLayers.Remove(Priority);
            view.RefreshAnimationPresentation();
            foreach (var texture in ownedTextures) texture.Dispose();
            ownedTextures.Clear();
            frames.Clear();
            CurrentFrame = null;
        }

        public static AnimationLayer Load(PetView view, string path, int priority, Func<bool> canPresent)
        {
            path = path.Replace('\\', '/');
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read)
                ?? throw new ArgumentException("Cannot open animation layer definition.", nameof(path));
            if (file.GetLength() > 1024 * 1024) throw new ArgumentException("Animation layer definition is too large.", nameof(path));
            var definition = PetAnimationLayerDefinition.FromJson(file.GetAsText());
            var directory = path[..(path.LastIndexOf('/') + 1)];
            var frames = new Dictionary<string, LayerFrame>(StringComparer.Ordinal);
            var cached = new Dictionary<string, (Texture2D Texture, Vector2[] Outline)>(StringComparer.Ordinal);
            var owned = new List<Texture2D>();
            try
            {
                foreach (var (name, frame) in definition.Frames)
                {
                    if (!cached.TryGetValue(frame.Image, out var image))
                    {
                        var imagePath = directory + frame.Image;
                        RejectLinkedFrame(directory, frame.Image);
                        var texture = LoadTexture(imagePath);
                        if (!imagePath.StartsWith("res://")) owned.Add(texture);
                        if (texture.GetWidth() != definition.FrameSize || texture.GetHeight() != definition.FrameSize)
                            throw new ArgumentException("All animation layer frames must match frameSize.");
                        using var bitmap = new Bitmap();
                        using var pixels = texture.GetImage();
                        bitmap.CreateFromImageAlpha(pixels, 0.01f);
                        var outline = bitmap.OpaqueToPolygons(new Rect2I(0, 0, definition.FrameSize, definition.FrameSize))
                            .SelectMany(points => points).ToArray();
                        if (outline.Length == 0) throw new ArgumentException("Animation layer frames must contain visible pixels.");
                        image = (texture, Geometry2D.ConvexHull(outline));
                        cached.Add(frame.Image, image);
                    }
                    frames.Add(name, new LayerFrame(image.Texture, image.Outline, frame.Anchors));
                }
                return new AnimationLayer(view, definition, priority, canPresent, frames, owned);
            }
            catch
            {
                foreach (var texture in owned) texture.Dispose();
                throw;
            }
        }

        private static void RejectLinkedFrame(string directory, string relativePath)
        {
            if (directory.StartsWith("res://")) return;
            var current = ProjectSettings.GlobalizePath(directory);
            foreach (var part in relativePath.Split('/'))
            {
                current = System.IO.Path.Combine(current, part);
                if ((System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("Animation layer frame paths cannot traverse symbolic links.");
            }
        }
    }
}

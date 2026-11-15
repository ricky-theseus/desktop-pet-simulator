using System.Text.Json;

namespace DesktopPet.Core;

/// <summary>临时动画层格式 v1；帧由 Mod 显式选择，挂点使用源图片左上角起算的像素坐标。</summary>
public sealed class PetAnimationLayerDefinition
{
    public int FormatVersion { get; init; }
    public string CompatibleAppearanceId { get; init; } = "";
    public int FrameSize { get; init; } = 64;
    public Dictionary<string, PetAnimationLayerFrame> Frames { get; init; } = [];

    /// <summary>校验版本、尺寸、路径、数量及挂点范围；无效配置统一抛出 ArgumentException。</summary>
    public static PetAnimationLayerDefinition FromJson(string json)
    {
        PetAnimationLayerDefinition definition;
        try
        {
            definition = JsonSerializer.Deserialize<PetAnimationLayerDefinition>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new ArgumentException("Animation layer must be a JSON object.", nameof(json));
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Invalid animation layer JSON.", nameof(json), exception);
        }

        if (definition.FormatVersion != 1 || string.IsNullOrWhiteSpace(definition.CompatibleAppearanceId) ||
            definition.FrameSize is < 8 or > 512 || definition.Frames is null || definition.Frames.Count is < 1 or > 128)
            throw new ArgumentException("Animation layer v1 requires an appearance id, frameSize 8..512 and 1..128 frames.", nameof(json));

        foreach (var (name, frame) in definition.Frames)
        {
            if (string.IsNullOrWhiteSpace(name) || frame is null || !IsRelativePng(frame.Image) || frame.Anchors is null || frame.Anchors.Count > 64)
                throw new ArgumentException("Frames require a name, a directory-relative PNG path and at most 64 anchors.", nameof(json));
            foreach (var (anchorName, anchor) in frame.Anchors)
                if (string.IsNullOrWhiteSpace(anchorName) || anchor is null || !float.IsFinite(anchor.X) || !float.IsFinite(anchor.Y) ||
                    anchor.X < 0 || anchor.Y < 0 || anchor.X > definition.FrameSize || anchor.Y > definition.FrameSize)
                    throw new ArgumentException("Anchors require names and finite coordinates within frameSize.", nameof(json));
        }
        return definition;
    }

    private static bool IsRelativePng(string path) => !string.IsNullOrWhiteSpace(path) &&
        !path.Any(char.IsControl) && path.IndexOfAny([':', '\\', '*', '?', '"', '<', '>', '|']) < 0 &&
        !path.StartsWith('/') && !path.Split('/').Any(part => part is ".." or "." or "" || part.EndsWith('.') || part.EndsWith(' ')) &&
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
}

public sealed class PetAnimationLayerFrame
{
    public string Image { get; init; } = "";
    public Dictionary<string, PetAnimationAnchor> Anchors { get; init; } = [];
}

public sealed record PetAnimationAnchor(float X, float Y);

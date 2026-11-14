using System.Text.Json;

namespace DesktopPet.Core;

/// <summary>外观格式 v1；只描述图像与动作，不包含宠物性格和玩法规则。</summary>
public sealed class PetAppearanceDefinition
{
    public int FormatVersion { get; init; }
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public int FrameSize { get; init; } = 64;
    public string[] Frames { get; init; } = [];
    public Dictionary<string, PetAnimationClip> Actions { get; init; } = [];

    /// <summary>校验格式版本、包内相对路径和帧引用；无效配置抛出 ArgumentException。</summary>
    public static PetAppearanceDefinition FromJson(string json)
    {
        PetAppearanceDefinition definition;
        try
        {
            definition = JsonSerializer.Deserialize<PetAppearanceDefinition>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new ArgumentException("Appearance must be a JSON object.", nameof(json));
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Invalid appearance JSON.", nameof(json), exception);
        }

        if (definition.FormatVersion != 1 || string.IsNullOrWhiteSpace(definition.Id) ||
            string.IsNullOrWhiteSpace(definition.DisplayName) || definition.FrameSize is < 8 or > 512 ||
            definition.Frames is null || definition.Frames.Length is < 1 or > 4096 ||
            definition.Actions is null || !definition.Actions.TryGetValue("idle", out var idle) || idle is null || !idle.Loop)
        {
            throw new ArgumentException("Appearance v1 requires an id, name, frames and a looping idle action.", nameof(json));
        }

        foreach (var path in definition.Frames)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Contains(':') || path.Contains('\\') ||
                path.StartsWith('/') || path.Split('/').Any(part => part is ".." or "." or "") ||
                !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Frame paths must be package-relative PNG paths.", nameof(json));
            }
        }

        foreach (var (name, clip) in definition.Actions)
        {
            if (string.IsNullOrWhiteSpace(name) || clip is null || !double.IsFinite(clip.Fps) || clip.Fps is <= 0 or > 60 ||
                clip.Frames is null || clip.Frames.Length == 0 ||
                clip.Frames.Any(index => index < 0 || index >= definition.Frames.Length))
            {
                throw new ArgumentException($"Invalid animation: {name}.", nameof(json));
            }
        }

        return definition;
    }
}

public sealed class PetAnimationClip
{
    public int[] Frames { get; init; } = [];
    public double Fps { get; init; } = 6;
    public bool Loop { get; init; }
}

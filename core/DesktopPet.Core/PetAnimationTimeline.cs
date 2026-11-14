namespace DesktopPet.Core;

/// <summary>v1 帧序列播放器；调用方负责单线程推进，未知动作及单次动作结束后回到 idle。</summary>
public sealed class PetAnimationTimeline(PetAppearanceDefinition appearance)
{
    private double elapsed;
    public string Action { get; private set; } = "idle";
    private PetAnimationClip Clip => appearance.Actions[Action];
    public int FrameIndex => Clip.Frames[Math.Min((int)(elapsed * Clip.Fps), Clip.Frames.Length - 1)];

    public void Play(string action)
    {
        Action = appearance.Actions.ContainsKey(action) ? action : "idle";
        elapsed = 0;
    }

    public void Tick(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        var duration = Clip.Frames.Length / Clip.Fps;
        elapsed += deltaSeconds;
        if (elapsed < duration) return;
        if (Clip.Loop)
        {
            elapsed %= duration;
        }
        else
        {
            var remainder = elapsed - duration;
            Play("idle");
            elapsed = remainder % (Clip.Frames.Length / Clip.Fps);
        }
    }
}

namespace DesktopPet.Core;

/// <summary>持久显示参数服务 v1；主线程调用。与临时动作变形分离，只影响取得该服务的宠物。</summary>
public interface IPetDisplayService
{
    string ApiVersion { get; }
    PetDisplayOptions GetOptions();
    void Apply(PetDisplayOptions options);
}

public sealed record PetDisplayOptions(double Scale = 1, double Opacity = 1, bool AlwaysOnTop = true, bool SmoothScaling = false)
{
    public void Validate()
    {
        if (!double.IsFinite(Scale) || Scale is < .5 or > 2) throw new ArgumentOutOfRangeException(nameof(Scale));
        if (!double.IsFinite(Opacity) || Opacity is < .5 or > 1) throw new ArgumentOutOfRangeException(nameof(Opacity));
    }
}

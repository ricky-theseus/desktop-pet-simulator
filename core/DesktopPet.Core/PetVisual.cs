namespace DesktopPet.Core;

/// <summary>以脚底为原点的视觉变形；偏移以显示高度为单位，角度以弧度为单位。</summary>
public readonly record struct PetVisualPose(float X, float Y, float ScaleX, float ScaleY, float Rotation, float Skew)
{
    public static PetVisualPose Neutral => new(0, 0, 1, 1, 0, 0);

    public bool IsValid => float.IsFinite(X) && float.IsFinite(Y) &&
        Math.Abs(X) <= .3f && Math.Abs(Y) <= .3f &&
        ScaleX is >= .5f and <= 1.5f && ScaleY is >= .5f and <= 1.5f &&
        float.IsFinite(Rotation) && Math.Abs(Rotation) <= .5f &&
        float.IsFinite(Skew) && Math.Abs(Skew) <= .5f;
}

/// <summary>仅在主线程调用。交互动作优先；Mod 停止时须恢复 Neutral。</summary>
public interface IPetVisualService
{
    string ApiVersion { get; }
    bool IsVisible { get; }
    bool CanPose { get; }
    void SetPose(PetVisualPose pose);
}

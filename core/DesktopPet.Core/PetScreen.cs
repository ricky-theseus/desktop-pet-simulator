namespace DesktopPet.Core;

/// <summary>宠物中立姿态的屏幕位置，单位与宿主窗口坐标一致；脚底为 GroundY。</summary>
public readonly record struct PetScreenState(bool Visible, float CenterX, float CenterY, float GroundY, float DisplaySize);

/// <summary>屏幕位置服务 v1；仅在主线程读取，每帧重新读取以跟随拖动和外观切换。</summary>
public interface IPetScreenService
{
    string ApiVersion { get; }
    PetScreenState GetState();
}

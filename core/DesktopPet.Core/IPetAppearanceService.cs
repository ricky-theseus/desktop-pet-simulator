namespace DesktopPet.Core;

/// <summary>外观显示服务 v1。主线程调用；null 恢复默认外观，失败时保留当前画面。</summary>
public interface IPetAppearanceService
{
    string ApiVersion { get; }
    void ApplyAppearance(string? path);
}

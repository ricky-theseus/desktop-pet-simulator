namespace DesktopPet.Core;

/// <summary>临时帧覆盖服务 v1；仅在主线程调用，不修改玩家选择的外观或 Mod 状态。</summary>
public interface IPetAnimationService
{
    string ApiVersion { get; }
    /// <summary>当前已加载外观定义的 id；没有外观时为 null。</summary>
    string? AppearanceId { get; }
    /// <summary>加载并校验全部帧，获取唯一租约。owner 须为有效 Mod 标识；已有租约时抛出 InvalidOperationException。</summary>
    IPetAnimationLayer CreateLayer(string owner, string definitionPath);
}

/// <summary>调用方负责选择帧，并在停止或卸载时释放；释放后基础动画继续，重复释放无副作用。</summary>
public interface IPetAnimationLayer : IDisposable
{
    /// <summary>仅在已选帧、外观兼容且宠物允许姿态覆盖时为 true。</summary>
    bool IsPresented { get; }
    /// <summary>设置帧和水平镜像；未知帧抛出 ArgumentException，释放后抛出 ObjectDisposedException。</summary>
    void SetFrame(string frame, bool mirrorX = false);
    /// <summary>取得当前帧挂点的物理屏幕坐标；未显示、已释放或挂点不存在时返回 false。</summary>
    bool TryGetAnchor(string name, out PetAnchorPosition position);
}

public readonly record struct PetAnchorPosition(float X, float Y);

namespace DesktopPet.Core;

/// <summary>临时帧覆盖服务 v1；仅在主线程调用，不修改玩家选择的外观或 Mod 状态。</summary>
public interface IPetAnimationService
{
    string ApiVersion { get; }
    /// <summary>当前已加载外观定义的 id；没有外观时为 null。</summary>
    string? AppearanceId { get; }
    /// <summary>是否存在已选帧、外观兼容且宠物可见的层；交互抢占不清除此状态。未覆写此成员的既有实现默认返回 false。</summary>
    bool HasActiveLayer => false;
    /// <summary>加载并校验全部帧，获取优先级 0 的租约。owner 须为有效 Mod 标识；相同优先级已有租约时抛出 InvalidOperationException。</summary>
    IPetAnimationLayer CreateLayer(string owner, string definitionPath);
    /// <summary>优先级范围为 -1000–1000，较大值优先呈现；同优先级独占。默认实现仅支持 0，其余合法值抛出 NotSupportedException。</summary>
    IPetAnimationLayer CreateLayer(string owner, string definitionPath, int priority)
    {
        if (priority is < -1000 or > 1000) throw new ArgumentOutOfRangeException(nameof(priority));
        return priority == 0 ? CreateLayer(owner, definitionPath)
            : throw new NotSupportedException("This host does not support animation layer priorities.");
    }
}

/// <summary>调用方负责选择帧，并在停止或卸载时释放；释放后恢复下一个可呈现层或基础动画，重复释放无副作用。</summary>
public interface IPetAnimationLayer : IDisposable
{
    /// <summary>仅对实际呈现的层为 true：已选帧、外观兼容、宠物允许姿态覆盖且没有更高优先级的兼容活动层。</summary>
    bool IsPresented { get; }
    /// <summary>设置帧和水平镜像；未知帧抛出 ArgumentException，释放后抛出 ObjectDisposedException。</summary>
    void SetFrame(string frame, bool mirrorX = false);
    /// <summary>取得当前帧挂点的物理屏幕坐标；未显示、已释放或挂点不存在时返回 false。</summary>
    bool TryGetAnchor(string name, out PetAnchorPosition position);
}

public readonly record struct PetAnchorPosition(float X, float Y);

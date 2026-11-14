namespace DesktopPet.Core;

/// <summary>协作式独占活动服务 v1；仅在主线程调用。持有者结束或卸载时须释放租约。</summary>
public interface IPetActivityService
{
    string ApiVersion { get; }
    bool IsBusy { get; }
    IDisposable? TryAcquire(string owner);
}

public sealed class PetActivityService : IPetActivityService
{
    public string ApiVersion => "1.0";
    private Lease? current;
    public bool IsBusy => current is not null;

    public IDisposable? TryAcquire(string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (current is not null) return null;
        return current = new Lease(this);
    }

    private sealed class Lease(PetActivityService service) : IDisposable
    {
        public void Dispose()
        {
            if (ReferenceEquals(service.current, this)) service.current = null;
        }
    }
}

namespace DesktopPet.Core;

public sealed class ServiceRegistry
{
    private readonly Dictionary<Type, object> services = [];

    public void Add<TService>(TService service) where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(service);
        services[typeof(TService)] = service;
    }

    public TService GetRequired<TService>() where TService : notnull
    {
        if (services.TryGetValue(typeof(TService), out var service))
        {
            return (TService)service;
        }

        throw new InvalidOperationException($"Required service is not registered: {typeof(TService).Name}.");
    }
}

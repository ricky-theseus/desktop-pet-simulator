namespace DesktopPet.Core;

public sealed record ModManifest(string Id, string Name, string Version, string ApiVersion);

public interface IMod
{
    ModManifest Manifest { get; }

    void Start(ModContext context);

    void Stop(ModContext context);
}

public interface IDesktopProvider
{
    string PlatformId { get; }
}

public sealed record PetDefinition(string AssetKey, string DisplayName);

namespace Meteor.Umbra.Framework;

public enum UmbraPluginManagerTab
{
    Installed,
    Browse,
    Updates,
    Settings,
    Logs
}

public sealed record UmbraPluginManagerState(
    bool IsOpen,
    UmbraPluginManagerTab ActiveTab,
    IReadOnlyList<UmbraPluginManifest> InstalledPlugins,
    IReadOnlyList<string> RepositoryUrls,
    bool SafeMode)
{
    public static UmbraPluginManagerState Default => new(
        false,
        UmbraPluginManagerTab.Installed,
        Array.Empty<UmbraPluginManifest>(),
        Array.Empty<string>(),
        false);
}

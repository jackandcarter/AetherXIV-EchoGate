namespace EchoGate.Core;

public sealed record LauncherProfile(
    string ClientRootPath,
    string PatchLibraryRootPath,
    string LauncherServiceUrl,
    string PatchBaseUrl,
    ServerProfile ServerProfile,
    WineRuntimeProfile RuntimeProfile,
    RuntimeSelectionMode RuntimeMode = RuntimeSelectionMode.AutomaticManaged,
    ClientLaunchHelperMode LaunchHelperMode = ClientLaunchHelperMode.Automatic,
    ClientGraphicsTarget GraphicsTarget = ClientGraphicsTarget.OpenGLCompatibility,
    string SavedUsername = "",
    bool RememberUsername = false,
    ClientWindowMode WindowMode = ClientWindowMode.WineVirtualDesktop,
    int WindowWidth = ClientWindowDefaults.DefaultWidth,
    int WindowHeight = ClientWindowDefaults.DefaultHeight)
{
    public static LauncherProfile LocalDefault() => new(
        "",
        "",
        "http://127.0.0.1:8080/launcher",
        "",
        ServerProfile.LocalDefault(),
        LauncherPlatform.Current.RequiresCompatibilityRuntime
            ? WineRuntimeProfile.WinePrefix(
                "Echo Gate Managed",
                RuntimeInstallStore.ManagedPrefixPath,
                "wine")
            : WineRuntimeProfile.NativeWindows(),
        LauncherPlatform.Current.RequiresCompatibilityRuntime
            ? RuntimeSelectionMode.AutomaticManaged
            : RuntimeSelectionMode.CustomRuntime,
        ClientLaunchHelperMode.Automatic,
        ClientGraphicsTarget.OpenGLCompatibility,
        "",
        false,
        ClientWindowMode.WineVirtualDesktop,
        ClientWindowDefaults.DefaultWidth,
        ClientWindowDefaults.DefaultHeight);
}

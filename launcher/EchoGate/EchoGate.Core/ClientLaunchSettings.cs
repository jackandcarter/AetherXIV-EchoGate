namespace EchoGate.Core;

public enum ClientLaunchHelperMode
{
    Automatic,
    X86,
    X64,
    Arm64
}

public enum ClientGraphicsTarget
{
    OpenGLCompatibility,
    WineDefault,
    OpenGLThreaded,
    WineD3DVulkan
}

public enum ClientWindowMode
{
    WineVirtualDesktop,
    NormalWindow
}

public static class ClientWindowDefaults
{
    public const string DesktopNamePrefix = "EchoGateXIV";
    public const int DefaultWidth = 1600;
    public const int DefaultHeight = 900;
    public const int MinimumWidth = 800;
    public const int MinimumHeight = 600;
    public const int MaximumWidth = 3840;
    public const int MaximumHeight = 2160;

    public static string BuildDesktopName(int width, int height)
    {
        int desktopWidth = Math.Clamp(width, MinimumWidth, MaximumWidth);
        int desktopHeight = Math.Clamp(height, MinimumHeight, MaximumHeight);
        return $"{DesktopNamePrefix}-{desktopWidth}x{desktopHeight}";
    }
}

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Aether.Umbra.Framework;

public static class UmbraInProcessEntryPoint
{
    [UnmanagedCallersOnly(EntryPoint = "UmbraBootstrap", CallConvs = [typeof(CallConvStdcall)])]
    public static int UmbraBootstrap()
    {
        return UmbraBootstrapRunner.RunFromEnvironmentAsync().GetAwaiter().GetResult();
    }

    public static int UmbraBootstrapCoreClr(IntPtr args, int sizeBytes)
    {
        return UmbraBootstrapRunner.RunFromEnvironmentAsync().GetAwaiter().GetResult();
    }
}

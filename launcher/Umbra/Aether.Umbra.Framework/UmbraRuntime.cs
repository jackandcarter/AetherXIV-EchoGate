using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

public sealed class UmbraRuntime : IDisposable
{
    private readonly CancellationTokenSource shutdown = new();
    private readonly UmbraSystemPluginHost systemPlugins;
    private readonly Task updateLoop;
    private bool disposed;

    private UmbraRuntime(
        UmbraRuntimeOptions options,
        UmbraRuntimeLog log,
        UmbraPluginManagerState pluginManager,
        UmbraDevBridgeService devBridge)
    {
        Options = options;
        Log = log;
        PluginManager = pluginManager;
        DevBridge = devBridge;
        systemPlugins = new UmbraSystemPluginHost(this);
        systemPlugins.Register(new UmbraDevBridgePlugin());
        systemPlugins.Register(new UmbraTraceCompanionPlugin());
        systemPlugins.Initialize();
        updateLoop = Task.Run(() => RunUpdateLoopAsync(shutdown.Token));
    }

    public UmbraRuntimeOptions Options { get; }

    public UmbraRuntimeLog Log { get; }

    public UmbraPluginManagerState PluginManager { get; }

    public UmbraDevBridgeService DevBridge { get; }

    public static async Task<UmbraRuntime> StartAsync(UmbraRuntimeOptions options, UmbraRuntimeLog log)
    {
        Directory.CreateDirectory(options.PluginDirectory);
        Directory.CreateDirectory(options.CacheDirectory);
        Directory.CreateDirectory(options.DevBridgeDirectory);

        log.Info("umbra_runtime_starting=true");
        log.Info($"umbra_cache_dir={options.CacheDirectory}");
        log.Info($"umbra_dev_bridge_dir={options.DevBridgeDirectory}");
        log.Info($"umbra_dev_bridge_control={options.DevBridgeControlPath}");
        log.Info($"umbra_dev_bridge_initial_enabled={options.DevBridgeInitiallyEnabled}");

        IReadOnlyList<UmbraPluginManifest> manifests = UmbraPluginDiscovery.Discover(options.PluginDirectory, log);
        IReadOnlyList<UmbraStoreEntry> storeEntries;
        using (CancellationTokenSource repositoryTimeout = new(TimeSpan.FromSeconds(5)))
        {
            storeEntries = await UmbraRepositoryFetcher.FetchAsync(
                options.RepositorySources,
                Path.Combine(options.CacheDirectory, "Repositories"),
                log,
                repositoryTimeout.Token);
        }

        UmbraPluginCatalogState catalog = UmbraPluginCatalogState.Build(manifests, storeEntries);
        UmbraPluginManagerState pluginManager = new(
            true,
            UmbraPluginManagerTab.Installed,
            catalog,
            options.RepositorySources,
            options.SafeMode,
            DebugLoggingEnabled: false,
            DevUiEnabled: false,
            PluginExecutionEnabled: false);

        log.Info($"umbra_plugin_manifest_count={pluginManager.InstalledPlugins.Count}");
        log.Info($"umbra_plugin_enabled_count={pluginManager.InstalledPlugins.Count(plugin => plugin.Enabled)}");
        log.Info($"umbra_supported_plugin_count={pluginManager.SupportedPlugins.Count}");
        log.Info($"umbra_available_plugin_count={pluginManager.AvailablePlugins.Count}");
        log.Info($"umbra_plugin_update_count={pluginManager.Updates.Count}");
        log.Info("umbra_plugin_load_mode=system_plugins_only");
        log.Info("umbra_plugin_execution_enabled=false");

        UmbraDevBridgeService devBridge = new(options, log, new UmbraReadOnlyMemory(log));
        UmbraDevBridgeControl.Ensure(options.DevBridgeControlPath, options.DevBridgeInitiallyEnabled, options.DevBridgePort);

        UmbraRuntime runtime = new(options, log, pluginManager, devBridge);
        log.Info("umbra_runtime_started=true");
        return runtime;
    }

    public TService? GetService<TService>() where TService : class
    {
        if (DevBridge is TService devBridge)
            return devBridge;

        if (Log is TService log)
            return log;

        if (PluginManager is TService pluginManager)
            return pluginManager;

        if (Options is TService options)
            return options;

        return null;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        shutdown.Cancel();
        try
        {
            updateLoop.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Process teardown is already underway.
        }

        systemPlugins.Dispose();
        DevBridge.Dispose();
        shutdown.Dispose();
        Log.Info("umbra_runtime_stopped=true");
    }

    private async Task RunUpdateLoopAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset last = DateTimeOffset.UtcNow;
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(500));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TimeSpan delta = now - last;
            last = now;
            systemPlugins.Update(delta);
        }
    }
}

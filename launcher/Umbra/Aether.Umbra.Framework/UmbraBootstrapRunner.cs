namespace Aether.Umbra.Framework;

public static class UmbraBootstrapRunner
{
    public static async Task<int> RunFromEnvironmentAsync()
    {
        UmbraRuntimeOptions options = UmbraRuntimeOptions.FromEnvironment();
        UmbraRuntimeLog log = UmbraRuntimeLog.Open(options.LogPath);
        try
        {
            log.Info("umbra_framework_started=true");
            log.Info($"umbra_framework_version={UmbraFrameworkInfo.Version}");
            log.Info($"umbra_api_version={UmbraFrameworkInfo.ApiVersion}");
            log.Info($"umbra_plugin_dir={options.PluginDirectory}");
            log.Info($"umbra_cache_dir={options.CacheDirectory}");
            log.Info($"umbra_safe_mode={options.SafeMode}");
            log.Info($"umbra_repository_urls={string.Join(";", options.RepositoryUrls)}");
            log.Info($"umbra_repository_source_count={options.RepositorySources.Count}");
            log.Info("umbra_host_mode=in_process");
            log.Info("umbra_dx9_hook_installed=false");
            log.Info("umbra_imgui_backend_ready=false");
            log.Info("umbra_title_screen_icons_rendered=false");
            log.Info("umbra_ready=false");

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
            UmbraPluginManagerState state = new(
                true,
                UmbraPluginManagerTab.Installed,
                catalog,
                options.RepositorySources,
                options.SafeMode,
                DebugLoggingEnabled: false,
                DevUiEnabled: false,
                PluginExecutionEnabled: false);

            log.Info($"umbra_plugin_manifest_count={state.InstalledPlugins.Count}");
            log.Info($"umbra_plugin_enabled_count={state.InstalledPlugins.Count(plugin => plugin.Enabled)}");
            log.Info($"umbra_supported_plugin_count={state.SupportedPlugins.Count}");
            log.Info($"umbra_available_plugin_count={state.AvailablePlugins.Count}");
            log.Info($"umbra_plugin_update_count={state.Updates.Count}");
            log.Info("umbra_plugin_load_mode=manifest_discovery_only");
            log.Info("umbra_plugin_execution_enabled=false");
            log.Info("umbra_ui_shell=settings,plugin_installer,toasts");
            log.Info("umbra_framework_completed=true");
            Console.WriteLine($"{UmbraFrameworkInfo.Name} bootstrap complete: {state.InstalledPlugins.Count} plugin manifest(s)");
            return 0;
        }
        catch (Exception ex)
        {
            log.Error("umbra_framework_failed=true");
            log.Error($"umbra_framework_error={ex}");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}

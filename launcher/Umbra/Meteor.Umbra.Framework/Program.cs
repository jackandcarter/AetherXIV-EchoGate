using Meteor.Umbra.Framework;

if (args.Length == 1 && string.Equals(args[0], "--probe", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine(UmbraFrameworkInfo.ProbeText);
    return 0;
}

if (args.Length == 1 && string.Equals(args[0], "--bootstrap", StringComparison.OrdinalIgnoreCase))
{
    UmbraRuntimeOptions options = UmbraRuntimeOptions.FromEnvironment();
    UmbraRuntimeLog log = UmbraRuntimeLog.Open(options.LogPath);
    try
    {
        log.Info("umbra_framework_started=true");
        log.Info($"umbra_framework_version={UmbraFrameworkInfo.Version}");
        log.Info($"umbra_api_version={UmbraFrameworkInfo.ApiVersion}");
        log.Info($"umbra_plugin_dir={options.PluginDirectory}");
        log.Info($"umbra_safe_mode={options.SafeMode}");
        log.Info($"umbra_repository_urls={string.Join(";", options.RepositoryUrls)}");

        IReadOnlyList<UmbraPluginManifest> manifests = UmbraPluginDiscovery.Discover(options.PluginDirectory, log);
        UmbraPluginManagerState state = new(
            true,
            UmbraPluginManagerTab.Installed,
            manifests,
            options.RepositoryUrls,
            options.SafeMode);

        log.Info($"umbra_plugin_manifest_count={state.InstalledPlugins.Count}");
        log.Info($"umbra_plugin_enabled_count={state.InstalledPlugins.Count(plugin => plugin.Enabled)}");
        log.Info("umbra_plugin_load_mode=manifest_discovery_only");
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

Console.WriteLine($"{UmbraFrameworkInfo.Name} {UmbraFrameworkInfo.Version}");
return 0;

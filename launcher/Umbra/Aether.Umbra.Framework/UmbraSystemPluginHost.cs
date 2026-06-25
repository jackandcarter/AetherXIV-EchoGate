using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

public sealed class UmbraSystemPluginHost(UmbraRuntime runtime) : IDisposable
{
    private readonly List<IUmbraPlugin> plugins = new();

    public void Register(IUmbraPlugin plugin)
    {
        plugins.Add(plugin);
    }

    public void Initialize()
    {
        foreach (IUmbraPlugin plugin in plugins)
        {
            try
            {
                runtime.Log.Info($"umbra_system_plugin_initialize id={plugin.GetType().FullName} name={plugin.Name}");
                plugin.Initialize(new UmbraPluginContext(runtime, plugin.GetType().FullName ?? plugin.Name));
                runtime.Log.Info($"umbra_system_plugin_initialized name={plugin.Name}");
            }
            catch (Exception ex)
            {
                runtime.Log.Error($"umbra_system_plugin_initialize_failed name={plugin.Name}", ex);
            }
        }
    }

    public void Update(TimeSpan delta)
    {
        foreach (IUmbraPlugin plugin in plugins)
        {
            try
            {
                plugin.Update(delta);
            }
            catch (Exception ex)
            {
                runtime.Log.Error($"umbra_system_plugin_update_failed name={plugin.Name}", ex);
            }
        }
    }

    public void Dispose()
    {
        for (int index = plugins.Count - 1; index >= 0; index--)
        {
            IUmbraPlugin plugin = plugins[index];
            try
            {
                plugin.Dispose();
                runtime.Log.Info($"umbra_system_plugin_disposed name={plugin.Name}");
            }
            catch (Exception ex)
            {
                runtime.Log.Error($"umbra_system_plugin_dispose_failed name={plugin.Name}", ex);
            }
        }
    }
}

public sealed class UmbraPluginContext(UmbraRuntime runtime, string pluginId) : IUmbraPluginContext
{
    public string PluginId { get; } = pluginId;

    public string ConfigDirectory { get; } = Path.Combine(runtime.Options.CacheDirectory, "PluginConfig", Sanitize(pluginId));

    public IUmbraLogger Logger { get; } = new UmbraFrameworkLogger(runtime.Log, Sanitize(pluginId));

    public TService? GetService<TService>() where TService : class
    {
        return runtime.GetService<TService>();
    }

    private static string Sanitize(string value)
    {
        char[] chars = value
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_')
            .ToArray();
        string sanitized = new(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "plugin" : sanitized;
    }
}

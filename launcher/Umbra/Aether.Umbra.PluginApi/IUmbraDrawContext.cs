namespace Aether.Umbra.PluginApi;

public interface IUmbraDrawContext
{
    bool IsPluginManagerOpen { get; }

    void RequestPluginManagerOpen();
}

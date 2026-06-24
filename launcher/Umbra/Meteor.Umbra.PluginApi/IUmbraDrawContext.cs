namespace Meteor.Umbra.PluginApi;

public interface IUmbraDrawContext
{
    bool IsPluginManagerOpen { get; }

    void RequestPluginManagerOpen();
}

namespace Meteor.Umbra.PluginApi;

public interface IUmbraPluginContext
{
    string PluginId { get; }

    string ConfigDirectory { get; }

    IUmbraLogger Logger { get; }
}

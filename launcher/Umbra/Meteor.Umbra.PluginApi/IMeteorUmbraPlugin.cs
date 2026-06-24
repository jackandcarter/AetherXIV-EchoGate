namespace Meteor.Umbra.PluginApi;

public interface IMeteorUmbraPlugin : IDisposable
{
    string Name { get; }

    void Initialize(IUmbraPluginContext context);

    void Update(TimeSpan delta);

    void Draw(IUmbraDrawContext drawContext);
}

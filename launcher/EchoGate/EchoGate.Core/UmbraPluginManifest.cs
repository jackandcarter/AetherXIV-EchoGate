using System.Text.Json;
using System.Text.Json.Serialization;

namespace EchoGate.Core;

public sealed record UmbraPluginManifest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("api_version")] string ApiVersion,
    [property: JsonPropertyName("entry")] string Entry,
    [property: JsonPropertyName("minimum_framework_version")] string MinimumFrameworkVersion,
    [property: JsonPropertyName("enabled")] bool Enabled)
{
    public static UmbraPluginManifest Load(string path)
    {
        string json = File.ReadAllText(path);
        UmbraPluginManifest? manifest = JsonSerializer.Deserialize<UmbraPluginManifest>(json);
        if (manifest is null)
            throw new InvalidDataException("Umbra plugin manifest could not be read.");

        manifest.Validate();
        return manifest;
    }

    public void Validate()
    {
        Require(Id, "id");
        Require(Name, "name");
        Require(Version, "version");
        Require(ApiVersion, "api_version");
        Require(Entry, "entry");
        Require(MinimumFrameworkVersion, "minimum_framework_version");

        if (Path.IsPathRooted(Entry)
            || Entry.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
        {
            throw new InvalidDataException("Umbra plugin entry must be a relative assembly path.");
        }
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Umbra plugin manifest is missing {name}.");
    }
}

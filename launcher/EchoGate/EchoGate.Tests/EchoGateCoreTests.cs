using System.Xml.Linq;
using EchoGate.Core;

namespace EchoGate.Tests;

public sealed class EchoGateCoreTests
{
    [Fact]
    public void ServerXmlWriterIncludesLocalPorts()
    {
        ServerProfile profile = ServerProfile.LocalDefault();

        string xml = ServerXmlWriter.ToXml(new[] { profile });
        XElement server = XDocument.Parse(xml).Root!.Element("server")!;

        Assert.Equal("Local Meteor", (string?)server.Attribute("name"));
        Assert.Equal("127.0.0.1", (string?)server.Attribute("host"));
        Assert.Equal("54994", (string?)server.Attribute("lobbyPort"));
        Assert.Equal("54992", (string?)server.Attribute("worldPort"));
        Assert.Equal("1989", (string?)server.Attribute("mapPort"));
    }

    [Fact]
    public void StaticActorsLocatorFindsClientScriptFile()
    {
        string root = CreateTempDirectory();
        string scriptPath = Path.Combine(root, "client", "script");
        Directory.CreateDirectory(scriptPath);
        string sourcePath = Path.Combine(scriptPath, StaticActorsLocator.StaticActorsFileName);
        File.WriteAllText(sourcePath, "fixture");

        bool found = StaticActorsLocator.TryFindSource(root, out string result);

        Assert.True(found);
        Assert.Equal(sourcePath, result);
    }

    [Fact]
    public void LaunchPlanCarriesServerAndRuntimeEnvironment()
    {
        string root = CreateTempDirectory();
        string exePath = Path.Combine(root, "ffxivboot.exe");
        File.WriteAllText(exePath, "");

        ClientInstall client = ClientInstall.FromPath(root);
        ServerProfile server = ServerProfile.LocalDefault();
        WineRuntimeProfile runtime = WineRuntimeProfile.WinePrefix("Wine", "/tmp/echo-gate-prefix");

        LaunchPlan plan = LaunchPlan.Create(client, server, runtime);

        Assert.Contains("ffxivboot.exe", plan.Arguments);
        Assert.Equal("127.0.0.1", plan.Environment["ECHO_GATE_SERVER_HOST"]);
        Assert.Equal("/tmp/echo-gate-prefix", plan.Environment["WINEPREFIX"]);
    }

    [Fact]
    public void ProfileStoreRoundTripsLocalProfile()
    {
        string path = Path.Combine(CreateTempDirectory(), "profile.json");
        LauncherProfile profile = new(
            "/games/ffxiv-1x",
            ServerProfile.LocalDefault(),
            WineRuntimeProfile.CrossOverBottle("CrossOver", "EchoGate"));

        ProfileStore.Save(path, profile);
        LauncherProfile loaded = ProfileStore.Load(path);

        Assert.Equal(profile.ClientRootPath, loaded.ClientRootPath);
        Assert.Equal(profile.ServerProfile, loaded.ServerProfile);
        Assert.Equal(profile.RuntimeProfile.Name, loaded.RuntimeProfile.Name);
        Assert.Equal(profile.RuntimeProfile.Kind, loaded.RuntimeProfile.Kind);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "echo-gate-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

using System.Xml.Linq;
using System.Text;
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
    public void ClientInstallReportClassifiesBaseInstall()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ffxivboot.exe"), "");
        File.WriteAllText(Path.Combine(root, "ffxivupdater.exe"), "");
        File.WriteAllText(Path.Combine(root, "boot.ver"), ClientVersionInfo.BaseVersion);
        File.WriteAllText(Path.Combine(root, "game.ver"), ClientVersionInfo.BaseVersion);

        ClientInstallReport report = ClientInstall.FromPath(root).Inspect();

        Assert.Equal(ClientInstallState.BaseInstall, report.State);
        Assert.False(report.HasDirectGameExecutable);
        Assert.Contains(report.RequiredActions, action => action.Contains("patch chain", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientInstallReportClassifiesTargetInstall()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ffxivboot.exe"), "");
        File.WriteAllText(Path.Combine(root, "ffxivupdater.exe"), "");
        File.WriteAllText(Path.Combine(root, "ffxivgame.exe"), "");
        File.WriteAllText(Path.Combine(root, "boot.ver"), ClientVersionInfo.TargetBootVersion);
        File.WriteAllText(Path.Combine(root, "game.ver"), ClientVersionInfo.TargetGameVersion);

        ClientInstallReport report = ClientInstall.FromPath(root).Inspect();

        Assert.Equal(ClientInstallState.Ready123b, report.State);
        Assert.True(report.IsLaunchReady);
    }

    [Fact]
    public void LegacyPatchManifestMatchesKnownPatchChain()
    {
        IReadOnlyList<PatchEntry> entries = LegacyPatchManifest.Entries;

        Assert.Equal(52, entries.Count);
        Assert.Equal(PatchRepository.Boot, entries[0].Repository);
        Assert.Equal(ClientVersionInfo.TargetBootVersion, entries[0].ToVersion);
        Assert.Equal(5571687, entries[0].ExpectedSizeBytes);
        Assert.Equal(0x47DDE5EDu, entries[0].ExpectedCrc32);
        Assert.Equal(PatchRepository.Game, entries[^1].Repository);
        Assert.Equal(ClientVersionInfo.TargetGameVersion, entries[^1].ToVersion);
        Assert.Equal(20874726, entries[^1].ExpectedSizeBytes);
        Assert.Equal(0x8A775526u, entries[^1].ExpectedCrc32);
    }

    [Fact]
    public void PatchLibraryReportDetectsCompleteLibrary()
    {
        string root = CreateTempDirectory();
        foreach (PatchEntry entry in LegacyPatchManifest.Entries)
        {
            string patchPath = Path.Combine(root, entry.RelativePatchPath);
            string metainfoPath = Path.Combine(root, entry.RelativeMetainfoPath);
            Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(metainfoPath)!);
            File.WriteAllText(patchPath, "patch");
            File.WriteAllText(metainfoPath, "torrent");
        }

        PatchLibraryReport report = LegacyPatchManifest.InspectLibrary(
            root,
            PatchLibraryInspectionMode.PresenceOnly);

        Assert.True(report.IsComplete);
        Assert.Equal(52, report.PresentPatchCount);
        Assert.Equal(52, report.PresentMetainfoCount);
    }

    [Fact]
    public void PatchLibraryReportDetectsInvalidPatchSize()
    {
        string root = CreateTempDirectory();
        PatchEntry entry = LegacyPatchManifest.Entries[0];
        string patchPath = Path.Combine(root, entry.RelativePatchPath);
        string metainfoPath = Path.Combine(root, entry.RelativeMetainfoPath);
        Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(metainfoPath)!);
        File.WriteAllText(patchPath, "patch");
        File.WriteAllText(metainfoPath, "torrent");

        PatchLibraryReport report = LegacyPatchManifest.InspectLibrary(root);

        PatchFileReport invalid = Assert.Single(report.InvalidPatchFiles);
        Assert.Equal(entry, invalid.Entry);
        Assert.Equal(5, invalid.ActualSizeBytes);
        Assert.False(report.IsComplete);
    }

    [Fact]
    public void Crc32MatchesStandardCheckValue()
    {
        byte[] data = Encoding.ASCII.GetBytes("123456789");

        uint crc32 = Crc32.Compute(data);

        Assert.Equal(0xCBF43926u, crc32);
    }

    [Fact]
    public void RuntimeDiscoveryFindsKnownMacRuntimeTools()
    {
        IReadOnlyList<RuntimeCandidate> candidates = RuntimeDiscovery.Discover(
            path => path.Contains("XIV on Mac", StringComparison.Ordinal)
                || path.Contains("WhiskyCmd", StringComparison.Ordinal),
            _ => false);

        Assert.Contains(candidates, candidate => candidate.Name == "XIV on Mac Wine");
        Assert.Contains(candidates, candidate => candidate.Kind == WineRuntimeKind.WhiskyBottle);
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

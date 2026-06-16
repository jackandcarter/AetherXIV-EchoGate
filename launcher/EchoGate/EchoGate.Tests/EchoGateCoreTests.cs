using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using System.Text;
using System.Text.Json;
using EchoGate.Core;

namespace EchoGate.Tests;

public sealed class EchoGateCoreTests
{
    [Fact]
    public void ServerXmlWriterIncludesLocalPorts()
    {
        ServerProfile profile = ServerProfile.LocalDefault();

        string xml = ServerXmlWriter.ToXml(new[] { profile });
        XElement root = XDocument.Parse(xml).Root!;
        XElement server = root.Element("Server")!;

        Assert.Equal("Servers", root.Name.LocalName);
        Assert.Equal("Local MeteorXIV Core", (string?)server.Attribute("Name"));
        Assert.Equal("127.0.0.1", (string?)server.Attribute("Address"));
        Assert.Equal("http://127.0.0.1:8080/login/index.php", (string?)server.Attribute("LoginUrl"));
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
    public void StaticActorsLocatorFindsNestedPreparedStaticActorsFile()
    {
        string root = CreateTempDirectory();
        string scriptPath = Path.Combine(root, "client", "nested", "script");
        Directory.CreateDirectory(scriptPath);
        string sourcePath = Path.Combine(scriptPath, StaticActorsLocator.PreparedStaticActorsFileName);
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
        Assert.Equal(WineRuntimeProfile.DefaultDirect3DConfig, plan.Environment["WINE_D3D_CONFIG"]);
        Assert.False(string.IsNullOrWhiteSpace(plan.LogPath));
    }

    [Fact]
    public void GameLaunchTokenCarriesSqexArgumentPrefix()
    {
        string sessionId = new('a', 56);

        GameLaunchToken token = GameLaunchTokenGenerator.Generate(sessionId, () => 12345678);
        GameLaunchToken prefixedToken = GameLaunchTokenGenerator.Generate($"sessionId={sessionId}", () => 12345678);

        Assert.Equal(12345678u, token.TickCount);
        Assert.StartsWith(" sqex0002", token.LaunchArgument, StringComparison.Ordinal);
        Assert.EndsWith("!////", token.LaunchArgument, StringComparison.Ordinal);
        Assert.DoesNotContain("+", token.Token, StringComparison.Ordinal);
        Assert.DoesNotContain("/", token.Token, StringComparison.Ordinal);
        Assert.Equal(token.Token, prefixedToken.Token);
    }

    [Fact]
    public void WinePathMapperMapsUnixRootThroughZDrive()
    {
        string mapped = WinePathMapper.ToWindowsPath("/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV/ffxivgame.exe");

        Assert.Equal("Z:\\Volumes\\Dev2\\SquareEnix\\FINAL FANTASY XIV\\ffxivgame.exe", mapped);
    }

    [Fact]
    public void ClientLaunchHelperLocatorHonorsLaunchHelperMode()
    {
        string root = CreateTempDirectory();
        string x86Directory = Path.Combine(root, "Helpers", "win-x86");
        string x64Directory = Path.Combine(root, "Helpers", "win-x64");
        string arm64Directory = Path.Combine(root, "Helpers", "win-arm64");
        Directory.CreateDirectory(x86Directory);
        Directory.CreateDirectory(x64Directory);
        Directory.CreateDirectory(arm64Directory);

        string x86Helper = Path.Combine(x86Directory, "EchoGate.ClientLauncher.exe");
        string x64Helper = Path.Combine(x64Directory, "EchoGate.ClientLauncher.exe");
        string arm64Helper = Path.Combine(arm64Directory, "EchoGate.ClientLauncher.exe");
        File.WriteAllText(x86Helper, "");
        File.WriteAllText(x64Helper, "");
        File.WriteAllText(arm64Helper, "");

        Assert.Equal(x86Helper, ClientLaunchHelperLocator.Find(root));
        Assert.Equal(x64Helper, ClientLaunchHelperLocator.FindLaunchHelper(root));
        Assert.Equal(x64Helper, ClientLaunchHelperLocator.FindLaunchHelper(ClientLaunchHelperMode.Automatic, root));
        Assert.Equal(x86Helper, ClientLaunchHelperLocator.FindLaunchHelper(ClientLaunchHelperMode.X86, root));
        Assert.Equal(x64Helper, ClientLaunchHelperLocator.FindLaunchHelper(ClientLaunchHelperMode.X64, root));
        Assert.Equal(arm64Helper, ClientLaunchHelperLocator.FindLaunchHelper(ClientLaunchHelperMode.Arm64, root));
    }

    [Fact]
    public void LaunchPlanWithHelperCarriesSessionAndMappedGamePath()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ffxivgame.exe"), "");
        ClientInstall client = ClientInstall.FromPath(root);
        ServerProfile server = ServerProfile.LocalDefault();
        WineRuntimeProfile runtime = WineRuntimeProfile.WinePrefix("Wine", "/tmp/echo-gate-prefix");
        string helper = Path.Combine(root, "EchoGate.ClientLauncher.exe");

        LaunchPlan plan = LaunchPlan.CreateWithHelper(
            client,
            server,
            runtime,
            helper,
            new string('b', 56),
            mapClientPathsForWine: true,
            logPath: Path.Combine(root, "launch.log"));

        Assert.Equal(helper, plan.WindowsExecutablePath);
        Assert.Contains("explorer", plan.Arguments);
        Assert.Contains("/desktop=EchoGateXIV-1600x900,1600x900", plan.Arguments);
        Assert.Contains("--session", plan.Arguments);
        Assert.Contains("Z:", plan.Arguments);
        Assert.Contains("127.0.0.1", plan.Arguments);
        Assert.Equal("/tmp/echo-gate-prefix", plan.Environment["WINEPREFIX"]);
        Assert.Equal(WineRuntimeProfile.DefaultDirect3DConfig, plan.Environment["WINE_D3D_CONFIG"]);
    }

    [Fact]
    public void LaunchPlanWithHelperCanUseCustomVirtualDesktopResolution()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ffxivgame.exe"), "");
        ClientInstall client = ClientInstall.FromPath(root);
        ServerProfile server = ServerProfile.LocalDefault();
        WineRuntimeProfile runtime = WineRuntimeProfile.WinePrefix("Wine", "/tmp/echo-gate-prefix");
        string helper = Path.Combine(root, "EchoGate.ClientLauncher.exe");

        LaunchPlan plan = LaunchPlan.CreateWithHelper(
            client,
            server,
            runtime,
            helper,
            new string('b', 56),
            mapClientPathsForWine: true,
            windowMode: ClientWindowMode.WineVirtualDesktop,
            windowWidth: 1920,
            windowHeight: 1080);

        Assert.Contains("/desktop=EchoGateXIV-1920x1080,1920x1080", plan.Arguments);
    }

    [Fact]
    public void LaunchPlanWithHelperCanUseNormalWineWindow()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ffxivgame.exe"), "");
        ClientInstall client = ClientInstall.FromPath(root);
        ServerProfile server = ServerProfile.LocalDefault();
        WineRuntimeProfile runtime = WineRuntimeProfile.WinePrefix("Wine", "/tmp/echo-gate-prefix");
        string helper = Path.Combine(root, "EchoGate.ClientLauncher.exe");

        LaunchPlan plan = LaunchPlan.CreateWithHelper(
            client,
            server,
            runtime,
            helper,
            new string('b', 56),
            mapClientPathsForWine: true,
            windowMode: ClientWindowMode.NormalWindow);

        Assert.DoesNotContain("/desktop=EchoGateXIV", plan.Arguments);
        Assert.Contains(helper, plan.Arguments);
    }

    [Fact]
    public void WineRuntimeConfiguratorBuildsMacRegistrySettings()
    {
        IReadOnlyList<WineRegistrySetting> settings = WineRuntimeConfigurator.BuildRegistrySettings(
            new WineRuntimeConfigurationSettings(
                ClientWindowMode.WineVirtualDesktop,
                1920,
                1080,
                LauncherOperatingSystem.MacOS));

        Assert.Contains(settings, setting =>
            setting.Key == @"HKCU\Software\Wine\Explorer\Desktops"
            && setting.ValueName == "EchoGateXIV-1920x1080"
            && setting.Data == "1920x1080");
        Assert.Contains(settings, setting =>
            setting.Key == @"HKCU\Software\Wine\DirectInput"
            && setting.ValueName == "MouseWarpOverride"
            && setting.Data == "force");
        Assert.Contains(settings, setting =>
            setting.Key == @"HKCU\Software\Wine\Mac Driver"
            && setting.ValueName == "CaptureDisplaysForFullscreen"
            && setting.Data == "y");
    }

    [Fact]
    public void WineRuntimeConfiguratorBuildsLinuxRegistrySettings()
    {
        IReadOnlyList<WineRegistrySetting> settings = WineRuntimeConfigurator.BuildRegistrySettings(
            new WineRuntimeConfigurationSettings(
                ClientWindowMode.WineVirtualDesktop,
                1280,
                720,
                LauncherOperatingSystem.Linux));

        Assert.Contains(settings, setting =>
            setting.Key == @"HKCU\Software\Wine\X11 Driver"
            && setting.ValueName == "GrabFullscreen"
            && setting.Data == "Y");
    }

    [Fact]
    public void WineRuntimeConfiguratorOmitsDesktopSizeForNormalWindow()
    {
        IReadOnlyList<WineRegistrySetting> settings = WineRuntimeConfigurator.BuildRegistrySettings(
            new WineRuntimeConfigurationSettings(
                ClientWindowMode.NormalWindow,
                1920,
                1080,
                LauncherOperatingSystem.Linux));

        Assert.DoesNotContain(settings, setting => setting.Key == @"HKCU\Software\Wine\Explorer\Desktops");
    }

    [Fact]
    public void WineRuntimeConfiguratorQuotesRegistryArguments()
    {
        WineRegistrySetting setting = new(
            @"HKCU\Software\Wine\Explorer\Desktops",
            "EchoGateXIV-1920x1080",
            "REG_SZ",
            "1920x1080");

        string arguments = WineRuntimeConfigurator.BuildRegAddArguments(setting);

        Assert.Contains("reg add", arguments);
        Assert.Contains(@"HKCU\Software\Wine\Explorer\Desktops", arguments);
        Assert.Contains("/v EchoGateXIV-1920x1080", arguments);
        Assert.Contains("/d 1920x1080", arguments);
    }

    [Fact]
    public void WinePrefixPreservesExplicitDirect3DConfig()
    {
        WineRuntimeProfile runtime = WineRuntimeProfile.WinePrefix(
            "Wine",
            "/tmp/echo-gate-prefix",
            environment: new Dictionary<string, string>
            {
                ["WINE_D3D_CONFIG"] = "renderer=vulkan"
            });

        Assert.Equal("renderer=vulkan", runtime.Environment["WINE_D3D_CONFIG"]);
    }

    [Fact]
    public void WineRuntimeProfileAppliesGraphicsTargets()
    {
        WineRuntimeProfile runtime = WineRuntimeProfile.WinePrefix("Wine", "/tmp/echo-gate-prefix");

        Assert.Equal(
            WineRuntimeProfile.DefaultDirect3DConfig,
            runtime.WithGraphicsTarget(ClientGraphicsTarget.OpenGLCompatibility).Environment["WINE_D3D_CONFIG"]);
        Assert.Equal(
            WineRuntimeProfile.OpenGLThreadedDirect3DConfig,
            runtime.WithGraphicsTarget(ClientGraphicsTarget.OpenGLThreaded).Environment["WINE_D3D_CONFIG"]);
        Assert.Equal(
            WineRuntimeProfile.VulkanDirect3DConfig,
            runtime.WithGraphicsTarget(ClientGraphicsTarget.WineD3DVulkan).Environment["WINE_D3D_CONFIG"]);
        Assert.False(runtime.WithGraphicsTarget(ClientGraphicsTarget.WineDefault).Environment.ContainsKey("WINE_D3D_CONFIG"));
    }

    [Fact]
    public void WhiskyRuntimeArgumentsSeparateHelperArguments()
    {
        WineRuntimeProfile runtime = WineRuntimeProfile.WhiskyBottle(
            "Whisky - wow",
            "wow",
            "/Applications/Whisky.app/Contents/Resources/WhiskyCmd");

        string arguments = runtime.BuildArguments(
            "/path/EchoGate.ClientLauncher.exe",
            "--probe");

        Assert.Contains("run wow", arguments);
        Assert.Contains("/path/EchoGate.ClientLauncher.exe -- --probe", arguments);
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
    public void PatchLibraryReportDetectsFfxivPatchesLayout()
    {
        string root = CreateTempDirectory();
        foreach (PatchEntry entry in LegacyPatchManifest.Entries)
        {
            string patchPath = Path.Combine(root, "ffxiv_patches", entry.RepositoryId, "patch", entry.PatchFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
            File.WriteAllText(patchPath, "patch");
        }

        PatchLibraryReport report = LegacyPatchManifest.InspectLibrary(
            root,
            PatchLibraryInspectionMode.PresenceOnly);

        Assert.True(report.IsPatchChainReady);
        Assert.False(report.IsComplete);
        Assert.EndsWith("ffxiv_patches", report.PatchBasePath);
        Assert.Equal(52, report.PresentPatchCount);
        Assert.Equal(0, report.PresentMetainfoCount);
    }

    [Fact]
    public void PatchLibraryReportPrefersMoreCompletePatchLayout()
    {
        string root = CreateTempDirectory();
        PatchEntry staleEntry = LegacyPatchManifest.Entries[0];
        string stalePath = Path.Combine(root, "ffxiv", staleEntry.RepositoryId, "patch", staleEntry.PatchFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(stalePath)!);
        File.WriteAllText(stalePath, "stale");

        foreach (PatchEntry entry in LegacyPatchManifest.Entries)
        {
            string patchPath = Path.Combine(root, "ffxiv_patches", entry.RepositoryId, "patch", entry.PatchFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
            File.WriteAllText(patchPath, "patch");
        }

        PatchLibraryReport report = LegacyPatchManifest.InspectLibrary(
            root,
            PatchLibraryInspectionMode.PresenceOnly);

        Assert.EndsWith("ffxiv_patches", report.PatchBasePath);
        Assert.True(report.IsPatchChainReady);
        Assert.Equal(52, report.PresentPatchCount);
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
    public void LegacyPatchApplierAppliesRawFileEntry()
    {
        string root = CreateTempDirectory();
        string patchPath = Path.Combine(CreateTempDirectory(), "raw.patch");
        byte[] payload = Encoding.ASCII.GetBytes("hello");
        WritePatchFile(patchPath, "client/script/staticactors.bin", payload, compressed: false);

        LegacyPatchApplier.ApplyPatchFile(root, patchPath);

        Assert.Equal(payload, File.ReadAllBytes(Path.Combine(root, "client", "script", "staticactors.bin")));
    }

    [Fact]
    public void LegacyPatchApplierAppliesCompressedFileEntry()
    {
        string root = CreateTempDirectory();
        string patchPath = Path.Combine(CreateTempDirectory(), "compressed.patch");
        byte[] payload = Encoding.ASCII.GetBytes("compressed static actors");
        WritePatchFile(patchPath, "client/script/staticactors.bin", payload, compressed: true);

        LegacyPatchApplier.ApplyPatchFile(root, patchPath);

        Assert.Equal(payload, File.ReadAllBytes(Path.Combine(root, "client", "script", "staticactors.bin")));
    }

    [Fact]
    public void LegacyPatchApplierAppendsMultiChunkFileEntry()
    {
        string root = CreateTempDirectory();
        string patchPath = Path.Combine(CreateTempDirectory(), "multi.patch");
        byte[] first = Encoding.ASCII.GetBytes("first ");
        byte[] second = Encoding.ASCII.GetBytes("second");
        byte[] expected = [.. first, .. second];

        WritePatchFileChunks(
            patchPath,
            "client/script/staticactors.bin",
            (0x41, first, false, (uint)expected.Length),
            (0x4D, second, true, (uint)expected.Length));

        LegacyPatchApplier.ApplyPatchFile(root, patchPath);

        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(root, "client", "script", "staticactors.bin")));
    }

    [Fact]
    public void LegacyPatchApplierDeletesFileEntry()
    {
        string root = CreateTempDirectory();
        string targetPath = Path.Combine(root, "client", "script", "staticactors.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.WriteAllText(targetPath, "delete me");
        string patchPath = Path.Combine(CreateTempDirectory(), "delete.patch");

        WritePatchFileChunks(
            patchPath,
            "client/script/staticactors.bin",
            (0x44, [], false, 0));

        LegacyPatchApplier.ApplyPatchFile(root, patchPath);

        Assert.False(File.Exists(targetPath));
    }

    [Fact]
    public void LegacyPatchApplierRejectsEscapingPath()
    {
        string root = CreateTempDirectory();
        string patchPath = Path.Combine(CreateTempDirectory(), "escape.patch");
        WritePatchFile(patchPath, "../escaped.bin", Encoding.ASCII.GetBytes("nope"), compressed: false);

        Assert.Throws<InvalidDataException>(() => LegacyPatchApplier.ApplyPatchFile(root, patchPath));
    }

    [Fact]
    public void RuntimeDiscoveryFindsKnownMacRuntimeTools()
    {
        IReadOnlyList<RuntimeCandidate> candidates = RuntimeDiscovery.Discover(
            path => path.Contains("Wine Stable.app", StringComparison.Ordinal)
                || path.Contains("XIV on Mac", StringComparison.Ordinal)
                || path.Contains("WhiskyCmd", StringComparison.Ordinal),
            _ => false,
            _ => new[] { "wow" });

        Assert.Equal("Homebrew Wine Stable", candidates[0].Name);
        Assert.Contains(candidates, candidate => candidate.Name == "XIV on Mac Wine");
        Assert.Contains(candidates, candidate =>
            candidate.Name == "Homebrew Wine Stable"
            && candidate.Kind == WineRuntimeKind.WinePrefix
            && candidate.Command.Contains("Wine Stable.app", StringComparison.Ordinal));
        Assert.Contains(candidates, candidate =>
            candidate.Kind == WineRuntimeKind.WhiskyBottle
            && candidate.Name == "Whisky - wow"
            && candidate.BottleOrPrefix == "wow");
    }

    [Fact]
    public void RuntimeDiscoveryFindsCommonLinuxWinePrefixes()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dotWine = Path.Combine(home, ".wine");
        string homeWine = Path.Combine(home, "wine");

        IReadOnlyList<RuntimeCandidate> candidates = RuntimeDiscovery.Discover(
            _ => false,
            path => string.Equals(path, dotWine, StringComparison.Ordinal)
                || string.Equals(path, homeWine, StringComparison.Ordinal));

        Assert.Contains(candidates, candidate =>
            candidate.Name == "Default Wine prefix"
            && candidate.Kind == WineRuntimeKind.WinePrefix
            && candidate.BottleOrPrefix == dotWine);
        Assert.Contains(candidates, candidate =>
            candidate.Name == "Home wine prefix"
            && candidate.Kind == WineRuntimeKind.WinePrefix
            && candidate.BottleOrPrefix == homeWine);
    }

    [Fact]
    public void RuntimeDiscoveryFindsCustomHomeWinePrefix()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string customPrefix = Path.Combine(home, ".wine_custom");

        IReadOnlyList<RuntimeCandidate> candidates = RuntimeDiscovery.Discover(
            _ => false,
            path => string.Equals(path, customPrefix, StringComparison.Ordinal),
            _ => Array.Empty<string>(),
            _ => new[] { customPrefix });

        Assert.Contains(candidates, candidate =>
            candidate.Name == "Wine prefix .wine_custom"
            && candidate.Kind == WineRuntimeKind.WinePrefix
            && candidate.BottleOrPrefix == customPrefix);
    }

    [Fact]
    public void RuntimeDiscoveryParsesWhiskyBottleList()
    {
        string output = """
            +------+-----------------+--------------------------------------------------------------------------+
            | Name | Windows Version | Path                                                                     |
            +------+-----------------+--------------------------------------------------------------------------+
            | Wow  | Windows 10      | /Volumes/Dev/604B73F2-10BC-435C-8D4A-9331841FC7B3                        |
            | wow  | Windows 10      | ~/Library/Containers/Whisky/Bottles/14A60580-0075-47F8-813E-38CE3A0CE5D4 |
            +------+-----------------+--------------------------------------------------------------------------+
            """;

        IReadOnlyList<string> bottles = RuntimeDiscovery.ParseWhiskyBottleNames(output);

        Assert.Equal(new[] { "Wow", "wow" }, bottles);
    }

    [Fact]
    public void WhiskyShellEnvResolvesWineProfile()
    {
        string root = CreateTempDirectory();
        string wineDirectory = Path.Combine(root, "Wine", "bin");
        Directory.CreateDirectory(wineDirectory);
        string winePath = Path.Combine(wineDirectory, "wine64");
        File.WriteAllText(winePath, "");

        string shellEnv = $"""
            export PATH="{wineDirectory}:$PATH"
            export WINE="wine64"
            export WINEPREFIX="~/Library/Containers/Whisky/Bottles/test"
            export WINEDEBUG="fixme-all"
            """;

        bool resolved = WhiskyRuntimeEnvironment.TryCreateWineProfileFromShellEnv(
            "Whisky - test",
            shellEnv,
            out WineRuntimeProfile profile,
            out string error);

        Assert.True(resolved, error);
        Assert.Equal(WineRuntimeKind.WinePrefix, profile.Kind);
        Assert.Equal(winePath, profile.Command);
        Assert.EndsWith(Path.Combine("Library", "Containers", "Whisky", "Bottles", "test"), profile.PrefixPath);
        Assert.Equal("fixme-all", profile.Environment["WINEDEBUG"]);
    }

    [Fact]
    public void ProfileStoreRoundTripsLocalProfile()
    {
        string path = Path.Combine(CreateTempDirectory(), "profile.json");
        LauncherProfile profile = new(
            "/games/ffxiv-1x",
            "/patches/ffxiv-1x",
            "https://launcher.example.test/launcher",
            "https://cdn.example.test/ffxiv_patches",
            ServerProfile.LocalDefault(),
            WineRuntimeProfile.CrossOverBottle("CrossOver", "EchoGate"),
            RuntimeSelectionMode.CustomRuntime,
            ClientLaunchHelperMode.X86,
            ClientGraphicsTarget.WineD3DVulkan);

        ProfileStore.Save(path, profile);
        LauncherProfile loaded = ProfileStore.Load(path);

        Assert.Equal(profile.ClientRootPath, loaded.ClientRootPath);
        Assert.Equal(profile.PatchLibraryRootPath, loaded.PatchLibraryRootPath);
        Assert.Equal(profile.LauncherServiceUrl, loaded.LauncherServiceUrl);
        Assert.Equal(profile.PatchBaseUrl, loaded.PatchBaseUrl);
        Assert.Equal(profile.ServerProfile, loaded.ServerProfile);
        Assert.Equal(profile.RuntimeProfile.Name, loaded.RuntimeProfile.Name);
        Assert.Equal(profile.RuntimeProfile.Kind, loaded.RuntimeProfile.Kind);
        Assert.Equal(profile.RuntimeMode, loaded.RuntimeMode);
        Assert.Equal(profile.LaunchHelperMode, loaded.LaunchHelperMode);
        Assert.Equal(profile.GraphicsTarget, loaded.GraphicsTarget);
    }

    [Fact]
    public void LauncherPatchManifestUsesKnownPatchChain()
    {
        LauncherPatchManifest manifest = LauncherPatchManifest.FromKnownPatchChain("https://cdn.example.test/ffxiv_patches/");

        Assert.Equal(ClientVersionInfo.TargetBootVersion, manifest.TargetBootVersion);
        Assert.Equal(ClientVersionInfo.TargetGameVersion, manifest.TargetGameVersion);
        Assert.Equal(52, manifest.Files.Count);
        Assert.Equal("ffxiv/2d2a390f/patch/D2010.09.18.0000.patch", manifest.Files[0].RelativePath);
        Assert.Equal("47DDE5ED", manifest.Files[0].Crc32);
        Assert.Equal("https://cdn.example.test/ffxiv_patches", manifest.PatchBaseUrl);
    }

    [Fact]
    public async Task PatchDownloadServiceDownloadsAndReusesValidatedFiles()
    {
        byte[] payload = Encoding.ASCII.GetBytes("123456789");
        LauncherPatchManifest manifest = new(
            ClientVersionInfo.TargetBootVersion,
            ClientVersionInfo.TargetGameVersion,
            "https://cdn.example.test/patches",
            new[]
            {
                new LauncherPatchFile("ffxiv/48eca647/patch/test.patch", payload.Length, "CBF43926", null)
            });
        HttpClient client = new(new StaticPatchHandler(payload));
        string root = CreateTempDirectory();
        List<PatchDownloadProgress> progress = new();

        PatchDownloadResult first = await PatchDownloadService.DownloadPatchLibraryAsync(
            manifest,
            root,
            client,
            new Progress<PatchDownloadProgress>(progress.Add));
        PatchDownloadResult second = await PatchDownloadService.DownloadPatchLibraryAsync(
            manifest,
            root,
            client);

        string localPath = Path.Combine(root, "ffxiv", "48eca647", "patch", "test.patch");
        Assert.True(File.Exists(localPath));
        Assert.Equal(payload, File.ReadAllBytes(localPath));
        Assert.Equal(1, first.DownloadedFileCount);
        Assert.Equal(0, first.ReusedFileCount);
        Assert.Equal(0, second.DownloadedFileCount);
        Assert.Equal(1, second.ReusedFileCount);
        Assert.Contains(progress, update => update.LogMessage);
    }

    [Fact]
    public void RuntimeCatalogDeserializesAndSelectsDefaultArtifact()
    {
        string json = """
        {
          "platform": "osx-arm64",
          "artifacts": [
            {
              "name": "Fallback Wine",
              "version": "1.0",
              "platform_rid": "osx-arm64",
              "runtime_kind": "wine",
              "archive_url": "https://cdn.example.test/runtime-fallback.zip",
              "archive_format": "zip",
              "size_bytes": 12,
              "sha256": "ABC",
              "executable_relative_path": "bin/wine",
              "prefix_arch": "win64",
              "environment": {},
              "is_default": false,
              "is_active": true,
              "sort_order": 20
            },
            {
              "name": "Echo Gate Wine",
              "version": "2.0",
              "platform_rid": "osx-arm64",
              "runtime_kind": "wine",
              "archive_url": "https://cdn.example.test/runtime.zip",
              "archive_format": "zip",
              "size_bytes": 12,
              "sha256": "DEF",
              "executable_relative_path": "bin/wine",
              "prefix_arch": "win64",
              "environment": { "WINEDEBUG": "-all" },
              "is_default": true,
              "is_active": true,
              "sort_order": 10
            }
          ]
        }
        """;

        RuntimeCatalog catalog = JsonSerializer.Deserialize<RuntimeCatalog>(json)!;
        RuntimeArtifact selected = catalog.SelectDefault()!;

        Assert.Equal("osx-arm64", catalog.Platform);
        Assert.Equal("Echo Gate Wine", selected.Name);
        Assert.Equal("-all", selected.Environment["WINEDEBUG"]);
    }

    [Fact]
    public async Task RuntimeDownloadServiceInstallsValidatedZipArchive()
    {
        byte[] archive = CreateRuntimeArchive(("bin/wine", Encoding.ASCII.GetBytes("#!/bin/sh\n")));
        RuntimeArtifact artifact = CreateRuntimeArtifact(archive);
        string root = CreateTempDirectory();
        List<RuntimeDownloadProgress> progress = new();

        RuntimeDownloadResult result = await RuntimeDownloadService.DownloadAndInstallAsync(
            artifact,
            new HttpClient(new StaticPatchHandler(archive)),
            new Progress<RuntimeDownloadProgress>(progress.Add),
            runtimesRoot: Path.Combine(root, "runtimes"),
            cacheRoot: Path.Combine(root, "cache"));

        Assert.True(File.Exists(result.Install.ExecutablePath));
        Assert.True(File.Exists(RuntimeInstallStore.ManifestPathFor(result.Install.InstallPath)));
        Assert.Equal(artifact.Name, result.Install.Name);
        Assert.Contains(progress, update => update.LogMessage);
    }

    [Fact]
    public async Task RuntimeDownloadServiceRejectsPathTraversalArchive()
    {
        byte[] archive = CreateRuntimeArchive(("../escape.sh", Encoding.ASCII.GetBytes("nope")));
        RuntimeArtifact artifact = CreateRuntimeArtifact(archive);
        string root = CreateTempDirectory();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            RuntimeDownloadService.DownloadAndInstallAsync(
                artifact,
                new HttpClient(new StaticPatchHandler(archive)),
                runtimesRoot: Path.Combine(root, "runtimes"),
                cacheRoot: Path.Combine(root, "cache")));
    }

    [Fact]
    public void RuntimeInstallManifestRoundTrips()
    {
        string root = CreateTempDirectory();
        string installRoot = Path.Combine(root, "runtime");
        string executable = Path.Combine(installRoot, "bin", "wine");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.WriteAllText(executable, "");

        ManagedRuntimeInstall install = new(
            "Echo Gate Wine",
            "1.0",
            "osx-arm64",
            "wine",
            installRoot,
            executable,
            "win64",
            new Dictionary<string, string> { ["WINEDEBUG"] = "-all" },
            DateTimeOffset.UtcNow);

        RuntimeInstallStore.Save(install);
        ManagedRuntimeInstall loaded = RuntimeInstallStore.Load(installRoot);

        Assert.Equal(install.Name, loaded.Name);
        Assert.Equal(install.ExecutablePath, loaded.ExecutablePath);
        Assert.Equal("-all", loaded.Environment["WINEDEBUG"]);
    }

    [Fact]
    public void ManagedPrefixPathUsesApplicationDataLayout()
    {
        string prefixPath = RuntimeInstallStore.ManagedPrefixPath;

        Assert.EndsWith(Path.Combine("Prefixes", "ffxiv-1x"), prefixPath);
        Assert.Contains("Demi Dev Unit", prefixPath, StringComparison.Ordinal);
        Assert.Contains("Echo Gate", prefixPath, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeProfileResolverPrefersManagedRuntimeInAutomaticMode()
    {
        ManagedRuntimeInstall install = new(
            "Echo Gate Wine",
            "1.0",
            "osx-arm64",
            "wine",
            "/managed/runtime",
            "/managed/runtime/bin/wine",
            "win64",
            new Dictionary<string, string> { ["WINEDEBUG"] = "-all" },
            DateTimeOffset.UtcNow);
        RuntimeCandidate detected = new(
            "Detected Wine",
            WineRuntimeKind.WinePrefix,
            "/usr/local/bin/wine",
            "/tmp/detected-prefix",
            "test");
        WineRuntimeProfile custom = WineRuntimeProfile.Custom("Custom", "/custom/wine");

        WineRuntimeProfile resolved = RuntimeProfileResolver.Resolve(
            RuntimeSelectionMode.AutomaticManaged,
            install,
            new[] { detected },
            custom,
            "/managed/prefix");

        Assert.Equal("/managed/runtime/bin/wine", resolved.Command);
        Assert.Equal("/managed/prefix", resolved.Environment["WINEPREFIX"]);
        Assert.Equal("-all", resolved.Environment["WINEDEBUG"]);
        Assert.Equal(WineRuntimeProfile.DefaultDirect3DConfig, resolved.Environment["WINE_D3D_CONFIG"]);
    }

    [Fact]
    public void RuntimeProfileResolverKeepsDetectedWinePrefix()
    {
        RuntimeCandidate detected = new(
            "Default Wine prefix",
            WineRuntimeKind.WinePrefix,
            "wine",
            "/home/devunit/.wine",
            "WINEPREFIX");
        WineRuntimeProfile custom = WineRuntimeProfile.Custom("Custom", "/custom/wine");

        WineRuntimeProfile resolved = RuntimeProfileResolver.Resolve(
            RuntimeSelectionMode.DetectedRuntime,
            null,
            new[] { detected },
            custom,
            "/managed/prefix");

        Assert.Equal("wine", resolved.Command);
        Assert.Equal("/home/devunit/.wine", resolved.PrefixPath);
        Assert.Equal("/home/devunit/.wine", resolved.Environment["WINEPREFIX"]);
    }

    [Fact]
    public void RuntimeLaunchDiagnosticsRedactsSessionArgument()
    {
        string arguments = "wine-helper.exe --game ffxivgame.exe --session \"sessionId=secret-session\" --server-host 127.0.0.1";

        string redacted = RuntimeLaunchDiagnostics.RedactSensitiveArguments(arguments);

        Assert.DoesNotContain("secret-session", redacted);
        Assert.Contains("--session <redacted>", redacted, StringComparison.Ordinal);
        Assert.Contains("--server-host 127.0.0.1", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeValidatorFallsBackToWineBuiltinWineboot()
    {
        if (OperatingSystem.IsWindows())
            return;

        string root = CreateTempDirectory();
        string fakeWine = Path.Combine(root, "wine");
        string argsLog = Path.Combine(root, "args.log");
        await File.WriteAllTextAsync(
            fakeWine,
            $"""
            #!/bin/sh
            echo "$@" >> "{argsLog}"
            if [ "$1" = "--version" ]; then
              echo "wine-11.0"
              exit 0
            fi
            if [ "$1" = "wineboot" ]; then
              exit 0
            fi
            exit 1
            """);
        File.SetUnixFileMode(
            fakeWine,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute);

        string prefix = Path.Combine(root, "prefix");
        WineRuntimeProfile profile = WineRuntimeProfile.WinePrefix("Fake Wine", prefix, fakeWine);

        string? oldPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", root);
            RuntimeValidationResult result = await RuntimeValidator.ValidateAsync(profile, prefix);

            Assert.True(result.IsReady);
            string log = await File.ReadAllTextAsync(argsLog);
            Assert.Contains("--version", log);
            Assert.Contains("wineboot -u", log);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", oldPath);
        }
    }

    [Fact]
    public async Task RuntimeValidatorSkipsWinebootForInitializedPrefix()
    {
        if (OperatingSystem.IsWindows())
            return;

        string root = CreateTempDirectory();
        string fakeWine = Path.Combine(root, "wine");
        string argsLog = Path.Combine(root, "args.log");
        await File.WriteAllTextAsync(
            fakeWine,
            $"""
            #!/bin/sh
            echo "$@" >> "{argsLog}"
            if [ "$1" = "--version" ]; then
              echo "wine-11.0"
              exit 0
            fi
            if [ "$1" = "wineboot" ]; then
              exit 9
            fi
            exit 0
            """);
        File.SetUnixFileMode(
            fakeWine,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute);

        string prefix = Path.Combine(root, "prefix");
        Directory.CreateDirectory(prefix);
        await File.WriteAllTextAsync(Path.Combine(prefix, "system.reg"), "system");
        await File.WriteAllTextAsync(Path.Combine(prefix, "user.reg"), "user");
        WineRuntimeProfile profile = WineRuntimeProfile.WinePrefix("Fake Wine", prefix, fakeWine);

        string? oldPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", root);
            RuntimeValidationResult result = await RuntimeValidator.ValidateAsync(profile, prefix);

            Assert.True(result.IsReady);
            string log = await File.ReadAllTextAsync(argsLog);
            Assert.Contains("--version", log);
            Assert.DoesNotContain("wineboot", log);

            string validationLog = await File.ReadAllTextAsync(result.LogPath);
            Assert.Contains("prefix_already_initialized=", validationLog);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", oldPath);
        }
    }

    [Fact]
    public async Task RuntimeValidatorUsesProfileWinePrefixOverFallback()
    {
        if (OperatingSystem.IsWindows())
            return;

        string root = CreateTempDirectory();
        string fakeWine = Path.Combine(root, "wine");
        await File.WriteAllTextAsync(
            fakeWine,
            """
            #!/bin/sh
            if [ "$1" = "--version" ]; then
              echo "wine-11.0"
              exit 0
            fi
            exit 0
            """);
        File.SetUnixFileMode(
            fakeWine,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute);

        string selectedPrefix = Path.Combine(root, ".wine_custom");
        Directory.CreateDirectory(selectedPrefix);
        await File.WriteAllTextAsync(Path.Combine(selectedPrefix, "system.reg"), "system");
        await File.WriteAllTextAsync(Path.Combine(selectedPrefix, "user.reg"), "user");
        string fallbackPrefix = Path.Combine(root, "managed-prefix");
        WineRuntimeProfile profile = WineRuntimeProfile.WinePrefix("Custom Wine", selectedPrefix, fakeWine);

        RuntimeValidationResult result = await RuntimeValidator.ValidateAsync(profile, fallbackPrefix);

        Assert.True(result.IsReady);
        Assert.Equal(selectedPrefix, result.PrefixPath);
        Assert.False(Directory.Exists(fallbackPrefix));
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "echo-gate-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WritePatchFile(string patchPath, string relativePath, byte[] payload, bool compressed)
    {
        WritePatchFileChunks(patchPath, relativePath, (0x41, payload, compressed, (uint)payload.Length));
    }

    private static void WritePatchFileChunks(
        string patchPath,
        string relativePath,
        params (uint Mode, byte[] Payload, bool Compressed, uint NewFileSize)[] chunks)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
        using FileStream stream = File.Create(patchPath);
        stream.Write([0x91, (byte)'Z', (byte)'I', (byte)'P', (byte)'A', (byte)'T', (byte)'C', (byte)'H']);
        stream.Write(new byte[8]);
        WriteCommand(stream, "ETRY");

        byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
        WriteUInt32BigEndian(stream, (uint)pathBytes.Length);
        stream.Write(pathBytes);
        WriteUInt32BigEndian(stream, (uint)chunks.Length);

        foreach ((uint mode, byte[] payload, bool compressed, uint newFileSize) in chunks)
        {
            WriteUInt32LittleEndian(stream, mode);
            stream.Write(new byte[0x14]);
            stream.Write(new byte[0x14]);

            byte[] storedPayload = compressed ? CompressZlib(payload) : payload;
            WriteUInt32LittleEndian(stream, compressed ? 0x5Au : 0x4Eu);
            WriteUInt32BigEndian(stream, (uint)storedPayload.Length);
            WriteUInt32BigEndian(stream, 0);
            WriteUInt32BigEndian(stream, newFileSize);
            stream.Write(storedPayload);
        }

        stream.Write(new byte[8]);
    }

    private static byte[] CompressZlib(byte[] payload)
    {
        using MemoryStream output = new();
        using (ZLibStream zlib = new(output, CompressionMode.Compress))
        {
            zlib.Write(payload);
        }

        return output.ToArray();
    }

    private static void WriteCommand(Stream stream, string command)
    {
        stream.Write(Encoding.ASCII.GetBytes(command));
    }

    private static void WriteUInt32BigEndian(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt32LittleEndian(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static byte[] CreateRuntimeArchive(params (string Path, byte[] Payload)[] files)
    {
        using MemoryStream output = new();
        using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string path, byte[] payload) in files)
            {
                ZipArchiveEntry entry = archive.CreateEntry(path);
                using Stream stream = entry.Open();
                stream.Write(payload);
            }
        }

        return output.ToArray();
    }

    private static RuntimeArtifact CreateRuntimeArtifact(byte[] archive)
    {
        return new RuntimeArtifact(
            "Echo Gate Wine",
            "1.0",
            "osx-arm64",
            "wine",
            "https://cdn.example.test/runtime.zip",
            "zip",
            archive.Length,
            Convert.ToHexString(SHA256.HashData(archive)),
            "bin/wine",
            "win64",
            new Dictionary<string, string> { ["WINEDEBUG"] = "-all" },
            true,
            true,
            10);
    }

    private sealed class StaticPatchHandler : HttpMessageHandler
    {
        private readonly byte[] payload;

        public StaticPatchHandler(byte[] payload)
        {
            this.payload = payload;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            };
            return Task.FromResult(response);
        }
    }
}

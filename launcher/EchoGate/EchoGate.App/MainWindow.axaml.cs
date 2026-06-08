using Avalonia.Controls;
using EchoGate.Core;

namespace EchoGate.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AppendLog("Echo Gate initialized.");
        ScanRuntimeCandidates(false);
    }

    private void ValidateBackend_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            ServerProfile profile = ReadServerProfile();
            string xml = ServerXmlWriter.ToXml(new[] { profile });
            AppendLog($"Backend profile valid: {profile.Name} {profile.Host}:{profile.LobbyPort}");
            AppendLog(xml);
        }
        catch (Exception ex)
        {
            AppendLog($"Backend validation failed: {ex.Message}");
        }
    }

    private void ValidateClient_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            ClientInstall clientInstall = ClientInstall.FromPath(ClientPathBox.Text ?? "");
            ClientInstallReport report = clientInstall.Inspect();

            ClientVersionStatus.Text = $"{report.State}: {report.Version.DisplayText}";
            ExecutableStatus.Text = FormatExecutableStatus(report);
            StaticActorsStatus.Text = report.HasStaticActors ? clientInstall.StaticActorsSourcePath : "Not found";

            AppendLog($"Client state: {report.State} ({report.Version.DisplayText})");
            foreach (string action in report.RequiredActions)
                AppendLog($"Client action: {action}");

            ValidatePatchLibrary();
        }
        catch (Exception ex)
        {
            ClientVersionStatus.Text = "Validation failed";
            ExecutableStatus.Text = "Validation failed";
            StaticActorsStatus.Text = "Validation failed";
            AppendLog($"Client validation failed: {ex.Message}");
        }
    }

    private void BuildLaunchPlan_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            ClientInstall clientInstall = ClientInstall.FromPath(ClientPathBox.Text ?? "");
            ServerProfile serverProfile = ReadServerProfile();
            WineRuntimeProfile runtimeProfile = ReadRuntimeProfile();
            LaunchPlan plan = LaunchPlan.Create(clientInstall, serverProfile, runtimeProfile);

            AppendLog($"Launch plan ready: {plan.Arguments}");
            foreach (KeyValuePair<string, string> pair in plan.Environment)
                AppendLog($"{pair.Key}={pair.Value}");
        }
        catch (Exception ex)
        {
            AppendLog($"Launch plan failed: {ex.Message}");
        }
    }

    private void ClearLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LaunchLogBox.Text = "";
    }

    private void ScanRuntime_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ScanRuntimeCandidates(true);
    }

    private ServerProfile ReadServerProfile()
    {
        ServerProfile profile = new(
            ServerNameBox.Text ?? "",
            ServerHostBox.Text ?? "",
            Convert.ToInt32(LobbyPortBox.Value ?? 54994),
            Convert.ToInt32(WorldPortBox.Value ?? 54992),
            Convert.ToInt32(MapPortBox.Value ?? 1989));

        profile.Validate();
        return profile;
    }

    private WineRuntimeProfile ReadRuntimeProfile()
    {
        string name = RuntimeNameBox.Text ?? "Local Wine Runtime";
        string command = RuntimeCommandBox.Text ?? "wine";
        string value = RuntimeValueBox.Text ?? "";

        return RuntimeKindBox.SelectedIndex switch
        {
            0 => WineRuntimeProfile.CrossOverBottle(name, string.IsNullOrWhiteSpace(value) ? "EchoGate" : value, command),
            1 => WineRuntimeProfile.WinePrefix(name, value, command),
            2 => WineRuntimeProfile.WhiskyBottle(name, string.IsNullOrWhiteSpace(value) ? "EchoGate" : value, command),
            _ => WineRuntimeProfile.Custom(name, command)
        };
    }

    private void ValidatePatchLibrary()
    {
        string patchLibraryPath = PatchLibraryPathBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(patchLibraryPath))
        {
            PatchLibraryStatus.Text = "Not selected";
            AppendLog("Patch library path is not selected.");
            return;
        }

        PatchLibraryReport report = LegacyPatchManifest.InspectLibrary(patchLibraryPath);
        PatchLibraryStatus.Text = report.Summary;
        AppendLog($"Patch library: {report.Summary}");

        LogMissingEntries("Missing patch", report.MissingPatchFiles);
        LogMissingEntries("Missing metainfo", report.MissingMetainfoFiles);
        LogInvalidEntries(report.InvalidPatchFiles);
    }

    private void ScanRuntimeCandidates(bool appendLog)
    {
        IReadOnlyList<RuntimeCandidate> candidates = RuntimeDiscovery.Discover();
        RuntimeCandidatesBox.Text = candidates.Count == 0
            ? "No known runtime tools detected."
            : string.Join(Environment.NewLine, candidates.Select(FormatRuntimeCandidate));

        if (appendLog)
            AppendLog($"Runtime scan found {candidates.Count} candidate(s).");
    }

    private static string FormatExecutableStatus(ClientInstallReport report)
    {
        string[] parts =
        {
            FormatStatus("boot", report.HasBootExecutable),
            FormatStatus("updater", report.HasUpdaterExecutable),
            FormatStatus("game", report.HasDirectGameExecutable),
            FormatStatus("config", report.HasConfigExecutable)
        };

        return string.Join(", ", parts);
    }

    private static string FormatRuntimeCandidate(RuntimeCandidate candidate)
    {
        string value = string.IsNullOrWhiteSpace(candidate.BottleOrPrefix) ? "profile required" : candidate.BottleOrPrefix;
        return $"{candidate.Name} | {candidate.Kind} | {candidate.Command} | {value}";
    }

    private static string FormatStatus(string label, bool isPresent)
    {
        return $"{label}={(isPresent ? "ok" : "missing")}";
    }

    private void LogMissingEntries(string label, IReadOnlyList<PatchEntry> entries)
    {
        foreach (PatchEntry entry in entries.Take(3))
            AppendLog($"{label}: {entry.RelativePatchPath}");

        if (entries.Count > 3)
            AppendLog($"{label}: {entries.Count - 3} more");
    }

    private void LogInvalidEntries(IReadOnlyList<PatchFileReport> entries)
    {
        foreach (PatchFileReport report in entries.Take(3))
        {
            string actualSize = report.ActualSizeBytes?.ToString() ?? "missing";
            string actualCrc32 = report.ActualCrc32?.ToString("X8") ?? "not checked";
            AppendLog(
                "Invalid patch: "
                + $"{report.Entry.RelativePatchPath} "
                + $"expected {report.Entry.ExpectedSizeBytes} bytes/{report.Entry.ExpectedCrc32Text}, "
                + $"actual {actualSize} bytes/{actualCrc32}");
        }

        if (entries.Count > 3)
            AppendLog($"Invalid patch: {entries.Count - 3} more");
    }

    private void AppendLog(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LaunchLogBox.Text = string.IsNullOrEmpty(LaunchLogBox.Text)
            ? line
            : $"{LaunchLogBox.Text}{Environment.NewLine}{line}";
    }
}

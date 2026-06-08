using Avalonia.Controls;
using EchoGate.Core;

namespace EchoGate.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AppendLog("Echo Gate initialized.");
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
        string clientPath = ClientPathBox.Text ?? "";
        if (StaticActorsLocator.TryFindSource(clientPath, out string sourcePath))
        {
            StaticActorsStatus.Text = sourcePath;
            AppendLog($"Static actors source found: {sourcePath}");
        }
        else
        {
            StaticActorsStatus.Text = "Not found";
            AppendLog("Static actors source was not found.");
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
            _ => WineRuntimeProfile.Custom(name, command)
        };
    }

    private void AppendLog(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LaunchLogBox.Text = string.IsNullOrEmpty(LaunchLogBox.Text)
            ? line
            : $"{LaunchLogBox.Text}{Environment.NewLine}{line}";
    }
}

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using EchoGate.Core;

namespace EchoGate.App;

public sealed partial class MainWindow : Window
{
    private readonly HttpClient httpClient = new();
    private readonly LauncherPlatform platform = LauncherPlatform.Current;
    private CancellationTokenSource? patchCancellation;
    private CancellationTokenSource? runtimeCancellation;
    private LauncherConfig? launcherConfig;
    private RuntimeCatalog? runtimeCatalog;
    private RuntimeArtifact? selectedRuntimeArtifact;
    private ManagedRuntimeInstall? managedRuntimeInstall;
    private IReadOnlyList<RuntimeCandidate> runtimeCandidates = Array.Empty<RuntimeCandidate>();
    private string? currentSessionId;
    private string? currentSessionUsername;
    private bool launchInProgress;
    private bool isInitialized;

    public MainWindow()
    {
        InitializeComponent();
        isInitialized = true;
        ConfigurePlatformUi();
        LoadSavedProfile();
        AppendLog("Echo Gate initialized.");
        ScanRuntimeCandidates(false);
        RefreshInstalledManagedRuntimeStatus();
        ValidateClientIfSelected();
        UpdateHomeState();
        Closing += (_, _) => SaveCurrentProfile();
        _ = RefreshLauncherServicesAsync();
    }

    private async void RefreshServices_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RefreshLauncherServicesAsync();
    }

    private void SaveSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SaveCurrentProfile();
        AppendLog($"Settings saved: {ProfileStore.DefaultProfilePath}");
    }

    private void ValidateClient_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ValidateClient();
    }

    private async void LocateClientFromHome_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 2;
        await SelectClientExecutableAsync();
    }

    private async void BrowseClient_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await SelectClientExecutableAsync();
    }

    private async Task SelectClientExecutableAsync()
    {
        if (!StorageProvider.CanOpen)
        {
            AppendLog("Client executable picker is not available on this platform.");
            return;
        }

        FilePickerFileType clientExecutableType = new("FFXIV 1.x executable")
        {
            Patterns = new[] { "ffxivboot.exe", "ffxivgame.exe", "*.exe" },
            AppleUniformTypeIdentifiers = new[] { "public.executable", "com.microsoft.windows-executable", "public.item" }
        };

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select ffxivboot.exe or ffxivgame.exe",
            AllowMultiple = false,
            SuggestedFileType = clientExecutableType,
            FileTypeFilter = new[] { clientExecutableType, FilePickerFileTypes.All }
        });

        if (files.Count == 0)
        {
            AppendLog("Client executable selection cancelled.");
            return;
        }

        string? selectedPath = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            AppendLog("Client executable selection failed: local path is unavailable.");
            return;
        }

        string fileName = Path.GetFileName(selectedPath);
        if (!IsKnownClientExecutable(fileName))
            AppendLog($"Client executable note: expected ffxivboot.exe or ffxivgame.exe, selected {fileName}.");

        string? clientRoot = Path.GetDirectoryName(selectedPath);
        if (string.IsNullOrWhiteSpace(clientRoot))
        {
            AppendLog("Client executable selection failed: containing folder is unavailable.");
            return;
        }

        ClientPathBox.Text = clientRoot;
        AppendLog($"Client root selected: {clientRoot}");
        ValidateClient();
        SaveCurrentProfile();
    }

    private async void BrowsePatchLibrary_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!StorageProvider.CanPickFolder)
        {
            AppendLog("Patch library folder picker is not available on this platform.");
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select FFXIV 1.x patch library folder",
            AllowMultiple = false
        });

        if (folders.Count == 0)
        {
            AppendLog("Patch library folder selection cancelled.");
            return;
        }

        string? selectedPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            AppendLog("Patch library folder selection failed: local path is unavailable.");
            return;
        }

        PatchLibraryPathBox.Text = selectedPath;
        AppendLog($"Patch library root selected: {selectedPath}");
        ValidatePatchLibrary();
        SaveCurrentProfile();
    }

    private void ValidateClientIfSelected()
    {
        if (!string.IsNullOrWhiteSpace(ClientPathBox.Text))
            ValidateClient();
    }

    private void ValidateClient()
    {
        try
        {
            ClientInstall clientInstall = ClientInstall.FromPath(ClientPathBox.Text ?? "");
            ClientInstallReport report = clientInstall.Inspect();

            ClientVersionStatus.Text = $"{report.State}: {report.Version.DisplayText}";
            ExecutableStatus.Text = FormatExecutableStatus(report);
            StaticActorsStatus.Text = report.HasStaticActors ? clientInstall.StaticActorsSourcePath : "Not found";
            HomeClientStatus.Text = report.IsLaunchReady
                ? $"Client: ready ({report.Version.GameVersion})"
                : $"Client: {report.State}";
            HomeClientVersionStatus.Text = $"Version: {report.Version.DisplayText}";
            UpdateLaunchButtonState(report.IsLaunchReady);
            HomeLocateClientButton.IsVisible = false;

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
            HomeClientStatus.Text = "Client: validation failed";
            HomeClientVersionStatus.Text = "Version: validation failed";
            UpdateLaunchButtonState(false);
            HomeLocateClientButton.IsVisible = true;
            AppendLog($"Client validation failed: {ex.Message}");
        }
        finally
        {
            UpdateHomeState();
        }
    }

    private async void DownloadPatches_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (patchCancellation is not null)
            return;

        patchCancellation = new CancellationTokenSource();
        ApplyPatchesButton.IsEnabled = false;
        DownloadPatchesButton.IsEnabled = false;
        CancelPatchButton.IsEnabled = true;
        PatchApplyProgressBar.Value = 0;
        HomeProgressBar.Value = 0;
        PatchApplyStatus.Text = "Preparing patch download...";

        try
        {
            CancellationToken cancellationToken = patchCancellation.Token;
            LauncherPatchManifest manifest = await ResolvePatchManifestAsync(cancellationToken);
            string patchRoot = ResolvePatchLibraryRoot();
            PatchLibraryPathBox.Text = patchRoot;

            Progress<PatchDownloadProgress> progress = new(update =>
            {
                PatchApplyStatus.Text = update.Message;
                if (update.TotalSizeBytes > 0)
                {
                    double percent = (double)update.TotalBytesDownloaded / update.TotalSizeBytes * 100;
                    PatchApplyProgressBar.Value = Math.Clamp(percent, 0, 100);
                    HomeProgressBar.Value = Math.Clamp(percent, 0, 100);
                }

                if (update.LogMessage)
                    AppendLog(update.Message);
            });

            PatchDownloadResult result = await PatchDownloadService.DownloadPatchLibraryAsync(
                manifest,
                patchRoot,
                httpClient,
                progress,
                cancellationToken);

            AppendLog($"Patch download complete: {result.DownloadedFileCount} downloaded, {result.ReusedFileCount} reused.");
            PatchApplyStatus.Text = "Patch download complete.";
            PatchApplyProgressBar.Value = 100;
            HomeProgressBar.Value = 100;
            ValidatePatchLibrary();
            SaveCurrentProfile();
        }
        catch (OperationCanceledException)
        {
            AppendLog("Patch download cancelled.");
            PatchApplyStatus.Text = "Patch download cancelled.";
        }
        catch (Exception ex)
        {
            AppendLog($"Patch download failed: {ex.Message}");
            PatchApplyStatus.Text = "Patch download failed.";
        }
        finally
        {
            ApplyPatchesButton.IsEnabled = true;
            DownloadPatchesButton.IsEnabled = true;
            CancelPatchButton.IsEnabled = false;
            patchCancellation.Dispose();
            patchCancellation = null;
        }
    }

    private async void ApplyPatches_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (patchCancellation is not null)
            return;

        patchCancellation = new CancellationTokenSource();
        ApplyPatchesButton.IsEnabled = false;
        DownloadPatchesButton.IsEnabled = false;
        CancelPatchButton.IsEnabled = true;
        PatchApplyProgressBar.Value = 0;
        PatchApplyProgressBar.IsIndeterminate = false;
        HomeProgressBar.Value = 0;
        PatchApplyStatus.Text = "Preparing patch apply...";

        try
        {
            CancellationToken cancellationToken = patchCancellation.Token;
            ClientInstall clientInstall = ClientInstall.FromPath(ClientPathBox.Text ?? "");
            ClientInstallReport clientReport = clientInstall.Inspect();
            if (clientReport.State == ClientInstallState.Ready123b)
            {
                AppendLog("Patch apply skipped: client already reports 1.23b.");
                PatchApplyStatus.Text = "Client already reports 1.23b.";
                return;
            }

            string patchLibraryPath = PatchLibraryPathBox.Text ?? "";
            PatchApplyStatus.Text = "Verifying patch library checksums...";
            PatchApplyProgressBar.IsIndeterminate = true;
            AppendLog("Patch verification started.");

            PatchLibraryReport patchReport = await Task.Run(
                () => LegacyPatchManifest.InspectLibrary(
                    patchLibraryPath,
                    PatchLibraryInspectionMode.Checksum),
                cancellationToken);

            PatchLibraryStatus.Text = patchReport.Summary;
            AppendLog($"Patch library: {patchReport.Summary}");
            AppendLog($"Patch base: {patchReport.PatchBasePath}");
            PatchApplyProgressBar.IsIndeterminate = false;
            PatchApplyProgressBar.Value = 0;

            if (!patchReport.IsPatchChainReady)
            {
                LogMissingEntries("Missing patch", patchReport.MissingPatchFiles);
                LogInvalidEntries(patchReport.InvalidPatchFiles);
                AppendLog("Patch apply blocked: patch library is not checksum-ready.");
                PatchApplyStatus.Text = "Patch library is not checksum-ready.";
                return;
            }

            Progress<PatchApplyProgress> progress = new(update =>
            {
                PatchApplyStatus.Text = update.Message;
                if (update.TotalBytes > 0)
                {
                    double percent = (double)update.BytesProcessed / update.TotalBytes * 100;
                    PatchApplyProgressBar.Value = Math.Clamp(percent, 0, 100);
                    HomeProgressBar.Value = Math.Clamp(percent, 0, 100);
                }

                if (update.LogMessage)
                    AppendLog(update.Message);
            });

            PatchApplyResult result = await Task.Run(() =>
                LegacyPatchApplier.ApplyPatchChain(clientInstall, patchReport, progress, cancellationToken),
                cancellationToken);

            AppendLog($"Patch apply complete: {result.AppliedPatchCount} patch files applied.");
            PatchApplyProgressBar.Value = 100;
            HomeProgressBar.Value = 100;
            PatchApplyStatus.Text = "Patch apply complete.";
            foreach (string message in result.Messages.Take(8))
                AppendLog(message);

            if (result.Messages.Count > 8)
                AppendLog($"Patch apply notes: {result.Messages.Count - 8} more");

            ClientInstallReport updatedReport = clientInstall.Inspect();
            ClientVersionStatus.Text = $"{updatedReport.State}: {updatedReport.Version.DisplayText}";
            ExecutableStatus.Text = FormatExecutableStatus(updatedReport);
            StaticActorsStatus.Text = updatedReport.HasStaticActors ? clientInstall.StaticActorsSourcePath : "Not found";
            HomeClientStatus.Text = updatedReport.IsLaunchReady
                ? $"Client: ready ({updatedReport.Version.GameVersion})"
                : $"Client: {updatedReport.State}";
            HomeClientVersionStatus.Text = $"Version: {updatedReport.Version.DisplayText}";
            UpdateLaunchButtonState(updatedReport.IsLaunchReady);
            HomeLocateClientButton.IsVisible = false;
            SaveCurrentProfile();
        }
        catch (OperationCanceledException)
        {
            AppendLog("Patch apply cancelled.");
            PatchApplyStatus.Text = "Patch apply cancelled.";
        }
        catch (Exception ex)
        {
            AppendLog($"Patch apply failed: {ex.Message}");
            PatchApplyStatus.Text = "Patch apply failed.";
        }
        finally
        {
            ApplyPatchesButton.IsEnabled = true;
            DownloadPatchesButton.IsEnabled = true;
            CancelPatchButton.IsEnabled = false;
            PatchApplyProgressBar.IsIndeterminate = false;
            patchCancellation.Dispose();
            patchCancellation = null;
            UpdateHomeState();
        }
    }

    private void CancelPatch_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        patchCancellation?.Cancel();
        PatchApplyStatus.Text = "Cancelling...";
        AppendLog("Patch cancel requested.");
    }

    private async void CreateAccount_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ShowCreateAccountDialogAsync();
    }

    private async Task ShowCreateAccountDialogAsync()
    {
        TextBox usernameBox = new() { PlaceholderText = "Username", Text = LoginUserBox.Text ?? "" };
        TextBox emailBox = new() { PlaceholderText = "Email" };
        TextBox passwordBox = new() { PlaceholderText = "Password", PasswordChar = '*' };
        TextBox confirmPasswordBox = new() { PlaceholderText = "Confirm password", PasswordChar = '*' };
        TextBlock statusText = new()
        {
            Text = "",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Avalonia.Media.Brushes.LightGray
        };
        Button createButton = new() { Content = "Create Account", HorizontalAlignment = HorizontalAlignment.Stretch };
        Button cancelButton = new() { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Stretch };

        Window dialog = new()
        {
            Title = "Create Account",
            Width = 420,
            Height = 360,
            MinWidth = 380,
            MinHeight = 340,
            Background = Background,
            Foreground = Foreground,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Grid
            {
                Margin = new Thickness(18),
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto"),
                RowSpacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Create Account",
                        FontSize = 20,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold
                    },
                    usernameBox,
                    emailBox,
                    passwordBox,
                    confirmPasswordBox,
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            statusText,
                            createButton,
                            cancelButton
                        }
                    }
                }
            }
        };

        Grid.SetRow(usernameBox, 1);
        Grid.SetRow(emailBox, 2);
        Grid.SetRow(passwordBox, 3);
        Grid.SetRow(confirmPasswordBox, 4);
        Grid.SetRow((Control)((Grid)dialog.Content!).Children[5], 5);

        cancelButton.Click += (_, _) => dialog.Close();
        createButton.Click += async (_, _) =>
        {
            createButton.IsEnabled = false;
            statusText.Text = "Creating account...";
            try
            {
                LauncherAuthResponse response = await CreateAccountAsync(
                    usernameBox.Text ?? "",
                    emailBox.Text ?? "",
                    passwordBox.Text ?? "",
                    confirmPasswordBox.Text ?? "");

                if (!response.Success || string.IsNullOrWhiteSpace(response.SessionId))
                {
                    statusText.Text = string.IsNullOrWhiteSpace(response.Message)
                        ? "Account creation failed."
                        : response.Message;
                    return;
                }

                currentSessionId = response.SessionId;
                currentSessionUsername = response.Username ?? usernameBox.Text ?? "";
                LoginUserBox.Text = currentSessionUsername;
                LoginPasswordBox.Text = passwordBox.Text ?? "";
                HomeLoginStatus.Text = $"Signed in as {currentSessionUsername}.";
                SaveCurrentProfile();
                AppendLog($"Account created and signed in: {currentSessionUsername}");
                dialog.Close();
            }
            catch (Exception ex)
            {
                statusText.Text = ex.Message;
                AppendLog($"Account creation failed: {ex.Message}");
            }
            finally
            {
                createButton.IsEnabled = true;
            }
        };

        await dialog.ShowDialog(this);
    }

    private async Task<LauncherAuthResponse> CreateAccountAsync(
        string username,
        string email,
        string password,
        string confirmPassword)
    {
        LauncherApiClient client = new(httpClient, LauncherServiceUrlBox.Text ?? "");
        LauncherAuthResponse? response = await client.CreateAccountAsync(
            new LauncherCreateAccountRequest(username.Trim(), password, confirmPassword, email.Trim()),
            launcherConfig?.AccountCreateUrl);

        return response ?? new LauncherAuthResponse(false, "Account service unavailable.", null, null);
    }

    private async Task<string> EnsureSessionAsync()
    {
        string username = (LoginUserBox.Text ?? "").Trim();
        string password = LoginPasswordBox.Text ?? "";

        if (!string.IsNullOrWhiteSpace(currentSessionId)
            && string.Equals(currentSessionUsername, username, StringComparison.Ordinal))
        {
            return currentSessionId;
        }

        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Username is required.");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Password is required.");

        HomeLoginStatus.Text = "Signing in...";
        LauncherApiClient client = new(httpClient, LauncherServiceUrlBox.Text ?? "");
        LauncherAuthResponse? response = await client.LoginAsync(
            new LauncherAuthRequest(username, password),
            launcherConfig?.LoginUrl);

        if (response is null)
            throw new InvalidOperationException("Login service unavailable.");
        if (!response.Success || string.IsNullOrWhiteSpace(response.SessionId))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? "Login failed." : response.Message);

        currentSessionId = response.SessionId;
        currentSessionUsername = response.Username ?? username;
        HomeLoginStatus.Text = $"Signed in as {currentSessionUsername}.";
        SaveCurrentProfile();
        return currentSessionId;
    }

    private void OpenSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 1;
    }

    private void ClearLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LaunchLogBox.Text = "";
    }

    private void ScanRuntime_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ScanRuntimeCandidates(true);
    }

    private async void RefreshRuntimeCatalog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RefreshRuntimeCatalogAsync();
    }

    private async void InstallRuntime_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (runtimeCancellation is not null)
            return;

        if (!platform.RequiresCompatibilityRuntime)
        {
            ManagedRuntimeStatus.Text = "Windows builds launch the client directly.";
            return;
        }

        if (selectedRuntimeArtifact is null)
            await RefreshRuntimeCatalogAsync();

        if (selectedRuntimeArtifact is null)
        {
            ManagedRuntimeStatus.Text = "No managed runtime package is available for this platform.";
            AppendLog("Runtime install blocked: catalog did not provide a runtime package.");
            return;
        }

        runtimeCancellation = new CancellationTokenSource();
        SetRuntimeBusy(true);
        RuntimeProgressBar.Value = 0;

        try
        {
            Progress<RuntimeDownloadProgress> progress = new(update =>
            {
                ManagedRuntimeStatus.Text = update.Message;
                if (update.TotalBytes > 0)
                {
                    double percent = (double)update.BytesDownloaded / update.TotalBytes * 100;
                    RuntimeProgressBar.Value = Math.Clamp(percent, 0, 100);
                }

                if (update.LogMessage)
                    AppendLog(update.Message);
            });

            RuntimeDownloadResult result = await RuntimeDownloadService.DownloadAndInstallAsync(
                selectedRuntimeArtifact,
                httpClient,
                progress,
                runtimeCancellation.Token);

            managedRuntimeInstall = result.Install;
            RuntimeProgressBar.Value = 100;
            ManagedRuntimeStatus.Text = $"Installed: {managedRuntimeInstall.Name} {managedRuntimeInstall.Version}";
            foreach (string message in result.Messages)
                AppendLog(message);
            SaveCurrentProfile();
        }
        catch (OperationCanceledException)
        {
            ManagedRuntimeStatus.Text = "Runtime install cancelled.";
            AppendLog("Runtime install cancelled.");
        }
        catch (Exception ex)
        {
            ManagedRuntimeStatus.Text = "Runtime install failed.";
            AppendLog($"Runtime install failed: {ex.Message}");
        }
        finally
        {
            runtimeCancellation.Dispose();
            runtimeCancellation = null;
            SetRuntimeBusy(false);
            UpdateRuntimeUiState();
        }
    }

    private async void ValidateRuntime_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!platform.RequiresCompatibilityRuntime)
        {
            ManagedRuntimeStatus.Text = "Windows builds launch the client directly.";
            return;
        }

        SetRuntimeBusy(true);
        RuntimeProgressBar.IsIndeterminate = true;

        try
        {
            WineRuntimeProfile runtimeProfile = ReadRuntimeProfile();
            RuntimeValidationResult result = await RuntimeValidator.ValidateAsync(
                runtimeProfile,
                RuntimeInstallStore.ManagedPrefixPath);

            ManagedRuntimeStatus.Text = result.IsReady
                ? $"Ready: {result.VersionText}"
                : result.Message;
            AppendLog($"Runtime validation: {result.Message}");
            AppendLog($"Runtime validation log: {result.LogPath}");
        }
        catch (Exception ex)
        {
            ManagedRuntimeStatus.Text = "Runtime validation failed.";
            AppendLog($"Runtime validation failed: {ex.Message}");
        }
        finally
        {
            RuntimeProgressBar.IsIndeterminate = false;
            SetRuntimeBusy(false);
            UpdateRuntimeUiState();
        }
    }

    private void ResetPrefix_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            RuntimeInstallStore.ResetManagedPrefix();
            ManagedRuntimeStatus.Text = managedRuntimeInstall is null
                ? "Managed prefix reset. Runtime is not installed."
                : $"Managed prefix reset for {managedRuntimeInstall.Name} {managedRuntimeInstall.Version}.";
            AppendLog($"Managed prefix reset: {RuntimeInstallStore.ManagedPrefixPath}");
        }
        catch (Exception ex)
        {
            ManagedRuntimeStatus.Text = "Managed prefix reset failed.";
            AppendLog($"Managed prefix reset failed: {ex.Message}");
        }
    }

    private void RuntimeMode_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isInitialized)
            return;

        UpdateRuntimeUiState();
        SaveCurrentProfile();
    }

    private async void LaunchGame_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (launchInProgress)
            return;

        SetLaunchInProgress("Preparing...", "Preparing launch...");
        try
        {
            SaveCurrentProfile();
            ClientInstall clientInstall = ClientInstall.FromPath(ClientPathBox.Text ?? "");
            ClientInstallReport report = clientInstall.Inspect();
            if (!report.IsLaunchReady)
            {
                AppendLog("Launch blocked: client is not ready.");
                HomeLoginStatus.Text = "Client is not ready to launch.";
                UpdateHomeState();
                return;
            }

            ServerProfile serverProfile = ReadServerProfile();
            WineRuntimeProfile runtimeProfile = ReadRuntimeProfile();
            Directory.CreateDirectory(Path.GetDirectoryName(RuntimeInstallStore.ServerProfilePath)!);
            ServerXmlWriter.Write(RuntimeInstallStore.ServerProfilePath, new[] { serverProfile });
            SetLaunchInProgress("Signing in...", "Signing in...");
            string sessionId = await EnsureSessionAsync();

            if (platform.RequiresCompatibilityRuntime)
            {
                SetLaunchInProgress("Checking runtime...", "Checking Wine runtime...");
                ManagedRuntimeStatus.Text = "Validating runtime before launch...";
                RuntimeValidationResult validation = await RuntimeValidator.ValidateAsync(
                    runtimeProfile,
                    RuntimeInstallStore.ManagedPrefixPath);
                AppendLog($"Runtime validation: {validation.Message}");
                AppendLog($"Runtime validation log: {validation.LogPath}");

                if (!validation.IsReady)
                {
                    ManagedRuntimeStatus.Text = validation.Message;
                    AppendLog("Launch blocked: runtime validation failed.");
                    HomeLoginStatus.Text = "Runtime validation failed.";
                    return;
                }
            }

            SetLaunchInProgress("Launching game...", "Launching game...");
            string helperPath = ClientLaunchHelperLocator.FindLaunchHelperRequired();
            LaunchPlan plan = LaunchPlan.CreateWithHelper(
                clientInstall,
                serverProfile,
                runtimeProfile,
                helperPath,
                sessionId,
                platform.RequiresCompatibilityRuntime);
            ProcessStartInfo startInfo = BuildProcessStartInfo(plan);
            RuntimeLaunchResult result = RuntimeLaunchDiagnostics.StartWithLogging(startInfo, plan.LogPath);
            AppendLog($"Launch started: pid {result.ProcessId}");
            AppendLog($"Launch log: {result.LogPath}");
            HomeLoginStatus.Text = "Launch sent to Wine. Watch for the game window.";
            HomeProgressBar.Value = 100;
        }
        catch (Exception ex)
        {
            AppendLog($"Launch failed: {ex.Message}");
            HomeLoginStatus.Text = FormatLaunchFailure(ex.Message);
        }
        finally
        {
            ClearLaunchInProgress();
        }
    }

    private ServerProfile ReadServerProfile()
    {
        ServerProfile profile = new(
            ServerNameBox.Text ?? "",
            ServerHostBox.Text ?? "",
            Convert.ToInt32(LobbyPortBox.Value ?? 54994),
            Convert.ToInt32(WorldPortBox.Value ?? 54992),
            1989,
            ResolveLauncherEndpoint(launcherConfig?.ClientLoginUrl, "../login/index.php"));

        profile.Validate();
        return profile;
    }

    private void SetLaunchInProgress(string buttonText, string statusText)
    {
        launchInProgress = true;
        PlayGameButton.Content = buttonText;
        PlayGameButton.IsEnabled = false;
        HomeProgressBar.Value = 0;
        HomeProgressBar.IsIndeterminate = true;
        HomeLoginStatus.Text = statusText;
    }

    private void ClearLaunchInProgress()
    {
        launchInProgress = false;
        HomeProgressBar.IsIndeterminate = false;
        UpdateHomeState();
    }

    private void UpdateLaunchButtonState(bool? clientReady = null)
    {
        if (launchInProgress)
        {
            PlayGameButton.IsEnabled = false;
            return;
        }

        PlayGameButton.Content = "Log In & Play";
        PlayGameButton.IsEnabled = clientReady ?? IsSelectedClientLaunchReady();
    }

    private bool IsSelectedClientLaunchReady()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ClientPathBox.Text))
                return false;

            return ClientInstall.FromPath(ClientPathBox.Text).Inspect().IsLaunchReady;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatLaunchFailure(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Launch failed.";

        if (message.Contains("username", StringComparison.OrdinalIgnoreCase)
            || message.Contains("password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Login", StringComparison.OrdinalIgnoreCase))
        {
            return $"Login failed: {message}";
        }

        return $"Launch failed: {message}";
    }

    private WineRuntimeProfile ReadRuntimeProfile()
    {
        if (platform.UsesNativeWindowsClient)
            return WineRuntimeProfile.NativeWindows();

        RuntimeSelectionMode mode = ReadRuntimeMode();
        if (mode == RuntimeSelectionMode.DetectedRuntime)
        {
            int index = DetectedRuntimeBox.SelectedIndex;
            if (index >= 0 && index < runtimeCandidates.Count)
                return NormalizeRuntimeProfile(RuntimeProfileResolver.CandidateToProfile(runtimeCandidates[index], RuntimeInstallStore.ManagedPrefixPath));
        }

        WineRuntimeProfile profile = RuntimeProfileResolver.Resolve(
            mode,
            managedRuntimeInstall,
            runtimeCandidates,
            ReadCustomRuntimeProfile(),
            RuntimeInstallStore.ManagedPrefixPath);
        return NormalizeRuntimeProfile(profile);
    }

    private static WineRuntimeProfile NormalizeRuntimeProfile(WineRuntimeProfile profile)
    {
        if (profile.Kind == WineRuntimeKind.WhiskyBottle
            && !string.IsNullOrWhiteSpace(profile.BottleName)
            && WhiskyRuntimeEnvironment.TryCreateWineProfile(
                profile.Command,
                profile.BottleName,
                out WineRuntimeProfile whiskyWineProfile,
                out _))
        {
            return whiskyWineProfile;
        }

        return profile;
    }

    private WineRuntimeProfile ReadCustomRuntimeProfile()
    {
        string name = RuntimeNameBox.Text ?? "Local Wine Runtime";
        string command = RuntimeCommandBox.Text ?? "wine";
        string value = RuntimeValueBox.Text ?? "";

        return CustomRuntimeKindBox.SelectedIndex switch
        {
            0 => WineRuntimeProfile.CrossOverBottle(name, string.IsNullOrWhiteSpace(value) ? "EchoGate" : value, command),
            1 => WineRuntimeProfile.WinePrefix(
                name,
                string.IsNullOrWhiteSpace(value) ? RuntimeInstallStore.ManagedPrefixPath : value,
                command),
            2 => WineRuntimeProfile.WhiskyBottle(name, string.IsNullOrWhiteSpace(value) ? "EchoGate" : value, command),
            _ => WineRuntimeProfile.Custom(name, command)
        };
    }

    private RuntimeSelectionMode ReadRuntimeMode()
    {
        if (platform.UsesNativeWindowsClient)
            return RuntimeSelectionMode.CustomRuntime;

        return RuntimeModeBox.SelectedIndex switch
        {
            1 => RuntimeSelectionMode.DetectedRuntime,
            2 => RuntimeSelectionMode.CustomRuntime,
            _ => RuntimeSelectionMode.AutomaticManaged
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
        AppendLog($"Patch base: {report.PatchBasePath}");
        AppendLog($"Metainfo base: {report.MetainfoBasePath}");

        LogMissingEntries("Missing patch", report.MissingPatchFiles);
        LogMissingEntries("Missing metainfo", report.MissingMetainfoFiles);
        LogInvalidEntries(report.InvalidPatchFiles);
    }

    private async Task RefreshLauncherServicesAsync()
    {
        string serviceUrl = LauncherServiceUrlBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            HeaderServiceStatus.Text = "Launcher services not configured";
            HomeServerStatus.Text = "Server: services not configured";
            return;
        }

        try
        {
            HeaderServiceStatus.Text = "Checking launcher services...";
            HomeServerStatus.Text = "Server: checking";
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            LauncherApiClient client = new(httpClient, serviceUrl);
            launcherConfig = await client.GetConfigAsync(timeout.Token);
            if (launcherConfig is not null)
            {
                HeaderServiceStatus.Text = $"{launcherConfig.ServerName}: service online";
                if (!string.IsNullOrWhiteSpace(launcherConfig.PatchBaseUrl))
                    PatchBaseUrlBox.Text = launcherConfig.PatchBaseUrl;
            }

            LauncherStatus? status = await client.GetStatusAsync(timeout.Token);
            if (status is not null)
            {
                HomeServerStatus.Text = $"Server: {status.State} - {status.Message}";
            }
            else
            {
                HomeServerStatus.Text = launcherConfig is null
                    ? "Server: service unavailable"
                    : "Server: status unavailable";
            }

            LauncherNewsFeed? news = await client.GetNewsAsync(timeout.Token);
            await ApplyNewsFeedAsync(news, timeout.Token);
            await RefreshRuntimeCatalogAsync(timeout.Token);
            SaveCurrentProfile();
        }
        catch (Exception ex)
        {
            HeaderServiceStatus.Text = "Launcher services unavailable";
            HomeServerStatus.Text = "Server: service unavailable";
            AppendLog($"Launcher service refresh failed: {ex.Message}");
            await ApplyNewsFeedAsync(null);
            RuntimeCatalogStatus.Text = "Runtime catalog unavailable.";
        }
    }

    private async Task RefreshRuntimeCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (!platform.RequiresCompatibilityRuntime)
        {
            RuntimeCatalogStatus.Text = "Windows builds launch the client directly.";
            ManagedRuntimeStatus.Text = "No compatibility runtime required.";
            return;
        }

        string serviceUrl = LauncherServiceUrlBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            RuntimeCatalogStatus.Text = "Launcher service is not configured.";
            RefreshInstalledManagedRuntimeStatus();
            return;
        }

        try
        {
            RuntimeCatalogStatus.Text = "Refreshing runtime catalog...";
            LauncherApiClient client = new(httpClient, serviceUrl);
            runtimeCatalog = await client.GetRuntimeCatalogAsync(
                platform.RuntimeIdentifier,
                launcherConfig?.RuntimeCatalogUrl,
                cancellationToken);
            selectedRuntimeArtifact = runtimeCatalog?.SelectDefault();

            if (selectedRuntimeArtifact is null)
            {
                RuntimeCatalogStatus.Text = $"No managed runtime package is available for {platform.RuntimeIdentifier}.";
                managedRuntimeInstall = null;
                ManagedRuntimeStatus.Text = "Detected or custom runtimes remain available.";
            }
            else
            {
                RuntimeCatalogStatus.Text = $"Available: {FormatRuntimeArtifact(selectedRuntimeArtifact)}";
                RefreshInstalledManagedRuntimeStatus();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RuntimeCatalogStatus.Text = "Runtime catalog unavailable.";
            AppendLog($"Runtime catalog refresh failed: {ex.Message}");
            RefreshInstalledManagedRuntimeStatus();
        }
        finally
        {
            UpdateRuntimeUiState();
        }
    }

    private async Task ApplyNewsFeedAsync(
        LauncherNewsFeed? feed,
        CancellationToken cancellationToken = default)
    {
        LauncherNewsItem? first = feed?.Items.OrderByDescending(item => item.PublishedAt).FirstOrDefault();
        if (first is null)
        {
            NewsTitleText.Text = "Awaiting launcher news service";
            NewsSummaryText.Text = "News from Demi Dev Unit will appear here when the launcher service is available.";
            NewsDateText.Text = "";
            ClearNewsBanner();
            return;
        }

        NewsTitleText.Text = first.Title;
        NewsSummaryText.Text = first.Summary;
        NewsDateText.Text = first.PublishedAt.ToLocalTime().ToString("MMM d, yyyy");
        if (string.IsNullOrWhiteSpace(first.BannerUrl)
            || !Uri.TryCreate(first.BannerUrl, UriKind.Absolute, out Uri? bannerUri))
        {
            ClearNewsBanner();
            return;
        }

        try
        {
            await using Stream stream = await httpClient.GetStreamAsync(bannerUri, cancellationToken);
            NewsBannerImage.Source = new Bitmap(stream);
        }
        catch (Exception ex)
        {
            AppendLog($"News banner unavailable: {ex.Message}");
            ClearNewsBanner();
        }
    }

    private void ClearNewsBanner()
    {
        NewsBannerImage.Source = null;
    }

    private async Task<LauncherPatchManifest> ResolvePatchManifestAsync(CancellationToken cancellationToken)
    {
        string serviceUrl = LauncherServiceUrlBox.Text ?? "";
        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            LauncherApiClient client = new(httpClient, serviceUrl);
            LauncherPatchManifest? manifest = await client.GetPatchManifestAsync(cancellationToken);
            if (manifest is not null)
                return manifest;
        }

        string patchBaseUrl = PatchBaseUrlBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(patchBaseUrl))
            throw new InvalidOperationException("Patch base URL is not configured.");

        return LauncherPatchManifest.FromKnownPatchChain(patchBaseUrl);
    }

    private string ResolvePatchLibraryRoot()
    {
        string selected = PatchLibraryPathBox.Text ?? "";
        if (!string.IsNullOrWhiteSpace(selected))
            return selected;

        string? profileDir = Path.GetDirectoryName(ProfileStore.DefaultProfilePath);
        return Path.Combine(profileDir ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PatchLibrary");
    }

    private void ConfigurePlatformUi()
    {
        RuntimePlatformStatus.Text = $"{platform.OperatingSystem} ({platform.RuntimeIdentifier})";
        RuntimeTab.IsVisible = platform.RequiresCompatibilityRuntime;
        if (platform.UsesNativeWindowsClient)
            RuntimePlatformStatus.Text = "Windows native client launch";
        UpdateRuntimeUiState();
    }

    private void LoadSavedProfile()
    {
        try
        {
            LauncherProfile profile = ProfileStore.LoadDefaultOrCreate();
            ClientPathBox.Text = profile.ClientRootPath ?? "";
            PatchLibraryPathBox.Text = profile.PatchLibraryRootPath ?? "";
            LauncherServiceUrlBox.Text = string.IsNullOrWhiteSpace(profile.LauncherServiceUrl)
                ? LauncherProfile.LocalDefault().LauncherServiceUrl
                : profile.LauncherServiceUrl;
            PatchBaseUrlBox.Text = profile.PatchBaseUrl ?? "";
            ServerNameBox.Text = profile.ServerProfile.Name;
            ServerHostBox.Text = profile.ServerProfile.Host;
            LobbyPortBox.Value = profile.ServerProfile.LobbyPort;
            WorldPortBox.Value = profile.ServerProfile.WorldPort;
            RememberUsernameBox.IsChecked = profile.RememberUsername;
            LoginUserBox.Text = profile.RememberUsername ? profile.SavedUsername : "";
            ApplyRuntimeMode(profile.RuntimeMode);
            ApplyRuntimeProfile(profile.RuntimeProfile);
        }
        catch (Exception ex)
        {
            AppendLog($"Profile load failed: {ex.Message}");
            ApplyRuntimeMode(LauncherProfile.LocalDefault().RuntimeMode);
            ApplyRuntimeProfile(LauncherProfile.LocalDefault().RuntimeProfile);
        }
    }

    private void ApplyRuntimeMode(RuntimeSelectionMode runtimeMode)
    {
        RuntimeModeBox.SelectedIndex = runtimeMode switch
        {
            RuntimeSelectionMode.DetectedRuntime => 1,
            RuntimeSelectionMode.CustomRuntime => 2,
            _ => 0
        };
        UpdateRuntimeUiState();
    }

    private void ApplyRuntimeProfile(WineRuntimeProfile runtimeProfile)
    {
        RuntimeNameBox.Text = runtimeProfile.Name;
        RuntimeCommandBox.Text = string.IsNullOrWhiteSpace(runtimeProfile.Command) ? "wine" : runtimeProfile.Command;
        RuntimeValueBox.Text = runtimeProfile.BottleName ?? runtimeProfile.PrefixPath ?? "";
        CustomRuntimeKindBox.SelectedIndex = runtimeProfile.Kind switch
        {
            WineRuntimeKind.CrossOverBottle => 0,
            WineRuntimeKind.WinePrefix => 1,
            WineRuntimeKind.WhiskyBottle => 2,
            WineRuntimeKind.CustomCommand => 3,
            _ => 1
        };
    }

    private void SaveCurrentProfile()
    {
        try
        {
            LauncherProfile profile = new(
                ClientPathBox.Text ?? "",
                PatchLibraryPathBox.Text ?? "",
                LauncherServiceUrlBox.Text ?? "",
                PatchBaseUrlBox.Text ?? "",
                ReadServerProfile(),
                ReadRuntimeProfile(),
                ReadRuntimeMode(),
                RememberUsernameBox.IsChecked == true ? (LoginUserBox.Text ?? "").Trim() : "",
                RememberUsernameBox.IsChecked == true);
            ProfileStore.SaveDefault(profile);
        }
        catch (Exception ex)
        {
            AppendLog($"Profile save failed: {ex.Message}");
        }
    }

    private void ScanRuntimeCandidates(bool appendLog)
    {
        if (!platform.RequiresCompatibilityRuntime)
        {
            RuntimeCandidatesBox.Text = "Windows builds launch the client directly.";
            return;
        }

        runtimeCandidates = RuntimeDiscovery.Discover();
        RuntimeCandidatesBox.Text = runtimeCandidates.Count == 0
            ? "No known runtime tools detected."
            : string.Join(Environment.NewLine, runtimeCandidates.Select(FormatRuntimeCandidate));
        DetectedRuntimeBox.ItemsSource = runtimeCandidates.Select(FormatRuntimeCandidate).ToArray();
        if (runtimeCandidates.Count > 0 && DetectedRuntimeBox.SelectedIndex < 0)
            DetectedRuntimeBox.SelectedIndex = 0;

        if (appendLog)
            AppendLog($"Runtime scan found {runtimeCandidates.Count} candidate(s).");

        UpdateRuntimeUiState();
    }

    private void UpdateHomeState()
    {
        if (string.IsNullOrWhiteSpace(ClientPathBox.Text))
        {
            HomeClientStatus.Text = "Client: not selected";
            HomeClientVersionStatus.Text = "Version: not checked";
            UpdateLaunchButtonState(false);
            HomeLocateClientButton.IsVisible = true;
        }
        else
        {
            HomeLocateClientButton.IsVisible = false;
            UpdateLaunchButtonState();
        }

    }

    private string ResolveLauncherEndpoint(string? configuredPath, string fallbackPath)
    {
        string endpoint = string.IsNullOrWhiteSpace(configuredPath)
            ? fallbackPath
            : configuredPath;

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? absoluteUri))
            return absoluteUri.ToString();

        string serviceUrl = LauncherServiceUrlBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(serviceUrl))
            return endpoint;

        Uri baseUri = new(serviceUrl.EndsWith('/') ? serviceUrl : $"{serviceUrl}/", UriKind.Absolute);
        return new Uri(baseUri, endpoint).ToString();
    }

    private ProcessStartInfo BuildProcessStartInfo(LaunchPlan plan)
    {
        ProcessStartInfo info;
        if (platform.UsesNativeWindowsClient || plan.RuntimeProfile.Kind == WineRuntimeKind.NativeWindows)
        {
            info = new ProcessStartInfo
            {
                FileName = plan.WindowsExecutablePath,
                Arguments = plan.Arguments,
                WorkingDirectory = plan.ClientInstall.RootPath,
                UseShellExecute = false
            };
        }
        else
        {
            info = new ProcessStartInfo
            {
                FileName = plan.RuntimeProfile.Command,
                Arguments = plan.Arguments,
                WorkingDirectory = plan.ClientInstall.RootPath,
                UseShellExecute = false
            };
        }

        foreach (KeyValuePair<string, string> pair in plan.Environment)
            info.Environment[pair.Key] = pair.Value;
        info.Environment["ECHO_GATE_SERVER_XML"] = RuntimeInstallStore.ServerProfilePath;
        info.Environment["ECHO_GATE_LAUNCH_LOG"] = plan.LogPath;

        return info;
    }

    private void RefreshInstalledManagedRuntimeStatus()
    {
        if (!platform.RequiresCompatibilityRuntime)
            return;

        if (selectedRuntimeArtifact is null)
        {
            managedRuntimeInstall = null;
            ManagedRuntimeStatus.Text = "Managed runtime catalog not loaded.";
            UpdateRuntimeUiState();
            return;
        }

        managedRuntimeInstall = RuntimeInstallStore.FindInstalled(selectedRuntimeArtifact);
        ManagedRuntimeStatus.Text = managedRuntimeInstall is null
            ? $"Not installed: {selectedRuntimeArtifact.Name} {selectedRuntimeArtifact.Version}"
            : $"Installed: {managedRuntimeInstall.Name} {managedRuntimeInstall.Version}";
        UpdateRuntimeUiState();
    }

    private void SetRuntimeBusy(bool isBusy)
    {
        InstallRuntimeButton.IsEnabled = !isBusy && selectedRuntimeArtifact is not null;
        ValidateRuntimeButton.IsEnabled = !isBusy;
        ResetPrefixButton.IsEnabled = !isBusy;
        RuntimeModeBox.IsEnabled = !isBusy;
        DetectedRuntimeBox.IsEnabled = !isBusy;
        CustomRuntimeKindBox.IsEnabled = !isBusy;
        RuntimeNameBox.IsEnabled = !isBusy;
        RuntimeCommandBox.IsEnabled = !isBusy;
        RuntimeValueBox.IsEnabled = !isBusy;
    }

    private void UpdateRuntimeUiState()
    {
        if (!platform.RequiresCompatibilityRuntime)
            return;

        bool isBusy = runtimeCancellation is not null;
        RuntimeSelectionMode mode = ReadRuntimeMode();
        bool automatic = mode == RuntimeSelectionMode.AutomaticManaged;
        bool detected = mode == RuntimeSelectionMode.DetectedRuntime;
        bool custom = mode == RuntimeSelectionMode.CustomRuntime;

        InstallRuntimeButton.IsEnabled = !isBusy && automatic && selectedRuntimeArtifact is not null;
        ValidateRuntimeButton.IsEnabled = !isBusy;
        ResetPrefixButton.IsEnabled = !isBusy;
        DetectedRuntimeBox.IsEnabled = !isBusy && detected;
        CustomRuntimeKindBox.IsEnabled = !isBusy && custom;
        RuntimeNameBox.IsEnabled = !isBusy && custom;
        RuntimeCommandBox.IsEnabled = !isBusy && custom;
        RuntimeValueBox.IsEnabled = !isBusy && custom;

        if (automatic && selectedRuntimeArtifact is null)
            RuntimeCatalogStatus.Text = "Automatic managed mode is waiting for a runtime catalog package.";
    }

    private static string FormatRuntimeArtifact(RuntimeArtifact artifact)
    {
        return $"{artifact.Name} {artifact.Version} ({artifact.PlatformRid}, {artifact.SizeBytes} bytes)";
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

    private static bool IsKnownClientExecutable(string fileName)
    {
        return string.Equals(fileName, "ffxivboot.exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "ffxivgame.exe", StringComparison.OrdinalIgnoreCase);
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

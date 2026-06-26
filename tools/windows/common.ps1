$ErrorActionPreference = "Stop"

# Keep this pinned to a checksummed Windows PHP x64 NTS zip from https://windows.php.net/download/.
$script:MeteorManagedPhp = [pscustomobject]@{
    Version = "8.5.7"
    DirectoryName = "php-8.5.7-nts-Win32-vs17-x64"
    Url = "https://downloads.php.net/~windows/releases/archives/php-8.5.7-nts-Win32-vs17-x64.zip"
    Sha256 = "2ff43fea9a243085493b48c7c47152c0678cff0b05c61a3b4f4b43ba22de212c"
}

$script:AetherManagedNuGet = [pscustomobject]@{
    DirectoryName = "NuGet"
    FileName = "nuget.exe"
    Url = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
}

$script:AetherVcRedistX64 = [pscustomobject]@{
    DirectoryName = "VcRedist"
    FileName = "vc_redist.x64.exe"
    Url = "https://aka.ms/vc14/vc_redist.x64.exe"
    MinimumVersion = [version]"14.44.0.0"
}

function Get-MeteorRoot {
    $scriptDir = Split-Path -Parent $PSScriptRoot
    return (Resolve-Path (Join-Path $scriptDir "..")).Path
}

function Import-MeteorEnv {
    param([string]$RootDir = (Get-MeteorRoot))

    foreach ($fileName in @(".env.defaults", ".env.local")) {
        $path = Join-Path $RootDir $fileName
        if (-not (Test-Path -LiteralPath $path)) {
            continue
        }

        foreach ($line in Get-Content -LiteralPath $path) {
            $trimmed = $line.Trim()
            if ($trimmed -eq "" -or $trimmed.StartsWith("#") -or $trimmed -notmatch "=") {
                continue
            }

            $parts = $trimmed.Split("=", 2)
            $name = $parts[0].Trim()
            $value = $parts[1].Trim()
            if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
                $value = $value.Substring(1, $value.Length - 2)
            }

            if ($name -match "^[A-Za-z_][A-Za-z0-9_]*$") {
                [Environment]::SetEnvironmentVariable($name, $value, "Process")
            }
        }
    }
}

function Get-EnvValue {
    param(
        [string]$Name,
        [string]$Default = ""
    )

    $value = [Environment]::GetEnvironmentVariable($Name, "Process")
    if ([string]::IsNullOrEmpty($value)) {
        return $Default
    }

    return $value
}

function Test-WindowsAdministrator {
    try {
        if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
            return $false
        }

        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = [Security.Principal.WindowsPrincipal]::new($identity)
        return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    } catch {
        return $false
    }
}

function Start-ElevatedWindowsScript {
    param(
        [Parameter(Mandatory = $true)][string]$ScriptPath,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory = (Get-MeteorRoot)
    )

    $shell = Find-OptionalCommand -Names @("pwsh.exe", "powershell.exe")
    if ($null -eq $shell) {
        $shell = "powershell.exe"
    }

    $processArguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $ScriptPath) + $Arguments
    Start-Process -FilePath $shell -ArgumentList (Join-ProcessArguments $processArguments) -WorkingDirectory $WorkingDirectory -Verb RunAs | Out-Null
}

function Get-EchoGateDataRoot {
    $configured = Get-EnvValue "ECHO_GATE_DATA_ROOT"
    if ($configured -ne "") {
        return $configured
    }

    $appData = [Environment]::GetFolderPath("ApplicationData")
    if ($appData -eq "") {
        $appData = [Environment]::GetFolderPath("UserProfile")
    }

    return (Join-Path $appData "Demi Dev Unit\Echo Gate")
}

function Get-EchoGateToolsRoot {
    return (Join-Path (Get-EchoGateDataRoot) "Tools")
}

function Get-EchoGateSetupStatePath {
    return (Join-Path (Get-EchoGateDataRoot) "setup-state.json")
}

function Get-WindowsToolsLogRoot {
    return (Join-Path $PSScriptRoot "logs")
}

function Start-WindowsToolLog {
    param([string]$Name = "windows-tool")

    if ($env:AETHER_WINDOWS_TOOL_LOG_ACTIVE -eq "1") {
        return $null
    }

    try {
        $logRoot = Get-WindowsToolsLogRoot
        New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
        $safeName = ($Name -replace "[^A-Za-z0-9_.-]", "-").Trim("-")
        if ([string]::IsNullOrWhiteSpace($safeName)) {
            $safeName = "windows-tool"
        }
        $logPath = Join-Path $logRoot ("{0}-{1:yyyyMMdd-HHmmss}.log" -f $safeName, (Get-Date))
        Start-Transcript -LiteralPath $logPath -Append | Out-Null
        $env:AETHER_WINDOWS_TOOL_LOG_ACTIVE = "1"
        Write-Host "Tool log: $logPath"
        return $logPath
    } catch {
        Write-Warning "Could not start Windows tool transcript: $($_.Exception.Message)"
        return $null
    }
}

function Stop-WindowsToolLog {
    param([string]$Path = "")

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    try {
        Stop-Transcript | Out-Null
    } catch {
    }
    [Environment]::SetEnvironmentVariable("AETHER_WINDOWS_TOOL_LOG_ACTIVE", $null, "Process")
}

function Write-EchoGateSetupState {
    param([Parameter(Mandatory = $true)]$State)

    $path = Get-EchoGateSetupStatePath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path) | Out-Null
    $State | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}

function Read-EchoGateSetupState {
    $path = Get-EchoGateSetupStatePath
    if (-not (Test-Path -LiteralPath $path)) {
        return $null
    }

    return (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json)
}

function Find-RequiredCommand {
    param(
        [string[]]$Names,
        [string]$FriendlyName
    )

    foreach ($name in $Names) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($null -ne $command) {
            return $command.Source
        }
    }

    throw "$FriendlyName was not found on PATH. Install it, then open a new PowerShell window."
}

function Find-OptionalCommand {
    param([string[]]$Names)

    foreach ($name in $Names) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($null -ne $command) {
            return $command.Source
        }
    }

    return $null
}

function Resolve-CommandOrExistingFile {
    param([string]$Candidate)

    if ([string]::IsNullOrWhiteSpace($Candidate)) {
        return $null
    }

    $command = Get-Command $Candidate -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    if (Test-Path -LiteralPath $Candidate -PathType Leaf) {
        return (Resolve-Path -LiteralPath $Candidate).Path
    }

    return $null
}

function Get-ExecutablePathFromCommandLine {
    param([string]$CommandLine)

    if ([string]::IsNullOrWhiteSpace($CommandLine)) {
        return $null
    }

    $trimmed = $CommandLine.Trim()
    if ($trimmed.StartsWith('"') -and $trimmed -match '^"([^"]+)"') {
        return $matches[1]
    }

    $first = $trimmed.Split(" ", 2)[0]
    if ($first.StartsWith("\??\")) {
        return $first.Substring(4)
    }

    return $first
}

function Find-MySqlClientNearServer {
    param([string]$ServerExecutable)

    $serverPath = Resolve-CommandOrExistingFile -Candidate $ServerExecutable
    if ($null -eq $serverPath) {
        return $null
    }

    $binDir = Split-Path -Parent $serverPath
    foreach ($name in @("mariadb.exe", "mysql.exe")) {
        $candidate = Join-Path $binDir $name
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return $null
}

function Find-MySqlClientInDirectory {
    param([string]$Directory)

    if ([string]::IsNullOrWhiteSpace($Directory)) {
        return $null
    }

    $directories = @($Directory)
    $binDir = Join-Path $Directory "bin"
    if (Test-Path -LiteralPath $binDir -PathType Container) {
        $directories = @($binDir) + $directories
    }

    foreach ($dir in $directories) {
        foreach ($name in @("mariadb.exe", "mysql.exe")) {
            $candidate = Join-Path $dir $name
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                return (Resolve-Path -LiteralPath $candidate).Path
            }
        }
    }

    return $null
}

function Find-MySqlCommandFromRegistry {
    $registryPaths = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\mariadb.exe",
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\mysql.exe",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\mariadb.exe",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\mysql.exe",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\mariadb.exe",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\mysql.exe"
    )

    foreach ($path in $registryPaths) {
        try {
            $item = Get-Item -LiteralPath $path -ErrorAction SilentlyContinue
            if ($null -eq $item) {
                continue
            }

            $value = $item.GetValue("")
            $resolved = Resolve-CommandOrExistingFile -Candidate $value
            if ($null -ne $resolved) {
                return $resolved
            }
        } catch {
            continue
        }
    }

    return $null
}

function Find-MySqlCommandFromUninstallRegistry {
    if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
        return $null
    }

    $registryRoots = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    )

    foreach ($root in $registryRoots) {
        try {
            $items = @(Get-ChildItem -LiteralPath $root -ErrorAction SilentlyContinue)
        } catch {
            continue
        }

        foreach ($item in $items) {
            try {
                $props = Get-ItemProperty -LiteralPath $item.PSPath -ErrorAction Stop
            } catch {
                continue
            }

            $displayName = [string]$props.DisplayName
            if ($displayName -notmatch "(?i)(mariadb|mysql)") {
                continue
            }

            $locations = @($props.InstallLocation, $props.InstallSource)
            $innoAppPath = $props.PSObject.Properties["Inno Setup: App Path"]
            if ($null -ne $innoAppPath) {
                $locations += $innoAppPath.Value
            }

            foreach ($location in $locations) {
                $clientPath = Find-MySqlClientInDirectory -Directory $location
                if ($null -ne $clientPath) {
                    return $clientPath
                }
            }

            $uninstallPath = Get-ExecutablePathFromCommandLine -CommandLine $props.UninstallString
            if ($null -ne $uninstallPath) {
                $clientPath = Find-MySqlClientNearServer -ServerExecutable $uninstallPath
                if ($null -ne $clientPath) {
                    return $clientPath
                }
            }
        }
    }

    return $null
}

function Find-MySqlCommandFromService {
    if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
        return $null
    }

    try {
        $services = @(Get-CimInstance -ClassName Win32_Service -Filter "Name LIKE '%MariaDB%' OR DisplayName LIKE '%MariaDB%' OR Name LIKE '%MySQL%' OR DisplayName LIKE '%MySQL%'" -ErrorAction Stop)
    } catch {
        return $null
    }

    foreach ($service in $services) {
        $serverPath = Get-ExecutablePathFromCommandLine -CommandLine $service.PathName
        $clientPath = Find-MySqlClientNearServer -ServerExecutable $serverPath
        if ($null -ne $clientPath) {
            return $clientPath
        }
    }

    return $null
}

function Find-MySqlCommandFromKnownDirectories {
    $roots = @()
    if ($env:ProgramFiles) { $roots += $env:ProgramFiles }
    if (${env:ProgramFiles(x86)}) { $roots += ${env:ProgramFiles(x86)} }
    if ($env:ProgramW6432) { $roots += $env:ProgramW6432 }
    if ($env:ProgramData) { $roots += $env:ProgramData }
    if ($env:LOCALAPPDATA) { $roots += (Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages") }
    if ($env:USERPROFILE) {
        $roots += (Join-Path $env:USERPROFILE "scoop\apps")
    }
    if ($env:SCOOP) { $roots += (Join-Path $env:SCOOP "apps") }
    $roots += "C:\"

    $patterns = @()
    foreach ($root in ($roots | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)) {
        $patterns += (Join-Path $root "MariaDB*\bin\mariadb.exe")
        $patterns += (Join-Path $root "MariaDB*\bin\mysql.exe")
        $patterns += (Join-Path $root "MariaDB Server*\bin\mariadb.exe")
        $patterns += (Join-Path $root "MariaDB Server*\bin\mysql.exe")
        $patterns += (Join-Path $root "MySQL\MySQL Server *\bin\mysql.exe")
        $patterns += (Join-Path $root "MySQL Server *\bin\mysql.exe")
        $patterns += (Join-Path $root "mysql*\bin\mysql.exe")
        $patterns += (Join-Path $root "MariaDB.Server_*\MariaDB*\bin\mariadb.exe")
        $patterns += (Join-Path $root "MariaDB.Server_*\MariaDB*\bin\mysql.exe")
        $patterns += (Join-Path $root "mariadb\current\bin\mariadb.exe")
        $patterns += (Join-Path $root "mariadb\current\bin\mysql.exe")
        $patterns += (Join-Path $root "mysql\current\bin\mysql.exe")
        $patterns += (Join-Path $root "chocolatey\lib\mariadb\tools\*\bin\mariadb.exe")
        $patterns += (Join-Path $root "chocolatey\lib\mysql\tools\*\bin\mysql.exe")
    }

    foreach ($pattern in ($patterns | Select-Object -Unique)) {
        $matches = @(Get-ChildItem -Path $pattern -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc, FullName -Descending)
        if ($matches.Count -gt 0) {
            return $matches[0].FullName
        }
    }

    foreach ($root in ($roots | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -Unique)) {
        foreach ($filter in @("MariaDB*", "MySQL*")) {
            $directories = @(Get-ChildItem -LiteralPath $root -Directory -Filter $filter -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc, FullName -Descending)
            foreach ($directory in $directories) {
                $clientPath = Find-MySqlClientInDirectory -Directory $directory.FullName
                if ($null -ne $clientPath) {
                    return $clientPath
                }
            }
        }
    }

    return $null
}

function Find-MySqlCommand {
    $configured = Get-EnvValue "MYSQL_BIN"
    $resolved = Resolve-CommandOrExistingFile -Candidate $configured
    if ($null -ne $resolved) {
        return $resolved
    }

    $command = Find-OptionalCommand -Names @("mariadb.exe", "mysql.exe", "mariadb", "mysql")
    if ($null -ne $command) {
        return $command
    }

    $registryCommand = Find-MySqlCommandFromRegistry
    if ($null -ne $registryCommand) {
        return $registryCommand
    }

    $serviceCommand = Find-MySqlCommandFromService
    if ($null -ne $serviceCommand) {
        return $serviceCommand
    }

    $uninstallCommand = Find-MySqlCommandFromUninstallRegistry
    if ($null -ne $uninstallCommand) {
        return $uninstallCommand
    }

    $knownDirectoryCommand = Find-MySqlCommandFromKnownDirectories
    if ($null -ne $knownDirectoryCommand) {
        return $knownDirectoryCommand
    }

    return $null
}

function Find-PhpCommand {
    $configured = Get-EnvValue "PHP_BIN"
    if (-not [string]::IsNullOrEmpty($configured)) {
        $command = Get-Command $configured -ErrorAction SilentlyContinue
        if ($null -ne $command) {
            return $command.Source
        }
        if (Test-Path -LiteralPath $configured) {
            return (Resolve-Path -LiteralPath $configured).Path
        }
    }

    $managedPhp = Join-Path (Join-Path (Get-EchoGateToolsRoot) $script:MeteorManagedPhp.DirectoryName) "php.exe"
    if (Test-Path -LiteralPath $managedPhp) {
        return (Resolve-Path -LiteralPath $managedPhp).Path
    }

    $command = Find-OptionalCommand -Names @("php.exe", "php")
    if ($null -ne $command) {
        return $command
    }

    $patterns = @()
    if ($env:LOCALAPPDATA) {
        $patterns += (Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages\PHP.PHP.*\php.exe")
    }
    if ($env:ProgramFiles) {
        $patterns += (Join-Path $env:ProgramFiles "PHP*\php.exe")
        $patterns += (Join-Path $env:ProgramFiles "PHP\*\php.exe")
    }
    if (${env:ProgramFiles(x86)}) {
        $patterns += (Join-Path ${env:ProgramFiles(x86)} "PHP*\php.exe")
        $patterns += (Join-Path ${env:ProgramFiles(x86)} "PHP\*\php.exe")
    }

    foreach ($pattern in ($patterns | Select-Object -Unique)) {
        $matches = @(Get-ChildItem -Path $pattern -File -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
        if ($matches.Count -gt 0) {
            return $matches[0].FullName
        }
    }

    return $null
}

function Find-MsBuildCommand {
    $command = Find-OptionalCommand -Names @("msbuild.exe", "msbuild")
    if ($null -ne $command) {
        return $command
    }

    $vswhereCandidates = @()
    if (${env:ProgramFiles(x86)}) {
        $vswhereCandidates += (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe")
    }
    if ($env:ProgramFiles) {
        $vswhereCandidates += (Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe")
    }

    foreach ($vswhere in $vswhereCandidates) {
        if (-not (Test-Path -LiteralPath $vswhere)) {
            continue
        }

        $matches = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null
        foreach ($match in $matches) {
            if (Test-Path -LiteralPath $match) {
                return $match
            }
        }
    }

    return $null
}

function Get-ManagedNuGetPath {
    return (Join-Path (Join-Path (Get-EchoGateToolsRoot) $script:AetherManagedNuGet.DirectoryName) $script:AetherManagedNuGet.FileName)
}

function Find-NuGetCommand {
    foreach ($envName in @("NUGET_EXE", "NUGET_BIN")) {
        $configured = Get-EnvValue $envName
        $resolved = Resolve-CommandOrExistingFile -Candidate $configured
        if ($null -ne $resolved) {
            return $resolved
        }
    }

    $managedNuGet = Get-ManagedNuGetPath
    if (Test-Path -LiteralPath $managedNuGet -PathType Leaf) {
        return (Resolve-Path -LiteralPath $managedNuGet).Path
    }

    return (Find-OptionalCommand -Names @("nuget.exe", "nuget"))
}

function Install-ManagedNuGet {
    param([switch]$Force)

    $nuget = Get-ManagedNuGetPath
    if ((Test-Path -LiteralPath $nuget -PathType Leaf) -and -not $Force) {
        return (Resolve-Path -LiteralPath $nuget).Path
    }

    $nugetDir = Split-Path -Parent $nuget
    New-Item -ItemType Directory -Force -Path $nugetDir | Out-Null

    if ((Test-Path -LiteralPath $nuget -PathType Leaf) -and $Force) {
        Remove-Item -LiteralPath $nuget -Force
    }

    Write-Host "Downloading NuGet from official distribution endpoint"
    Write-Host "  $($script:AetherManagedNuGet.Url)"
    Write-Host "  -> $nuget"
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    } catch {
    }
    Invoke-WebRequest -Uri $script:AetherManagedNuGet.Url -OutFile $nuget

    if (-not (Test-Path -LiteralPath $nuget -PathType Leaf)) {
        throw "Managed NuGet download did not produce $nuget."
    }

    $signatureCommand = Get-Command Get-AuthenticodeSignature -ErrorAction SilentlyContinue
    if ($null -ne $signatureCommand) {
        $signature = Get-AuthenticodeSignature -LiteralPath $nuget
        if ($signature.Status -ne "Valid") {
            Remove-Item -LiteralPath $nuget -Force -ErrorAction SilentlyContinue
            throw "Managed NuGet Authenticode signature is $($signature.Status), expected Valid."
        }
    }

    return (Resolve-Path -LiteralPath $nuget).Path
}

function Get-NuGetCommand {
    $nuget = Find-NuGetCommand
    if ($null -ne $nuget) {
        return $nuget
    }

    throw "NuGet was not found on PATH or in Echo Gate managed tools. Run tools\windows\install-prereqs.ps1 -Mode Build -Install, or set NUGET_EXE to the full nuget.exe path."
}

function Test-DotNetFramework472 {
    try {
        $release = Get-ItemPropertyValue -LiteralPath "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" -Name Release -ErrorAction Stop
        return ($release -ge 461808)
    } catch {
        return $false
    }
}

function ConvertTo-VersionOrNull {
    param([object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    $text = "$Value"
    if ($text -match "(\d+)\.(\d+)(?:\.(\d+))?(?:\.(\d+))?") {
        $major = [int]$Matches[1]
        $minor = [int]$Matches[2]
        $build = 0
        $revision = 0
        if (-not [string]::IsNullOrWhiteSpace($Matches[3])) { $build = [int]$Matches[3] }
        if (-not [string]::IsNullOrWhiteSpace($Matches[4])) { $revision = [int]$Matches[4] }
        return [version]::new($major, $minor, $build, $revision)
    }

    return $null
}

function Invoke-ProcessCapture {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$Arguments = @(),
        [string]$InputFile = ""
    )

    $processInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = $FilePath
    if ($processInfo.PSObject.Properties.Name -contains "ArgumentList") {
        foreach ($arg in $Arguments) {
            [void]$processInfo.ArgumentList.Add($arg)
        }
    } else {
        $processInfo.Arguments = Join-ProcessArguments -Arguments $Arguments
    }
    $processInfo.UseShellExecute = $false
    $processInfo.CreateNoWindow = $true
    $processInfo.RedirectStandardOutput = $true
    $processInfo.RedirectStandardError = $true
    if ($InputFile -ne "") {
        $processInfo.RedirectStandardInput = $true
    }

    $process = [System.Diagnostics.Process]::Start($processInfo)
    try {
        if ($InputFile -ne "") {
            $fileStream = $null
            try {
                $fileStream = [System.IO.File]::OpenRead($InputFile)
                $fileStream.CopyTo($process.StandardInput.BaseStream)
            } finally {
                if ($null -ne $fileStream) { $fileStream.Dispose() }
                if ($null -ne $process.StandardInput) { $process.StandardInput.Close() }
            }
        }

        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            Stdout = $stdout
            Stderr = $stderr
        }
    } finally {
        if ($null -ne $process) {
            $process.Dispose()
        }
    }
}

function Test-VcRedistX64 {
    $status = Get-VcRedistX64Status
    return $status.Ok
}

function Get-VcRedistX64InstallerPath {
    $cacheRoot = Join-Path (Get-EchoGateDataRoot) "ToolCache"
    return (Join-Path (Join-Path $cacheRoot $script:AetherVcRedistX64.DirectoryName) $script:AetherVcRedistX64.FileName)
}

function Get-VcRedistX64Status {
    $minimum = $script:AetherVcRedistX64.MinimumVersion
    $registryVersion = $null
    $registryInstalled = $false
    $registryPath = ""
    $runtimePath = ""
    $runtimeVersion = $null

    $registryPaths = @(
        "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
    )

    foreach ($path in $registryPaths) {
        try {
            $item = Get-ItemProperty -LiteralPath $path -ErrorAction Stop
            $installed = $item.Installed
            if ([int]$installed -eq 1) {
                $registryInstalled = $true
                $registryPath = $path
                $major = 0
                $minor = 0
                $bld = 0
                $rbld = 0
                if ($null -ne $item.Major) { $major = [int]$item.Major }
                if ($null -ne $item.Minor) { $minor = [int]$item.Minor }
                if ($null -ne $item.Bld) { $bld = [int]$item.Bld }
                if ($null -ne $item.Rbld) { $rbld = [int]$item.Rbld }
                if ($major -gt 0) {
                    $registryVersion = [version]::new($major, $minor, $bld, $rbld)
                }
                break
            }
        } catch {
        }
    }

    $systemRoot = $env:SystemRoot
    if (-not [string]::IsNullOrWhiteSpace($systemRoot)) {
        $candidate = Join-Path (Join-Path $systemRoot "System32") "vcruntime140.dll"
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $runtimePath = (Resolve-Path -LiteralPath $candidate).Path
            try {
                $runtimeVersion = ConvertTo-VersionOrNull -Value (Get-Item -LiteralPath $runtimePath).VersionInfo.FileVersion
            } catch {
            }
        }
    }

    $bestVersion = $runtimeVersion
    if ($null -eq $bestVersion) {
        $bestVersion = $registryVersion
    }

    $ok = ($registryInstalled -and $null -ne $bestVersion -and $bestVersion -ge $minimum)
    $reason = "Visual C++ x64 runtime is installed and new enough."
    if (-not $registryInstalled -and $runtimePath -eq "") {
        $reason = "Visual C++ x64 runtime is not installed."
    } elseif ($null -eq $bestVersion) {
        $reason = "Visual C++ x64 runtime was found, but its version could not be read."
    } elseif ($bestVersion -lt $minimum) {
        $reason = "Visual C++ x64 runtime is $bestVersion, but this PHP build requires at least $minimum."
    } elseif (-not $registryInstalled) {
        $reason = "vcruntime140.dll is present, but the Visual C++ x64 runtime registry entry is missing."
    }

    return [pscustomobject]@{
        Ok = $ok
        Reason = $reason
        MinimumVersion = $minimum
        RegistryInstalled = $registryInstalled
        RegistryPath = $registryPath
        RegistryVersion = $registryVersion
        RuntimePath = $runtimePath
        RuntimeVersion = $runtimeVersion
        Source = $script:AetherVcRedistX64.Url
    }
}

function Install-VcRedistX64 {
    param([switch]$Force)

    if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT -and -not (Test-WindowsAdministrator)) {
        throw "Visual C++ Redistributable repair requires administrator permission. Run tools\windows\setup.ps1 -InstallMissing from an elevated prompt, or allow setup to elevate."
    }

    $installer = Get-VcRedistX64InstallerPath
    $installerDir = Split-Path -Parent $installer
    New-Item -ItemType Directory -Force -Path $installerDir | Out-Null

    if ($Force -and (Test-Path -LiteralPath $installer -PathType Leaf)) {
        Remove-Item -LiteralPath $installer -Force
    }

    if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
        Write-Host "Downloading Microsoft Visual C++ Redistributable x64"
        Write-Host "  $($script:AetherVcRedistX64.Url)"
        Write-Host "  -> $installer"
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        } catch {
        }
        Invoke-WebRequest -Uri $script:AetherVcRedistX64.Url -OutFile $installer
    }

    if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
        throw "Visual C++ Redistributable download did not produce $installer."
    }

    $signatureCommand = Get-Command Get-AuthenticodeSignature -ErrorAction SilentlyContinue
    if ($null -ne $signatureCommand) {
        $signature = Get-AuthenticodeSignature -LiteralPath $installer
        if ($signature.Status -ne "Valid") {
            Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
            throw "Visual C++ Redistributable Authenticode signature is $($signature.Status), expected Valid."
        }
    }

    Write-Host "Installing Microsoft Visual C++ Redistributable x64"
    $process = Start-Process -FilePath $installer -ArgumentList @("/install", "/quiet", "/norestart") -Wait -PassThru
    if ($process.ExitCode -eq 3010) {
        Write-Warning "Visual C++ Redistributable installed and requested a reboot. Restart Windows before running the launcher if PHP still reports runtime loader errors."
    } elseif ($process.ExitCode -ne 0) {
        $statusAfterInstall = Get-VcRedistX64Status
        if (-not $statusAfterInstall.Ok) {
            throw "Visual C++ Redistributable installer failed with exit code $($process.ExitCode). $($statusAfterInstall.Reason)"
        }
        Write-Warning "Visual C++ Redistributable installer returned exit code $($process.ExitCode), but the runtime now satisfies setup."
    }

    return (Get-VcRedistX64Status).Ok
}

function Test-PhpMysqli {
    param([string]$Php)

    if ([string]::IsNullOrEmpty($Php)) {
        return $false
    }

    try {
        $result = Invoke-ProcessCapture -FilePath $Php -Arguments @("-m")
        $modules = @($result.Stdout -split "`r?`n")
        return ($modules | Where-Object { $_ -eq "mysqli" }).Count -gt 0
    } catch {
        return $false
    }
}

function Get-PhpMysqliDiagnostics {
    param([string]$Php)

    if ([string]::IsNullOrEmpty($Php)) {
        return [pscustomobject]@{
            Php = ""
            Ini = ""
            ExtensionDir = ""
            MysqliDll = ""
            ModuleOutput = @("PHP was not found.")
        }
    }

    $phpDir = Split-Path -Parent $Php
    $ini = Get-PhpIniPath -Php $Php
    $extensionDir = Join-Path $phpDir "ext"
    if ($null -ne $ini -and (Test-Path -LiteralPath $ini -PathType Leaf)) {
        foreach ($line in Get-Content -LiteralPath $ini) {
            if ($line -match "^\s*extension_dir\s*=\s*`"?([^`"]+)`"?\s*$") {
                $configured = $Matches[1].Trim()
                if ([System.IO.Path]::IsPathRooted($configured)) {
                    $extensionDir = $configured
                } else {
                    $extensionDir = Join-Path $phpDir $configured
                }
            }
        }
    }

    $mysqliDll = Join-Path $extensionDir "php_mysqli.dll"
    $moduleOutput = @()
    try {
        $result = Invoke-ProcessCapture -FilePath $Php -Arguments @("-m")
        $moduleOutput = @()
        if (-not [string]::IsNullOrWhiteSpace($result.Stdout)) {
            $moduleOutput += ($result.Stdout.Trim() -split "`r?`n")
        }
        if (-not [string]::IsNullOrWhiteSpace($result.Stderr)) {
            $moduleOutput += ($result.Stderr.Trim() -split "`r?`n")
        }
    } catch {
        $moduleOutput = @($_.Exception.Message)
    }

    return [pscustomobject]@{
        Php = $Php
        Ini = $ini
        ExtensionDir = $extensionDir
        MysqliDll = $mysqliDll
        ModuleOutput = $moduleOutput
    }
}

function Get-PhpMysqliStatus {
    param([string]$Php)

    $diag = Get-PhpMysqliDiagnostics -Php $Php
    $loaded = $false
    foreach ($line in $diag.ModuleOutput) {
        if ("$line" -eq "mysqli") {
            $loaded = $true
            break
        }
    }

    $iniExists = (-not [string]::IsNullOrWhiteSpace($diag.Ini) -and (Test-Path -LiteralPath $diag.Ini -PathType Leaf))
    $extensionDirExists = (-not [string]::IsNullOrWhiteSpace($diag.ExtensionDir) -and (Test-Path -LiteralPath $diag.ExtensionDir -PathType Container))
    $mysqliDllExists = (-not [string]::IsNullOrWhiteSpace($diag.MysqliDll) -and (Test-Path -LiteralPath $diag.MysqliDll -PathType Leaf))
    $iniHasMysqli = $false
    $iniHasExtensionDir = $false

    if ($iniExists) {
        foreach ($line in Get-Content -LiteralPath $diag.Ini) {
            if ($line -match "^\s*extension\s*=\s*(mysqli|php_mysqli\.dll)\s*$") {
                $iniHasMysqli = $true
            }
            if ($line -match "^\s*extension_dir\s*=") {
                $iniHasExtensionDir = $true
            }
        }
    }

    $loaderOutput = @($diag.ModuleOutput | Where-Object { "$_" -match "(?i)(mysqli|warning|error|unable|failed|dll|extension)" })
    $reason = "mysqli is loaded."
    if ([string]::IsNullOrEmpty($Php)) {
        $reason = "php.exe was not found."
    } elseif (-not $iniExists) {
        $reason = "php.ini was not found or loaded."
    } elseif (-not $extensionDirExists) {
        $reason = "extension_dir does not exist."
    } elseif (-not $mysqliDllExists) {
        $reason = "php_mysqli.dll is missing from extension_dir."
    } elseif (-not $iniHasExtensionDir) {
        $reason = "php.ini does not define extension_dir."
    } elseif (-not $iniHasMysqli) {
        $reason = "php.ini does not enable extension=mysqli."
    } elseif (-not $loaded -and $loaderOutput.Count -gt 0) {
        $reason = "PHP reported a loader error for mysqli."
    } elseif (-not $loaded) {
        $reason = "php_mysqli.dll is present and php.ini enables it, but php -m did not list mysqli."
    }

    return [pscustomobject]@{
        Loaded = $loaded
        Reason = $reason
        Php = $diag.Php
        Ini = $diag.Ini
        IniExists = $iniExists
        ExtensionDir = $diag.ExtensionDir
        ExtensionDirExists = $extensionDirExists
        MysqliDll = $diag.MysqliDll
        MysqliDllExists = $mysqliDllExists
        IniHasMysqli = $iniHasMysqli
        IniHasExtensionDir = $iniHasExtensionDir
        LoaderOutput = $loaderOutput
    }
}

function Write-PhpMysqliDiagnostics {
    param([string]$Php)

    $status = Get-PhpMysqliStatus -Php $Php
    Write-Host "  php.exe:                 $($status.Php)"
    Write-Host "  php.ini:                 $($status.Ini)"
    Write-Host "  php.ini exists:          $($status.IniExists)"
    Write-Host "  extension_dir:           $($status.ExtensionDir)"
    Write-Host "  extension_dir exists:    $($status.ExtensionDirExists)"
    Write-Host "  extension=mysqli in ini: $($status.IniHasMysqli)"
    Write-Host "  extension_dir in ini:    $($status.IniHasExtensionDir)"
    Write-Host "  php_mysqli.dll:          $($status.MysqliDll)"
    Write-Host "  php_mysqli.dll exists:   $($status.MysqliDllExists)"
    Write-Host "  php -m lists mysqli:     $($status.Loaded)"
    Write-Host "  result:                  $($status.Reason)"

    if ($status.LoaderOutput.Count -gt 0) {
        Write-Host "  PHP loader output:"
        foreach ($line in $status.LoaderOutput) {
            Write-Host "    $line"
        }
    }
}

function Get-PhpIniPath {
    param([string]$Php)

    if ([string]::IsNullOrEmpty($Php)) {
        return $null
    }

    try {
        $result = Invoke-ProcessCapture -FilePath $Php -Arguments @("--ini")
        $iniOutput = @($result.Stdout -split "`r?`n")
        foreach ($line in $iniOutput) {
            if ($line -match "^\s*Loaded Configuration File:\s*(.+)\s*$") {
                $candidate = $Matches[1].Trim()
                if ($candidate -ne "" -and $candidate -ne "(none)" -and (Test-Path -LiteralPath $candidate)) {
                    return $candidate
                }
            }
        }
    } catch {
    }

    $phpDir = Split-Path -Parent $Php
    $candidateIni = Join-Path $phpDir "php.ini"
    if (Test-Path -LiteralPath $candidateIni) {
        return $candidateIni
    }

    return $null
}

function Enable-PhpMysqli {
    param([string]$Php = "")

    $php = $Php
    if ([string]::IsNullOrEmpty($php)) {
        $php = Find-PhpCommand
    }

    if ($null -eq $php) {
        throw "PHP was not found, so mysqli could not be enabled."
    }

    $phpDir = Split-Path -Parent $php
    $ini = Get-PhpIniPath -Php $php
    if ($null -eq $ini) {
        $ini = Join-Path $phpDir "php.ini"
        $templates = @(
            (Join-Path $phpDir "php.ini-development"),
            (Join-Path $phpDir "php.ini-production")
        )

        foreach ($template in $templates) {
            if (Test-Path -LiteralPath $template) {
                Copy-Item -LiteralPath $template -Destination $ini -Force
                break
            }
        }
    }

    if (-not (Test-Path -LiteralPath $ini)) {
        throw "Could not find or create php.ini next to '$php'."
    }

    $extensionDir = Join-Path $phpDir "ext"
    if (-not (Test-Path -LiteralPath $extensionDir -PathType Container)) {
        throw "PHP extension directory was not found: $extensionDir"
    }
    $extensionDirForIni = $extensionDir.Replace("\", "/")
    $mysqliDll = Join-Path $extensionDir "php_mysqli.dll"
    if (-not (Test-Path -LiteralPath $mysqliDll -PathType Leaf)) {
        Write-Warning "php_mysqli.dll was not found at $mysqliDll."
    }

    $lines = @(Get-Content -LiteralPath $ini)
    $updated = New-Object System.Collections.Generic.List[string]
    $hasMysqli = $false
    $hasExtensionDir = $false

    foreach ($line in $lines) {
        if ($line -match "^\s*;?\s*extension\s*=\s*(mysqli|php_mysqli\.dll)\s*$") {
            if (-not $hasMysqli) {
                $updated.Add("extension=mysqli")
                $hasMysqli = $true
            } elseif ($line -match "^\s*extension\s*=") {
                $updated.Add("; duplicate disabled by AetherXIV setup: $line")
            } else {
                $updated.Add($line)
            }
            continue
        }

        if ($line -match "^\s*;?\s*extension_dir\s*=") {
            if (-not $hasExtensionDir) {
                $updated.Add("extension_dir = `"$extensionDirForIni`"")
                $hasExtensionDir = $true
            } elseif ($line -match "^\s*extension_dir\s*=") {
                $updated.Add("; $line")
            } else {
                $updated.Add($line)
            }
            continue
        }

        if ($line -match "^\s*extension_dir\s*=") {
            $hasExtensionDir = $true
        }

        $updated.Add($line)
    }

    if (-not $hasExtensionDir) {
        $updated.Add("extension_dir = `"$extensionDirForIni`"")
    }
    if (-not $hasMysqli) {
        $updated.Add("extension=mysqli")
    }

    Set-Content -LiteralPath $ini -Value $updated -Encoding ASCII
    Write-Host "Enabled PHP mysqli extension in $ini"

    $enabled = Test-PhpMysqli -Php $php
    if (-not $enabled) {
        Write-PhpMysqliDiagnostics -Php $php
    }

    return $enabled
}

function Install-ManagedPhp {
    param([switch]$Force)

    $toolsRoot = Get-EchoGateToolsRoot
    $installRoot = Join-Path $toolsRoot $script:MeteorManagedPhp.DirectoryName
    $php = Join-Path $installRoot "php.exe"
    if ((Test-Path -LiteralPath $php) -and -not $Force) {
        [void](Enable-PhpMysqli -Php $php)
        return (Resolve-Path -LiteralPath $php).Path
    }

    $cacheRoot = Join-Path (Get-EchoGateDataRoot) "ToolCache"
    New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $toolsRoot | Out-Null

    $archive = Join-Path $cacheRoot "$($script:MeteorManagedPhp.DirectoryName).zip"
    if ($Force -and (Test-Path -LiteralPath $archive -PathType Leaf)) {
        Remove-Item -LiteralPath $archive -Force
    }

    $needsDownload = $true
    if (Test-Path -LiteralPath $archive) {
        $existingHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
        $needsDownload = ($existingHash -ne $script:MeteorManagedPhp.Sha256)
    }

    if ($needsDownload) {
        Write-Host "Downloading managed PHP $($script:MeteorManagedPhp.Version) from official Windows PHP archive"
        Write-Host "  $($script:MeteorManagedPhp.Url)"
        Write-Host "  -> $archive"
        Invoke-WebRequest -Uri $script:MeteorManagedPhp.Url -OutFile $archive
    }

    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
    if ($hash -ne $script:MeteorManagedPhp.Sha256) {
        Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
        throw "Managed PHP download checksum mismatch. Expected $($script:MeteorManagedPhp.Sha256), got $hash."
    }

    if (Test-Path -LiteralPath $installRoot) {
        Remove-Item -LiteralPath $installRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
    Expand-Archive -LiteralPath $archive -DestinationPath $installRoot -Force

    if (-not (Test-Path -LiteralPath $php)) {
        throw "Managed PHP install did not produce php.exe at $php."
    }

    [void](Enable-PhpMysqli -Php $php)
    return (Resolve-Path -LiteralPath $php).Path
}

function Join-ProcessArguments {
    param([string[]]$Arguments)

    function Quote-OneArgument {
        param([string]$Argument)

        if ($Argument -ne "" -and $Argument -notmatch '[\s"]') {
            return $Argument
        }

        $result = '"'
        $backslashes = 0
        foreach ($char in $Argument.ToCharArray()) {
            if ($char -eq '\') {
                $backslashes += 1
                continue
            }

            if ($char -eq '"') {
                $result += ('\' * (($backslashes * 2) + 1))
                $result += '"'
                $backslashes = 0
                continue
            }

            if ($backslashes -gt 0) {
                $result += ('\' * $backslashes)
                $backslashes = 0
            }
            $result += $char
        }

        if ($backslashes -gt 0) {
            $result += ('\' * ($backslashes * 2))
        }
        $result += '"'
        return $result
    }

    return (($Arguments | ForEach-Object { Quote-OneArgument $_ }) -join " ")
}

function Get-MySqlCommand {
    $configured = Get-EnvValue "MYSQL_BIN"
    if (-not [string]::IsNullOrEmpty($configured)) {
        $resolved = Resolve-CommandOrExistingFile -Candidate $configured
        if ($null -eq $resolved) {
            throw "MYSQL_BIN is set to '$configured', but that command was not found."
        }
        return $resolved
    }

    $command = Find-MySqlCommand
    if ($null -ne $command) {
        return $command
    }

    throw "MariaDB/MySQL client was not found on PATH, in common install folders, registry entries, WinGet/Scoop/Chocolatey package folders, or next to an installed MariaDB/MySQL Windows service. Install MariaDB/MySQL, open a new PowerShell window, run tools\windows\diagnose-mariadb.ps1, or set MYSQL_BIN to the full mariadb.exe/mysql.exe path."
}

function Get-PhpCommand {
    $command = Find-PhpCommand
    if ($null -ne $command) {
        return $command
    }

    throw "PHP was not found in Echo Gate managed tools, PATH, or common winget install folders. Run tools\windows\setup.ps1 -InstallMissing or set PHP_BIN to the full php.exe path."
}

function Get-DbSettings {
    $settings = [ordered]@{}
    $settings.DbHost = Get-EnvValue "DB_HOST" "localhost"
    $settings.DbPort = Get-EnvValue "DB_PORT" "3306"
    $settings.DbName = Get-EnvValue "DB_NAME" (Get-EnvValue "AETHER_DB_NAME" (Get-EnvValue "METEOR_DB_NAME" "ffxiv_server"))
    $settings.AdminUser = Get-EnvValue "DB_ADMIN_USER" (Get-EnvValue "DB_USER" "root")
    $settings.AdminPass = Get-EnvValue "DB_ADMIN_PASS" (Get-EnvValue "DB_PASS" "")
    $settings.AppHost = Get-EnvValue "DB_APP_HOST" (Get-EnvValue "AETHER_DB_HOST" (Get-EnvValue "METEOR_DB_HOST" "127.0.0.1"))
    $settings.AppPort = Get-EnvValue "DB_APP_PORT" (Get-EnvValue "AETHER_DB_PORT" (Get-EnvValue "METEOR_DB_PORT" "3306"))
    $settings.AppUser = Get-EnvValue "DB_APP_USER" (Get-EnvValue "AETHER_DB_USER" (Get-EnvValue "METEOR_DB_USER" "aetherxiv"))
    $settings.AppPass = Get-EnvValue "DB_APP_PASS" (Get-EnvValue "AETHER_DB_PASS" (Get-EnvValue "METEOR_DB_PASS" "aether_dev"))
    $settings.AppHosts = (Get-EnvValue "DB_APP_HOSTS" "localhost 127.0.0.1").Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($settings.AppHosts -notcontains $settings.AppHost) {
        $settings.AppHosts += $settings.AppHost
    }
    return [pscustomobject]$settings
}

function Escape-SqlLiteral {
    param([string]$Value)
    return $Value.Replace("\", "\\").Replace("'", "\'")
}

function Escape-SqlIdentifier {
    param([string]$Value)
    return $Value.Replace('`', '``')
}

function Invoke-MySql {
    param(
        [Parameter(Mandatory = $true)][string]$MySql,
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$Port,
        [Parameter(Mandatory = $true)][string]$User,
        [string]$Password = "",
        [string]$Database = "",
        [string]$Sql = "",
        [string]$InputFile = ""
    )

    $args = @("--default-character-set=utf8", "-h", $HostName, "-P", $Port, "-u", $User)
    if ($Password -ne "") {
        $args += "-p$Password"
    }
    if ($Database -ne "") {
        $args += $Database
    }
    if ($Sql -ne "") {
        $args += @("-e", $Sql)
    }

    $result = Invoke-ProcessCapture -FilePath $MySql -Arguments $args -InputFile $InputFile

    if ($result.ExitCode -ne 0) {
        $detailLines = @()
        if (-not [string]::IsNullOrWhiteSpace($result.Stderr)) {
            $detailLines += ($result.Stderr.Trim() -split "`r?`n")
        }
        if (-not [string]::IsNullOrWhiteSpace($result.Stdout)) {
            $detailLines += ($result.Stdout.Trim() -split "`r?`n")
        }
        $detail = ($detailLines | Select-Object -First 8) -join " "
        if ($detail -ne "") {
            throw "MariaDB/MySQL command failed with exit code $($result.ExitCode). $detail"
        }
        throw "MariaDB/MySQL command failed with exit code $($result.ExitCode)."
    }

    if (-not [string]::IsNullOrWhiteSpace($result.Stderr)) {
        Write-Warning $result.Stderr.Trim()
    }
    if (-not [string]::IsNullOrWhiteSpace($result.Stdout)) {
        Write-Output $result.Stdout.TrimEnd()
    }
}

function Resolve-ServerDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$RootDir,
        [Parameter(Mandatory = $true)][string]$ServerName,
        [string]$Configuration = "Release"
    )

    $exeName = Get-ServerExecutableName -ServerName $ServerName
    $sourceBuild = Join-Path $RootDir "$ServerName\bin\$Configuration"
    $releaseLayout = Join-Path $RootDir $ServerName

    if (Test-Path -LiteralPath (Join-Path $sourceBuild $exeName)) {
        return $sourceBuild
    }

    if (Test-Path -LiteralPath (Join-Path $releaseLayout $exeName)) {
        return $releaseLayout
    }

    if (Test-Path -LiteralPath $sourceBuild) {
        return $sourceBuild
    }

    return $releaseLayout
}

function Get-ServerExecutableName {
    param([Parameter(Mandatory = $true)][string]$ServerName)

    return "AetherXIV.Core.$(($ServerName -split ' ')[0]).exe"
}

function Resolve-ServerExecutable {
    param(
        [Parameter(Mandatory = $true)][string]$RootDir,
        [Parameter(Mandatory = $true)][string]$ServerName,
        [string]$Configuration = "Release"
    )

    $dir = Resolve-ServerDirectory -RootDir $RootDir -ServerName $ServerName -Configuration $Configuration
    $exeName = Get-ServerExecutableName -ServerName $ServerName
    $exe = Join-Path $dir $exeName
    if (Test-Path -LiteralPath $exe) {
        return [pscustomobject]@{
            Directory = $dir
            Path = $exe
            Name = $exeName
        }
    }

    $legacyName = "MeteorXIV.Core.$(($ServerName -split ' ')[0]).exe"
    $sourceBuild = Join-Path $RootDir "$ServerName\bin\$Configuration"
    $releaseLayout = Join-Path $RootDir $ServerName
    $legacyMatches = @(@(
        (Join-Path $sourceBuild $legacyName),
        (Join-Path $releaseLayout $legacyName)
    ) | Where-Object { Test-Path -LiteralPath $_ })

    if ($legacyMatches.Count -gt 0) {
        throw "$ServerName still has a legacy MeteorXIV executable at $($legacyMatches[0]), but the AetherXIV launch scripts require $exeName. Rebuild the server core or re-download a current AetherXIV Server Core release package."
    }

    throw "$ServerName executable not found: $exe. Build the legacy servers with .\tools\windows\build-legacy.ps1 -Configuration $Configuration, or run .\tools\windows\setup.ps1 -Mode Build."
}

function Set-IniValue {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $lines = Get-Content -LiteralPath $Path
    $found = $false
    $updated = foreach ($line in $lines) {
        if ($line -match "^\s*$([regex]::Escape($Key))\s*=") {
            $found = $true
            "$Key=$Value"
        } else {
            $line
        }
    }

    if (-not $found) {
        $updated += "$Key=$Value"
    }

    Set-Content -LiteralPath $Path -Value $updated -Encoding ASCII
}

function Copy-DirectoryFresh {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$DestinationParent
    )

    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Missing directory: $Source"
    }

    New-Item -ItemType Directory -Force -Path $DestinationParent | Out-Null
    $destination = Join-Path $DestinationParent (Split-Path -Leaf $Source)
    if (Test-Path -LiteralPath $destination) {
        Remove-Item -LiteralPath $destination -Recurse -Force
    }
    Copy-Item -LiteralPath $Source -Destination $DestinationParent -Recurse
}

function Get-EchoGateClientPathCandidates {
    param([string]$ClientDir = "")

    $candidates = @()
    if ($ClientDir -ne "") { $candidates += $ClientDir }

    $envClientDir = Get-EnvValue "CLIENT_DIR"
    if ($envClientDir -ne "") { $candidates += $envClientDir }

    foreach ($profilePath in Get-EchoGateProfilePathCandidates) {
        if (-not (Test-Path -LiteralPath $profilePath)) {
            continue
        }

        try {
            $profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
            if ($profile.ClientRootPath) { $candidates += $profile.ClientRootPath }
        } catch {
            Write-Warning "Could not read Echo Gate profile: $profilePath"
        }
    }

    $programFilesX86 = [Environment]::GetFolderPath("ProgramFilesX86")
    $programFiles = [Environment]::GetFolderPath("ProgramFiles")
    $desktop = [Environment]::GetFolderPath("Desktop")
    if ($programFilesX86 -ne "") { $candidates += (Join-Path $programFilesX86 "SquareEnix\FINAL FANTASY XIV") }
    if ($programFiles -ne "") { $candidates += (Join-Path $programFiles "SquareEnix\FINAL FANTASY XIV") }
    if ($desktop -ne "") {
        $candidates += (Join-Path $desktop "FINAL FANTASY XIV")
        $candidates += (Join-Path $desktop "FFXIV")
    }

    return ($candidates | Where-Object { $_ -ne "" } | Select-Object -Unique)
}

function Find-EchoGateClientInstall {
    param([string]$ClientDir = "")

    foreach ($candidate in Get-EchoGateClientPathCandidates -ClientDir $ClientDir) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $leaf = (Split-Path -Leaf $candidate).ToLowerInvariant()
            if ($leaf -eq "rq9q1797qvs.san" -or $leaf -eq "staticactors.bin") {
                return [pscustomobject]@{
                    ClientRoot = Split-Path -Parent $candidate
                    StaticActorsPath = (Resolve-Path -LiteralPath $candidate).Path
                    GameExecutablePath = $null
                    Source = "staticactors"
                }
            }
        }

        if (-not (Test-Path -LiteralPath $candidate -PathType Container)) {
            continue
        }

        $staticCandidates = @(
            (Join-Path $candidate "client\script\rq9q1797qvs.san"),
            (Join-Path $candidate "script\rq9q1797qvs.san"),
            (Join-Path $candidate "rq9q1797qvs.san"),
            (Join-Path $candidate "client\script\staticactors.bin"),
            (Join-Path $candidate "script\staticactors.bin"),
            (Join-Path $candidate "staticactors.bin")
        )

        $staticActors = $null
        foreach ($path in $staticCandidates) {
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                $staticActors = (Resolve-Path -LiteralPath $path).Path
                break
            }
        }

        if ($null -eq $staticActors) {
            $recursive = Get-ChildItem -LiteralPath $candidate -Recurse -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -ieq "rq9q1797qvs.san" -or $_.Name -ieq "staticactors.bin" } |
                Select-Object -First 1
            if ($null -ne $recursive) {
                $staticActors = $recursive.FullName
            }
        }

        $gameCandidates = @(
            (Join-Path $candidate "client\ffxivgame.exe"),
            (Join-Path $candidate "ffxivgame.exe")
        )
        $gameExe = $null
        foreach ($path in $gameCandidates) {
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                $gameExe = (Resolve-Path -LiteralPath $path).Path
                break
            }
        }

        if ($null -ne $staticActors -or $null -ne $gameExe) {
            return [pscustomobject]@{
                ClientRoot = (Resolve-Path -LiteralPath $candidate).Path
                StaticActorsPath = $staticActors
                GameExecutablePath = $gameExe
                Source = "client-root"
            }
        }
    }

    return $null
}

function Set-LauncherServerState {
    param(
        [Parameter(Mandatory = $true)][string]$State,
        [Parameter(Mandatory = $true)][string]$Message
    )

    $db = Get-DbSettings
    $mysql = Get-MySqlCommand
    $stateValue = Escape-SqlLiteral $State
    $messageValue = Escape-SqlLiteral $Message
    $sql = @"
INSERT INTO launcher_config (config_key, config_value) VALUES
('server_state', '$stateValue'),
('server_message', '$messageValue')
ON DUPLICATE KEY UPDATE config_value=VALUES(config_value);
"@

    Invoke-MySql -MySql $mysql -HostName $db.AppHost -Port $db.AppPort -User $db.AppUser -Password $db.AppPass -Database $db.DbName -Sql $sql
}

function Get-EchoGateProfilePathCandidates {
    $candidates = @()
    $configured = Get-EnvValue "ECHO_GATE_PROFILE_PATH"
    if ($configured -ne "") {
        $candidates += $configured
    }
    $appData = [Environment]::GetFolderPath("ApplicationData")
    $localAppData = [Environment]::GetFolderPath("LocalApplicationData")
    if ($appData -ne "") {
        $candidates += (Join-Path $appData "Demi Dev Unit\Echo Gate\profile.json")
    }
    if ($localAppData -ne "") {
        $candidates += (Join-Path $localAppData "Demi Dev Unit\Echo Gate\profile.json")
    }
    return $candidates
}

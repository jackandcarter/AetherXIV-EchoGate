$ErrorActionPreference = "Stop"

# Keep this pinned to a checksummed Windows PHP x64 NTS zip from https://windows.php.net/download/.
$script:MeteorManagedPhp = [pscustomobject]@{
    Version = "8.5.7"
    DirectoryName = "php-8.5.7-nts-Win32-vs17-x64"
    Url = "https://downloads.php.net/~windows/releases/archives/php-8.5.7-nts-Win32-vs17-x64.zip"
    Sha256 = "2ff43fea9a243085493b48c7c47152c0678cff0b05c61a3b4f4b43ba22de212c"
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

function Find-MySqlCommand {
    $command = Find-OptionalCommand -Names @("mariadb.exe", "mysql.exe", "mariadb", "mysql")
    if ($null -ne $command) {
        return $command
    }

    $roots = @()
    if ($env:ProgramFiles) { $roots += $env:ProgramFiles }
    if (${env:ProgramFiles(x86)}) { $roots += ${env:ProgramFiles(x86)} }
    if ($env:ProgramW6432) { $roots += $env:ProgramW6432 }

    $patterns = @()
    foreach ($root in ($roots | Select-Object -Unique)) {
        $patterns += (Join-Path $root "MariaDB*\bin\mariadb.exe")
        $patterns += (Join-Path $root "MariaDB*\bin\mysql.exe")
        $patterns += (Join-Path $root "MySQL\MySQL Server *\bin\mysql.exe")
    }

    foreach ($pattern in ($patterns | Select-Object -Unique)) {
        $matches = @(Get-ChildItem -Path $pattern -File -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
        if ($matches.Count -gt 0) {
            return $matches[0].FullName
        }
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

function Test-DotNetFramework472 {
    try {
        $release = Get-ItemPropertyValue -LiteralPath "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" -Name Release -ErrorAction Stop
        return ($release -ge 461808)
    } catch {
        return $false
    }
}

function Test-VcRedistX64 {
    $paths = @(
        "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
    )

    foreach ($path in $paths) {
        try {
            $installed = Get-ItemPropertyValue -LiteralPath $path -Name Installed -ErrorAction Stop
            if ([int]$installed -eq 1) {
                return $true
            }
        } catch {
        }
    }

    return $false
}

function Test-PhpMysqli {
    param([string]$Php)

    if ([string]::IsNullOrEmpty($Php)) {
        return $false
    }

    try {
        $modules = & $Php -m 2>$null
        return ($modules -contains "mysqli")
    } catch {
        return $false
    }
}

function Get-PhpIniPath {
    param([string]$Php)

    if ([string]::IsNullOrEmpty($Php)) {
        return $null
    }

    try {
        $iniOutput = & $Php --ini 2>$null
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

    $lines = @(Get-Content -LiteralPath $ini)
    $updated = New-Object System.Collections.Generic.List[string]
    $hasMysqli = $false
    $hasExtensionDir = $false

    foreach ($line in $lines) {
        if ($line -match "^\s*;?\s*extension\s*=\s*mysqli\s*$") {
            $updated.Add("extension=mysqli")
            $hasMysqli = $true
            continue
        }

        if ($line -match "^\s*;?\s*extension_dir\s*=\s*`"?ext`"?\s*$") {
            $updated.Add('extension_dir = "ext"')
            $hasExtensionDir = $true
            continue
        }

        if ($line -match "^\s*extension\s*=\s*mysqli\s*$") {
            $hasMysqli = $true
        }
        if ($line -match "^\s*extension_dir\s*=") {
            $hasExtensionDir = $true
        }

        $updated.Add($line)
    }

    if (-not $hasExtensionDir -and (Test-Path -LiteralPath (Join-Path $phpDir "ext"))) {
        $updated.Add('extension_dir = "ext"')
    }
    if (-not $hasMysqli) {
        $updated.Add("extension=mysqli")
    }

    Set-Content -LiteralPath $ini -Value $updated -Encoding ASCII
    Write-Host "Enabled PHP mysqli extension in $ini"

    return (Test-PhpMysqli -Php $php)
}

function Install-ManagedPhp {
    $toolsRoot = Get-EchoGateToolsRoot
    $installRoot = Join-Path $toolsRoot $script:MeteorManagedPhp.DirectoryName
    $php = Join-Path $installRoot "php.exe"
    if (Test-Path -LiteralPath $php) {
        [void](Enable-PhpMysqli -Php $php)
        return (Resolve-Path -LiteralPath $php).Path
    }

    $cacheRoot = Join-Path (Get-EchoGateDataRoot) "ToolCache"
    New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $toolsRoot | Out-Null

    $archive = Join-Path $cacheRoot "$($script:MeteorManagedPhp.DirectoryName).zip"
    $needsDownload = $true
    if (Test-Path -LiteralPath $archive) {
        $existingHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
        $needsDownload = ($existingHash -ne $script:MeteorManagedPhp.Sha256)
    }

    if ($needsDownload) {
        Write-Host "Downloading managed PHP $($script:MeteorManagedPhp.Version)"
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
        $command = Get-Command $configured -ErrorAction SilentlyContinue
        if ($null -eq $command) {
            throw "MYSQL_BIN is set to '$configured', but that command was not found."
        }
        return $command.Source
    }

    $command = Find-MySqlCommand
    if ($null -ne $command) {
        return $command
    }

    throw "MariaDB/MySQL client was not found on PATH or in common Program Files install folders. Install MariaDB/MySQL, open a new PowerShell window, or set MYSQL_BIN to the full client path."
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
    $settings.DbName = Get-EnvValue "DB_NAME" (Get-EnvValue "METEOR_DB_NAME" "ffxiv_server")
    $settings.AdminUser = Get-EnvValue "DB_ADMIN_USER" (Get-EnvValue "DB_USER" "root")
    $settings.AdminPass = Get-EnvValue "DB_ADMIN_PASS" (Get-EnvValue "DB_PASS" "")
    $settings.AppHost = Get-EnvValue "DB_APP_HOST" (Get-EnvValue "METEOR_DB_HOST" "127.0.0.1")
    $settings.AppPort = Get-EnvValue "DB_APP_PORT" (Get-EnvValue "METEOR_DB_PORT" "3306")
    $settings.AppUser = Get-EnvValue "DB_APP_USER" (Get-EnvValue "METEOR_DB_USER" "meteor")
    $settings.AppPass = Get-EnvValue "DB_APP_PASS" (Get-EnvValue "METEOR_DB_PASS" "meteor_dev")
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

    if ($InputFile -ne "") {
        $processInfo = [System.Diagnostics.ProcessStartInfo]::new()
        $processInfo.FileName = $MySql
        if ($processInfo.PSObject.Properties.Name -contains "ArgumentList") {
            foreach ($arg in $args) {
                [void]$processInfo.ArgumentList.Add($arg)
            }
        } else {
            $processInfo.Arguments = Join-ProcessArguments -Arguments $args
        }
        $processInfo.UseShellExecute = $false
        $processInfo.RedirectStandardInput = $true

        $process = [System.Diagnostics.Process]::Start($processInfo)
        $exitCode = $null
        try {
            $fileStream = [System.IO.File]::OpenRead($InputFile)
            try {
                $fileStream.CopyTo($process.StandardInput.BaseStream)
            } finally {
                $fileStream.Dispose()
                $process.StandardInput.Close()
            }
            $process.WaitForExit()
            $exitCode = $process.ExitCode
        } finally {
            if ($null -ne $process) {
                $process.Dispose()
            }
        }

        if ($exitCode -ne 0) {
            throw "MariaDB/MySQL command failed with exit code $exitCode."
        }
    } else {
        & $MySql @args

        if ($LASTEXITCODE -ne 0) {
            throw "MariaDB/MySQL command failed with exit code $LASTEXITCODE."
        }
    }
}

function Resolve-ServerDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$RootDir,
        [Parameter(Mandatory = $true)][string]$ServerName,
        [string]$Configuration = "Release"
    )

    $sourceBuild = Join-Path $RootDir "$ServerName\bin\$Configuration"
    $releaseLayout = Join-Path $RootDir $ServerName

    if (Test-Path -LiteralPath (Join-Path $sourceBuild "MeteorXIV.Core.$(($ServerName -split ' ')[0]).exe")) {
        return $sourceBuild
    }

    if (Test-Path -LiteralPath (Join-Path $releaseLayout "MeteorXIV.Core.$(($ServerName -split ' ')[0]).exe")) {
        return $releaseLayout
    }

    if (Test-Path -LiteralPath $sourceBuild) {
        return $sourceBuild
    }

    return $releaseLayout
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

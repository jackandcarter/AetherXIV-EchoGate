$ErrorActionPreference = "Stop"

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

    return Find-RequiredCommand -Names @("mariadb.exe", "mysql.exe", "mariadb", "mysql") -FriendlyName "MariaDB/MySQL client"
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

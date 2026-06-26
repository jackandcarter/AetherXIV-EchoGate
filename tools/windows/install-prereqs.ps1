param(
    [ValidateSet("Run", "Build", "All")]
    [string]$Mode = "Run",
    [switch]$Install,
    [switch]$Yes,
    [switch]$RefreshManagedTools
)

. "$PSScriptRoot\common.ps1"

$toolLogPath = Start-WindowsToolLog -Name "install-prereqs"
trap {
    if (-not [string]::IsNullOrWhiteSpace($toolLogPath)) {
        Write-Host
        Write-Host "Tool log saved: $toolLogPath"
        Stop-WindowsToolLog -Path $toolLogPath
    }
    throw
}

function Requirement {
    param(
        [string]$Name,
        [scriptblock]$Test,
        [string]$WingetId = "",
        [string[]]$WingetIds = @(),
        [string]$WingetOverride = "",
        [scriptblock]$Repair = $null,
        [scriptblock]$Refresh = $null,
        [scriptblock]$Detail = $null,
        [scriptblock]$Diagnose = $null,
        [string]$Hint = ""
    )

    if ($WingetId -ne "" -and $WingetIds.Count -eq 0) {
        $WingetIds = @($WingetId)
    }

    [pscustomobject]@{
        Name = $Name
        Test = $Test
        WingetId = $WingetId
        WingetIds = $WingetIds
        WingetOverride = $WingetOverride
        Repair = $Repair
        Refresh = $Refresh
        Detail = $Detail
        Diagnose = $Diagnose
        Hint = $Hint
    }
}

function Confirm-Action {
    param([string]$Prompt)

    if ($Yes) {
        return $true
    }

    $answer = Read-Host "$Prompt [y/N]"
    return ($answer -match "^(y|yes)$")
}

function Test-WingetNoUpdateExitCode {
    param([int]$ExitCode)

    return ($ExitCode -eq -1978335189)
}

function Write-ExternalToolOutput {
    param([object[]]$Output)

    foreach ($item in $Output) {
        $line = "$item".Trim()
        if ($line -eq "") {
            continue
        }

        # winget progress frames are useful interactively but become one noisy
        # line per spinner tick when captured through PowerShell.
        if ($line -match "^[\-\|/\\]+$") {
            continue
        }
        if ($line -match "^[\u2588\u2591\u2592\u2593\s\-\|/\\]+$") {
            continue
        }

        Write-Host $line
    }
}

function Install-WingetPackage {
    param(
        [string]$Name,
        [string]$Id,
        [string]$Override = ""
    )

    $winget = Find-OptionalCommand -Names @("winget.exe", "winget")
    if ($null -eq $winget) {
        Write-Warning "winget was not found. Install $Name manually."
        return $false
    }

    if (-not (Confirm-Action -Prompt "Install $Name with winget package '$Id' now?")) {
        Write-Host "Skipped: $Name"
        return $false
    }

    $args = @(
        "install",
        "--id", $Id,
        "--exact",
        "--source", "winget",
        "--accept-package-agreements",
        "--accept-source-agreements",
        "--disable-interactivity"
    )

    if ($Yes) {
        $args += "--silent"
    }
    if ($Override -ne "") {
        $args += @("--override", $Override)
    }

    Write-Host "Installing $Name with winget package '$Id'"
    $output = @(& $winget @args 2>&1)
    $exitCode = $LASTEXITCODE
    Write-ExternalToolOutput -Output $output
    if ($exitCode -ne 0) {
        if (Test-WingetNoUpdateExitCode -ExitCode $exitCode) {
            Write-Host "winget reports $Name is already installed and no newer package is available."
            return $true
        }
        Write-Warning "winget install failed for $Name with exit code $exitCode."
        return $false
    }

    return $true
}

function Update-ProcessPath {
    $machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $paths = @()
    if (-not [string]::IsNullOrEmpty($machinePath)) { $paths += $machinePath }
    if (-not [string]::IsNullOrEmpty($userPath)) { $paths += $userPath }
    if ($paths.Count -gt 0) {
        $env:Path = ($paths -join ";")
    }
}

function Test-Requirement {
    param($Requirement)

    try {
        return [bool](& $Requirement.Test)
    } catch {
        return $false
    }
}

function Get-RequirementDetail {
    param($Requirement)

    if ($null -eq $Requirement.Detail) {
        return ""
    }

    try {
        $detail = & $Requirement.Detail
        if ($null -eq $detail) {
            return ""
        }
        return [string]$detail
    } catch {
        return "detail unavailable: $($_.Exception.Message)"
    }
}

function Write-RequirementLine {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Detail = ""
    )

    "{0,-36} {1}" -f $Name, $Status
    if (-not [string]::IsNullOrWhiteSpace($Detail)) {
        Write-Host "  $Detail"
    }
}

function Invoke-RequirementDiagnostics {
    param($Requirement)

    if ($null -eq $Requirement.Diagnose) {
        return
    }

    try {
        & $Requirement.Diagnose
    } catch {
        Write-Warning "Diagnostic output failed for $($Requirement.Name): $($_.Exception.Message)"
    }
}

$runRequirements = @(
    (Requirement -Name "MariaDB or MySQL client/server" -Test { $null -ne (Find-MySqlCommand) } -Detail {
            $mysql = Find-MySqlCommand
            if ($null -ne $mysql) { return "found: $mysql" }
            return "not found by PATH, MYSQL_BIN, registry, service, or common install folders"
        } -WingetId "MariaDB.Server" -Hint "Required for local database setup. If it is installed but not detected, run tools\windows\diagnose-mariadb.ps1 or set MYSQL_BIN to the full mariadb.exe/mysql.exe path.")
    (Requirement -Name "Microsoft Visual C++ x64 runtime" -Test { Test-VcRedistX64 } -Detail {
            $status = Get-VcRedistX64Status
            if ($status.Ok) {
                if ($null -ne $status.RuntimeVersion) { return "found: $($status.RuntimePath) ($($status.RuntimeVersion))" }
                return "found in registry: $($status.RegistryPath) ($($status.RegistryVersion))"
            }
            return $status.Reason
        } -Repair { Install-VcRedistX64 } -Diagnose {
            $status = Get-VcRedistX64Status
            Write-Host "  required version:        $($status.MinimumVersion)"
            Write-Host "  runtime dll:             $($status.RuntimePath)"
            Write-Host "  runtime dll version:     $($status.RuntimeVersion)"
            Write-Host "  registry installed:      $($status.RegistryInstalled)"
            Write-Host "  registry path:           $($status.RegistryPath)"
            Write-Host "  registry version:        $($status.RegistryVersion)"
            Write-Host "  official source:         $($status.Source)"
            Write-Host "  result:                  $($status.Reason)"
        } -Hint "Required by the managed PHP runtime from windows.php.net. Setup repairs it with Microsoft's official latest x64 Visual C++ Redistributable.")
    (Requirement -Name "PHP" -Test { $null -ne (Find-PhpCommand) } -Detail {
            $php = Find-PhpCommand
            if ($null -ne $php) { return "found: $php" }
            return "not found; setup can install managed PHP from windows.php.net"
        } -Repair { Install-ManagedPhp } -Refresh { Install-ManagedPhp -Force } -Hint "Required only when hosting the local launcher HTTP service. Setup installs Echo Gate's managed PHP when missing, or set PHP_BIN to the full php.exe path.")
    (Requirement -Name "PHP mysqli extension" -Test {
            $php = Find-PhpCommand
            Test-PhpMysqli -Php $php
        } -Detail {
            $php = Find-PhpCommand
            $status = Get-PhpMysqliStatus -Php $php
            if ($status.Loaded -and $status.MysqliDllExists) { return "loaded: $($status.MysqliDll)" }
            if ($status.Loaded) { return "loaded by php -m" }
            return $status.Reason
        } -Repair {
            $php = Find-PhpCommand
            Enable-PhpMysqli -Php $php
        } -Diagnose {
            $php = Find-PhpCommand
            Write-PhpMysqliDiagnostics -Php $php
        } -Hint "Required by the launcher HTTP service database calls. The installer can enable extension=mysqli in php.ini when PHP is detected.")
    (Requirement -Name ".NET Framework 4.7.2 or newer" -Test { Test-DotNetFramework472 } -Detail {
            if (Test-DotNetFramework472) { return "detected in .NET Framework v4 Full registry" }
            return "not detected in .NET Framework v4 Full registry"
        } -Hint "Required by the legacy server executables. Modern Windows 10/11 usually already has a newer .NET Framework.")
)

$buildRequirements = @(
    (Requirement -Name "Visual Studio Build Tools / MSBuild" -Test { $null -ne (Find-MsBuildCommand) } -Detail {
            $msbuild = Find-MsBuildCommand
            if ($null -ne $msbuild) { return "found: $msbuild" }
            return "not found; setup can install Visual Studio Build Tools through winget"
        } -WingetId "Microsoft.VisualStudio.2022.BuildTools" -WingetOverride "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.MSBuildTools --add Microsoft.Net.Component.4.7.2.TargetingPack" -Hint "Required to build the legacy server solution.")
    (Requirement -Name "NuGet" -Test { $null -ne (Find-NuGetCommand) } -Detail {
            $nuget = Find-NuGetCommand
            if ($null -ne $nuget) { return "found: $nuget" }
            return "not found; setup can download nuget.exe directly from $($script:AetherManagedNuGet.Url)"
        } -Repair { Install-ManagedNuGet } -Refresh { Install-ManagedNuGet -Force } -Diagnose {
            Write-Host "  NuGet source: $($script:AetherManagedNuGet.Url)"
            Write-Host "  Managed path: $(Get-ManagedNuGetPath)"
        } -Hint "Required to restore legacy server packages. Setup downloads a signed nuget.exe from NuGet's official distribution endpoint when NuGet is not already available.")
    (Requirement -Name ".NET 10 SDK" -Test {
            $dotnet = Find-OptionalCommand -Names @("dotnet.exe", "dotnet")
            if ($null -eq $dotnet) { return $false }
            $sdks = & $dotnet --list-sdks 2>$null
            return ($sdks -match "^10\.")
        } -Detail {
            $dotnet = Find-OptionalCommand -Names @("dotnet.exe", "dotnet")
            if ($null -eq $dotnet) { return "dotnet was not found" }
            $sdks = @(& $dotnet --list-sdks 2>$null | Where-Object { $_ -match "^10\." })
            if ($sdks.Count -gt 0) { return "found: $($sdks -join ', ')" }
            return "dotnet found, but no .NET 10 SDK was listed"
        } -WingetId "Microsoft.DotNet.SDK.10" -Hint "Required to publish Echo Gate from source.")
)

if ($Mode -eq "Run") {
    $requirements = $runRequirements
} elseif ($Mode -eq "Build") {
    $requirements = $runRequirements + $buildRequirements
} else {
    $requirements = $runRequirements + $buildRequirements
}

Write-Host "Windows prerequisite check ($Mode)"
Write-Host

foreach ($requirement in $requirements) {
    $ok = Test-Requirement -Requirement $requirement
    $detail = Get-RequirementDetail -Requirement $requirement

    if ($ok) {
        Write-RequirementLine -Name $requirement.Name -Status "ok" -Detail $detail
        if ($RefreshManagedTools -and $null -ne $requirement.Refresh) {
            if (Confirm-Action -Prompt "Refresh managed $($requirement.Name) from its official source now?") {
                try {
                    & $requirement.Refresh
                    Update-ProcessPath
                    $ok = Test-Requirement -Requirement $requirement
                    $detail = Get-RequirementDetail -Requirement $requirement
                    if ($ok) {
                        Write-RequirementLine -Name $requirement.Name -Status "refreshed" -Detail $detail
                    } else {
                        Write-RequirementLine -Name $requirement.Name -Status "missing" -Detail $detail
                        Invoke-RequirementDiagnostics -Requirement $requirement
                    }
                } catch {
                    Write-Warning "Refresh failed for $($requirement.Name): $($_.Exception.Message)"
                }
            }
        }
        continue
    }

    Write-RequirementLine -Name $requirement.Name -Status "missing" -Detail $detail
    if ($requirement.Hint -ne "") {
        Write-Host "  $($requirement.Hint)"
    }

    if ($Install) {
        $handled = $false

        if ($requirement.WingetIds.Count -gt 0) {
            foreach ($wingetId in $requirement.WingetIds) {
                $handled = $true
                $installed = Install-WingetPackage -Name $requirement.Name -Id $wingetId -Override $requirement.WingetOverride
                Update-ProcessPath
                if ($installed -and (Test-Requirement -Requirement $requirement)) {
                    break
                }
            }
        }

        if (-not (Test-Requirement -Requirement $requirement) -and $null -ne $requirement.Repair) {
            $handled = $true
            if (Confirm-Action -Prompt "Install or repair $($requirement.Name) now?") {
                try {
                    & $requirement.Repair
                    Update-ProcessPath
                } catch {
                    Write-Warning "Automatic repair failed for $($requirement.Name): $($_.Exception.Message)"
                }
            } else {
                Write-Host "Skipped: $($requirement.Name)"
            }
        }

        if ($handled) {
            if (Test-Requirement -Requirement $requirement) {
                $detail = Get-RequirementDetail -Requirement $requirement
                Write-RequirementLine -Name $requirement.Name -Status "ok" -Detail $detail
            } else {
                $detail = Get-RequirementDetail -Requirement $requirement
                Write-Warning "$($requirement.Name) is still missing after automatic install/repair."
                Write-RequirementLine -Name $requirement.Name -Status "missing" -Detail $detail
                Invoke-RequirementDiagnostics -Requirement $requirement
                if ($requirement.Hint -ne "") {
                    Write-Host "  $($requirement.Hint)"
                }
            }
        } else {
            Write-Host "  Automatic install is not available for this check."
        }
    }
}

Write-Host
if ($Install) {
    Update-ProcessPath
    Write-Host "Prerequisite install pass complete. PATH was refreshed for this PowerShell process."
} else {
    Write-Host "No prerequisites were installed or repaired. Rerun with -Install to fix missing prerequisites."
}

if (-not [string]::IsNullOrWhiteSpace($toolLogPath)) {
    Write-Host
    Write-Host "Tool log saved: $toolLogPath"
    Stop-WindowsToolLog -Path $toolLogPath
}

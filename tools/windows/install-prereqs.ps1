param(
    [ValidateSet("Run", "Build", "All")]
    [string]$Mode = "Run",
    [switch]$Install,
    [switch]$Yes
)

. "$PSScriptRoot\common.ps1"

function Requirement {
    param(
        [string]$Name,
        [scriptblock]$Test,
        [string]$WingetId = "",
        [string]$WingetOverride = "",
        [string]$Hint = ""
    )

    [pscustomobject]@{
        Name = $Name
        Test = $Test
        WingetId = $WingetId
        WingetOverride = $WingetOverride
        Hint = $Hint
    }
}

function Confirm-Install {
    param([string]$Name)

    if ($Yes) {
        return $true
    }

    $answer = Read-Host "Install $Name now? [y/N]"
    return ($answer -match "^(y|yes)$")
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
        return
    }

    if (-not (Confirm-Install -Name $Name)) {
        Write-Host "Skipped: $Name"
        return
    }

    $args = @(
        "install",
        "--id", $Id,
        "--exact",
        "--source", "winget",
        "--accept-package-agreements",
        "--accept-source-agreements"
    )

    if ($Yes) {
        $args += "--silent"
    }
    if ($Override -ne "") {
        $args += @("--override", $Override)
    }

    Write-Host "Installing $Name with winget package '$Id'"
    & $winget @args
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "winget install failed for $Name with exit code $LASTEXITCODE."
    }
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

$runRequirements = @(
    (Requirement -Name "MariaDB or MySQL client/server" -Test { $null -ne (Find-OptionalCommand -Names @("mariadb.exe", "mysql.exe", "mariadb", "mysql")) } -WingetId "MariaDB.Server" -Hint "Required for local database setup.")
    (Requirement -Name "PHP" -Test { $null -ne (Find-OptionalCommand -Names @("php.exe", "php")) } -WingetId "PHP.PHP" -Hint "Required for the launcher HTTP service.")
    (Requirement -Name "PHP mysqli extension" -Test {
            $php = Find-OptionalCommand -Names @("php.exe", "php")
            Test-PhpMysqli -Php $php
        } -Hint "Enable extension=mysqli in php.ini. Run 'php --ini' to find the active php.ini.")
    (Requirement -Name ".NET Framework 4.7.2 or newer" -Test { Test-DotNetFramework472 } -Hint "Required by the legacy server executables. Modern Windows 10/11 usually already has a newer .NET Framework.")
)

$buildRequirements = @(
    (Requirement -Name "Visual Studio Build Tools / MSBuild" -Test { $null -ne (Find-MsBuildCommand) } -WingetId "Microsoft.VisualStudio.2022.BuildTools" -WingetOverride "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.MSBuildTools --add Microsoft.Net.Component.4.7.2.TargetingPack" -Hint "Required to build the legacy server solution.")
    (Requirement -Name "NuGet" -Test { $null -ne (Find-OptionalCommand -Names @("nuget.exe", "nuget")) } -WingetId "NuGet.NuGet" -Hint "Required to restore legacy server packages.")
    (Requirement -Name ".NET 10 SDK" -Test {
            $dotnet = Find-OptionalCommand -Names @("dotnet.exe", "dotnet")
            if ($null -eq $dotnet) { return $false }
            $sdks = & $dotnet --list-sdks 2>$null
            return ($sdks -match "^10\.")
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
    $ok = $false
    try {
        $ok = [bool](& $requirement.Test)
    } catch {
        $ok = $false
    }

    if ($ok) {
        "{0,-36} ok" -f $requirement.Name
        continue
    }

    "{0,-36} missing" -f $requirement.Name
    if ($requirement.Hint -ne "") {
        Write-Host "  $($requirement.Hint)"
    }

    if ($Install -and $requirement.WingetId -ne "") {
        Install-WingetPackage -Name $requirement.Name -Id $requirement.WingetId -Override $requirement.WingetOverride
    } elseif ($Install -and $requirement.WingetId -eq "") {
        Write-Host "  Automatic install is not available for this check."
    }
}

Write-Host
if ($Install) {
    Update-ProcessPath
    Write-Host "Prerequisite install pass complete. PATH was refreshed for this PowerShell process."
} else {
    Write-Host "No packages were installed. Rerun with -Install to install missing winget packages."
}

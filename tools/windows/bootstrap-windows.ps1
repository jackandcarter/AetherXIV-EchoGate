param(
    [ValidateSet("win-x86", "win-x64", "win-arm64")]
    [string]$Runtime = "win-x86",
    [string]$Configuration = "Release",
    [string]$ClientDir = "",
    [switch]$SkipBuild,
    [switch]$SkipDatabase,
    [switch]$SkipLauncher,
    [switch]$InstallMissing,
    [switch]$Yes,
    [switch]$RefreshManagedTools
)

. "$PSScriptRoot\common.ps1"

$setupArgs = @("-Mode", "Build", "-Runtime", $Runtime, "-Configuration", $Configuration, "-SkipSmoke")
if ($ClientDir -ne "") { $setupArgs += @("-ClientDir", $ClientDir) }
if ($SkipBuild) { $setupArgs += "-SkipBuild" }
if ($SkipDatabase) { $setupArgs += "-SkipDatabase" }
if ($SkipLauncher) { $setupArgs += "-SkipLauncher" }
if ($InstallMissing) { $setupArgs += "-InstallMissing" }
if ($Yes) { $setupArgs += "-Yes" }
if ($RefreshManagedTools) { $setupArgs += "-RefreshManagedTools" }

Write-Host "bootstrap-windows.ps1 is a compatibility wrapper. New users should run tools\windows\setup.ps1 -Mode Build."
& "$PSScriptRoot\setup.ps1" @setupArgs

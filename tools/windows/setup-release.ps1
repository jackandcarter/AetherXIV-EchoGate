param(
    [string]$ClientDir = "",
    [switch]$InstallMissing,
    [switch]$Yes,
    [switch]$SkipDatabase,
    [switch]$SkipRuntimeData,
    [switch]$SkipSmoke,
    [switch]$AllowMissingStaticActors
)

. "$PSScriptRoot\common.ps1"

$setupArgs = @("-Mode", "Run")
if ($ClientDir -ne "") { $setupArgs += @("-ClientDir", $ClientDir) }
if ($InstallMissing) { $setupArgs += "-InstallMissing" }
if ($Yes) { $setupArgs += "-Yes" }
if ($SkipDatabase) { $setupArgs += "-SkipDatabase" }
if ($SkipRuntimeData) { $setupArgs += "-SkipRuntimeData" }
if ($SkipSmoke) { $setupArgs += "-SkipSmoke" }
if ($AllowMissingStaticActors) { $setupArgs += "-AllowMissingStaticActors" }

Write-Host "setup-release.ps1 is a compatibility wrapper. New users should run tools\windows\setup.ps1."
& "$PSScriptRoot\setup.ps1" @setupArgs

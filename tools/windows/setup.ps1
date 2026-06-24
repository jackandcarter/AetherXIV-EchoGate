param(
    [ValidateSet("Run", "Build", "All")]
    [string]$Mode = "Run",
    [ValidateSet("win-x86", "win-x64", "win-arm64")]
    [string]$Runtime = "win-x86",
    [string]$Configuration = "Release",
    [string]$ClientDir = "",
    [switch]$InstallMissing,
    [switch]$Yes,
    [switch]$SkipDatabase,
    [switch]$SkipRuntimeData,
    [switch]$SkipSmoke,
    [switch]$AllowMissingStaticActors,
    [switch]$SkipBuild,
    [switch]$SkipLauncher,
    [switch]$StartLocalStack,
    [switch]$SkipWeb,
    [int]$StartupTimeoutSeconds = 45,
    [switch]$NoElevate
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root

if ($InstallMissing -and -not $NoElevate -and -not (Test-WindowsAdministrator) -and [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
    $forwarded = @("-Mode", $Mode, "-Runtime", $Runtime, "-Configuration", $Configuration, "-InstallMissing", "-NoElevate")
    if ($ClientDir -ne "") { $forwarded += @("-ClientDir", $ClientDir) }
    if ($Yes) { $forwarded += "-Yes" }
    if ($SkipDatabase) { $forwarded += "-SkipDatabase" }
    if ($SkipRuntimeData) { $forwarded += "-SkipRuntimeData" }
    if ($SkipSmoke) { $forwarded += "-SkipSmoke" }
    if ($AllowMissingStaticActors) { $forwarded += "-AllowMissingStaticActors" }
    if ($SkipBuild) { $forwarded += "-SkipBuild" }
    if ($SkipLauncher) { $forwarded += "-SkipLauncher" }
    if ($StartLocalStack) { $forwarded += "-StartLocalStack" }
    if ($SkipWeb) { $forwarded += "-SkipWeb" }
    $forwarded += @("-StartupTimeoutSeconds", "$StartupTimeoutSeconds")

    Write-Host "Requesting administrator permission for Windows prerequisite setup..."
    Start-ElevatedWindowsScript -ScriptPath $PSCommandPath -Arguments $forwarded -WorkingDirectory $root
    return
}

Write-Host "Echo Gate Windows setup"
Write-Host "Mode: $Mode"
if ($Mode -ne "Run") { Write-Host "Runtime: $Runtime" }
Write-Host

& "$PSScriptRoot\install-prereqs.ps1" -Mode $Mode -Install:$InstallMissing -Yes:$Yes

if (-not $SkipDatabase) {
    & "$PSScriptRoot\setup-local-db.ps1"
}

if ($Mode -ne "Run" -and -not $SkipBuild) {
    & "$PSScriptRoot\build-legacy.ps1" -Configuration $Configuration
}

if (-not $SkipRuntimeData) {
    $copyArgs = @("-Configuration", $Configuration)
    if ($ClientDir -ne "") { $copyArgs += @("-ClientDir", $ClientDir) }
    & "$PSScriptRoot\copy-runtime-data.ps1" @copyArgs
}

if ($Mode -ne "Run" -and -not $SkipLauncher) {
    & "$PSScriptRoot\build-echo-gate.ps1" -Runtime $Runtime -Configuration $Configuration
}

if (-not $SkipSmoke) {
    $smokeArgs = @("-Configuration", $Configuration)
    if ($AllowMissingStaticActors) { $smokeArgs += "-AllowMissingStaticActors" }
    if ($Mode -ne "Run") { $smokeArgs += "-BuildTools" }
    & "$PSScriptRoot\smoke-local.ps1" @smokeArgs
}

Write-Host
& "$PSScriptRoot\doctor.ps1" -Configuration $Configuration -ClientDir $ClientDir -AllowMissingStaticActors:$AllowMissingStaticActors -BuildTools:($Mode -ne "Run")

Write-Host
Write-Host "Windows setup complete."
Write-Host "Setup did not start the launcher web service or game servers."
Write-Host "To start a local self-hosted stack:"
Write-Host "  .\tools\windows\run-local-stack.ps1"
Write-Host "To start only game services without the local launcher web service:"
Write-Host "  .\tools\windows\run-local-stack.ps1 -SkipWeb"

if ($StartLocalStack) {
    $stackArgs = @("-Configuration", $Configuration, "-StartupTimeoutSeconds", "$StartupTimeoutSeconds")
    if ($SkipWeb) { $stackArgs += "-SkipWeb" }
    if ($ClientDir -ne "") { $stackArgs += @("-ClientDir", $ClientDir) }
    & "$PSScriptRoot\run-local-stack.ps1" @stackArgs
}

param(
    [ValidateSet("win-x86", "win-x64", "win-arm64")]
    [string]$Runtime = "win-x86",
    [string]$Configuration = "Release",
    [string]$ClientDir = "",
    [switch]$SkipBuild,
    [switch]$SkipDatabase,
    [switch]$SkipLauncher,
    [switch]$InstallMissing,
    [switch]$Yes
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root

if ($InstallMissing) {
    & "$PSScriptRoot\install-prereqs.ps1" -Mode Build -Install -Yes:$Yes
}

if (-not $SkipDatabase) {
    & "$PSScriptRoot\setup-local-db.ps1"
}

if (-not $SkipBuild) {
    & "$PSScriptRoot\build-legacy.ps1" -Configuration $Configuration
}

$copyArgs = @("-Configuration", $Configuration)
if ($ClientDir -ne "") { $copyArgs += @("-ClientDir", $ClientDir) }
& "$PSScriptRoot\copy-runtime-data.ps1" @copyArgs

if (-not $SkipLauncher) {
    & "$PSScriptRoot\build-echo-gate.ps1" -Runtime $Runtime -Configuration $Configuration
}

Write-Host
Write-Host "Windows bootstrap complete."
Write-Host "Start the stack with:"
Write-Host "  .\tools\windows\run-local-stack.ps1 -Configuration $Configuration"
Write-Host "Then configure Echo Gate server URL:"
Write-Host "  http://127.0.0.1:8080/launcher"

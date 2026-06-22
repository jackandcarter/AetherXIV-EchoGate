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

if ($InstallMissing) {
    & "$PSScriptRoot\install-prereqs.ps1" -Mode Run -Install -Yes:$Yes
}

if (-not $SkipDatabase) {
    & "$PSScriptRoot\setup-local-db.ps1"
}

if (-not $SkipRuntimeData) {
    $copyArgs = @()
    if ($ClientDir -ne "") { $copyArgs += @("-ClientDir", $ClientDir) }
    & "$PSScriptRoot\copy-runtime-data.ps1" @copyArgs
}

if (-not $SkipSmoke) {
    $smokeArgs = @()
    if ($AllowMissingStaticActors) { $smokeArgs += "-AllowMissingStaticActors" }
    & "$PSScriptRoot\smoke-local.ps1" @smokeArgs
}

Write-Host
Write-Host "Release setup complete."
Write-Host "Start the local stack with:"
Write-Host "  .\tools\windows\run-local-stack.ps1"

param(
    [string]$Configuration = "Release",
    [string]$ReadyFile = "",
    [string]$ClientDir = "",
    [switch]$NoPrepareRuntimeData,
    [switch]$DevDiagnostics,
    [string]$DevDiagnosticsDir = ""
)
. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
$db = Get-DbSettings
$serverIp = Get-EnvValue "SERVER_IP" "127.0.0.1"
$mapPort = Get-EnvValue "MAP_PORT" "1989"
$resolved = Resolve-ServerExecutable -RootDir $root -ServerName "Map Server" -Configuration $Configuration
$dir = $resolved.Directory
$exe = $resolved.Path

$runtimeStaticActors = Join-Path $dir "staticactors.bin"
$runtimeScriptProbe = Join-Path $dir "scripts\effects\default.lua"
if (-not $NoPrepareRuntimeData -and (-not (Test-Path -LiteralPath $runtimeStaticActors -PathType Leaf) -or -not (Test-Path -LiteralPath $runtimeScriptProbe -PathType Leaf))) {
    Write-Host "Map runtime data is incomplete; copying configs, scripts, and static actor data."
    $copyArgs = @("-Configuration", $Configuration)
    if ($ClientDir -ne "") { $copyArgs += @("-ClientDir", $ClientDir) }
    & "$PSScriptRoot\copy-runtime-data.ps1" @copyArgs
}

$previousAetherReadyFile = $env:AETHER_READY_FILE
$previousReadyFile = $env:METEOR_READY_FILE
if ($ReadyFile -ne "") { $env:AETHER_READY_FILE = $ReadyFile; $env:METEOR_READY_FILE = $ReadyFile }
$serverArgs = @("--ip", $serverIp, "--port", $mapPort, "--host", $db.AppHost, "--db", $db.DbName, "--user", $db.AppUser, "--p", $db.AppPass)
$previousAetherDiagnostics = $env:AETHER_DEV_DIAGNOSTICS
$previousDiagnosticsDir = $env:AETHER_DEV_DIAGNOSTICS_DIR
if ($DevDiagnostics) { $serverArgs += "--dev-diagnostics"; $env:AETHER_DEV_DIAGNOSTICS = "1" }
if ($DevDiagnosticsDir -ne "") { $env:AETHER_DEV_DIAGNOSTICS_DIR = $DevDiagnosticsDir }
Push-Location $dir
try { & $exe @serverArgs } finally { $env:AETHER_READY_FILE = $previousAetherReadyFile; $env:METEOR_READY_FILE = $previousReadyFile; $env:AETHER_DEV_DIAGNOSTICS = $previousAetherDiagnostics; $env:AETHER_DEV_DIAGNOSTICS_DIR = $previousDiagnosticsDir; Pop-Location }

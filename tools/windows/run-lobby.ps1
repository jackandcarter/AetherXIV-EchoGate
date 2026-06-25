param(
    [string]$Configuration = "Release",
    [string]$ReadyFile = ""
)
. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
$db = Get-DbSettings
$serverIp = Get-EnvValue "SERVER_IP" "127.0.0.1"
$lobbyPort = Get-EnvValue "LOBBY_PORT" "54994"
$resolved = Resolve-ServerExecutable -RootDir $root -ServerName "Lobby Server" -Configuration $Configuration
$dir = $resolved.Directory
$exe = $resolved.Path
$previousAetherReadyFile = $env:AETHER_READY_FILE
$previousReadyFile = $env:METEOR_READY_FILE
if ($ReadyFile -ne "") { $env:AETHER_READY_FILE = $ReadyFile; $env:METEOR_READY_FILE = $ReadyFile }
$serverArgs = @("--ip", $serverIp, "--port", $lobbyPort, "--host", $db.AppHost, "--db", $db.DbName, "--user", $db.AppUser, "--p", $db.AppPass)
Push-Location $dir
try { & $exe @serverArgs } finally { $env:AETHER_READY_FILE = $previousAetherReadyFile; $env:METEOR_READY_FILE = $previousReadyFile; Pop-Location }

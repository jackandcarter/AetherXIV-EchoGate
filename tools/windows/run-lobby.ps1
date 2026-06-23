param([string]$Configuration = "Release")
. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
$db = Get-DbSettings
$serverIp = Get-EnvValue "SERVER_IP" "127.0.0.1"
$lobbyPort = Get-EnvValue "LOBBY_PORT" "54994"
$dir = Resolve-ServerDirectory -RootDir $root -ServerName "Lobby Server" -Configuration $Configuration
$exe = Join-Path $dir "MeteorXIV.Core.Lobby.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw "Lobby server executable not found: $exe" }
$serverArgs = @("--ip", $serverIp, "--port", $lobbyPort, "--host", $db.AppHost, "--db", $db.DbName, "--user", $db.AppUser, "--p", $db.AppPass)
Push-Location $dir
try { & $exe @serverArgs } finally { Pop-Location }

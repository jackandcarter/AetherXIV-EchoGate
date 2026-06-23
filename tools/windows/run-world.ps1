param([string]$Configuration = "Release")
. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
$db = Get-DbSettings
$serverIp = Get-EnvValue "SERVER_IP" "127.0.0.1"
$worldPort = Get-EnvValue "WORLD_PORT" "54992"
$dir = Resolve-ServerDirectory -RootDir $root -ServerName "World Server" -Configuration $Configuration
$exe = Join-Path $dir "MeteorXIV.Core.World.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw "World server executable not found: $exe" }
$serverArgs = @("--ip", $serverIp, "--port", $worldPort, "--host", $db.AppHost, "--db", $db.DbName, "--user", $db.AppUser, "--p", $db.AppPass)
Push-Location $dir
try { & $exe @serverArgs } finally { Pop-Location }

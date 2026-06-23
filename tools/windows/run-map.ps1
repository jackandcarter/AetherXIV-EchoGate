param([string]$Configuration = "Release")
. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
$db = Get-DbSettings
$serverIp = Get-EnvValue "SERVER_IP" "127.0.0.1"
$mapPort = Get-EnvValue "MAP_PORT" "1989"
$dir = Resolve-ServerDirectory -RootDir $root -ServerName "Map Server" -Configuration $Configuration
$exe = Join-Path $dir "MeteorXIV.Core.Map.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw "Map server executable not found: $exe" }
$serverArgs = @("--ip", $serverIp, "--port", $mapPort, "--host", $db.AppHost, "--db", $db.DbName, "--user", $db.AppUser, "--p", $db.AppPass)
Push-Location $dir
try { & $exe @serverArgs } finally { Pop-Location }

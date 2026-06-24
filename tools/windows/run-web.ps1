param(
    [string]$Bind = "",
    [string]$Port = ""
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
$db = Get-DbSettings
$php = Get-PhpCommand
if ($Bind -eq "") { $Bind = Get-EnvValue "WEB_BIND" "127.0.0.1" }
if ($Port -eq "") { $Port = Get-EnvValue "WEB_PORT" "8080" }

$env:METEOR_DB_HOST = $db.AppHost
$env:METEOR_DB_PORT = $db.AppPort
$env:METEOR_DB_NAME = $db.DbName
$env:METEOR_DB_USER = $db.AppUser
$env:METEOR_DB_PASS = $db.AppPass

$www = Join-Path $root "Data\www"
$endpoint = "{0}:{1}" -f $Bind, $Port
Write-Host "Starting launcher service at http://$endpoint/launcher"
Push-Location $www
try {
    & $php -S $endpoint -t .
} finally {
    Pop-Location
}

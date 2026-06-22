param(
    [switch]$Drop,
    [string]$SqlDir = "",
    [string]$Database = "",
    [string]$AdminUser = "",
    [string]$AdminPassword = ""
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
$db = Get-DbSettings
$mysql = Get-MySqlCommand

if ($Database -ne "") { $db.DbName = $Database }
if ($AdminUser -ne "") { $db.AdminUser = $AdminUser }
if ($AdminPassword -ne "") { $db.AdminPass = $AdminPassword }
if ($SqlDir -eq "") { $SqlDir = Join-Path $root "Data\sql" }
if (-not (Test-Path -LiteralPath $SqlDir)) { throw "SQL directory not found: $SqlDir" }

$dbNameSql = Escape-SqlIdentifier $db.DbName
$quotedDbName = ([char]96) + $dbNameSql + ([char]96)
Write-Host "Importing Meteor database '$($db.DbName)' from $SqlDir"
if ($Drop) {
    Invoke-MySql -MySql $mysql -HostName $db.DbHost -Port $db.DbPort -User $db.AdminUser -Password $db.AdminPass -Sql "DROP DATABASE IF EXISTS $quotedDbName;"
}
Invoke-MySql -MySql $mysql -HostName $db.DbHost -Port $db.DbPort -User $db.AdminUser -Password $db.AdminPass -Sql "CREATE DATABASE IF NOT EXISTS $quotedDbName CHARACTER SET utf8 COLLATE utf8_general_ci;"

Get-ChildItem -LiteralPath $SqlDir -Filter "*.sql" | Sort-Object Name | ForEach-Object {
    Write-Host "Importing $($_.Name)"
    Invoke-MySql -MySql $mysql -HostName $db.DbHost -Port $db.DbPort -User $db.AdminUser -Password $db.AdminPass -Database $db.DbName -InputFile $_.FullName
}

Write-Host "Database import complete: $($db.DbName)"

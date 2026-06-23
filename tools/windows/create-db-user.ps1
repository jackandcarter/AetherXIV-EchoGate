param(
    [string]$Database = "",
    [string]$AdminUser = "",
    [string]$AdminPassword = "",
    [string]$AppUser = "",
    [string]$AppPassword = ""
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
$db = Get-DbSettings
$mysql = Get-MySqlCommand

if ($Database -ne "") { $db.DbName = $Database }
if ($AdminUser -ne "") { $db.AdminUser = $AdminUser }
if ($AdminPassword -ne "") { $db.AdminPass = $AdminPassword }
if ($AppUser -ne "") { $db.AppUser = $AppUser }
if ($AppPassword -ne "") { $db.AppPass = $AppPassword }

$dbNameSql = Escape-SqlIdentifier $db.DbName
$quotedDbName = ([char]96) + $dbNameSql + ([char]96)
$appUserSql = Escape-SqlLiteral $db.AppUser
$appPassSql = Escape-SqlLiteral $db.AppPass

foreach ($appHost in $db.AppHosts) {
    $appHostSql = Escape-SqlLiteral $appHost
    Write-Host "Creating/updating '$($db.AppUser)'@'$appHost' for '$($db.DbName)'"
    $sql = @"
CREATE USER IF NOT EXISTS '$appUserSql'@'$appHostSql' IDENTIFIED BY '$appPassSql';
ALTER USER '$appUserSql'@'$appHostSql' IDENTIFIED BY '$appPassSql';
GRANT ALL PRIVILEGES ON $quotedDbName.* TO '$appUserSql'@'$appHostSql';
"@
    Invoke-MySql -MySql $mysql -HostName $db.DbHost -Port $db.DbPort -User $db.AdminUser -Password $db.AdminPass -Sql $sql
}

Invoke-MySql -MySql $mysql -HostName $db.DbHost -Port $db.DbPort -User $db.AdminUser -Password $db.AdminPass -Sql "FLUSH PRIVILEGES;"
Write-Host "Database user ready: $($db.AppUser)"

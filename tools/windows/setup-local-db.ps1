param(
    [switch]$Drop,
    [switch]$NoImport,
    [switch]$NoUser,
    [string]$AdminUser = "",
    [string]$AdminPassword = ""
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
$db = Get-DbSettings

if ($AdminUser -ne "") { $db.AdminUser = $AdminUser }
if ($AdminPassword -ne "") { $db.AdminPass = $AdminPassword }

Write-Host "AetherXIV local database setup"
Write-Host "Database:    $($db.DbName)"
Write-Host "App account: $($db.AppUser) / $($db.AppPass)"
Write-Host "App hosts:   $($db.AppHosts -join ', ')"
Write-Host

$mysql = Get-MySqlCommand
$env:MYSQL_BIN = $mysql
Write-Host "DB client:   $mysql"
Write-Host

if ($db.AdminPass -eq "") {
    $secure = Read-Host "MariaDB admin password for '$($db.AdminUser)' (leave blank if none)" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        if ($plain -ne "") {
            $db.AdminPass = $plain
            $AdminPassword = $plain
            $env:DB_ADMIN_PASS = $plain
        }
    } finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

if (-not $NoImport) {
    $importArgs = @{
        AdminUser = $db.AdminUser
    }
    if ($Drop) { $importArgs.Drop = $true }
    if ($db.AdminPass -ne "") { $importArgs.AdminPassword = $db.AdminPass }
    & "$PSScriptRoot\import-db.ps1" @importArgs
}

if (-not $NoUser) {
    $userArgs = @{
        AdminUser = $db.AdminUser
    }
    if ($db.AdminPass -ne "") { $userArgs.AdminPassword = $db.AdminPass }
    & "$PSScriptRoot\create-db-user.ps1" @userArgs
}

Write-Host "Local database setup complete."

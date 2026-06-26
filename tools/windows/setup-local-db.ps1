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

function Read-MariaDbAdminPassword {
    param([string]$User)

    while ($true) {
        Write-Host "Enter the MariaDB admin password for '$User'."
        Write-Host "Use the root password you chose during MariaDB setup. Blank passwords are uncommon on Windows."
        $secure = Read-Host "Password" -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
        try {
            $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        } finally {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }

        if ($password -ne "") {
            return $password
        }

        $blankConfirm = Read-Host "Use a blank MariaDB admin password for '$User'? [y/N]"
        if ($blankConfirm -match "^(y|yes)$") {
            return ""
        }
    }
}

function Set-MariaDbAdminPassword {
    param([string]$Password)

    $db.AdminPass = $Password
    if ($Password -ne "") {
        $env:DB_ADMIN_PASS = $Password
    } else {
        [Environment]::SetEnvironmentVariable("DB_ADMIN_PASS", $null, "Process")
    }
}

function Read-MariaDbAdminRetry {
    $retry = Read-Host "Try another MariaDB admin login? [Y/n]"
    return ($retry -notmatch "^(n|no|q|quit)$")
}

function Read-MariaDbAdminUser {
    param([string]$CurrentUser)

    $newUser = Read-Host "MariaDB admin user [$CurrentUser]"
    if ([string]::IsNullOrWhiteSpace($newUser)) {
        return $CurrentUser
    }

    return $newUser.Trim()
}

Write-Host "AetherXIV local database setup"
Write-Host "Database:    $($db.DbName)"
Write-Host "App account: $($db.AppUser) / $($db.AppPass)"
Write-Host "App hosts:   $($db.AppHosts -join ', ')"
Write-Host

try {
    $mysql = Get-MySqlCommand
} catch {
    Write-Warning $_.Exception.Message
    $diagnostic = Join-Path $PSScriptRoot "diagnose-mariadb.ps1"
    if (Test-Path -LiteralPath $diagnostic -PathType Leaf) {
        Write-Host
        Write-Host "Running MariaDB detector..."
        & $diagnostic
    }
    throw
}
$env:MYSQL_BIN = $mysql
Write-Host "DB client:   $mysql"
Write-Host "             If MariaDB is installed but setup cannot find it, set MYSQL_BIN to this client path."
Write-Host

$adminPasswordKnown = (
    $PSBoundParameters.ContainsKey("AdminPassword") -or
    (Get-EnvValue "DB_ADMIN_PASS" "") -ne "" -or
    (Get-EnvValue "DB_PASS" "") -ne ""
)
while ($true) {
    if (-not $adminPasswordKnown) {
        Set-MariaDbAdminPassword -Password (Read-MariaDbAdminPassword -User $db.AdminUser)
        $adminPasswordKnown = $true
    }

    try {
        Invoke-MySql -MySql $mysql -HostName $db.DbHost -Port $db.DbPort -User $db.AdminUser -Password $db.AdminPass -Sql "SELECT 1;" *> $null
        break
    } catch {
        Write-Warning "MariaDB admin login failed for '$($db.AdminUser)' on $($db.DbHost):$($db.DbPort). $($_.Exception.Message)"
        Write-Host "MariaDB's Windows installer normally asks for a root password. Silent installs can set one through the MSI PASSWORD property."
        Write-Host "If this machine had an older MariaDB data directory, the old root password may still be in effect."
        if (-not (Read-MariaDbAdminRetry)) {
            throw "MariaDB admin login failed. Rerun setup-local-db.ps1 with the correct -AdminUser/-AdminPassword, reset the local MariaDB root password, or reinstall MariaDB with a known root password."
        }

        $db.AdminUser = Read-MariaDbAdminUser -CurrentUser $db.AdminUser
        Set-MariaDbAdminPassword -Password (Read-MariaDbAdminPassword -User $db.AdminUser)
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

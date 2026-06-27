param(
    [string]$MigrationsDir = "",
    [string]$Database = "",
    [string]$AdminUser = "",
    [string]$AdminPassword = "",
    [switch]$DryRun,
    [switch]$NoSetupLog
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
$db = Get-DbSettings
$mysql = Get-MySqlCommand

if ($Database -ne "") { $db.DbName = $Database }
if ($AdminUser -ne "") { $db.AdminUser = $AdminUser }
if ($AdminPassword -ne "") { $db.AdminPass = $AdminPassword }
if ($MigrationsDir -eq "") { $MigrationsDir = Join-Path $root "Data\sql\migrations" }
if (-not (Test-Path -LiteralPath $MigrationsDir -PathType Container)) {
    throw "Migration directory not found: $MigrationsDir"
}

function Read-MigrationAdminPassword {
    param([string]$User)

    Write-Host "Enter the MariaDB admin password for '$User'."
    Write-Host "Use the root password you chose during MariaDB setup. Leave blank only if this MariaDB account has no password."
    $secure = Read-Host "Password" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    } finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Read-MigrationAdminRetry {
    $retry = Read-Host "Try another MariaDB admin login? [Y/n]"
    return ($retry -notmatch "^(n|no|q|quit)$")
}

function Read-MigrationAdminUser {
    param([string]$CurrentUser)

    $newUser = Read-Host "MariaDB admin user [$CurrentUser]"
    if ([string]::IsNullOrWhiteSpace($newUser)) {
        return $CurrentUser
    }

    return $newUser.Trim()
}

function Test-DatabaseExists {
    $dbNameSql = Escape-SqlLiteral $db.DbName
    $sql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '$dbNameSql';"
    $count = Invoke-MySqlScalar -MySql $mysql -HostName $db.DbHost -Port $db.DbPort -User $db.AdminUser -Password $db.AdminPass -Sql $sql
    return ($count -eq "1")
}

function Ensure-MigrationLedger {
    $sql = @'
CREATE TABLE IF NOT EXISTS `aether_schema_migrations` (
  `migration_name` varchar(255) NOT NULL,
  `checksum_sha256` char(64) NOT NULL,
  `applied_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`migration_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
'@
    Invoke-MySql -MySql $mysql -HostName $db.DbHost -Port $db.DbPort -User $db.AdminUser -Password $db.AdminPass -Database $db.DbName -Sql $sql | Out-Null
}

function Test-MigrationLedgerExists {
    $dbNameSql = Escape-SqlLiteral $db.DbName
    $sql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '$dbNameSql' AND TABLE_NAME = 'aether_schema_migrations';"
    $count = Invoke-MySqlScalar -MySql $mysql -HostName $db.DbHost -Port $db.DbPort -User $db.AdminUser -Password $db.AdminPass -Sql $sql
    return ($count -eq "1")
}

function Get-AppliedMigrationChecksum {
    param([string]$MigrationName)

    $migrationNameSql = Escape-SqlLiteral $MigrationName
    $sql = "SELECT checksum_sha256 FROM aether_schema_migrations WHERE migration_name = '$migrationNameSql' LIMIT 1;"
    return Invoke-MySqlScalar -MySql $mysql -HostName $db.DbHost -Port $db.DbPort -User $db.AdminUser -Password $db.AdminPass -Database $db.DbName -Sql $sql
}

function Record-AppliedMigration {
    param(
        [string]$MigrationName,
        [string]$Checksum
    )

    $migrationNameSql = Escape-SqlLiteral $MigrationName
    $checksumSql = Escape-SqlLiteral $Checksum
    $sql = "INSERT INTO aether_schema_migrations (migration_name, checksum_sha256) VALUES ('$migrationNameSql', '$checksumSql');"
    Invoke-MySql -MySql $mysql -HostName $db.DbHost -Port $db.DbPort -User $db.AdminUser -Password $db.AdminPass -Database $db.DbName -Sql $sql | Out-Null
}

$setupTranscriptStarted = $false
$setupLogPath = $null
if (-not $NoSetupLog) {
    try {
        $setupLogPath = Start-WindowsToolLog -Name "windows-db-migrations"
        $setupTranscriptStarted = (-not [string]::IsNullOrWhiteSpace($setupLogPath))
    } catch {
        Write-Warning "Could not start migration transcript: $($_.Exception.Message)"
    }
}

try {
    Write-Host "AetherXIV database migrations"
    Write-Host "Database:      $($db.DbName)"
    Write-Host "Migrations:    $MigrationsDir"
    Write-Host "DB client:     $mysql"
    Write-Host "Admin account: $($db.AdminUser)@$($db.DbHost):$($db.DbPort)"
    Write-Host

    while ($true) {
        try {
            Invoke-MySql -MySql $mysql -HostName $db.DbHost -Port $db.DbPort -User $db.AdminUser -Password $db.AdminPass -Sql "SELECT 1;" | Out-Null
            break
        } catch {
            Write-Warning "MariaDB admin login failed for '$($db.AdminUser)' on $($db.DbHost):$($db.DbPort). $($_.Exception.Message)"
            if (-not (Read-MigrationAdminRetry)) {
                throw "MariaDB admin login failed. Rerun apply-db-migrations.ps1 with -AdminUser/-AdminPassword, or reset the local MariaDB admin password."
            }

            $db.AdminUser = Read-MigrationAdminUser -CurrentUser $db.AdminUser
            $db.AdminPass = Read-MigrationAdminPassword -User $db.AdminUser
        }
    }

    if (-not (Test-DatabaseExists)) {
        throw "Database '$($db.DbName)' does not exist. Run .\tools\windows\setup-local-db.ps1 first, or pass -Database with the database name to migrate."
    }

    $migrationFiles = @(Get-ChildItem -LiteralPath $MigrationsDir -Filter "*.sql" -File | Sort-Object Name)
    if ($migrationFiles.Count -eq 0) {
        Write-Host "No migration files found."
        return
    }

    $ledgerExists = Test-MigrationLedgerExists
    if (-not $DryRun) {
        Ensure-MigrationLedger
        $ledgerExists = $true
    }

    $applied = 0
    $skipped = 0
    foreach ($file in $migrationFiles) {
        $checksum = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
        $recordedChecksum = ""
        if ($ledgerExists) {
            $recordedChecksum = Get-AppliedMigrationChecksum -MigrationName $file.Name
        }
        if (-not [string]::IsNullOrWhiteSpace($recordedChecksum)) {
            if ($recordedChecksum -ne $checksum) {
                Write-Warning "Skipping $($file.Name): already applied with checksum $recordedChecksum, but the current file checksum is $checksum."
            } else {
                Write-Host "Skipping $($file.Name): already applied."
            }
            $skipped += 1
            continue
        }

        if ($DryRun) {
            Write-Host "Would apply $($file.Name)"
            continue
        }

        Write-Host "Applying $($file.Name)"
        Invoke-MySql -MySql $mysql -HostName $db.DbHost -Port $db.DbPort -User $db.AdminUser -Password $db.AdminPass -Database $db.DbName -InputFile $file.FullName | Out-Null
        Record-AppliedMigration -MigrationName $file.Name -Checksum $checksum
        $applied += 1
    }

    if ($DryRun) {
        Write-Host "Dry run complete: $($migrationFiles.Count - $skipped) pending, $skipped already applied."
    } else {
        Write-Host "Database migrations complete: $applied applied, $skipped skipped."
    }
} finally {
    if ($setupTranscriptStarted) {
        Write-Host
        Write-Host "Migration log saved: $setupLogPath"
        Stop-WindowsToolLog -Path $setupLogPath
    }
}

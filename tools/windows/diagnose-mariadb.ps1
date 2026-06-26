param(
    [switch]$Detailed
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root

function Write-Section {
    param([string]$Title)

    Write-Host
    Write-Host "== $Title =="
}

function Write-DetectedPath {
    param([string]$Label, [string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    Write-Host ("{0,-24} {1}" -f $Label, $Path)
}

Write-Host "AetherXIV MariaDB detector"
Write-Host "Repository: $root"

Write-Section "Configured"
Write-DetectedPath "MYSQL_BIN" (Get-EnvValue "MYSQL_BIN")
Write-DetectedPath "DB_HOST" (Get-EnvValue "DB_HOST" "localhost")
Write-DetectedPath "DB_PORT" (Get-EnvValue "DB_PORT" "3306")
Write-DetectedPath "DB_ADMIN_USER" (Get-EnvValue "DB_ADMIN_USER" "root")

Write-Section "PATH"
foreach ($name in @("mariadb.exe", "mysql.exe", "mariadb", "mysql")) {
    $command = Get-Command $name -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        Write-DetectedPath $name $command.Source
    }
}

Write-Section "Windows Services"
try {
    $services = @(Get-CimInstance -ClassName Win32_Service -Filter "Name LIKE '%MariaDB%' OR DisplayName LIKE '%MariaDB%' OR Name LIKE '%MySQL%' OR DisplayName LIKE '%MySQL%'" -ErrorAction Stop)
    if ($services.Count -eq 0) {
        Write-Host "No MariaDB/MySQL Windows services were found."
    }

    foreach ($service in $services) {
        Write-Host "$($service.Name): $($service.State)"
        Write-Host "  $($service.PathName)"
        $serverPath = Get-ExecutablePathFromCommandLine -CommandLine $service.PathName
        $clientPath = Find-MySqlClientNearServer -ServerExecutable $serverPath
        Write-DetectedPath "  client nearby" $clientPath
    }
} catch {
    Write-Warning "Could not inspect Windows services: $($_.Exception.Message)"
}

Write-Section "Common Locations"
$commonRoots = @()
if ($env:ProgramFiles) { $commonRoots += $env:ProgramFiles }
if (${env:ProgramFiles(x86)}) { $commonRoots += ${env:ProgramFiles(x86)} }
if ($env:ProgramW6432) { $commonRoots += $env:ProgramW6432 }
if ($env:LOCALAPPDATA) { $commonRoots += (Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages") }
if ($env:ProgramData) { $commonRoots += $env:ProgramData }
$commonRoots += "C:\"

$foundCommon = $false
foreach ($commonRoot in ($commonRoots | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -Unique)) {
    foreach ($filter in @("MariaDB*", "MySQL*")) {
        $directories = @(Get-ChildItem -LiteralPath $commonRoot -Directory -Filter $filter -ErrorAction SilentlyContinue | Sort-Object FullName)
        foreach ($directory in $directories) {
            $clientPath = Find-MySqlClientInDirectory -Directory $directory.FullName
            Write-DetectedPath $directory.FullName $clientPath
            if ($null -ne $clientPath) {
                $foundCommon = $true
            } elseif ($Detailed) {
                Write-Host ("{0,-24} no client in directory" -f $directory.FullName)
            }
        }
    }
}
if (-not $foundCommon) {
    Write-Host "No client executable was found in common MariaDB/MySQL locations."
}

Write-Section "Registry"
$registryPath = Find-MySqlCommandFromRegistry
Write-DetectedPath "App Paths" $registryPath
$uninstallPath = Find-MySqlCommandFromUninstallRegistry
Write-DetectedPath "Uninstall entries" $uninstallPath

Write-Section "AetherXIV Setup Result"
$detected = Find-MySqlCommand
if ($null -eq $detected) {
    Write-Warning "Setup still cannot find mariadb.exe or mysql.exe."
    Write-Host "If you see the MariaDB bin folder above, run:"
    Write-Host '  $env:MYSQL_BIN = "C:\Program Files\MariaDB <version>\bin\mariadb.exe"'
    Write-Host "  .\tools\windows\setup.ps1 -InstallMissing"
    exit 1
}

Write-Host "Detected MariaDB/MySQL client:"
Write-Host "  $detected"
Write-Host
Write-Host "To test login manually:"
Write-Host "  & `"$detected`" -h localhost -P 3306 -u root -p"
Write-Host
Write-Host "To force setup to use this client in the current PowerShell window:"
Write-Host "  `$env:MYSQL_BIN = `"$detected`""
Write-Host "  .\tools\windows\setup.ps1 -InstallMissing"

param(
    [string]$Configuration = "Release",
    [string]$ClientDir = "",
    [switch]$AllowMissingStaticActors,
    [switch]$BuildTools,
    [switch]$NoWriteState
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
$db = Get-DbSettings
$checks = @()
$runningOnWindows = ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT)

function Add-Check {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Status,
        [string]$Detail = "",
        [bool]$Required = $true
    )

    $script:checks += [pscustomobject]@{
        name = $Name
        status = $Status
        detail = $Detail
        required = $Required
    }

    if ($Detail -eq "") {
        Write-Host ("{0,-34} {1}" -f $Name, $Status)
    } else {
        Write-Host ("{0,-34} {1}: {2}" -f $Name, $Status, $Detail)
    }
}

Write-Host "Echo Gate Windows doctor"
Write-Host

Add-Check "repository" "ok" $root | Out-Null
Add-Check "data root" "ok" (Get-EchoGateDataRoot) | Out-Null
Add-Check "administrator" ($(if (Test-WindowsAdministrator) { "yes" } else { "no" })) "" $false | Out-Null

$mysql = $null
try {
    $mysql = Get-MySqlCommand
    Add-Check "MariaDB/MySQL client" "ok" $mysql | Out-Null
} catch {
    Add-Check "MariaDB/MySQL client" "missing" $_.Exception.Message | Out-Null
}

if ($null -ne $mysql) {
    try {
        Invoke-MySql -MySql $mysql -HostName $db.AppHost -Port $db.AppPort -User $db.AppUser -Password $db.AppPass -Database $db.DbName -Sql "SELECT 1;" *> $null
        Add-Check "database app user" "ok" "$($db.AppUser)@$($db.AppHost):$($db.AppPort)/$($db.DbName)" | Out-Null
    } catch {
        Add-Check "database app user" "not reachable" "$($db.AppUser)@$($db.AppHost):$($db.AppPort)/$($db.DbName)" | Out-Null
    }
}

$php = Find-PhpCommand
if ($null -ne $php) {
    Add-Check "PHP" "ok" $php | Out-Null
    if (Test-PhpMysqli -Php $php) {
        Add-Check "PHP mysqli" "ok" | Out-Null
    } else {
        Add-Check "PHP mysqli" "missing" "Run setup.ps1 -InstallMissing or enable extension=mysqli in php.ini." | Out-Null
    }
} else {
    Add-Check "PHP" "missing" "Run setup.ps1 -InstallMissing to install managed PHP." | Out-Null
}

if (-not $runningOnWindows) {
    Add-Check "VC++ x64 runtime" "not checked" "Windows-only check." $false | Out-Null
} else {
    $vcStatus = Get-VcRedistX64Status
    if ($vcStatus.Ok) {
        $detail = "required >= $($vcStatus.MinimumVersion)"
        if ($null -ne $vcStatus.RuntimeVersion) {
            $detail = "$($vcStatus.RuntimePath) ($($vcStatus.RuntimeVersion)); $detail"
        }
        Add-Check "VC++ x64 runtime" "ok" $detail | Out-Null
    } else {
        Add-Check "VC++ x64 runtime" "missing" "$($vcStatus.Reason) Run setup.ps1 -InstallMissing." | Out-Null
    }
}

if (-not $runningOnWindows) {
    Add-Check ".NET Framework" "not checked" "Windows-only check." $false | Out-Null
} elseif (Test-DotNetFramework472) {
    Add-Check ".NET Framework" "ok" "4.7.2 or newer" | Out-Null
} else {
    Add-Check ".NET Framework" "missing" "4.7.2 or newer is required by the server executables." | Out-Null
}

if ($BuildTools -or (Test-Path -LiteralPath (Join-Path $root "AetherXIV.Core.sln"))) {
    $msbuild = Find-MsBuildCommand
    if ($null -ne $msbuild) {
        Add-Check "MSBuild" "ok" $msbuild | Out-Null
    } else {
        Add-Check "MSBuild" "missing" "Required only for building from source." $false | Out-Null
    }

    $dotnet = Find-OptionalCommand -Names @("dotnet.exe", "dotnet")
    if ($null -ne $dotnet) {
        Add-Check ".NET SDK" "ok" $dotnet | Out-Null
    } else {
        Add-Check ".NET SDK" "missing" "Required only for building Echo Gate from source." $false | Out-Null
    }
}

$client = Find-EchoGateClientInstall -ClientDir $ClientDir
if ($null -ne $client) {
    Add-Check "client install" "ok" $client.ClientRoot | Out-Null
    if ($client.GameExecutablePath) {
        Add-Check "ffxivgame.exe" "ok" $client.GameExecutablePath | Out-Null
    } else {
        Add-Check "ffxivgame.exe" "not found" "Launcher may still ask for the client path." $false | Out-Null
    }
    if ($client.StaticActorsPath) {
        Add-Check "client static actors" "ok" $client.StaticActorsPath | Out-Null
    } else {
        Add-Check "client static actors" "missing" "Map server data prep needs rq9q1797qvs.san/staticactors.bin." | Out-Null
    }
} else {
    Add-Check "client install" "not found" "Set CLIENT_DIR or run with -ClientDir." $false | Out-Null
}

$localStaticActors = Join-Path $root "Data\staticactors.bin"
if (Test-Path -LiteralPath $localStaticActors) {
    Add-Check "Data staticactors.bin" "ok" $localStaticActors | Out-Null
} elseif ($AllowMissingStaticActors) {
    Add-Check "Data staticactors.bin" "missing allowed" | Out-Null
} else {
    Add-Check "Data staticactors.bin" "missing" "Run copy-runtime-data.ps1 after selecting the client path." | Out-Null
}

$servers = @(
    @("Lobby executable", "Lobby Server"),
    @("Map executable", "Map Server"),
    @("World executable", "World Server")
)

foreach ($server in $servers) {
    try {
        $resolved = Resolve-ServerExecutable -RootDir $root -ServerName $server[1] -Configuration $Configuration
        Add-Check $server[0] "ok" $resolved.Path | Out-Null
    } catch {
        Add-Check $server[0] "missing" $_.Exception.Message | Out-Null
    }
}

$state = [ordered]@{
    schema_version = 1
    generated_at = (Get-Date).ToUniversalTime().ToString("o")
    root = $root
    data_root = Get-EchoGateDataRoot
    configuration = $Configuration
    database = [ordered]@{
        host = $db.AppHost
        port = $db.AppPort
        name = $db.DbName
        user = $db.AppUser
    }
    client = $client
    checks = $checks
}

if (-not $NoWriteState) {
    $statePath = Write-EchoGateSetupState -State $state
    Write-Host
    Write-Host "Wrote setup report:"
    Write-Host "  $statePath"
}

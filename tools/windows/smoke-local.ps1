param(
    [string]$Configuration = "Release",
    [switch]$AllowMissingStaticActors,
    [switch]$BuildTools
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
$db = Get-DbSettings
$mysql = $null

function Report($Name, $Status) {
    "{0,-24} {1}" -f $Name, $Status
}

Write-Host "Smoke baseline"
try { $mysql = Get-MySqlCommand; Report "mysql client" "ok: $mysql" } catch { Report "mysql client" "missing" }
$php = Find-PhpCommand
if ($null -ne $php) {
    Report "php" "ok: $php"
    if (Test-PhpMysqli -Php $php) {
        Report "php mysqli" "ok"
    } else {
        Report "php mysqli" "missing: enable extension=mysqli in php.ini"
    }
} else {
    Report "php" "missing"
}

if (Test-DotNetFramework472) {
    Report ".NET Framework" "ok: 4.7.2 or newer"
} else {
    Report ".NET Framework" "missing: install 4.7.2 or newer"
}

if ($BuildTools -or (Test-Path -LiteralPath (Join-Path $root "AetherXIV.Core.sln"))) {
    $msbuild = Find-MsBuildCommand
    if ($null -ne $msbuild) { Report "msbuild" "ok: $msbuild" } else { Report "msbuild" "missing" }
    try { $dotnet = Find-RequiredCommand -Names @("dotnet.exe", "dotnet") -FriendlyName ".NET SDK"; Report "dotnet" "ok: $dotnet" } catch { Report "dotnet" "missing" }
}

if ($null -ne $mysql) {
    try {
        Invoke-MySql -MySql $mysql -HostName $db.AppHost -Port $db.AppPort -User $db.AppUser -Password $db.AppPass -Database $db.DbName -Sql "SELECT 1;" | Out-Null
        Report "database app user" "ok: $($db.AppUser)@$($db.AppHost)/$($db.DbName)"
    } catch {
        Report "database app user" "not reachable"
    }
}

$staticActors = Join-Path $root "Data\staticactors.bin"
if (Test-Path -LiteralPath $staticActors) {
    Report "staticactors.bin" "ok"
} elseif ($AllowMissingStaticActors) {
    Report "staticactors.bin" "missing allowed"
} else {
    Report "staticactors.bin" "missing"
}

$servers = @(
    @("Lobby", "Lobby Server", "AetherXIV.Core.Lobby.exe", "54994"),
    @("World", "World Server", "AetherXIV.Core.World.exe", "54992"),
    @("Map", "Map Server", "AetherXIV.Core.Map.exe", "1989")
)

foreach ($server in $servers) {
    $name = $server[0]
    $dir = Resolve-ServerDirectory -RootDir $root -ServerName $server[1] -Configuration $Configuration
    $exe = Join-Path $dir $server[2]
    if (-not (Test-Path -LiteralPath $exe)) {
        Report "$name executable" "missing: $exe"
        continue
    }
    Report "$name executable" "ok"

    if ($name -eq "Map" -and -not (Test-Path -LiteralPath $staticActors) -and $AllowMissingStaticActors) {
        Report "Map smoke" "skipped: staticactors.bin missing"
        continue
    }

    Push-Location $dir
    try {
        & $exe --ip 127.0.0.1 --port $server[3] --host $db.AppHost --db $db.DbName --user $db.AppUser --p $db.AppPass --smoke
        if ($LASTEXITCODE -ne 0) { throw "$name smoke exited with $LASTEXITCODE" }
    } catch {
        Report "$name smoke" "failed: $($_.Exception.Message)"
    } finally {
        Pop-Location
    }
}

param(
    [string]$Configuration = "Release",
    [string]$ClientDir = "",
    [switch]$NoPrepareStaticActors
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
$db = Get-DbSettings

$lobbyDir = Resolve-ServerDirectory -RootDir $root -ServerName "Lobby Server" -Configuration $Configuration
$mapDir = Resolve-ServerDirectory -RootDir $root -ServerName "Map Server" -Configuration $Configuration
$worldDir = Resolve-ServerDirectory -RootDir $root -ServerName "World Server" -Configuration $Configuration

New-Item -ItemType Directory -Force -Path $lobbyDir, $mapDir, $worldDir | Out-Null
Copy-Item -LiteralPath (Join-Path $root "Data\lobby_config.ini") -Destination $lobbyDir -Force
Copy-Item -LiteralPath (Join-Path $root "Data\world_config.ini") -Destination $worldDir -Force
Copy-Item -LiteralPath (Join-Path $root "Data\map_config.ini") -Destination $mapDir -Force
Copy-DirectoryFresh -Source (Join-Path $root "Data\scripts") -DestinationParent $mapDir

foreach ($configPath in @((Join-Path $lobbyDir "lobby_config.ini"), (Join-Path $worldDir "world_config.ini"), (Join-Path $mapDir "map_config.ini"))) {
    Set-IniValue -Path $configPath -Key "host" -Value $db.AppHost
    Set-IniValue -Path $configPath -Key "port" -Value $db.AppPort
    Set-IniValue -Path $configPath -Key "database" -Value $db.DbName
    Set-IniValue -Path $configPath -Key "username" -Value $db.AppUser
    Set-IniValue -Path $configPath -Key "password" -Value $db.AppPass
}
Write-Host "Runtime database configs use $($db.AppUser)@$($db.AppHost):$($db.AppPort)/$($db.DbName)"

$staticActors = Join-Path $root "Data\staticactors.bin"
if (-not (Test-Path -LiteralPath $staticActors) -and -not $NoPrepareStaticActors) {
    $args = @("-Configuration", $Configuration)
    if ($ClientDir -ne "") { $args += @("-ClientDir", $ClientDir) }
    try {
        & "$PSScriptRoot\prepare-client-data.ps1" @args
    } catch {
        Write-Warning "Static actor data was not prepared automatically: $($_.Exception.Message) Rerun setup with -ClientDir `"C:\Path\To\FINAL FANTASY XIV`", or use -AllowMissingStaticActors only if you are intentionally skipping map runtime readiness."
    }
}

if (Test-Path -LiteralPath $staticActors) {
    Copy-Item -LiteralPath $staticActors -Destination $mapDir -Force
    Write-Host "Copied staticactors.bin to $mapDir"
} elseif ($NoPrepareStaticActors) {
    Write-Warning "Data\staticactors.bin is missing and static actor preparation was skipped; Map Server will not be runtime-ready."
} else {
    Write-Warning "Data\staticactors.bin is missing; Map Server will not be runtime-ready."
}

Write-Host "Runtime data copied."

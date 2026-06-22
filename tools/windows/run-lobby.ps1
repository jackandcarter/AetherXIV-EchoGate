param([string]$Configuration = "Release")
. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
$dir = Resolve-ServerDirectory -RootDir $root -ServerName "Lobby Server" -Configuration $Configuration
$exe = Join-Path $dir "MeteorXIV.Core.Lobby.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw "Lobby server executable not found: $exe" }
Push-Location $dir
try { & $exe } finally { Pop-Location }

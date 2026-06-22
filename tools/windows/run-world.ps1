param([string]$Configuration = "Release")
. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
$dir = Resolve-ServerDirectory -RootDir $root -ServerName "World Server" -Configuration $Configuration
$exe = Join-Path $dir "MeteorXIV.Core.World.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw "World server executable not found: $exe" }
Push-Location $dir
try { & $exe } finally { Pop-Location }

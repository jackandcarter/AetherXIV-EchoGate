param([string]$Configuration = "Release")
. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
$dir = Resolve-ServerDirectory -RootDir $root -ServerName "Map Server" -Configuration $Configuration
$exe = Join-Path $dir "MeteorXIV.Core.Map.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw "Map server executable not found: $exe" }
Push-Location $dir
try { & $exe } finally { Pop-Location }

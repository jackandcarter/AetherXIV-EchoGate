param(
    [string]$ClientDir = "",
    [string]$Output = ""
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
if ($Output -eq "") { $Output = Join-Path $root "Data\staticactors.bin" }

$client = Find-EchoGateClientInstall -ClientDir $ClientDir
if ($null -eq $client -or [string]::IsNullOrEmpty($client.StaticActorsPath)) {
    throw "Could not find rq9q1797qvs.san or staticactors.bin. Run again with -ClientDir 'C:\Path\To\FINAL FANTASY XIV'."
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Output) | Out-Null
Copy-Item -LiteralPath $client.StaticActorsPath -Destination $Output -Force
Write-Host "Prepared static actor data:"
Write-Host "  source: $($client.StaticActorsPath)"
Write-Host "  output: $Output"
Write-Host "Repository policy: client-derived assets remain local and excluded from version control."

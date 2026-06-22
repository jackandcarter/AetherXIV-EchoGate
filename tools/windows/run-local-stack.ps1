param(
    [string]$Configuration = "Release",
    [switch]$PrepareRuntimeData,
    [string]$ClientDir = ""
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
if ($PrepareRuntimeData) {
    $copyArgs = @("-Configuration", $Configuration)
    if ($ClientDir -ne "") { $copyArgs += @("-ClientDir", $ClientDir) }
    & "$PSScriptRoot\copy-runtime-data.ps1" @copyArgs
}

$scripts = @(
    @{ Name = "run-web.ps1"; Args = @() },
    @{ Name = "run-lobby.ps1"; Args = @("-Configuration", $Configuration) },
    @{ Name = "run-map.ps1"; Args = @("-Configuration", $Configuration) },
    @{ Name = "run-world.ps1"; Args = @("-Configuration", $Configuration) }
)

foreach ($script in $scripts) {
    $path = Join-Path $PSScriptRoot $script.Name
    Write-Host "Opening $($script.Name)"
    $quotedPath = '"{0}"' -f $path
    $arguments = @("-NoExit", "-ExecutionPolicy", "Bypass", "-File", $quotedPath) + $script.Args
    Start-Process powershell.exe -ArgumentList $arguments -WorkingDirectory $root
}

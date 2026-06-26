param(
    [string]$Configuration = "Release",
    [switch]$NoRestore,
    [switch]$ShowLegacyWarnings
)

. "$PSScriptRoot\common.ps1"

$toolLogPath = Start-WindowsToolLog -Name "build-legacy"
trap {
    if (-not [string]::IsNullOrWhiteSpace($toolLogPath)) {
        Write-Host
        Write-Host "Tool log saved: $toolLogPath"
        Stop-WindowsToolLog -Path $toolLogPath
    }
    throw
}

$root = Get-MeteorRoot
$solution = Join-Path $root "AetherXIV.Core.sln"
if (-not (Test-Path -LiteralPath $solution)) { throw "Solution file not found: $solution" }

if (-not $NoRestore) {
    $nuget = Find-NuGetCommand
    if ($null -eq $nuget) {
        Write-Host "NuGet was not found. Installing managed NuGet under Echo Gate app data."
        $nuget = Install-ManagedNuGet
    }
    Write-Host "Restoring NuGet packages"
    & $nuget restore $solution
    if ($LASTEXITCODE -ne 0) { throw "NuGet restore failed with exit code $LASTEXITCODE." }
}

$msbuild = Find-MsBuildCommand
if ($null -eq $msbuild) {
    throw "MSBuild was not found. Install Visual Studio Build Tools or run tools\windows\install-prereqs.ps1 -Mode Build -Install."
}
$args = @($solution, "/p:Configuration=$Configuration", "/verbosity:minimal")
if (-not $ShowLegacyWarnings) {
    $nowarn = "0108;0162;0168;0169;0219;0414;0649;0659;0675"
    $escapedNoWarn = $nowarn.Replace(";", "%3B")
    Write-Host "Suppressing known legacy C# warning noise: $nowarn"
    $args += "/p:NoWarn=$escapedNoWarn"
}

Write-Host "Building AetherXIV.Core.sln ($Configuration)"
& $msbuild @args
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }

if (-not [string]::IsNullOrWhiteSpace($toolLogPath)) {
    Write-Host
    Write-Host "Tool log saved: $toolLogPath"
    Stop-WindowsToolLog -Path $toolLogPath
}

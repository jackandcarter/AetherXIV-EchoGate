param(
    [ValidateSet("win-x86", "win-x64", "win-arm64")]
    [string]$Runtime = "win-x86",
    [string]$Configuration = "Release"
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
$dotnet = Find-RequiredCommand -Names @("dotnet.exe", "dotnet") -FriendlyName ".NET SDK"
$project = Join-Path $root "launcher\EchoGate\EchoGate.App\EchoGate.App.csproj"
$helperProject = Join-Path $root "launcher\EchoGate\EchoGate.ClientLauncher\EchoGate.ClientLauncher.csproj"
$output = Join-Path $root "build\echo-gate\$Runtime\publish"

if (-not (Test-Path -LiteralPath $project)) { throw "Echo Gate project not found: $project" }
if (-not (Test-Path -LiteralPath $helperProject)) { throw "Echo Gate helper project not found: $helperProject" }

$env:AVALONIA_TELEMETRY_OPTOUT = "1"
Write-Host "Publishing Echo Gate for $Runtime"
& $dotnet publish $project --configuration $Configuration --runtime $Runtime --self-contained true --output $output /p:PublishSingleFile=false /p:UseAppHost=true
if ($LASTEXITCODE -ne 0) { throw "Echo Gate publish failed with exit code $LASTEXITCODE." }

if ($Runtime -eq "win-x86") {
    $helperRids = @("win-x86")
} elseif ($Runtime -eq "win-x64") {
    $helperRids = @("win-x64", "win-x86")
} else {
    $helperRids = @("win-arm64", "win-x64", "win-x86")
}

foreach ($helperRid in $helperRids) {
    $helperOutput = Join-Path $output "Helpers\$helperRid"
    Write-Host "Publishing client launch helper for $helperRid"
    & $dotnet publish $helperProject --configuration $Configuration --runtime $helperRid --self-contained true --output $helperOutput /p:PublishSingleFile=false /p:UseAppHost=true
    if ($LASTEXITCODE -ne 0) { throw "Helper publish failed with exit code $LASTEXITCODE." }
}

Write-Host "Echo Gate publish complete: $output"

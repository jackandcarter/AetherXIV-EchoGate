param(
    [string]$ClientDir = "",
    [string]$Output = ""
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root
if ($ClientDir -eq "") { $ClientDir = Get-EnvValue "CLIENT_DIR" }
if ($Output -eq "") { $Output = Join-Path $root "Data\staticactors.bin" }

$candidates = @()
if ($ClientDir -ne "") { $candidates += $ClientDir }
foreach ($profilePath in Get-EchoGateProfilePathCandidates) {
    if (Test-Path -LiteralPath $profilePath) {
        try {
            $profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
            if ($profile.ClientRootPath) { $candidates += $profile.ClientRootPath }
        } catch {
            Write-Warning "Could not read Echo Gate profile: $profilePath"
        }
    }
}
$programFilesX86 = [Environment]::GetFolderPath("ProgramFilesX86")
$programFiles = [Environment]::GetFolderPath("ProgramFiles")
$desktop = [Environment]::GetFolderPath("Desktop")
if ($programFilesX86 -ne "") { $candidates += (Join-Path $programFilesX86 "SquareEnix\FINAL FANTASY XIV") }
if ($programFiles -ne "") { $candidates += (Join-Path $programFiles "SquareEnix\FINAL FANTASY XIV") }
if ($desktop -ne "") {
    $candidates += (Join-Path $desktop "FINAL FANTASY XIV")
    $candidates += (Join-Path $desktop "FFXIV")
}

$source = $null
foreach ($candidate in ($candidates | Where-Object { $_ -ne "" } | Select-Object -Unique)) {
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        $leaf = (Split-Path -Leaf $candidate).ToLowerInvariant()
        if ($leaf -eq "rq9q1797qvs.san" -or $leaf -eq "staticactors.bin") {
            $source = $candidate
            break
        }
    }

    if (Test-Path -LiteralPath $candidate -PathType Container) {
        $direct = @(
            (Join-Path $candidate "client\script\rq9q1797qvs.san"),
            (Join-Path $candidate "script\rq9q1797qvs.san"),
            (Join-Path $candidate "rq9q1797qvs.san"),
            (Join-Path $candidate "client\script\staticactors.bin"),
            (Join-Path $candidate "script\staticactors.bin"),
            (Join-Path $candidate "staticactors.bin")
        )
        foreach ($path in $direct) {
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                $source = $path
                break
            }
        }
        if ($null -ne $source) { break }

        $recursive = Get-ChildItem -LiteralPath $candidate -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ieq "rq9q1797qvs.san" -or $_.Name -ieq "staticactors.bin" } |
            Select-Object -First 1
        if ($null -ne $recursive) {
            $source = $recursive.FullName
            break
        }
    }
}

if ($null -eq $source) {
    throw "Could not find rq9q1797qvs.san or staticactors.bin. Run again with -ClientDir 'C:\Path\To\FINAL FANTASY XIV'."
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Output) | Out-Null
Copy-Item -LiteralPath $source -Destination $Output -Force
Write-Host "Prepared static actor data:"
Write-Host "  source: $source"
Write-Host "  output: $Output"
Write-Host "Repository policy: client-derived assets remain local and excluded from version control."

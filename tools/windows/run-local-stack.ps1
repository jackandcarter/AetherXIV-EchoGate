param(
    [string]$Configuration = "Release",
    [switch]$PrepareRuntimeData,
    [string]$ClientDir = "",
    [int]$StartupTimeoutSeconds = 45,
    [switch]$SkipWeb,
    [switch]$NoLauncherStatusUpdate
)

. "$PSScriptRoot\common.ps1"
$root = Get-MeteorRoot
Import-MeteorEnv $root

function Wait-ForTcpPort {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][int]$Port,
        [int]$TimeoutSeconds = 45
    )

    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    Write-Host "Waiting for $Name at ${HostName}:$Port"

    while ([DateTimeOffset]::Now -lt $deadline) {
        $client = [System.Net.Sockets.TcpClient]::new()
        try {
            $async = $client.BeginConnect($HostName, $Port, $null, $null)
            if ($async.AsyncWaitHandle.WaitOne(1000, $false)) {
                $client.EndConnect($async)
                Write-Host "$Name is listening at ${HostName}:$Port"
                return
            }
        } catch {
            # The process may still be starting; retry until the timeout expires.
        } finally {
            $client.Dispose()
        }

        Start-Sleep -Milliseconds 500
    }

    throw "$Name did not start listening at ${HostName}:$Port within $TimeoutSeconds seconds."
}

function Wait-ForReadyFile {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$ReadyFile,
        [int]$TimeoutSeconds = 45
    )

    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    Write-Host "Waiting for $Name readiness signal: $ReadyFile"

    while ([DateTimeOffset]::Now -lt $deadline) {
        if (Test-Path -LiteralPath $ReadyFile) {
            Write-Host "$Name reported ready."
            Get-Content -LiteralPath $ReadyFile | ForEach-Object { Write-Host "  $_" }
            return
        }

        Start-Sleep -Milliseconds 500
    }

    throw "$Name did not report ready within $TimeoutSeconds seconds."
}

function Resolve-WaitHost {
    param(
        [string]$BindValue,
        [string]$Default = "127.0.0.1"
    )

    if ([string]::IsNullOrWhiteSpace($BindValue)) {
        return $Default
    }

    $value = $BindValue.Trim()
    if ($value -eq "0.0.0.0" -or $value -eq "*" -or $value -eq "+") {
        return "127.0.0.1"
    }

    if ($value -eq "::") {
        return "::1"
    }

    return $value
}

function Start-StackScript {
    param(
        [Parameter(Mandatory = $true)][string]$ScriptName,
        [string[]]$Arguments = @()
    )

    $path = Join-Path $PSScriptRoot $ScriptName
    Write-Host "Opening $ScriptName"
    $processArguments = @("-NoExit", "-ExecutionPolicy", "Bypass", "-File", $path) + $Arguments
    Start-Process powershell.exe -ArgumentList (Join-ProcessArguments $processArguments) -WorkingDirectory $root | Out-Null
}

function New-ReadyFile {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$ReadyDirectory
    )

    $file = Join-Path $ReadyDirectory "$Name.ready"
    if (Test-Path -LiteralPath $file) {
        Remove-Item -LiteralPath $file -Force
    }
    return $file
}

function Test-ServerExecutables {
    foreach ($serverName in @("Lobby Server", "Map Server", "World Server")) {
        $resolved = Resolve-ServerExecutable -RootDir $root -ServerName $serverName -Configuration $Configuration
        Write-Host "$serverName executable: $($resolved.Path)"
    }
}

if ($PrepareRuntimeData) {
    $copyArgs = @("-Configuration", $Configuration)
    if ($ClientDir -ne "") { $copyArgs += @("-ClientDir", $ClientDir) }
    & "$PSScriptRoot\copy-runtime-data.ps1" @copyArgs
}

Test-ServerExecutables

$webHost = Resolve-WaitHost -BindValue (Get-EnvValue "WEB_BIND" "127.0.0.1")
$webPort = [int](Get-EnvValue "WEB_PORT" "8080")
$readyDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("meteorxiv-ready-{0}" -f ([guid]::NewGuid().ToString("N")))
New-Item -ItemType Directory -Force -Path $readyDirectory | Out-Null
$lobbyReady = New-ReadyFile -Name "lobby" -ReadyDirectory $readyDirectory
$mapReady = New-ReadyFile -Name "map" -ReadyDirectory $readyDirectory
$worldReady = New-ReadyFile -Name "world" -ReadyDirectory $readyDirectory

if (-not $NoLauncherStatusUpdate) {
    try {
        Set-LauncherServerState -State "starting" -Message "Local game services are starting."
    } catch {
        Write-Warning "Could not update launcher status to starting: $($_.Exception.Message)"
    }
}

try {
    if (-not $SkipWeb) {
        Start-StackScript -ScriptName "run-web.ps1"
        Wait-ForTcpPort -Name "launcher web service" -HostName $webHost -Port $webPort -TimeoutSeconds $StartupTimeoutSeconds
    } else {
        Write-Host "Skipping local launcher web service."
    }

    Start-StackScript -ScriptName "run-lobby.ps1" -Arguments @("-Configuration", $Configuration, "-ReadyFile", $lobbyReady)
    Wait-ForReadyFile -Name "lobby server" -ReadyFile $lobbyReady -TimeoutSeconds $StartupTimeoutSeconds

    Start-StackScript -ScriptName "run-map.ps1" -Arguments @("-Configuration", $Configuration, "-ReadyFile", $mapReady)
    Wait-ForReadyFile -Name "map server" -ReadyFile $mapReady -TimeoutSeconds $StartupTimeoutSeconds

    Start-StackScript -ScriptName "run-world.ps1" -Arguments @("-Configuration", $Configuration, "-ReadyFile", $worldReady)
    Wait-ForReadyFile -Name "world server" -ReadyFile $worldReady -TimeoutSeconds $StartupTimeoutSeconds

    if (-not $NoLauncherStatusUpdate) {
        try {
            Set-LauncherServerState -State "online" -Message "Local game services are ready."
        } catch {
            Write-Warning "Could not update launcher status to online: $($_.Exception.Message)"
        }
    }

    Write-Host "Local stack startup sequence complete."
} catch {
    if (-not $NoLauncherStatusUpdate) {
        try {
            Set-LauncherServerState -State "error" -Message $_.Exception.Message
        } catch {
            Write-Warning "Could not update launcher status to error: $($_.Exception.Message)"
        }
    }

    throw
}

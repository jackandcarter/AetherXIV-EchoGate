param(
    [string]$Configuration = "Release",
    [switch]$PrepareRuntimeData,
    [string]$ClientDir = "",
    [int]$StartupTimeoutSeconds = 45
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
    $quotedPath = '"{0}"' -f $path
    $processArguments = @("-NoExit", "-ExecutionPolicy", "Bypass", "-File", $quotedPath) + $Arguments
    Start-Process powershell.exe -ArgumentList $processArguments -WorkingDirectory $root | Out-Null
}

if ($PrepareRuntimeData) {
    $copyArgs = @("-Configuration", $Configuration)
    if ($ClientDir -ne "") { $copyArgs += @("-ClientDir", $ClientDir) }
    & "$PSScriptRoot\copy-runtime-data.ps1" @copyArgs
}

$webHost = Resolve-WaitHost -BindValue (Get-EnvValue "WEB_BIND" "127.0.0.1")
$serverHost = Resolve-WaitHost -BindValue (Get-EnvValue "SERVER_IP" "127.0.0.1")
$webPort = [int](Get-EnvValue "WEB_PORT" "8080")
$lobbyPort = [int](Get-EnvValue "LOBBY_PORT" "54994")
$mapPort = [int](Get-EnvValue "MAP_PORT" "1989")
$worldPort = [int](Get-EnvValue "WORLD_PORT" "54992")

Start-StackScript -ScriptName "run-web.ps1"
Wait-ForTcpPort -Name "launcher web service" -HostName $webHost -Port $webPort -TimeoutSeconds $StartupTimeoutSeconds

Start-StackScript -ScriptName "run-lobby.ps1" -Arguments @("-Configuration", $Configuration)
Wait-ForTcpPort -Name "lobby server" -HostName $serverHost -Port $lobbyPort -TimeoutSeconds $StartupTimeoutSeconds

Start-StackScript -ScriptName "run-map.ps1" -Arguments @("-Configuration", $Configuration)
Wait-ForTcpPort -Name "map server" -HostName $serverHost -Port $mapPort -TimeoutSeconds $StartupTimeoutSeconds

Start-StackScript -ScriptName "run-world.ps1" -Arguments @("-Configuration", $Configuration)
Wait-ForTcpPort -Name "world server" -HostName $serverHost -Port $worldPort -TimeoutSeconds $StartupTimeoutSeconds

Write-Host "Local stack startup sequence complete."

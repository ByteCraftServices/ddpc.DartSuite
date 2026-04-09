param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

function Write-Stage {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Stop-RepoDotnetProcesses {
    param([string]$RepoPath)

    Write-Stage "Stoppe blockierende dotnet Prozesse"

    $procs = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
        Where-Object {
            ($_.CommandLine -like "*$RepoPath*") -and (
                $_.CommandLine -like "*watch*" -or
                $_.CommandLine -like "* run *" -or
                $_.CommandLine -like "* run" -or
                $_.CommandLine -like "*run-api*" -or
                $_.CommandLine -like "*run-web*"
            )
        }

    if (-not $procs) {
        Write-Host "Keine blockierenden Prozesse gefunden."
        return
    }

    $procs | Select-Object ProcessId, CommandLine | Format-Table -AutoSize

    foreach ($proc in $procs) {
        Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
    }

    Write-Host "Gestoppte Prozesse: $($procs.Count)"
}

function Invoke-Step {
    param(
        [string]$Name,
        [string]$Command
    )

    Write-Stage $Name
    Write-Host "> $Command"
    Invoke-Expression $Command
}

$repoPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoPath

Stop-RepoDotnetProcesses -RepoPath $repoPath

Invoke-Step -Name "Build" -Command "dotnet build ddpc.DartSuite.slnx -c $Configuration /p:UseAppHost=false"
Invoke-Step -Name "Tests" -Command "dotnet test ddpc.DartSuite.slnx -c $Configuration /p:UseAppHost=false"

Write-Stage "Validierung erfolgreich"
Write-Host "Build und Tests sind gruen." -ForegroundColor Green

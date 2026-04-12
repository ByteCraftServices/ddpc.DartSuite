$ErrorActionPreference = "Stop"

$repoPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

Write-Host "[preflight] Suche blockierende dotnet Prozesse im Repo: $repoPath"

$processes = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
    Where-Object {
        $_.CommandLine -and
        $_.CommandLine -like "*$repoPath*" -and (
            $_.CommandLine -like "* watch *" -or
            $_.CommandLine -like "* watch" -or
            $_.CommandLine -like "*watch *" -or
            $_.CommandLine -like "* run *" -or
            $_.CommandLine -like "* run" -or
            $_.CommandLine -like "*run-api*" -or
            $_.CommandLine -like "*run-web*"
        )
    }

if (-not $processes) {
    Write-Host "[preflight] Keine blockierenden Prozesse gefunden."
    exit 0
}

$processes | Select-Object ProcessId, CommandLine | Format-Table -AutoSize

foreach ($process in $processes) {
    Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
}

Write-Host "[preflight] Gestoppte Prozesse: $($processes.Count)"

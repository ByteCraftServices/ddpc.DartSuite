$ErrorActionPreference = "Stop"

$repoPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$targetProjects = @(
    "src/ddpc.DartSuite.Api",
    "src/ddpc.DartSuite.Web"
)
$isWindowsPlatform = if (Get-Variable -Name IsWindows -ErrorAction SilentlyContinue) {
    [bool]$IsWindows
}
else {
    $PSVersionTable.PSEdition -eq "Desktop"
}

function Get-DotnetProcesses {
    if ($isWindowsPlatform) {
        return Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
            Select-Object ProcessId, CommandLine
    }

    return Get-Process -Name dotnet -ErrorAction SilentlyContinue |
        ForEach-Object {
            $cmdlinePath = "/proc/$($_.Id)/cmdline"
            $commandLine = $null

            if (Test-Path $cmdlinePath) {
                $commandLine = (Get-Content -Raw $cmdlinePath).Replace([char]0, ' ').Trim()
            }

            [PSCustomObject]@{
                ProcessId = $_.Id
                CommandLine = $commandLine
            }
        }
}

Write-Host "[preflight] Suche blockierende dotnet Prozesse im Repo: $repoPath"

$processes = Get-DotnetProcesses |
    Where-Object {
        if (-not $_.CommandLine) {
            return $false
        }

        $commandLineNormalized = $_.CommandLine.Replace('\', '/')
        $targetsApiOrWeb = $false

        foreach ($project in $targetProjects) {
            if ($commandLineNormalized -like "*$project*") {
                $targetsApiOrWeb = $true
                break
            }
        }

        $isWatchOrRun = $_.CommandLine -match "(^|[\s""'])watch($|[\s""'])|(^|[\s""'])run($|[\s""'])"
        return $targetsApiOrWeb -and $isWatchOrRun
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

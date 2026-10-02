param([int]$TimeoutSeconds = 180)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$logsPath = Join-Path $projectRoot 'Logs/LEDWall'
New-Item -ItemType Directory -Force -Path $logsPath | Out-Null
function Invoke-UnityCommand([string]$Command, [string[]]$CommandArguments = @()) {
    $maximumAttempts = if ($Command -in @('recompile_status', 'test_status', 'list_tests')) { 4 } else { 1 }
    for ($attempt = 1; $attempt -le $maximumAttempts; $attempt++) {
        try {
            $response = & unity command $Command @CommandArguments --project-path $projectRoot --timeout 15 --json | ConvertFrom-Json
            if (!$response.success) { throw ($response.errors | ConvertTo-Json -Compress) }
            return $response.data.result
        } catch {
            if ($attempt -eq $maximumAttempts) { throw }
            Start-Sleep -Seconds 2
        }
    }
}
Invoke-UnityCommand 'recompile' | Out-Null
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
do {
    Start-Sleep -Seconds 2
    $compile = Invoke-UnityCommand 'recompile_status'
    if ($compile.failed -or $compile.compilationFailed -or $compile.status -eq 'failed') { throw ($compile.errors | ConvertTo-Json -Depth 10) }
    if ((Get-Date) -gt $deadline) { throw 'Compilation timed out.' }
} while ($compile.status -notin @('completed', 'up_to_date'))
foreach ($mode in @('editor', 'playmode')) {
    $inventory = Invoke-UnityCommand 'list_tests' @('--mode', $mode)
    Write-Output "$mode inventory: $($inventory.Count) tests"
    Invoke-UnityCommand 'run_tests' @('--mode', $mode, '--filter', 'Mizotake.LedWall.Tests', '--async_tests', 'true') | Out-Null
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Seconds 2
        $status = Invoke-UnityCommand 'test_status'
        if ((Get-Date) -gt $deadline) { throw "$mode tests timed out." }
    } while ($status.status -ne 'completed')
    $status | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $logsPath "$mode-tests.json") -Encoding utf8
    Write-Output "$mode tests: $($status.summary.passed)/$($status.summary.total) passed; $($status.summary.failed) failed; $($status.summary.skipped) skipped"
    if ($status.summary.failed -ne 0 -or $status.summary.total -eq 0 -or $status.summary.skipped -ne 0) { throw ($status.results | Where-Object Status -ne 'Passed' | ConvertTo-Json -Depth 6) }
}

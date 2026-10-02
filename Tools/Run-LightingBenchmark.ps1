param([ValidateRange(1, 5)][int]$Runs = 3)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path $PSScriptRoot -Parent
$executable = Join-Path $projectRoot 'Builds/LightingBenchmark/UnityLEDSystem.exe'
if (!(Test-Path -LiteralPath $executable)) { throw 'Run Build-LightingBenchmark.ps1 first.' }
foreach ($run in 1..$Runs) {
    foreach ($mode in @('Blit', 'Compute', 'Spill')) {
        $output = Join-Path $projectRoot "Logs/LEDWall/player-$mode-$run.json"
        $log = Join-Path $projectRoot "Logs/LEDWall/player-$mode-$run.log"
        $arguments = @('-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-force-d3d11', '-ledBenchmark', $mode, '-ledBenchmarkOutput', ('"' + $output + '"'), '-logFile', ('"' + $log + '"'))
        $process = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
        if (!$process.WaitForExit(45000)) { $process.Kill(); throw "Benchmark timed out: $mode, run $run; inspect $log" }
        if ($process.ExitCode -notin @(0, 2)) { throw "Benchmark exited with $($process.ExitCode): $mode, run $run; inspect $log" }
        $result = Get-Content -LiteralPath $output | ConvertFrom-Json
        $bitmap = [Drawing.Bitmap]::new([IO.Path]::ChangeExtension($output, '.png'))
        try {
            $colors = [Collections.Generic.HashSet[int]]::new()
            for ($y = 0; $y -lt $bitmap.Height; $y += 36) { for ($x = 0; $x -lt $bitmap.Width; $x += 64) { [void]$colors.Add($bitmap.GetPixel($x, $y).ToArgb()) } }
            if ($colors.Count -lt 20) { throw "The benchmark did not render a populated scene: $mode, run $run" }
        } finally { $bitmap.Dispose() }
        $gpuDescription = if ($result.gpuTimingSamples -gt 0) { "GPU mean $($result.gpuMeanMilliseconds.ToString('F3')) ms; p95 $($result.gpuP95Milliseconds.ToString('F3')) ms" } else { 'GPU timestamps unavailable in hidden player; CPU submission and memory evidence only' }
        Write-Output "$mode run $run : $gpuDescription; textures $($result.ownedTextureBytes) bytes; buffers $($result.partialBufferBytes) bytes; lights $($result.generatedLights)"
    }
}

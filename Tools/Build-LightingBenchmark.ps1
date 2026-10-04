param([int]$TimeoutSeconds = 240)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$instrumentRoot = Join-Path $projectRoot 'Assets/LEDWallBenchmarkInstrumentation'
if (Test-Path -LiteralPath $instrumentRoot) { throw 'Temporary benchmark instrumentation already exists; inspect it before retrying.' }
New-Item -ItemType Directory -Path $instrumentRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PlayerLightingBenchmark.cs') -Destination (Join-Path $instrumentRoot 'PlayerLightingBenchmark.cs')
$import = @'
UnityEditor.AssetDatabase.ImportAsset("Assets/LEDWallBenchmarkInstrumentation", UnityEditor.ImportAssetOptions.ImportRecursive | UnityEditor.ImportAssetOptions.ForceUpdate);
return "Benchmark instrumentation imported";
'@
& unity command eval --code $import --project-path $projectRoot --json | Out-Null
& unity command recompile --project-path $projectRoot --json | Out-Null
$deadline = (Get-Date).AddSeconds(90)
do {
    Start-Sleep -Seconds 2
    $compile = & unity command recompile_status --project-path $projectRoot --json | ConvertFrom-Json
    if ($compile.data.result.failed) { throw ($compile.data.result.errors | ConvertTo-Json -Depth 5) }
    if ((Get-Date) -gt $deadline) { throw 'Benchmark instrumentation compilation timed out.' }
} while ($compile.data.result.status -notin @('completed', 'up_to_date'))
$statusPath = Join-Path $projectRoot 'Logs/LEDWall/benchmark-build.json'
$buildStarted = Get-Date
$build = @'
var path = System.IO.Path.GetFullPath("Logs/LEDWall/benchmark-build.json");
UnityEditor.EditorApplication.CallbackFunction buildTick = null;
buildTick = () => {
    UnityEditor.EditorApplication.update -= buildTick;
    System.IO.File.WriteAllText(path, "{\"result\":\"Building\"}");
    var frameTiming = UnityEditor.PlayerSettings.enableFrameTimingStats;
    try {
        UnityEditor.PlayerSettings.enableFrameTimingStats = true;
        var report = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions { scenes = new[] { "Assets/LEDGallery/Scenes/LEDGallery.unity" }, locationPathName = "Builds/LightingBenchmark/UnityLEDKit.exe", target = UnityEditor.BuildTarget.StandaloneWindows64, options = UnityEditor.BuildOptions.Development });
        System.IO.File.WriteAllText(path, "{\"result\":\"" + report.summary.result + "\",\"errors\":" + report.summary.totalErrors + ",\"warnings\":" + report.summary.totalWarnings + "}");
    } catch (System.Exception e) { System.IO.File.WriteAllText(path, "{\"result\":\"Exception\"}"); UnityEngine.Debug.LogException(e); }
    finally { UnityEditor.PlayerSettings.enableFrameTimingStats = frameTiming; UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.AssetDatabase.DeleteAsset("Assets/LEDWallBenchmarkInstrumentation"); }
};
UnityEditor.EditorApplication.update += buildTick;
UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
return "Development build queued";
'@
$response = & unity command eval --code $build --project-path $projectRoot --json | ConvertFrom-Json
if (!$response.success) { throw ($response.errors | ConvertTo-Json -Depth 5) }
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
do {
    Start-Sleep -Seconds 2
    if ((Get-Date) -gt $deadline) { throw 'Development build timed out.' }
} while (!(Test-Path -LiteralPath $statusPath) -or (Get-Item -LiteralPath $statusPath).LastWriteTime -lt $buildStarted -or (Get-Content -LiteralPath $statusPath | ConvertFrom-Json).result -eq 'Building')
$result = Get-Content -LiteralPath $statusPath | ConvertFrom-Json
if ($result.result -ne 'Succeeded' -or $result.errors -ne 0) { throw ($result | ConvertTo-Json -Compress) }
$result | ConvertTo-Json -Compress

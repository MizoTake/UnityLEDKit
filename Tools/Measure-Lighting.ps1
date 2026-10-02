param([ValidatePattern('^[a-zA-Z0-9-]+$')][string]$Label = 'baseline', [int]$Seconds = 20)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $projectRoot "Logs/LEDWall/$Label-profile.json"
$measurementStarted = Get-Date
$code = @'
if (!UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Enter Play Mode before measuring.");
var sampling = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "LED Lighting.GPU area reduction", 4096);
var compute = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "LED Emission.Compute reduction", 4096);
var lights = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "LED Lighting.Update generated lights", 4096);
var gpu = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "GPU Frame Time", 4096);
var started = UnityEngine.Time.realtimeSinceStartup;
var startFrame = UnityEngine.Time.frameCount;
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () => {
    if (UnityEngine.Time.realtimeSinceStartup - started < SECONDS) return;
    UnityEditor.EditorApplication.update -= tick;
    var entries = new System.Collections.Generic.List<string>();
    var recorders = new[] { sampling, compute, lights, gpu };
    var names = new[] { "blitSubmission", "computeSubmission", "lightUpdate", "gpuFrame" };
    for (var index = 0; index < recorders.Length; index++) {
        var values = new System.Collections.Generic.List<Unity.Profiling.ProfilerRecorderSample>();
        if (recorders[index].Valid) recorders[index].CopyTo(values);
        var calls = values.Sum(v => (long)v.Count);
        var nanoseconds = values.Sum(v => v.Value);
        entries.Add("\"" + names[index] + "\":{\"samples\":" + values.Count + ",\"calls\":" + calls + ",\"meanMilliseconds\":" + (calls == 0 ? "null" : (nanoseconds / (double)calls / 1000000.0).ToString("R", System.Globalization.CultureInfo.InvariantCulture)) + "}");
        recorders[index].Dispose();
    }
    var targets = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.RenderTexture>().Where(t => t.name.StartsWith("LED Area Reduction") || t.name.StartsWith("LED Soft Emitter") || t.name.StartsWith("LED Emission")).ToArray();
    entries.Add("\"ownedTextureBytes\":" + targets.Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t)));
    entries.Add("\"ownedTextureCount\":" + targets.Length);
    entries.Add("\"generatedLights\":" + UnityEngine.Object.FindObjectsByType<Mizotake.LedWall.LedPanelLighting>(UnityEngine.FindObjectsSortMode.None).Sum(l => l.GeneratedLights == null ? 0 : l.GeneratedLights.Length));
    entries.Add("\"elapsedSeconds\":" + (UnityEngine.Time.realtimeSinceStartup - started).ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    entries.Add("\"renderedFrames\":" + (UnityEngine.Time.frameCount - startFrame));
    entries.Add("\"environment\":\"Editor Play Mode; CPU markers measure submission, not GPU kernel duration\"");
    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(@"OUTPUT"));
    System.IO.File.WriteAllText(@"OUTPUT", "{" + string.Join(",", entries) + "}", new System.Text.UTF8Encoding(false));
};
UnityEditor.EditorApplication.update += tick;
return "Measurement queued";
'@
$code = $code.Replace('SECONDS', $Seconds.ToString()).Replace('OUTPUT', $output)
$response = & unity command eval --code $code --project-path $projectRoot --json | ConvertFrom-Json
if (!$response.success -or !$response.data.result.success) { throw ($response | ConvertTo-Json -Depth 8) }
$deadline = (Get-Date).AddSeconds($Seconds + 45)
do {
    Start-Sleep -Seconds 2
    if ((Get-Date) -gt $deadline) { throw 'Measurement timed out.' }
} while (!(Test-Path -LiteralPath $output) -or (Get-Item -LiteralPath $output).LastWriteTime -lt $measurementStarted)
Get-Content -LiteralPath $output

param([ValidateRange(1, 30)][int]$FrameRate = 12, [ValidateRange(1, 30)][int]$DurationSeconds = 12, [string]$OutputPath = 'Documentation/Images/LitColorSpill.gif')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$captureRoot = Join-Path $projectRoot ('Temp/ReadmeCapture-' + [Guid]::NewGuid().ToString('N'))
$outputFile = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath))
New-Item -ItemType Directory -Force -Path $captureRoot, (Split-Path $outputFile -Parent) | Out-Null
$code = @'
if (!UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isPaused) throw new System.InvalidOperationException("Open LEDGallery and enter Play Mode before recording.");
var camera = Camera.main;
var panel = UnityEngine.Object.FindObjectsByType<Mizotake.LedWall.LedPanel>(FindObjectsSortMode.None).OrderByDescending(item => item.transform.lossyScale.x).First();
var video = panel.Source;
if (camera == null || video == null || !video.isPrepared) throw new System.InvalidOperationException("The gallery camera and prepared VideoPlayer are required.");
var root = "{{CAPTURE_ROOT}}";
System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "Frames"));
var originalPosition = camera.transform.position;
var originalRotation = camera.transform.rotation;
var originalAspect = camera.aspect;
var originalVideoTime = video.time;
var originalVideoPlaying = video.isPlaying;
var originalBackgroundMode = Application.runInBackground;
var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false, false);
target.Create();
var times = new System.Collections.Generic.List<double>();
var frames = 0;
var start = UnityEditor.EditorApplication.timeSinceStartup + 0.4;
var next = start;
var encoding = new System.Text.UTF8Encoding(false);
var culture = System.Globalization.CultureInfo.InvariantCulture;
System.IO.File.WriteAllText(System.IO.Path.Combine(root, "frames.csv"), "index,elapsedSeconds,videoFrame,minimumEmissionSamples\n", encoding);
camera.transform.position = new Vector3(1.1f, 4.7f, -12.5f);
camera.transform.LookAt(new Vector3(0.4f, 2.2f, -1f));
camera.aspect = 960f / 540f;
video.time = 0;
Application.runInBackground = true;
video.Play();
UnityEditor.EditorApplication.CallbackFunction tick = null;
System.Action<string> finish = state =>
{
    UnityEditor.EditorApplication.update -= tick;
    camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
    camera.aspect = originalAspect;
    if (video != null && video.isPrepared) { video.time = originalVideoTime; if (!originalVideoPlaying) video.Pause(); }
    Application.runInBackground = originalBackgroundMode;
    target.Release();
    UnityEngine.Object.Destroy(target);
    UnityEngine.Object.Destroy(pixels);
    var concat = new System.Text.StringBuilder("ffconcat version 1.0\n");
    for (var index = 0; index < frames; index++)
    {
        concat.AppendLine("file 'Frames/frame-" + index.ToString("D4") + ".png'");
        concat.AppendLine("duration " + (index + 1 < frames ? times[index + 1] - times[index] : 1.0 / {{FPS}}).ToString("R", culture));
    }
    if (frames > 0) concat.AppendLine("file 'Frames/frame-" + (frames - 1).ToString("D4") + ".png'");
    System.IO.File.WriteAllText(System.IO.Path.Combine(root, "frames.ffconcat"), concat.ToString(), encoding);
    System.IO.File.WriteAllText(System.IO.Path.Combine(root, "status.json"), "{\"state\":\"" + state + "\",\"frames\":" + frames + ",\"durationSeconds\":" + (times.Count > 0 ? times[times.Count - 1] - times[0] : 0).ToString("R", culture) + "}", encoding);
};
tick = () =>
{
    try
    {
        var now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now < next) return;
        if (!UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isPaused) throw new System.InvalidOperationException("Play Mode ended or paused while recording.");
        if (now - start >= {{DURATION}}) { finish("complete"); return; }
        var previous = RenderTexture.active;
        try
        {
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0, false);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(root, "Frames", "frame-" + frames.ToString("D4") + ".png"), ImageConversion.EncodeToPNG(pixels));
        }
        finally { RenderTexture.active = previous; }
        times.Add(now - start);
        var samples = UnityEngine.Object.FindObjectsByType<Mizotake.LedWall.LedPanelLighting>(FindObjectsSortMode.None).Where(item => item.isActiveAndEnabled).Select(item => item.CompletedSamples)
            .Concat(UnityEngine.Object.FindObjectsByType<Mizotake.LedWall.LedColorSpill>(FindObjectsSortMode.None).Where(item => item.isActiveAndEnabled).Select(item => item.SubmittedSamples)).DefaultIfEmpty(0).Min();
        System.IO.File.AppendAllText(System.IO.Path.Combine(root, "frames.csv"), string.Format(culture, "{0},{1:R},{2},{3}\n", frames, now - start, video.frame, samples), encoding);
        frames++;
        next = now + 1.0 / {{FPS}};
    }
    catch (System.Exception exception) { System.IO.File.WriteAllText(System.IO.Path.Combine(root, "error.txt"), exception.ToString(), encoding); finish("failed"); }
};
UnityEditor.EditorApplication.update += tick;
return "Gallery recording started";
'@
$code = $code.Replace('{{CAPTURE_ROOT}}', $captureRoot.Replace('\', '\\').Replace('"', '\"')).Replace('{{FPS}}', $FrameRate.ToString()).Replace('{{DURATION}}', $DurationSeconds.ToString())
$response = & unity command eval --code $code --project-path $projectRoot --json | ConvertFrom-Json
if (!$response.success -or !$response.data.result.success) { throw ($response | ConvertTo-Json -Depth 10) }
$deadline = (Get-Date).AddSeconds($DurationSeconds + 45)
while (!(Test-Path -LiteralPath (Join-Path $captureRoot 'status.json'))) { if ((Get-Date) -gt $deadline) { throw 'Gallery recording timed out.' }; Start-Sleep -Milliseconds 250 }
$status = Get-Content -LiteralPath (Join-Path $captureRoot 'status.json') -Raw | ConvertFrom-Json
if ($status.state -ne 'complete' -or $status.frames -lt $DurationSeconds * $FrameRate / 2) { throw "Recording failed: $captureRoot" }
$sourceFrames = Import-Csv -LiteralPath (Join-Path $captureRoot 'frames.csv')
if (($sourceFrames.videoFrame | Sort-Object -Unique).Count -lt $DurationSeconds * $FrameRate / 4) { throw "The source video did not advance enough to record motion: $captureRoot" }
& ffmpeg -hide_banner -loglevel warning -y -f concat -safe 0 -i (Join-Path $captureRoot 'frames.ffconcat') -filter_complex "fps=$FrameRate,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle" -t $DurationSeconds -loop 0 $outputFile
if ($LASTEXITCODE -ne 0) { throw 'GIF encoding failed.' }
Write-Output "Captured $($status.frames) real Unity frames; encoded $DurationSeconds seconds at $FrameRate fps: $outputFile"
Write-Output "Capture timestamps and source video frame numbers: $captureRoot"

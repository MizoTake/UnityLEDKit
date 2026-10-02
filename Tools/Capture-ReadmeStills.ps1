param([string]$OutputDirectory = 'Assets/LEDGallery/Preview')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
$statusFile = Join-Path $projectRoot ('Temp/Stills-' + [Guid]::NewGuid().ToString('N') + '.txt')
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$code = @'
if (!UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isPaused) throw new System.InvalidOperationException("Open LEDGallery in Play Mode before capturing.");
var camera = Camera.main;
var panels = UnityEngine.Object.FindObjectsByType<Mizotake.LedWall.LedPanel>(FindObjectsSortMode.None);
var primary = panels.OrderByDescending(item => item.transform.lossyScale.x).First();
var sources = panels.Select(item => item.Source).ToArray();
var secondarySources = panels.Select(item => item.SecondarySource).ToArray();
var position = camera.transform.position;
var rotation = camera.transform.rotation;
var aspect = camera.aspect;
var orthographic = camera.orthographic;
var size = camera.orthographicSize;
var cameraData = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(camera);
var post = cameraData.renderPostProcessing;
var texture = primary.Texture;
var secondary = primary.SecondaryTexture;
var brightness = primary.BrightnessValue;
var diffusion = primary.DiffusionValue;
var transition = primary.Transition;
var background = Application.runInBackground;
var encoding = new System.Text.UTF8Encoding(false);
var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false, false);
target.Create();
Application.runInBackground = true;
for (var index = 0; index < panels.Length; index++) { panels[index].Source = null; panels[index].SecondarySource = null; panels[index].Apply(); }
var deadline = UnityEditor.EditorApplication.timeSinceStartup + 0.8;
UnityEditor.EditorApplication.CallbackFunction tick = null;
System.Action restore = () =>
{
    UnityEditor.EditorApplication.update -= tick;
    camera.transform.SetPositionAndRotation(position, rotation);
    camera.aspect = aspect;
    camera.orthographic = orthographic;
    camera.orthographicSize = size;
    cameraData.renderPostProcessing = post;
    primary.Texture = texture; primary.SecondaryTexture = secondary; primary.BrightnessValue = brightness; primary.DiffusionValue = diffusion; primary.Transition = transition;
    for (var index = 0; index < panels.Length; index++) { panels[index].Source = sources[index]; panels[index].SecondarySource = secondarySources[index]; panels[index].Apply(); }
    Application.runInBackground = background;
    target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels);
};
System.Action<string> capture = path =>
{
    var previous = RenderTexture.active;
    try
    {
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0, false);
        System.IO.File.WriteAllBytes(path, ImageConversion.EncodeToPNG(pixels));
    }
    finally { RenderTexture.active = previous; }
};
tick = () =>
{
    if (UnityEditor.EditorApplication.timeSinceStartup < deadline) return;
    try
    {
        if (!UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Play Mode ended during capture.");
        camera.aspect = 1600f / 900f;
        camera.orthographic = false;
        camera.transform.position = new Vector3(1.1f, 4.7f, -12.5f);
        camera.transform.LookAt(new Vector3(0.4f, 2.2f, -1f));
        capture("{{OUTPUT}}/Gallery.png");
        primary.Texture = Texture2D.grayTexture; primary.SecondaryTexture = Texture2D.grayTexture; primary.Transition = 0; primary.BrightnessValue = 0.9f; primary.DiffusionValue = 0; primary.Apply();
        camera.orthographic = true;
        camera.orthographicSize = 0.24f;
        camera.transform.SetPositionAndRotation(primary.transform.position - primary.transform.forward * 0.6f, primary.transform.rotation);
        cameraData.renderPostProcessing = false;
        capture("{{OUTPUT}}/RgbMacro.png");
        System.IO.File.WriteAllText("{{STATUS}}", "complete", encoding);
    }
    catch (System.Exception exception) { System.IO.File.WriteAllText("{{STATUS}}", exception.ToString(), encoding); }
    finally { restore(); }
};
UnityEditor.EditorApplication.update += tick;
return "Unity still capture started; gallery uses original poster and macro uses gray calibration input";
'@
$code = $code.Replace('{{OUTPUT}}', $outputRoot.Replace('\', '\\').Replace('"', '\"')).Replace('{{STATUS}}', $statusFile.Replace('\', '\\').Replace('"', '\"'))
$response = & unity command eval --code $code --project-path $projectRoot --json | ConvertFrom-Json
if (!$response.success -or !$response.data.result.success) { throw ($response | ConvertTo-Json -Depth 10) }
$deadline = (Get-Date).AddSeconds(30)
while (!(Test-Path -LiteralPath $statusFile)) { if ((Get-Date) -gt $deadline) { throw 'Still capture timed out.' }; Start-Sleep -Milliseconds 250 }
$state = Get-Content -LiteralPath $statusFile -Raw
if ($state -ne 'complete') { throw $state }
Write-Output "Unity stills captured and scene settings restored: $outputRoot"

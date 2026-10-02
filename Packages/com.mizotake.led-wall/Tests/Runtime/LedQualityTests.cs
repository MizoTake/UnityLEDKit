using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Mizotake.LedWall.Tests
{
    public sealed class LedQualityTests
    {
        [UnityTest]
        public IEnumerator TintAndMaterialUvTransformMatchDisplayedEmissionAndLighting()
        {
            if (!SystemInfo.supportsAsyncGPUReadback || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) Assert.Ignore("URP with GPU readback is required.");
            using (var fixture = new RenderFixture())
            {
                var source = new Texture2D(32, 32, TextureFormat.RGBA32, false, true);
                try
                {
                    var pixels = new Color[32 * 32];
                    for (var y = 0; y < 32; y++) for (var x = 0; x < 32; x++) pixels[y * 32 + x] = x < 16 ? Color.red : Color.blue;
                    source.SetPixels(pixels); source.Apply();
                    fixture.Material.SetTextureScale("_BaseMap", new Vector2(0.5f, 1f));
                    fixture.Material.SetTextureOffset("_BaseMap", new Vector2(0.5f, 0));
                    var panel = fixture.Emitter.AddComponent<LedPanel>();
                    panel.Texture = source;
                    panel.Tint = new Color(0.7f, 0.4f, 0.5f);
                    panel.BrightnessValue = 2f;
                    var lighting = fixture.Emitter.AddComponent<LedPanelLighting>();
                    lighting.SamplingShader = Shader.Find("Hidden/Mizotake/LED Wall/Lighting Sampler");
                    lighting.LightGrid = Vector2Int.one;
                    lighting.ResponseSeconds = 0f;
                    var deadline = Time.realtimeSinceStartup + 10f;
                    while (lighting.CompletedSamples < 2 && Time.realtimeSinceStartup < deadline) yield return null;
                    var sampled = lighting.GeneratedLights[0].color.linear;
                    Assert.That(sampled.b, Is.EqualTo(panel.EmissionTint.b).Within(0.015f));
                    Assert.That(sampled.r, Is.LessThan(0.015f));
                    panel.Apply();
                    var shown = fixture.CaptureMean();
                    Assert.That(shown.b, Is.EqualTo(sampled.b * panel.BrightnessValue).Within(0.02f));
                    Assert.That(lighting.GeneratedLights[0].cookie, Is.SameAs(lighting.CookieTexture));
                    var cookie = AsyncGPUReadback.Request(lighting.CookieTexture, 0, TextureFormat.RGBAFloat);
                    while (!cookie.done) yield return null;
                    Assert.That(cookie.hasError, Is.False);
                    var cookiePixels = cookie.GetData<Color>();
                    Assert.That(cookiePixels[32 * 64 + 32].r, Is.GreaterThan(0.95f));
                    Assert.That(cookiePixels[0].r, Is.LessThan(0.01f));
                    var previousSamples = lighting.CompletedSamples;
                    panel.ContentRect = new Vector4(0.5f, 1, -0.25f, 0);
                    deadline = Time.realtimeSinceStartup + 10f;
                    while (lighting.CompletedSamples < previousSamples + 2 && Time.realtimeSinceStartup < deadline) yield return null;
                    sampled = lighting.GeneratedLights[0].color.linear;
                    Assert.That(sampled.r, Is.EqualTo(panel.EmissionTint.r).Within(0.015f));
                    Assert.That(sampled.b, Is.LessThan(0.015f));
                    lighting.SoftDistribution = false;
                    yield return null;
                    Assert.That(lighting.GeneratedLights[0].cookie, Is.Null);
                    lighting.enabled = false;
                    Assert.That(lighting.CookieTexture, Is.Null);
                }
                finally { Object.Destroy(source); }
            }
        }

        [UnityTest]
        public IEnumerator SameTextureHardCutResetsHistoryImmediately()
        {
            if (!SystemInfo.supportsAsyncGPUReadback) Assert.Ignore("GPU readback is required.");
            var emitter = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var source = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            try
            {
                source.SetPixel(0, 0, Color.red); source.Apply();
                var panel = emitter.AddComponent<LedPanel>(); panel.Texture = source;
                var lighting = emitter.AddComponent<LedPanelLighting>();
                lighting.SamplingShader = Shader.Find("Hidden/Mizotake/LED Wall/Lighting Sampler");
                lighting.LightGrid = Vector2Int.one;
                lighting.ResponseSeconds = 0.5f;
                var deadline = Time.realtimeSinceStartup + 10;
                while (lighting.CompletedSamples < 2 && Time.realtimeSinceStartup < deadline) yield return null;
                var samples = lighting.CompletedSamples;
                var resets = lighting.HistoryResets;
                source.SetPixel(0, 0, Color.blue); source.Apply();
                deadline = Time.realtimeSinceStartup + 10;
                while (lighting.CompletedSamples < samples + 2 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(lighting.HistoryResets, Is.GreaterThan(resets));
                Assert.That(lighting.GeneratedLights[0].color.linear.b, Is.GreaterThan(0.99f));
                Assert.That(lighting.GeneratedLights[0].color.linear.r, Is.LessThan(0.01f));
            }
            finally { Object.Destroy(emitter); Object.Destroy(source); }
        }

        [UnityTest]
        public IEnumerator BothEmitterStylesPreserveMeanRadianceAcrossFillAndDistance()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) Assert.Ignore("URP is required.");
            using (var fixture = new RenderFixture())
            {
                yield return null;
                foreach (var style in new[] { "Mizotake/LED Wall/RGB LED", "Mizotake/LED Wall/Diffused LED" })
                {
                    fixture.Material.shader = Shader.Find(style);
                    fixture.ConfigureEmission();
                    foreach (var fill in new[] { 0.35f, 0.72f, 0.9f }) foreach (var diffusion in new[] { 0f, 0.7f })
                    {
                        fixture.Material.SetFloat("_Fill", fill);
                        fixture.Material.SetFloat("_Diffusion", diffusion);
                        fixture.Material.SetVector("_LedResolution", new Vector4(1, 1, 0, 0));
                        var near = fixture.CaptureMean();
                        fixture.Material.SetVector("_LedResolution", new Vector4(256, 256, 0, 0));
                        var far = fixture.CaptureMean();
                        foreach (var channel in new[] { near.r, near.g, near.b }) Assert.That(channel, Is.EqualTo(1f).Within(0.035f), style + ", fill=" + fill + ", diffusion=" + diffusion);
                        Assert.That(near.r, Is.EqualTo(far.r).Within(0.035f));
                        Assert.That(near.g, Is.EqualTo(far.g).Within(0.035f));
                        Assert.That(near.b, Is.EqualTo(far.b).Within(0.035f));
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator UnlitVideoAndLitHousingRespondIndependentlyToEnvironmentLight()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) Assert.Ignore("URP is required.");
            using (var fixture = new RenderFixture())
            {
                var lightObject = new GameObject("Physical LED surface test light");
                try
                {
                    var light = lightObject.AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.cullingMask = 1 << 30;
                    light.intensity = 0f;
                    fixture.Material.SetTexture("_BaseMap", Texture2D.blackTexture);
                    fixture.Material.SetTexture("_BlendMap", Texture2D.blackTexture);
                    fixture.Material.SetColor("_HousingColor", new Color(0.3f, 0.3f, 0.3f));
                    fixture.Material.SetFloat("_SurfaceLighting", 1f);
                    fixture.Material.SetVector("_LedResolution", new Vector4(8, 8, 0, 0));
                    yield return null;
                    var dark = fixture.CaptureMean();
                    light.intensity = 2f;
                    yield return null;
                    var illuminated = fixture.CaptureMean();
                    Assert.That(illuminated.grayscale - dark.grayscale, Is.GreaterThan(0.015f), "The non-emissive panel surface must receive scene lighting.");
                    fixture.Material.SetFloat("_SurfaceLighting", 0f);
                    var unlit = fixture.CaptureMean();
                    light.intensity = 0f;
                    yield return null;
                    Assert.That(fixture.CaptureMean().grayscale, Is.EqualTo(unlit.grayscale).Within(0.001f));
                }
                finally { Object.Destroy(lightObject); }
            }
        }

        private sealed class RenderFixture : System.IDisposable
        {
            public readonly GameObject Emitter = GameObject.CreatePrimitive(PrimitiveType.Quad);
            public readonly Material Material = new Material(Shader.Find("Mizotake/LED Wall/RGB LED"));
            private readonly GameObject cameraObject = new GameObject("LED quality GPU camera");
            private readonly RenderTexture target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            private readonly Texture2D pixels = new Texture2D(256, 256, TextureFormat.RGBAFloat, false, true);
            private readonly Camera camera;

            public RenderFixture()
            {
                Emitter.layer = 30;
                Emitter.GetComponent<Renderer>().sharedMaterial = Material;
                camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -2);
                camera.orthographic = true;
                camera.orthographicSize = 0.5f;
                camera.aspect = 1f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << 30;
                camera.allowHDR = true;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                target.Create();
                ConfigureEmission();
            }

            public void ConfigureEmission()
            {
                Material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                Material.SetTexture("_BlendMap", Texture2D.whiteTexture);
                Material.SetColor("_HousingColor", Color.black);
                Material.SetColor("_Tint", Color.white);
                Material.SetFloat("_Brightness", 1);
                Material.SetFloat("_ViewingAngle", 0);
                Material.SetFloat("_SurfaceLighting", 0);
            }

            public Color CaptureMean()
            {
                var previous = RenderTexture.active;
                try
                {
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0, false);
                    pixels.Apply();
                    var sum = Color.clear;
                    foreach (var color in pixels.GetPixels()) sum += color;
                    return sum / (256 * 256);
                }
                finally { RenderTexture.active = previous; }
            }

            void System.IDisposable.Dispose()
            {
                target.Release();
                Object.Destroy(Emitter); Object.Destroy(Material); Object.Destroy(cameraObject); Object.Destroy(target); Object.Destroy(pixels);
            }
        }

        [UnityTest]
        public IEnumerator RegionAverageIncludesBrightDetailsBetweenOldSamplingPoints()
        {
            if (!SystemInfo.supportsAsyncGPUReadback) Assert.Ignore("GPU readback is required.");
            var emitter = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var source = new Texture2D(64, 64, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
            try
            {
                var pixels = new Color[64 * 64];
                for (var y = 0; y < 64; y++) for (var x = 20; x < 28; x++) pixels[y * 64 + x] = Color.red;
                source.SetPixels(pixels);
                source.Apply();
                var panel = emitter.AddComponent<LedPanel>();
                panel.Texture = source;
                var lighting = emitter.AddComponent<LedPanelLighting>();
                lighting.SamplingShader = Shader.Find("Hidden/Mizotake/LED Wall/Lighting Sampler");
                lighting.LightGrid = Vector2Int.one;
                var deadline = Time.realtimeSinceStartup + 10f;
                while (lighting.CompletedSamples < 2 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(lighting.LastError, Is.Null);
                Assert.That(lighting.CompletedSamples, Is.GreaterThanOrEqualTo(2));
                var average = QualitySettings.activeColorSpace == ColorSpace.Linear ? lighting.GeneratedLights[0].color.linear : lighting.GeneratedLights[0].color;
                Assert.That(average.r, Is.EqualTo(8f / 64f).Within(0.015f), "A small bright stripe must contribute its covered area to the emitted light.");
                Assert.That(average.g + average.b, Is.LessThan(0.001f));
            }
            finally { Object.Destroy(emitter); Object.Destroy(source); }
        }
    }
}

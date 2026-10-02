using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Mizotake.LedWall.Tests
{
    public sealed class LedComputeAndSpillTests
    {
        [UnityTest]
        public IEnumerator ComputeMatchesBlitForHdrFadeCropAndUnevenRegionGrid()
        {
            RequireGpu();
            var emitter = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = new Material(Shader.Find("Mizotake/LED Wall/RGB LED"));
            var first = new Texture2D(63, 35, TextureFormat.RGBAFloat, true, true) { wrapMode = TextureWrapMode.Clamp };
            var second = new Texture2D(17, 23, TextureFormat.RGBAFloat, true, true) { wrapMode = TextureWrapMode.Clamp };
            var sampler = new LedEmissionSampler();
            try
            {
                emitter.GetComponent<Renderer>().sharedMaterial = material;
                var pixels = new Color[63 * 35];
                for (var y = 0; y < 35; y++) for (var x = 0; x < 63; x++) pixels[y * 63 + x] = new Color(x / 15f, y / 17f, x >= 20 && x < 28 ? 2f : 0);
                first.SetPixels(pixels); first.Apply();
                second.SetPixels(Enumerable.Repeat(new Color(0.25f, 0.4f, 3f), 17 * 23).ToArray()); second.Apply();
                var panel = emitter.AddComponent<LedPanel>();
                panel.Texture = first; panel.SecondaryTexture = second; panel.Transition = 0.37f;
                panel.Tint = new Color(0.8f, 0.7f, 0.9f);
                panel.ContentRect = new Vector4(0.65f, 0.8f, 0.1f, 0.05f);
                material.SetTextureScale("_BaseMap", new Vector2(0.9f, 0.9f));
                var lighting = emitter.AddComponent<LedPanelLighting>();
                lighting.SamplingShader = Shader.Find("Hidden/Mizotake/LED Wall/Lighting Sampler");
                lighting.SamplingBackend = LedSamplingBackend.Blit;
                lighting.LightGrid = new Vector2Int(3, 2); lighting.ResponseSeconds = 0;
                var deadline = Time.realtimeSinceStartup + 10;
                while (lighting.CompletedSamples < 2 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(lighting.CompletedSamples, Is.GreaterThanOrEqualTo(2));
                sampler.Ensure(LedEmissionSampler.DefaultShader, lighting.LightGrid, lighting.SamplingSize);
                sampler.Sample(panel);
                var request = AsyncGPUReadback.Request(sampler.Output, 0, TextureFormat.RGBAFloat);
                while (!request.done) yield return null;
                Assert.That(request.hasError, Is.False);
                var actual = request.GetData<Color>().ToArray();
                for (var index = 0; index < actual.Length; index++)
                {
                    var expected = QualitySettings.activeColorSpace == ColorSpace.Linear ? lighting.GeneratedLights[index].color.linear : lighting.GeneratedLights[index].color;
                    Assert.That(actual[index].r, Is.EqualTo(expected.r).Within(0.008f));
                    Assert.That(actual[index].g, Is.EqualTo(expected.g).Within(0.008f));
                    Assert.That(actual[index].b, Is.EqualTo(expected.b).Within(0.008f));
                }
                Assert.That(sampler.BufferBytes, Is.LessThan(100000));
                lighting.SamplingBackend = LedSamplingBackend.Compute;
                var previous = lighting.CompletedSamples;
                deadline = Time.realtimeSinceStartup + 10;
                while (lighting.CompletedSamples < previous + 2 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(lighting.LastError, Is.Null);
                for (var index = 0; index < actual.Length; index++) Assert.That(lighting.GeneratedLights[index].color.linear.b, Is.EqualTo(actual[index].b).Within(0.008f));
            }
            finally { ((IDisposable)sampler).Dispose(); Object.Destroy(emitter); Object.Destroy(material); Object.Destroy(first); Object.Destroy(second); }
        }

        [UnityTest]
        public IEnumerator RendererFeatureColorsOrdinaryLitWithoutLightsAndHonorsReceiverMask()
        {
            RequireGpu();
            using (var fixture = new SpillFixture())
            {
                fixture.Spill.ReceiverMask = 1 << 29;
                yield return null;
                var baseline = fixture.CaptureMean();
                fixture.Spill.ReceiverMask = 1 << 30;
                yield return null;
                var red = fixture.CaptureMean();
                Assert.That(red.r - baseline.r, Is.GreaterThan(0.02f));
                Assert.That(red.b - baseline.b, Is.LessThan(0.005f));
                fixture.Emitter.transform.rotation = Quaternion.Euler(0, 180, 0);
                Assert.That(fixture.CaptureMean().r, Is.EqualTo(baseline.r).Within(0.005f), "The back of a one-sided LED panel must not emit color.");
                fixture.Emitter.transform.rotation = Quaternion.identity;
                fixture.Spill.OverrideDistribution = true;
                fixture.Spill.Range = 0.1f;
                Assert.That(fixture.CaptureMean().r, Is.EqualTo(baseline.r).Within(0.005f));
                fixture.Spill.OverrideDistribution = false;
                fixture.Panel.Transition = 1;
                var samples = fixture.Spill.SubmittedSamples;
                while (fixture.Spill.SubmittedSamples == samples) yield return null;
                var blue = fixture.CaptureMean();
                Assert.That(blue.b - baseline.b, Is.GreaterThan(0.02f));
                Assert.That(blue.r - baseline.r, Is.LessThan(0.005f));
                fixture.Panel.BrightnessValue = 0;
                var zero = fixture.CaptureMean();
                Assert.That(zero.r, Is.EqualTo(baseline.r).Within(0.005f));
                Assert.That(zero.b, Is.EqualTo(baseline.b).Within(0.005f));
                Assert.That(fixture.Emitter.GetComponentsInChildren<Light>(true), Is.Empty);
                Assert.That(fixture.Receiver.GetComponent<Renderer>().sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(fixture.Spill.LastError, Is.Null);
                fixture.Spill.enabled = false;
                Assert.That(fixture.Spill.EmissionTexture, Is.Null);
                Assert.That(fixture.Spill.BufferBytes, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator SpillTracksSameTextureCutsAndPanelDisable()
        {
            RequireGpu();
            using (var fixture = new SpillFixture())
            {
                fixture.Spill.ResponseSeconds = 0.5f;
                yield return null;
                var request = AsyncGPUReadback.Request(fixture.Spill.EmissionTexture, 0, TextureFormat.RGBAFloat);
                while (!request.done) yield return null;
                Assert.That(request.GetData<Color>()[0].r, Is.GreaterThan(0.99f));
                fixture.First.SetPixel(0, 0, Color.blue); fixture.First.Apply();
                var samples = fixture.Spill.SubmittedSamples;
                while (fixture.Spill.SubmittedSamples == samples) yield return null;
                request = AsyncGPUReadback.Request(fixture.Spill.EmissionTexture, 0, TextureFormat.RGBAFloat);
                while (!request.done) yield return null;
                Assert.That(request.GetData<Color>()[0].b, Is.GreaterThan(0.99f));
                fixture.Panel.enabled = false;
                var off = fixture.CaptureMean();
                fixture.Panel.enabled = true;
                var on = fixture.CaptureMean();
                Assert.That(on.b - off.b, Is.GreaterThan(0.02f));
            }
        }

        private static void RequireGpu()
        {
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsAsyncGPUReadback || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) Assert.Ignore("URP, Compute Shader and GPU readback are required.");
        }

        private sealed class SpillFixture : IDisposable
        {
            public readonly GameObject Emitter = GameObject.CreatePrimitive(PrimitiveType.Quad);
            public readonly GameObject Receiver = GameObject.CreatePrimitive(PrimitiveType.Plane);
            public readonly Texture2D First = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            private readonly Texture2D second = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            private readonly Material led = new Material(Shader.Find("Mizotake/LED Wall/RGB LED"));
            private readonly Material lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            private readonly GameObject cameraObject = new GameObject("LED Spill GPU Test Camera");
            private readonly RenderTexture target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            private readonly Texture2D pixels = new Texture2D(128, 128, TextureFormat.RGBAFloat, false, true);
            private readonly ScriptableRendererData renderer;
            private readonly LedColorSpillRendererFeature feature;
            private readonly ScriptableRendererFeature[] previousFeatures;
            private readonly bool[] previousActive;
            private readonly Camera camera;
            public readonly LedPanel Panel;
            public readonly LedColorSpill Spill;

            public SpillFixture()
            {
                var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
                renderer = ((ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pipeline))[0];
                previousFeatures = renderer.rendererFeatures.OfType<LedColorSpillRendererFeature>().Cast<ScriptableRendererFeature>().ToArray();
                previousActive = previousFeatures.Select(item => item.isActive).ToArray();
                foreach (var item in previousFeatures) item.SetActive(false);
                feature = ScriptableObject.CreateInstance<LedColorSpillRendererFeature>();
                feature.SpillShader = Shader.Find("Hidden/Mizotake/LED Wall/Color Spill");
                renderer.rendererFeatures.Add(feature); renderer.SetDirty();
                Emitter.layer = Receiver.layer = 30;
                Emitter.transform.position = new Vector3(0, 2, 1);
                Emitter.transform.localScale = new Vector3(2, 2, 1);
                Emitter.GetComponent<Renderer>().sharedMaterial = led;
                // Test only the receiver; rendering the emitter would contaminate mean RGB measurements.
                Emitter.GetComponent<Renderer>().enabled = false;
                Receiver.transform.position = new Vector3(0, 0, -1);
                Receiver.transform.localScale = new Vector3(0.3f, 1, 0.3f);
                Receiver.GetComponent<Renderer>().sharedMaterial = lit;
                First.SetPixel(0, 0, Color.red); First.Apply(); second.SetPixel(0, 0, Color.blue); second.Apply();
                Panel = Emitter.AddComponent<LedPanel>(); Panel.Texture = First; Panel.SecondaryTexture = second; Panel.BrightnessValue = 2;
                Spill = Emitter.AddComponent<LedColorSpill>(); Spill.ReceiverMask = 1 << 30; Spill.EmitterGrid = Vector2Int.one; Spill.ResponseSeconds = 0; Spill.ScreenOcclusion = 0; Spill.Strength = 5;
                camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.transform.position = new Vector3(0, 3, -4); camera.transform.LookAt(new Vector3(0, 0, -1));
                camera.orthographic = true; camera.orthographicSize = 1.8f; camera.aspect = 1;
                camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.allowHDR = true;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                target.Create();
            }

            public Color CaptureMean()
            {
                var previous = RenderTexture.active;
                try
                {
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0, false); pixels.Apply();
                    var sum = Color.clear;
                    foreach (var value in pixels.GetPixels()) sum += value;
                    return sum / (128 * 128);
                }
                finally { RenderTexture.active = previous; }
            }

            void IDisposable.Dispose()
            {
                renderer.rendererFeatures.Remove(feature);
                for (var index = 0; index < previousFeatures.Length; index++) previousFeatures[index].SetActive(previousActive[index]);
                renderer.SetDirty();
                target.Release();
                Object.Destroy(feature); Object.Destroy(Emitter); Object.Destroy(Receiver); Object.Destroy(cameraObject);
                Object.Destroy(First); Object.Destroy(second); Object.Destroy(led); Object.Destroy(lit); Object.Destroy(target); Object.Destroy(pixels);
            }
        }
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Mizotake.LedWall.Tests
{
    public sealed class LedLightingTests
    {
        [UnityTest]
        public IEnumerator DualTextureFadeDrivesActualLightsAndAnOrdinaryUrpLitReceiver()
        {
            if (!SystemInfo.supportsAsyncGPUReadback || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) Assert.Ignore("This integration test needs URP and asynchronous GPU readback.");
            var emitter = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var cameraObject = new GameObject("URP Lit Color Receiver Test");
            var first = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            var second = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            var ledMaterial = new Material(Shader.Find("Mizotake/LED Wall/RGB LED"));
            var litMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var target = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGBHalf);
            var pixels = new Texture2D(96, 96, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active;
            try
            {
                first.SetPixel(0, 0, Color.red);
                first.Apply();
                second.SetPixel(0, 0, Color.blue);
                second.Apply();
                emitter.layer = 30;
                emitter.transform.position = new Vector3(0, 2, 1);
                emitter.GetComponent<Renderer>().sharedMaterial = ledMaterial;
                var panel = emitter.AddComponent<LedPanel>();
                panel.Texture = first;
                panel.SecondaryTexture = second;
                panel.BrightnessValue = 2f;
                var lighting = emitter.AddComponent<LedPanelLighting>();
                lighting.LightGrid = Vector2Int.one;
                lighting.SamplingShader = Shader.Find("Hidden/Mizotake/LED Wall/Lighting Sampler");
                lighting.OverrideLightParameters = true;
                lighting.DownwardBias = 1f;
                lighting.Intensity = 8f;
                receiver.layer = 30;
                receiver.GetComponent<Renderer>().sharedMaterial = litMaterial;
                litMaterial.SetColor("_BaseColor", Color.white);
                litMaterial.SetFloat("_Smoothness", 0f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = 1 << 30;
                camera.transform.position = new Vector3(0, 2, -4);
                camera.transform.LookAt(new Vector3(0, 0, -1));
                camera.allowHDR = true;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                target.Create();
                var timeout = Time.realtimeSinceStartup + 10f;
                while (lighting.CompletedSamples < 2 && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(lighting.LastError, Is.Null);
                Assert.That(lighting.CompletedSamples, Is.GreaterThanOrEqualTo(2));
                Assert.That(lighting.GeneratedLights.Length, Is.EqualTo(1));
                Assert.That(lighting.GeneratedLights[0].color.linear.r, Is.GreaterThan(0.9f));
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 96, 96), 0, 0);
                pixels.Apply();
                var redReceiver = pixels.GetPixel(48, 48);
                lighting.RenderingLayers = 2u;
                yield return null;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 96, 96), 0, 0);
                pixels.Apply();
                Assert.That(redReceiver.r - pixels.GetPixel(48, 48).r, Is.GreaterThan(0.05f), "A different rendering layer must exclude the ordinary Lit receiver.");
                lighting.RenderingLayers = 1u;
                panel.Transition = 1f;
                var previousCount = lighting.CompletedSamples;
                timeout = Time.realtimeSinceStartup + 10f;
                while (lighting.CompletedSamples < previousCount + 3 && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(lighting.GeneratedLights[0].color.linear.b, Is.GreaterThan(0.9f));
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 96, 96), 0, 0);
                pixels.Apply();
                var blueReceiver = pixels.GetPixel(48, 48);
                Assert.That(redReceiver.r - blueReceiver.r, Is.GreaterThan(0.05f), "The ordinary URP Lit receiver must lose red illumination when the LED changes to blue.");
                Assert.That(blueReceiver.b - redReceiver.b, Is.GreaterThan(0.05f), "The ordinary URP Lit receiver must gain blue illumination.");
                panel.Transition = 0.5f;
                previousCount = lighting.CompletedSamples;
                timeout = Time.realtimeSinceStartup + 10f;
                while (lighting.CompletedSamples < previousCount + 3 && Time.realtimeSinceStartup < timeout) yield return null;
                var mixed = lighting.GeneratedLights[0].color.linear;
                Assert.That(mixed.r, Is.EqualTo(0.5f).Within(0.03f));
                Assert.That(mixed.b, Is.EqualTo(0.5f).Within(0.03f));
                camera.transform.SetPositionAndRotation(new Vector3(0, 2, -1), Quaternion.identity);
                camera.orthographic = true;
                camera.orthographicSize = 0.5f;
                camera.aspect = 1f;
                foreach (var fade in new[] { 0f, 0.5f, 1f })
                {
                    panel.Transition = fade;
                    panel.Apply();
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 96, 96), 0, 0);
                    pixels.Apply();
                    var displayed = pixels.GetPixel(48, 48);
                    Assert.That(displayed.r, Is.EqualTo(2f * (1f - fade)).Within(0.06f));
                    Assert.That(displayed.b, Is.EqualTo(2f * fade).Within(0.06f));
                }
                lighting.enabled = false;
                Assert.That(lighting.GeneratedLights, Is.Null);
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(emitter);
                Object.Destroy(receiver);
                Object.Destroy(cameraObject);
                Object.Destroy(first);
                Object.Destroy(second);
                Object.Destroy(ledMaterial);
                Object.Destroy(litMaterial);
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        [UnityTest]
        public IEnumerator AutomaticLightingFollowsPanelAndOverridesAndMasksSurviveRegeneration()
        {
            if (!SystemInfo.supportsAsyncGPUReadback) Assert.Ignore("Asynchronous GPU readback is required.");
            var emitter = GameObject.CreatePrimitive(PrimitiveType.Quad);
            try
            {
                var panel = emitter.AddComponent<LedPanel>();
                panel.Texture = Texture2D.whiteTexture;
                panel.BrightnessValue = 2f;
                emitter.transform.localScale = new Vector3(4f, 3f, 1f);
                var lighting = emitter.AddComponent<LedPanelLighting>();
                lighting.SamplingShader = Shader.Find("Hidden/Mizotake/LED Wall/Lighting Sampler");
                lighting.LightGrid = Vector2Int.one;
                lighting.Intensity = 19f;
                lighting.Range = 29f;
                var timeout = Time.realtimeSinceStartup + 10f;
                while (lighting.CompletedSamples < 2 && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(lighting.CompletedSamples, Is.GreaterThanOrEqualTo(2));
                Assert.That(lighting.OverrideLightParameters, Is.False);
                Assert.That(lighting.GeneratedLights[0].intensity, Is.EqualTo(120f).Within(0.001f));
                Assert.That(lighting.GeneratedLights[0].range, Is.EqualTo(6.5f).Within(0.001f));
                panel.BrightnessValue = 3f;
                emitter.transform.localScale = new Vector3(8f, 6f, 1f);
                yield return null;
                Assert.That(lighting.GeneratedLights[0].intensity, Is.EqualTo(720f).Within(0.001f));
                Assert.That(lighting.GeneratedLights[0].range, Is.EqualTo(13f).Within(0.001f));
                lighting.OverrideLightParameters = true;
                lighting.Range = 7f;
                lighting.SpotAngle = 90f;
                lighting.InnerSpotRatio = 0.4f;
                lighting.CastShadows = true;
                lighting.CullingMask = 1 << 30;
                lighting.RenderingLayers = 2u;
                lighting.RenderMode = LightRenderMode.Auto;
                lighting.ShadowStrength = 0.6f;
                lighting.ShadowBias = 0.2f;
                lighting.ShadowNormalBias = 0.3f;
                lighting.ShadowNearPlane = 0.25f;
                lighting.CustomShadowLayers = true;
                lighting.ShadowRenderingLayers = 4u;
                yield return null;
                Assert.That(lighting.GeneratedLights[0].range, Is.EqualTo(7f));
                Assert.That(lighting.GeneratedLights[0].intensity, Is.EqualTo(19f * 3f * 48f));
                Assert.That(lighting.GeneratedLights[0].GetUniversalAdditionalLightData().shadowRenderingLayers, Is.EqualTo(4u));
                lighting.LightGrid = new Vector2Int(2, 1);
                lighting.CullingMask = 1 << 29;
                lighting.RenderingLayers = 8u;
                timeout = Time.realtimeSinceStartup + 10f;
                var count = lighting.CompletedSamples;
                while ((lighting.GeneratedLights.Length != 2 || lighting.CompletedSamples < count + 2) && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(lighting.GeneratedLights.Length, Is.EqualTo(2));
                foreach (var light in lighting.GeneratedLights)
                {
                    Assert.That(light.cullingMask, Is.EqualTo(1 << 29));
                    Assert.That(light.GetUniversalAdditionalLightData().renderingLayers, Is.EqualTo(8u));
                    Assert.That(light.GetUniversalAdditionalLightData().customShadowLayers, Is.True);
                    Assert.That(light.GetUniversalAdditionalLightData().shadowRenderingLayers, Is.EqualTo(4u));
                    Assert.That(light.renderMode, Is.EqualTo(LightRenderMode.Auto));
                    Assert.That(light.range, Is.EqualTo(7f));
                    Assert.That(light.spotAngle, Is.EqualTo(90f));
                    Assert.That(light.innerSpotAngle, Is.EqualTo(36f));
                    Assert.That(light.shadows, Is.EqualTo(LightShadows.Soft));
                    Assert.That(light.shadowStrength, Is.EqualTo(0.6f));
                    Assert.That(light.shadowBias, Is.EqualTo(0.2f));
                    Assert.That(light.shadowNormalBias, Is.EqualTo(0.3f));
                    Assert.That(light.shadowNearPlane, Is.EqualTo(0.25f));
                    Assert.That(light.intensity, Is.EqualTo(19f * 3f * 48f / 2f));
                }
                lighting.OverrideLightParameters = false;
                yield return null;
                Assert.That(lighting.GeneratedLights[0].range, Is.EqualTo(13f).Within(0.001f));
                lighting.enabled = false;
                Assert.That(lighting.GeneratedLights, Is.Null);
            }
            finally { Object.Destroy(emitter); }
        }
    }
}

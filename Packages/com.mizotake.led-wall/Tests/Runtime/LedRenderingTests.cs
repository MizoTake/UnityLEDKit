using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Mizotake.LedWall.Tests
{
    public sealed class LedRenderingTests
    {
        [UnityTest]
        public IEnumerator PlanarReflectionContainsColoredGeometryAfterGpuBlurAndReleasesResources()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) Assert.Ignore("URP must be active for the reflection test.");
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var emitter = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var cameraObject = new GameObject("LED Reflection Test Camera");
            var material = new Material(Shader.Find("Mizotake/LED Wall/RGB LED"));
            var sourceTarget = new RenderTexture(192, 192, 24);
            Texture2D pixels = null;
            var previous = RenderTexture.active;
            var previousCulling = GL.invertCulling;
            try
            {
                plane.layer = 4;
                emitter.layer = 30;
                emitter.transform.position = new Vector3(0, 1, 2);
                emitter.GetComponent<Renderer>().sharedMaterial = material;
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                material.SetColor("_Tint", Color.red);
                material.SetFloat("_RgbSeparation", 0f);
                material.SetFloat("_Diffusion", 1f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 2, -4);
                camera.transform.LookAt(new Vector3(0, 0, 2));
                camera.backgroundColor = Color.black;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.targetTexture = sourceTarget;
                sourceTarget.Create();
                var reflection = plane.AddComponent<LedPlanarReflection>();
                reflection.SourceCamera = camera;
                reflection.BlurShader = Shader.Find("Hidden/Mizotake/LED Wall/Reflection Blur");
                yield return null;
                Assert.That(reflection.RenderReflection(camera), Is.True);
                Assert.That(GL.invertCulling, Is.EqualTo(previousCulling));
                Assert.That(plane.GetComponent<Renderer>().forceRenderingOff, Is.False);
                var reflected = reflection.ReflectionTextureValue;
                RenderTexture.active = reflected;
                pixels = new Texture2D(reflected.width, reflected.height, TextureFormat.RGBAFloat, false, true);
                pixels.ReadPixels(new Rect(0, 0, reflected.width, reflected.height), 0, 0);
                pixels.Apply();
                var maximumRed = 0f;
                var minimumRed = float.MaxValue;
                foreach (var pixel in pixels.GetPixels()) { maximumRed = Mathf.Max(maximumRed, pixel.r); minimumRed = Mathf.Min(minimumRed, pixel.r); }
                Assert.That(maximumRed - minimumRed, Is.GreaterThan(0.1f), "Reflection blur must preserve the colored emitter instead of returning a uniform texture.");
                reflection.enabled = false;
                Assert.That(reflection.ReflectionTextureValue, Is.Null);
                Assert.That(reflected.IsCreated(), Is.False);
            }
            finally
            {
                RenderTexture.active = previous;
                sourceTarget.Release();
                Object.Destroy(plane);
                Object.Destroy(emitter);
                Object.Destroy(cameraObject);
                Object.Destroy(material);
                Object.Destroy(sourceTarget);
                if (pixels != null) Object.Destroy(pixels);
            }
        }

        [UnityTest]
        public IEnumerator RgbShaderRendersThreeSeparateEmitterColorsOnGpu()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) Assert.Ignore("URP must be active for the GPU render test.");
            var item = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var cameraObject = new GameObject("LED GPU Test Camera");
            var material = new Material(Shader.Find("Mizotake/LED Wall/RGB LED"));
            var target = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGBHalf);
            var pixels = new Texture2D(96, 96, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active;
            try
            {
                item.layer = 30;
                item.GetComponent<Renderer>().sharedMaterial = material;
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                material.SetVector("_LedResolution", new Vector4(1, 1, 0, 0));
                material.SetFloat("_Brightness", 1f);
                material.SetFloat("_Diffusion", 0f);
                material.SetFloat("_ViewingAngle", 0f);
                material.SetColor("_HousingColor", Color.black);
                var camera = cameraObject.AddComponent<Camera>();
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
                yield return null;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 96, 96), 0, 0);
                pixels.Apply();
                var red = pixels.GetPixel(16, 48);
                var green = pixels.GetPixel(48, 48);
                var blue = pixels.GetPixel(80, 48);
                Assert.That(red.r, Is.GreaterThan(1f));
                Assert.That(red.g + red.b, Is.LessThan(0.05f));
                Assert.That(green.g, Is.GreaterThan(1f));
                Assert.That(green.r + green.b, Is.LessThan(0.05f));
                Assert.That(blue.b, Is.GreaterThan(1f));
                Assert.That(blue.r + blue.g, Is.LessThan(0.05f));
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(item);
                Object.Destroy(cameraObject);
                Object.Destroy(material);
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        [UnityTest]
        public IEnumerator StandardVideoPlayerOutputIsSharedAndPlaybackRemainsUserControlled()
        {
            #if UNITY_EDITOR
            var clips = UnityEditor.AssetDatabase.FindAssets("NeonOrbits t:VideoClip");
            if (clips.Length == 0) Assert.Ignore("Import LED Gallery to run the original-video integration test.");
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(clips[0]));
            var item = GameObject.CreatePrimitive(PrimitiveType.Quad);
            item.SetActive(false);
            var source = item.AddComponent<UnityEngine.Video.VideoPlayer>();
            source.clip = clip;
            source.renderMode = UnityEngine.Video.VideoRenderMode.APIOnly;
            source.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;
            source.playOnAwake = false;
            source.isLooping = true;
            var panel = item.AddComponent<LedPanel>();
            panel.Source = source;
            panel.Texture = Texture2D.grayTexture;
            var previousBackgroundMode = Application.runInBackground;
            Application.runInBackground = true;
            try
            {
                item.SetActive(true);
                Assert.That(panel.PrimaryOutput, Is.SameAs(Texture2D.grayTexture));
                source.Play();
                var timeout = Time.realtimeSinceStartup + 20f;
                while (panel.PrimaryOutput == Texture2D.grayTexture && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(source.isPrepared, Is.True);
                Assert.That(panel.PrimaryOutput, Is.SameAs(source.texture));
                Assert.That(source.frame, Is.GreaterThanOrEqualTo(0));
                var frame = source.frame;
                var progressTimeout = Time.realtimeSinceStartup + 10f;
                while (source.frame <= frame && Time.realtimeSinceStartup < progressTimeout) yield return null;
                Assert.That(source.frame, Is.GreaterThan(frame));
                source.Pause();
                Assert.That(source.isPaused, Is.True);
                frame = source.frame;
                yield return new WaitForSeconds(0.15f);
                Assert.That(source.frame, Is.EqualTo(frame));
                source.Play();
                panel.enabled = false;
                Assert.That(source.isPlaying, Is.True, "Disabling a panel must not stop a shared VideoPlayer.");
                progressTimeout = Time.realtimeSinceStartup + 10f;
                while (source.frame <= frame && Time.realtimeSinceStartup < progressTimeout) yield return null;
                Assert.That(source.frame, Is.GreaterThan(frame));
                source.Stop();
                Assert.That(panel.PrimaryOutput, Is.SameAs(Texture2D.grayTexture));
            }
            finally { Application.runInBackground = previousBackgroundMode; Object.Destroy(item); }
            #else
            Assert.Ignore("Video sample integration test uses Editor asset discovery.");
            yield return null;
            #endif
        }
    }
}

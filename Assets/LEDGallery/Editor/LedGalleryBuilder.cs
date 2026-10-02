using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace Mizotake.LedWall.Samples.Editor
{
    public static class LedGalleryBuilder
    {
        private const string UrpRoot = "Packages/com.unity.render-pipelines.universal";

        public static string SampleRoot
        {
            get
            {
                foreach (var guid in AssetDatabase.FindAssets("LedGalleryBuilder t:MonoScript"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileName(path) == "LedGalleryBuilder.cs") return Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace('\\', '/');
                }
                throw new FileNotFoundException("The LED Gallery sample builder was not found in Assets.");
            }
        }

        [MenuItem("Tools/LED Wall/Create or Rebuild Gallery")]
        public static void Build()
        {
            var root = SampleRoot;
            Directory.CreateDirectory(root + "/Materials");
            Directory.CreateDirectory(root + "/Settings");
            Directory.CreateDirectory(root + "/Scenes");
            AssetDatabase.ImportAsset(root, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
            var pipeline = CreatePipeline(root);
            var rgb = MaterialAsset(root, "RGB LED", "Mizotake/LED Wall/RGB LED");
            var soft = MaterialAsset(root, "Diffused LED", "Mizotake/LED Wall/Diffused LED");
            var floorMaterial = MaterialAsset(root, "Lit Color Receiver", "Universal Render Pipeline/Lit");
            floorMaterial.SetColor("_BaseColor", new Color(0.09f, 0.105f, 0.13f));
            floorMaterial.SetFloat("_Metallic", 0.05f);
            floorMaterial.SetFloat("_Smoothness", 0.28f);
            var metal = MaterialAsset(root, "Graphite Frame", "Universal Render Pipeline/Lit");
            metal.SetColor("_BaseColor", new Color(0.035f, 0.046f, 0.061f));
            metal.SetFloat("_Metallic", 0.72f);
            metal.SetFloat("_Smoothness", 0.55f);
            var dark = MaterialAsset(root, "Stage", "Universal Render Pipeline/Lit");
            dark.SetColor("_BaseColor", new Color(0.018f, 0.025f, 0.038f));
            dark.SetFloat("_Metallic", 0.3f);
            var cyan = MaterialAsset(root, "Cyan Trim", "Universal Render Pipeline/Unlit");
            cyan.SetColor("_BaseColor", new Color(0.04f, 1.1f, 1.7f));
            var amber = MaterialAsset(root, "Amber Trim", "Universal Render Pipeline/Unlit");
            amber.SetColor("_BaseColor", new Color(1.6f, 0.45f, 0.06f));
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(root + "/Media/NeonOrbits.mp4");
            var poster = AssetDatabase.LoadAssetAtPath<Texture2D>(root + "/Media/NeonOrbits.png");
            var secondary = AssetDatabase.LoadAssetAtPath<Texture2D>(root + "/Media/Prism.png");
            if (clip == null || poster == null) throw new FileNotFoundException("Generate or import the NeonOrbits media before building the sample.");
            if (secondary == null) throw new FileNotFoundException("Generate or import Prism.png before building the sample.");
            rgb.SetTexture("_BaseMap", poster);
            soft.SetTexture("_BaseMap", poster);
            rgb.SetTexture("_BlendMap", secondary);
            soft.SetTexture("_BlendMap", secondary);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var stage = new GameObject("LED Gallery");
            var cameraObject = new GameObject("Gallery Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(stage.transform);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.009f, 0.015f, 0.026f);
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 100f;
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            var videoObject = new GameObject("Shared Video Player");
            videoObject.transform.SetParent(stage.transform);
            var video = videoObject.AddComponent<VideoPlayer>();
            video.clip = clip;
            video.renderMode = VideoRenderMode.APIOnly;
            video.audioOutputMode = VideoAudioOutputMode.None;
            video.playOnAwake = true;
            video.isLooping = true;
            video.waitForFirstFrame = true;
            video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            var main = Panel(stage.transform, "01 / RGB LED", new Vector3(-1.9f, 3.65f, 0f), new Vector2(8f, 4.5f), rgb, metal);
            main.Source = video;
            main.Texture = poster;
            main.SecondaryTexture = secondary;
            main.Resolution = new Vector2Int(256, 144);
            main.BrightnessValue = 2.5f;
            main.DiffusionValue = 0.1f;
            var second = Panel(stage.transform, "02 / Diffused LED", new Vector3(5.2f, 2.6f, 0.4f), new Vector2(4f, 2.25f), soft, metal);
            second.Source = video;
            second.Texture = poster;
            second.SecondaryTexture = secondary;
            second.Resolution = new Vector2Int(128, 72);
            second.BrightnessValue = 1.8f;
            second.DiffusionValue = 0.75f;
            var floorObject = Primitive(stage.transform, "URP Lit Color Receiver Floor", PrimitiveType.Plane, new Vector3(0, 0, -5), new Vector3(3.2f, 1f, 3.2f), floorMaterial);
            floorObject.layer = 4;
            var rgbLighting = main.gameObject.AddComponent<LedPanelLighting>();
            rgbLighting.SamplingShader = Shader.Find("Hidden/Mizotake/LED Wall/Lighting Sampler");
            var softLighting = second.gameObject.AddComponent<LedPanelLighting>();
            softLighting.SamplingShader = Shader.Find("Hidden/Mizotake/LED Wall/Lighting Sampler");
            softLighting.LightGrid = new Vector2Int(3, 2);
            Primitive(stage.transform, "Lit Receiver Cube", PrimitiveType.Cube, new Vector3(-5f, 0.42f, -3f), new Vector3(0.7f, 0.84f, 0.7f), floorMaterial);
            Primitive(stage.transform, "Lit Receiver Sphere", PrimitiveType.Sphere, new Vector3(1.3f, 0.45f, -2.7f), Vector3.one * 0.9f, floorMaterial);
            Primitive(stage.transform, "RGB Plinth", PrimitiveType.Cube, new Vector3(-1.9f, 0.21f, 0.28f), new Vector3(8.7f, 0.42f, 1.05f), dark);
            Primitive(stage.transform, "Soft Plinth", PrimitiveType.Cube, new Vector3(5.2f, 0.21f, 0.5f), new Vector3(4.65f, 0.42f, 1.05f), dark);
            Primitive(stage.transform, "RGB Accent", PrimitiveType.Cube, new Vector3(-1.9f, 0.43f, -0.3f), new Vector3(8.55f, 0.025f, 0.035f), cyan);
            Primitive(stage.transform, "Soft Accent", PrimitiveType.Cube, new Vector3(5.2f, 0.43f, 0f), new Vector3(4.55f, 0.025f, 0.035f), amber);
            Primitive(stage.transform, "Backdrop", PrimitiveType.Cube, new Vector3(0f, 4.1f, 2.8f), new Vector3(25f, 9f, 0.2f), dark);
            Label(stage.transform, "01    RGB / DIRECT VIEW", new Vector3(-5.87f, 1.03f, -0.05f), 0.15f, new Color(0.50f, 0.78f, 0.86f));
            Label(stage.transform, "256 x 144   /   THREE EMITTERS PER CELL", new Vector3(-5.87f, 0.75f, -0.05f), 0.084f, new Color(0.28f, 0.40f, 0.50f));
            Label(stage.transform, "02    DIFFUSED / SOFT LENS", new Vector3(3.23f, 1.06f, 0.32f), 0.12f, new Color(0.92f, 0.67f, 0.39f));
            Label(stage.transform, "128 x 72   /   OPTICAL DIFFUSION", new Vector3(3.23f, 0.82f, 0.32f), 0.078f, new Color(0.49f, 0.38f, 0.29f));
            var key = LightObject(stage.transform, "Soft Key", LightType.Directional, new Vector3(0, 6, -3), new Color(0.69f, 0.79f, 1f), 0.55f, 20f);
            key.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
            key.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.11f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogDensity = 0.016f;
            var volumeObject = new GameObject("Bloom and Tonemapping");
            volumeObject.transform.SetParent(stage.transform);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = CreateVolume(root);
            camera.transform.position = new Vector3(1.1f, 3.8f, -14.8f);
            camera.transform.LookAt(new Vector3(0.4f, 2.9f, 0f));
            main.Apply();
            second.Apply();
            var scenePath = root + "/Scenes/LEDGallery.unity";
            var existing = SceneManager.GetSceneByPath(scenePath);
            if (existing.IsValid() && existing.isLoaded && existing != scene)
            {
                if (existing.isDirty) throw new System.InvalidOperationException("Save the existing gallery scene before rebuilding it.");
                EditorSceneManager.CloseScene(existing, true);
            }
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = stage;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.LookAt(new Vector3(0, 3, 0), Quaternion.Euler(8f, 0f, 0f), 14f);
            Debug.Log($"LED Gallery created: {scenePath}; pipeline: {AssetDatabase.GetAssetPath(pipeline)}");
        }

        [MenuItem("Tools/LED Wall/Use Gallery URP Settings")]
        public static void UseGalleryPipeline()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(SampleRoot + "/Settings/LEDGalleryURP.asset");
            if (asset == null) throw new FileNotFoundException("Build the gallery before applying its URP settings.");
            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;
            AssetDatabase.SaveAssets();
        }

        private static UniversalRenderPipelineAsset CreatePipeline(string root)
        {
            var rendererPath = root + "/Settings/LEDGalleryRenderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(UrpRoot + "/Runtime/Data/PostProcessData.asset");
                ResourceReloader.ReloadAllNullIn(renderer, UrpRoot);
                var rendererSettings = new SerializedObject(renderer);
                rendererSettings.FindProperty("m_RenderingMode").intValue = (int)RenderingMode.ForwardPlus;
                rendererSettings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(renderer, rendererPath);
                var occlusion = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                occlusion.name = "Gallery Ambient Occlusion";
                ResourceReloader.ReloadAllNullIn(occlusion, UrpRoot);
                var settings = new SerializedObject(occlusion);
                settings.FindProperty("m_Settings.Intensity").floatValue = 0.3f;
                settings.FindProperty("m_Settings.Radius").floatValue = 0.2f;
                settings.FindProperty("m_Settings.Downsample").boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.AddObjectToAsset(occlusion, renderer);
                renderer.rendererFeatures.Add(occlusion);
                EditorUtility.SetDirty(renderer);
            }
            var pipelinePath = root + "/Settings/LEDGalleryURP.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null) { pipeline = UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(pipeline, pipelinePath); }
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = true;
            pipeline.useSRPBatcher = true;
            var pipelineSettings = new SerializedObject(pipeline);
            pipelineSettings.FindProperty("m_SupportsLightLayers").boolValue = true;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            pipeline.shadowDistance = 35f;
            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        private static VolumeProfile CreateVolume(string root)
        {
            var path = root + "/Settings/LEDGalleryVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.3f);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.65f);
            bloom.highQualityFiltering.Override(true);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.14f);
            vignette.smoothness.Override(0.55f);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Material MaterialAsset(string root, string name, string shaderName)
        {
            var path = root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.InvalidOperationException("Shader not found: " + shaderName);
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static LedPanel Panel(Transform parent, string name, Vector3 center, Vector2 size, Material display, Material frame)
        {
            Primitive(parent, name + " / Cabinet", PrimitiveType.Cube, center + new Vector3(0, 0, 0.14f), new Vector3(size.x + 0.16f, size.y + 0.16f, 0.28f), frame);
            var screen = Primitive(parent, name, PrimitiveType.Quad, center + new Vector3(0, 0, -0.011f), new Vector3(size.x, size.y, 1f), display);
            screen.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return screen.AddComponent<LedPanel>();
        }

        private static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent);
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            var collider = item.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return item;
        }

        private static void Label(Transform parent, string text, Vector3 position, float size, Color color)
        {
            var item = new GameObject(text);
            item.transform.SetParent(parent);
            item.transform.position = position;
            var label = item.AddComponent<TextMesh>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 64;
            label.characterSize = size / 4f;
            label.color = color;
            label.anchor = TextAnchor.MiddleLeft;
            item.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        }

        private static Light LightObject(Transform parent, string name, LightType type, Vector3 position, Color color, float intensity, float range)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent);
            item.transform.position = position;
            var light = item.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }
    }
}

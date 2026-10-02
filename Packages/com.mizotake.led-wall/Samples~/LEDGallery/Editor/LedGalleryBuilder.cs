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
            Directory.CreateDirectory(root + "/Surfaces");
            Directory.CreateDirectory(root + "/Geometry");
            AssetDatabase.ImportAsset(root, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
            var pipeline = CreatePipeline(root);
            var rgb = MaterialAsset(root, "RGB LED", "Mizotake/LED Wall/RGB LED");
            var soft = MaterialAsset(root, "Diffused LED", "Mizotake/LED Wall/Diffused LED");
            var floorMaterial = MaterialAsset(root, "Lit Color Receiver", "Universal Render Pipeline/Lit");
            floorMaterial.SetColor("_BaseColor", new Color(0.23f, 0.24f, 0.27f));
            floorMaterial.SetFloat("_Metallic", 0.05f);
            floorMaterial.SetFloat("_Smoothness", 0.28f);
            var metal = MaterialAsset(root, "Graphite Frame", "Universal Render Pipeline/Lit");
            metal.SetColor("_BaseColor", new Color(0.035f, 0.046f, 0.061f));
            metal.SetFloat("_Metallic", 0.72f);
            metal.SetFloat("_Smoothness", 0.55f);
            var fabric = MaterialAsset(root, "Woven Fabric Receiver", "Universal Render Pipeline/Lit");
            fabric.SetColor("_BaseColor", new Color(0.27f, 0.29f, 0.32f));
            fabric.SetFloat("_Smoothness", 0.1f);
            ApplySurface(root, floorMaterial, "Floor", 0, new Vector2(16, 16));
            ApplySurface(root, fabric, "Fabric", 1, new Vector2(2, 2));
            ApplySurface(root, metal, "BrushedMetal", 2, Vector2.one);
            rgb.SetFloat("_SurfaceLighting", 1f);
            rgb.SetFloat("_LensCurvature", 0.45f);
            rgb.SetFloat("_LensSmoothness", 0.82f);
            rgb.SetFloat("_HousingSmoothness", 0.2f);
            soft.SetFloat("_SurfaceLighting", 1f);
            soft.SetFloat("_LensCurvature", 0.55f);
            soft.SetFloat("_LensSmoothness", 0.86f);
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
            BeveledBox(stage.transform, "Lit Receiver Cube", new Vector3(-5f, 0.42f, -3f), new Vector3(0.7f, 0.84f, 0.7f), 0.055f, floorMaterial);
            Primitive(stage.transform, "Brushed Metal Receiver Sphere", PrimitiveType.Sphere, new Vector3(1.3f, 0.45f, -2.7f), Vector3.one * 0.9f, metal);
            BeveledBox(stage.transform, "RGB Plinth", new Vector3(-1.9f, 0.21f, 0.28f), new Vector3(8.7f, 0.42f, 1.05f), 0.065f, dark);
            BeveledBox(stage.transform, "Soft Plinth", new Vector3(5.2f, 0.21f, 0.5f), new Vector3(4.65f, 0.42f, 1.05f), 0.065f, dark);
            BeveledBox(stage.transform, "Left Lit Wall", new Vector3(-7.1f, 1.5f, -2.2f), new Vector3(0.18f, 3f, 4.5f), 0.035f, floorMaterial);
            BeveledBox(stage.transform, "Woven Fabric Side Panel", new Vector3(7.8f, 1.25f, -2.3f), new Vector3(0.12f, 2.5f, 3.2f), 0.025f, fabric);
            Primitive(stage.transform, "RGB Accent", PrimitiveType.Cube, new Vector3(-1.9f, 0.43f, -0.3f), new Vector3(8.55f, 0.025f, 0.035f), cyan);
            Primitive(stage.transform, "Soft Accent", PrimitiveType.Cube, new Vector3(5.2f, 0.43f, 0f), new Vector3(4.55f, 0.025f, 0.035f), amber);
            Primitive(stage.transform, "Backdrop", PrimitiveType.Cube, new Vector3(0f, 4.1f, 2.8f), new Vector3(25f, 9f, 0.2f), dark);
            Label(stage.transform, "01    RGB / DIRECT VIEW", new Vector3(-5.87f, 1.03f, -0.05f), 0.15f, new Color(0.50f, 0.78f, 0.86f));
            Label(stage.transform, "256 x 144   /   THREE EMITTERS PER CELL", new Vector3(-5.87f, 0.75f, -0.05f), 0.084f, new Color(0.28f, 0.40f, 0.50f));
            Label(stage.transform, "02    DIFFUSED / SOFT LENS", new Vector3(3.23f, 1.06f, 0.32f), 0.12f, new Color(0.92f, 0.67f, 0.39f));
            Label(stage.transform, "128 x 72   /   OPTICAL DIFFUSION", new Vector3(3.23f, 0.82f, 0.32f), 0.078f, new Color(0.49f, 0.38f, 0.29f));
            var key = LightObject(stage.transform, "Soft Key", LightType.Directional, new Vector3(0, 6, -3), new Color(0.69f, 0.79f, 1f), 0.35f, 20f);
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
            camera.transform.position = new Vector3(1.1f, 4.7f, -12.5f);
            camera.transform.LookAt(new Vector3(0.4f, 2.2f, -1f));
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
            var created = profile == null;
            if (created) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            if (!profile.TryGet<Bloom>(out var bloom)) bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.18f);
            bloom.threshold.Override(1.4f);
            bloom.scatter.Override(0.45f);
            bloom.highQualityFiltering.Override(true);
            if (!profile.TryGet<Tonemapping>(out var tone)) tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            if (!profile.TryGet<Vignette>(out var vignette)) vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.14f);
            vignette.smoothness.Override(0.55f);
            foreach (var component in profile.components) if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
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
            BeveledBox(parent, name + " / Cabinet", center + new Vector3(0, 0, 0.14f), new Vector3(size.x + 0.16f, size.y + 0.16f, 0.28f), 0.045f, frame);
            var screen = Primitive(parent, name, PrimitiveType.Quad, center + new Vector3(0, 0, -0.011f), new Vector3(size.x, size.y, 1f), display);
            screen.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return screen.AddComponent<LedPanel>();
        }

        private static void ApplySurface(string root, Material material, string name, int kind, Vector2 tiling)
        {
            var shader = Shader.Find("Hidden/Mizotake/LED Gallery/Surface Maps");
            if (shader == null) throw new System.InvalidOperationException("Import the GallerySurface shader before rebuilding.");
            var generator = new Material(shader);
            var target = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(1024, 1024, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            var properties = new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" };
            try
            {
                generator.SetFloat("_SurfaceKind", kind);
                target.Create();
                for (var pass = 0; pass < properties.Length; pass++)
                {
                    var path = root + "/Surfaces/" + name + "-" + pass + ".png";
                    Graphics.Blit(Texture2D.whiteTexture, target, generator, pass);
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0, false);
                    File.WriteAllBytes(path, ImageConversion.EncodeToPNG(pixels));
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = pass == 1 ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    importer.sRGBTexture = pass == 0;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.anisoLevel = 8;
                    importer.mipmapEnabled = true;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.SaveAndReimport();
                    material.SetTexture(properties[pass], AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                }
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(generator);
            }
            material.SetTextureScale("_BaseMap", tiling);
            material.SetFloat("_BumpScale", kind == 0 ? 0.65f : 1f);
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            var evidence = new SurfaceEvidence();
            evidence.files = System.Array.ConvertAll(Directory.GetFiles(root + "/Surfaces", "*.png"), path =>
            {
                using (var hash = System.Security.Cryptography.SHA256.Create()) return new SurfaceFile { path = Path.GetFileName(path), sha256 = System.BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant() };
            });
            System.Array.Sort(evidence.files, (a, b) => string.CompareOrdinal(a.path, b.path));
            var provenance = root + "/Surfaces/PROVENANCE.json";
            File.WriteAllText(provenance, JsonUtility.ToJson(evidence, true) + "\n", new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(provenance, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        [System.Serializable]
        private sealed class SurfaceEvidence
        {
            public string origin = "Original procedural surface maps generated on the GPU";
            public string generator = "Shaders/GallerySurface.shader and Editor/LedGalleryBuilder.cs";
            public int resolution = 1024;
            public bool thirdPartyImages = false;
            public string license = "No additional license granted; see the package LICENSE.md";
            public SurfaceFile[] files;
        }

        [System.Serializable]
        private sealed class SurfaceFile { public string path; public string sha256; }

        private static GameObject BeveledBox(Transform parent, string name, Vector3 position, Vector3 size, float bevel, Material material)
        {
            var path = SampleRoot + "/Geometry/" + name.Replace('/', '-').Replace(' ', '_') + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = name + " Beveled Mesh" }; AssetDatabase.CreateAsset(mesh, path); }
            else mesh.Clear();
            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();
            var half = size * 0.5f;
            bevel = Mathf.Min(bevel, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.75f);
            var inner = half - Vector3.one * bevel;
            foreach (var face in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                var u = Vector3.Cross(face, Mathf.Abs(face.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
                var v = Vector3.Cross(face, u);
                var hu = Vector3.Dot(new Vector3(Mathf.Abs(u.x), Mathf.Abs(u.y), Mathf.Abs(u.z)), half);
                var hv = Vector3.Dot(new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)), half);
                var hf = Vector3.Dot(new Vector3(Mathf.Abs(face.x), Mathf.Abs(face.y), Mathf.Abs(face.z)), half);
                var coordinatesU = BevelCoordinates(hu, bevel);
                var coordinatesV = BevelCoordinates(hv, bevel);
                var offset = vertices.Count;
                for (var y = 0; y < 8; y++) for (var x = 0; x < 8; x++)
                {
                    var point = face * hf + u * coordinatesU[x] + v * coordinatesV[y];
                    var closest = new Vector3(Mathf.Clamp(point.x, -inner.x, inner.x), Mathf.Clamp(point.y, -inner.y, inner.y), Mathf.Clamp(point.z, -inner.z, inner.z));
                    var normal = (point - closest).normalized;
                    vertices.Add(closest + normal * bevel);
                    normals.Add(normal);
                    uv.Add(new Vector2(coordinatesU[x] / (2 * hu) + 0.5f, coordinatesV[y] / (2 * hv) + 0.5f));
                }
                for (var y = 0; y < 7; y++) for (var x = 0; x < 7; x++)
                {
                    var a = offset + y * 8 + x;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 8);
                    triangles.Add(a + 1); triangles.Add(a + 9); triangles.Add(a + 8);
                }
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(parent);
            item.transform.position = position;
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            return item;
        }

        private static float[] BevelCoordinates(float half, float bevel) => new[] { -half, -half + bevel * 0.3f, -half + bevel * 0.7f, -half + bevel, half - bevel, half - bevel * 0.7f, half - bevel * 0.3f, half };

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

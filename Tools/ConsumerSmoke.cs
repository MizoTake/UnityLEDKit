using System;
using System.IO;
using System.Linq;
using Mizotake.LedWall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ConsumerSmoke
{
    [Serializable] private sealed class Result { public bool success; public string package; public string version; public string source; public string gitRevision; public int shaders; public int panels; public int videoPlayers; public string receiverShader; public string error; }

    public static void Run()
    {
        var result = new Result();
        try
        {
            var package = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Single(item => item.name == "com.mizotake.led-wall");
            result.package = package.name;
            result.version = package.version;
            result.source = package.source.ToString();
            result.gitRevision = package.git?.hash;
            foreach (var name in new[] { "Mizotake/LED Wall/RGB LED", "Mizotake/LED Wall/Diffused LED", "Mizotake/LED Wall/Reflective Floor", "Hidden/Mizotake/LED Wall/Reflection Blur", "Hidden/Mizotake/LED Wall/Lighting Sampler", "Hidden/Mizotake/LED Wall/Color Spill" })
            {
                var shader = Shader.Find(name);
                if (shader == null || !shader.isSupported) throw new InvalidOperationException("Shader unavailable: " + name);
                var errors = ShaderUtil.GetShaderMessages(shader).Where(message => message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(message => message.message).ToArray();
                if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors));
                result.shaders++;
            }
            EditorSceneManager.OpenScene("Assets/LEDGallery/Scenes/LEDGallery.unity");
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/LEDGallery/Settings/LEDGalleryURP.asset");
            if (pipeline == null) throw new InvalidOperationException("Sample URP asset is missing.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            var panels = UnityEngine.Object.FindObjectsByType<LedPanel>(FindObjectsSortMode.None);
            result.panels = panels.Length;
            if (panels.Length != 2 || panels.Any(panel => panel.Source == null || panel.Source.clip == null || panel.SecondaryTexture == null)) throw new InvalidOperationException("Sample panel texture/video references are incomplete.");
            result.videoPlayers = UnityEngine.Object.FindObjectsByType<UnityEngine.Video.VideoPlayer>(FindObjectsSortMode.None).Length;
            if (result.videoPlayers != 1 || panels[0].Source != panels[1].Source) throw new InvalidOperationException("The sample must share one ordinary VideoPlayer.");
            var floor = GameObject.Find("URP Lit Color Receiver Floor");
            if (floor == null || floor.GetComponent<LedPlanarReflection>() != null) throw new InvalidOperationException("The sample must use a Lit color receiver floor.");
            result.receiverShader = floor.GetComponent<Renderer>().sharedMaterial.shader.name;
            if (result.receiverShader != "Universal Render Pipeline/Lit") throw new InvalidOperationException("The floor receiver must use ordinary URP Lit.");
            if (UnityEngine.Object.FindObjectsByType<LedPanelLighting>(FindObjectsSortMode.None).Any(lighting => lighting.SamplingShader == null || lighting.OverrideLightParameters)) throw new InvalidOperationException("GPU lighting must use an assigned shader and automatic panel settings by default.");
            if (Resources.Load<ComputeShader>("Mizotake/LEDWall/EmissionReduction") == null) throw new InvalidOperationException("The packaged Compute Shader is unavailable.");
            var spill = UnityEngine.Object.FindObjectsByType<LedColorSpill>(FindObjectsSortMode.None);
            if (spill.Length != 2 || spill.Any(item => !item.enabled || item.ReceiverMask != (1 << floor.layer))) throw new InvalidOperationException("The default sample must use color spill on the Lit floor.");
            if (UnityEngine.Object.FindObjectsByType<LedPanelLighting>(FindObjectsSortMode.None).Any(item => item.enabled)) throw new InvalidOperationException("Default sample must not double count Spot Light output.");
            foreach (var root in floor.scene.GetRootGameObjects()) foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0) throw new InvalidOperationException("Missing script in the imported sample: " + transform.name);
                foreach (var script in transform.GetComponents<MonoBehaviour>()) if (script != null && script.GetType().Assembly.GetName().Name != "Mizotake.LedWall" && script.GetType().Namespace?.StartsWith("UnityEngine.Rendering") != true) throw new InvalidOperationException("Unexpected sample runtime controller: " + script.GetType().FullName);
            }
            result.success = true;
            Debug.Log("UPM_CONSUMER_PASS: package imports and sample references resolve in a clean project.");
        }
        catch (Exception exception) { result.error = exception.ToString(); Debug.LogException(exception); }
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "consumer-result.json")), JsonUtility.ToJson(result, true), new System.Text.UTF8Encoding(false));
        EditorApplication.Exit(result.success ? 0 : 1);
    }
}

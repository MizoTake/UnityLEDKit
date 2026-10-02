using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mizotake.LedWall
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LedPanel))]
    [DefaultExecutionOrder(300)]
    public sealed class LedPanelLighting : MonoBehaviour
    {
        [SerializeField] private Shader samplingShader;
        [SerializeField] private Vector2Int lightGrid = new Vector2Int(4, 2);
        [SerializeField, Range(1f, 30f)] private float updatesPerSecond = 15f;
        [SerializeField, Tooltip("Enable manual emission settings. Otherwise brightness, size and orientation follow LedPanel automatically.")] private bool overrideLightParameters;
        [SerializeField, Range(0f, 20f)] private float intensity = 5f;
        [SerializeField, Range(1f, 30f)] private float range = 12f;
        [SerializeField, Range(45f, 150f)] private float spotAngle = 115f;
        [SerializeField, Range(0f, 1f)] private float innerSpotRatio = 0.72f;
        [SerializeField, Range(0f, 2f)] private float downwardBias = 0.5f;
        [SerializeField] private bool castShadows;
        [SerializeField] private LayerMask cullingMask = ~0;
        [SerializeField] private RenderingLayerMask renderingLayers = RenderingLayerMask.defaultRenderingLayerMask;
        [SerializeField] private LightRenderMode renderMode = LightRenderMode.ForcePixel;
        [SerializeField, Range(0f, 1f)] private float shadowStrength = 1f;
        [SerializeField, Range(0f, 2f)] private float shadowBias = 0.05f;
        [SerializeField, Range(0f, 3f)] private float shadowNormalBias = 0.4f;
        [SerializeField, Min(0.01f)] private float shadowNearPlane = 0.2f;
        [SerializeField] private bool customShadowLayers;
        [SerializeField] private RenderingLayerMask shadowRenderingLayers = RenderingLayerMask.defaultRenderingLayerMask;
        private LedPanel panel;
        private Material samplingMaterial;
        private RenderTexture sampleTexture;
        private Light[] lights;
        private UniversalAdditionalLightData[] lightData;
        private Color[] colors;
        private GameObject lightRoot;
        private AsyncGPUReadbackRequest pendingRequest;
        private Action<AsyncGPUReadbackRequest> readbackCallback;
        private bool requestPending;
        private float nextUpdate;
        private Vector2Int allocatedGrid;
        private static readonly int BlendMapId = Shader.PropertyToID("_BlendMap");
        private static readonly int TransitionId = Shader.PropertyToID("_Transition");
        private static readonly int ContentRectId = Shader.PropertyToID("_ContentRect");
        private static readonly int CellSizeId = Shader.PropertyToID("_CellSize");

        public Shader SamplingShader { get => samplingShader; set => samplingShader = value; }
        public Vector2Int LightGrid { get => lightGrid; set => lightGrid = new Vector2Int(Mathf.Clamp(value.x, 1, 8), Mathf.Clamp(value.y, 1, 4)); }
        public bool OverrideLightParameters { get => overrideLightParameters; set => overrideLightParameters = value; }
        public float Intensity { get => intensity; set => intensity = Mathf.Clamp(value, 0f, 20f); }
        public float DownwardBias { get => downwardBias; set => downwardBias = Mathf.Clamp(value, 0f, 2f); }
        public float Range { get => range; set => range = Mathf.Max(0.01f, value); }
        public float SpotAngle { get => spotAngle; set => spotAngle = Mathf.Clamp(value, 1f, 179f); }
        public float InnerSpotRatio { get => innerSpotRatio; set => innerSpotRatio = Mathf.Clamp01(value); }
        public bool CastShadows { get => castShadows; set => castShadows = value; }
        public LayerMask CullingMask { get => cullingMask; set => cullingMask = value; }
        public RenderingLayerMask RenderingLayers { get => renderingLayers; set => renderingLayers = value; }
        public LightRenderMode RenderMode { get => renderMode; set => renderMode = value; }
        public float ShadowStrength { get => shadowStrength; set => shadowStrength = Mathf.Clamp01(value); }
        public float ShadowBias { get => shadowBias; set => shadowBias = Mathf.Clamp(value, 0f, 2f); }
        public float ShadowNormalBias { get => shadowNormalBias; set => shadowNormalBias = Mathf.Clamp(value, 0f, 3f); }
        public float ShadowNearPlane { get => shadowNearPlane; set => shadowNearPlane = Mathf.Max(0.01f, value); }
        public bool CustomShadowLayers { get => customShadowLayers; set => customShadowLayers = value; }
        public RenderingLayerMask ShadowRenderingLayers { get => shadowRenderingLayers; set => shadowRenderingLayers = value; }
        public Light[] GeneratedLights => lights;
        public int CompletedSamples { get; private set; }
        public string LastError { get; private set; }

        private void OnEnable()
        {
            panel = GetComponent<LedPanel>();
            readbackCallback = OnReadback;
            nextUpdate = 0f;
            CompletedSamples = 0;
            LastError = null;
        }

        private void OnValidate() => lightGrid = new Vector2Int(Mathf.Clamp(lightGrid.x, 1, 8), Mathf.Clamp(lightGrid.y, 1, 4));

        private void LateUpdate()
        {
            if (panel == null || !panel.isActiveAndEnabled) { SetLightsEnabled(false); return; }
            if (!SystemInfo.supportsAsyncGPUReadback || samplingShader == null)
            {
                LastError = !SystemInfo.supportsAsyncGPUReadback ? "Asynchronous GPU readback is unavailable on this graphics device." : "Assign the Lighting Sampler shader.";
                SetLightsEnabled(false);
                return;
            }
            EnsureResources();
            UpdateLightTransforms();
            if (requestPending || Time.unscaledTime < nextUpdate) return;
            samplingMaterial.SetTexture(BlendMapId, panel.SecondaryOutput != null ? panel.SecondaryOutput : Texture2D.blackTexture);
            samplingMaterial.SetFloat(TransitionId, panel.Transition);
            samplingMaterial.SetVector(ContentRectId, panel.ContentRect);
            samplingMaterial.SetVector(CellSizeId, new Vector4(1f / allocatedGrid.x, 1f / allocatedGrid.y, 0f, 0f));
            var previousTarget = RenderTexture.active;
            try { Graphics.Blit(panel.PrimaryOutput != null ? panel.PrimaryOutput : Texture2D.blackTexture, sampleTexture, samplingMaterial); }
            finally { RenderTexture.active = previousTarget; }
            requestPending = true;
            pendingRequest = AsyncGPUReadback.Request(sampleTexture, 0, TextureFormat.RGBAFloat, readbackCallback);
            nextUpdate = Time.unscaledTime + 1f / Mathf.Max(1f, updatesPerSecond);
        }

        private void EnsureResources()
        {
            var grid = new Vector2Int(Mathf.Clamp(lightGrid.x, 1, 8), Mathf.Clamp(lightGrid.y, 1, 4));
            if (sampleTexture != null && grid == allocatedGrid && samplingMaterial.shader == samplingShader) return;
            ReleaseResources();
            allocatedGrid = grid;
            samplingMaterial = new Material(samplingShader) { hideFlags = HideFlags.HideAndDontSave };
            sampleTexture = new RenderTexture(grid.x, grid.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) { name = "LED Lighting Samples", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            sampleTexture.Create();
            lightRoot = new GameObject("LED Video Lights") { hideFlags = HideFlags.DontSave };
            lightRoot.transform.SetParent(transform, false);
            lights = new Light[grid.x * grid.y];
            lightData = new UniversalAdditionalLightData[lights.Length];
            colors = new Color[lights.Length];
            for (var index = 0; index < lights.Length; index++)
            {
                var item = new GameObject("Emitter " + index) { hideFlags = HideFlags.DontSave };
                item.transform.SetParent(lightRoot.transform, false);
                var light = item.AddComponent<Light>();
                light.type = LightType.Spot;
                light.enabled = false;
                lights[index] = light;
                lightData[index] = light.GetUniversalAdditionalLightData();
            }
        }

        private void UpdateLightTransforms()
        {
            var right = transform.TransformVector(Vector3.right);
            var up = transform.TransformVector(Vector3.up);
            var normal = -transform.forward;
            var rotation = Quaternion.LookRotation((normal - Vector3.up * (overrideLightParameters ? downwardBias : 0.5f)).normalized, transform.up);
            var area = Vector3.Cross(right, up).magnitude;
            var automaticRange = Mathf.Max(0.01f, Mathf.Sqrt(right.sqrMagnitude + up.sqrMagnitude) * 1.3f);
            for (var index = 0; index < lights.Length; index++)
            {
                var x = index % allocatedGrid.x;
                var y = index / allocatedGrid.x;
                var position = transform.position + right * ((x + 0.5f) / allocatedGrid.x - 0.5f) + up * ((y + 0.5f) / allocatedGrid.y - 0.5f) + normal * 0.15f;
                lights[index].transform.SetPositionAndRotation(position, rotation);
                lights[index].range = overrideLightParameters ? Mathf.Max(0.01f, range) : automaticRange;
                lights[index].spotAngle = overrideLightParameters ? Mathf.Clamp(spotAngle, 1f, 179f) : 115f;
                lights[index].innerSpotAngle = lights[index].spotAngle * (overrideLightParameters ? Mathf.Clamp01(innerSpotRatio) : 0.72f);
                lights[index].intensity = (overrideLightParameters ? intensity : 5f) * panel.BrightnessValue * area / lights.Length;
                lights[index].shadows = castShadows ? LightShadows.Soft : LightShadows.None;
                lights[index].cullingMask = cullingMask;
                lights[index].renderMode = renderMode;
                lights[index].shadowStrength = Mathf.Clamp01(shadowStrength);
                lights[index].shadowBias = Mathf.Clamp(shadowBias, 0f, 2f);
                lights[index].shadowNormalBias = Mathf.Clamp(shadowNormalBias, 0f, 3f);
                lights[index].shadowNearPlane = Mathf.Max(0.01f, shadowNearPlane);
                lightData[index].renderingLayers = renderingLayers;
                lightData[index].customShadowLayers = customShadowLayers;
                lightData[index].shadowRenderingLayers = shadowRenderingLayers;
                lights[index].color = colors[index];
                lights[index].enabled = CompletedSamples > 0 && colors[index].maxColorComponent > 0.001f;
            }
        }

        private void OnReadback(AsyncGPUReadbackRequest request)
        {
            requestPending = false;
            if (!isActiveAndEnabled || colors == null) return;
            if (request.hasError) { LastError = "LED lighting GPU readback failed."; SetLightsEnabled(false); return; }
            var data = request.GetData<Color>();
            if (data.Length != colors.Length) return;
            for (var index = 0; index < data.Length; index++) colors[index] = QualitySettings.activeColorSpace == ColorSpace.Linear ? data[index].gamma : data[index];
            CompletedSamples++;
            LastError = null;
        }

        private void SetLightsEnabled(bool enabled)
        {
            if (lights == null) return;
            foreach (var light in lights) if (light != null) light.enabled = enabled;
        }

        private void OnDisable() => ReleaseResources();

        private void ReleaseResources()
        {
            SetLightsEnabled(false);
            if (requestPending) { pendingRequest.WaitForCompletion(); requestPending = false; }
            if (sampleTexture != null) { if (RenderTexture.active == sampleTexture) RenderTexture.active = null; sampleTexture.Release(); Destroy(sampleTexture); sampleTexture = null; }
            if (samplingMaterial != null) { Destroy(samplingMaterial); samplingMaterial = null; }
            if (lightRoot != null) { Destroy(lightRoot); lightRoot = null; }
            lights = null;
            lightData = null;
            colors = null;
        }
    }
}

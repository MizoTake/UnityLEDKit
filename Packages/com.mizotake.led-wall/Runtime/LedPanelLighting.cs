using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Video;

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
        [SerializeField, Range(64, 2048), Tooltip("Maximum source dimension before the power-of-two area reduction. Higher values retain smaller bright details.")] private int samplingResolution = 2048;
        [SerializeField, Range(0f, 0.5f), Tooltip("Exponential response time in seconds. Zero applies sampled colors immediately.")] private float responseSeconds = 0.08f;
        [SerializeField, Range(0f, 1f), Tooltip("Whole-panel color change that resets temporal smoothing. Zero disables color-cut detection; source changes and seeks still reset history.")] private float sceneCutThreshold = 0.55f;
        [SerializeField] private bool softDistribution = true;
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
        private RenderTexture[] reduction;
        private RenderTexture softCookie;
        private Light[] lights;
        private UniversalAdditionalLightData[] lightData;
        private Color[] colors;
        private Color[] targetColors;
        private Color[] newColors;
        private GameObject lightRoot;
        private AsyncGPUReadbackRequest pendingRequest;
        private Action<AsyncGPUReadbackRequest> readbackCallback;
        private bool requestPending;
        private bool pendingReset;
        private bool hasHistory;
        private float nextUpdate;
        private float submittedAt;
        private float cookieAspect = -1f;
        private Vector2Int allocatedGrid;
        private Vector2Int allocatedSize;
        private Texture lastPrimary;
        private Texture lastSecondary;
        private VideoClip lastPrimaryClip;
        private VideoClip lastSecondaryClip;
        private string lastPrimaryUrl;
        private string lastSecondaryUrl;
        private Vector4 lastRect;
        private float lastTransition;
        private long lastPrimaryFrame = -1;
        private long lastSecondaryFrame = -1;
        private static readonly int BlendMapId = Shader.PropertyToID("_BlendMap");
        private static readonly int TransitionId = Shader.PropertyToID("_Transition");
        private static readonly int ContentRectId = Shader.PropertyToID("_ContentRect");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int ReductionStepId = Shader.PropertyToID("_ReductionStep");
        private static readonly int CookieAspectId = Shader.PropertyToID("_CookieAspect");
        private static readonly ProfilerMarker SamplingMarker = new ProfilerMarker("LED Lighting.GPU area reduction");
        private static readonly ProfilerMarker LightMarker = new ProfilerMarker("LED Lighting.Update generated lights");

        public Shader SamplingShader { get => samplingShader; set => samplingShader = value; }
        public Vector2Int LightGrid { get => lightGrid; set => lightGrid = new Vector2Int(Mathf.Clamp(value.x, 1, 8), Mathf.Clamp(value.y, 1, 4)); }
        public int SamplingResolution { get => samplingResolution; set => samplingResolution = Mathf.Clamp(value, 64, 2048); }
        public float UpdatesPerSecond { get => updatesPerSecond; set => updatesPerSecond = Mathf.Clamp(value, 1f, 30f); }
        public float ResponseSeconds { get => responseSeconds; set => responseSeconds = Mathf.Clamp(value, 0f, 0.5f); }
        public float SceneCutThreshold { get => sceneCutThreshold; set => sceneCutThreshold = Mathf.Clamp01(value); }
        public bool SoftDistribution { get => softDistribution; set => softDistribution = value; }
        public bool OverrideLightParameters { get => overrideLightParameters; set => overrideLightParameters = value; }
        public float Intensity { get => intensity; set => intensity = Mathf.Clamp(value, 0f, 20f); }
        public float DownwardBias { get => downwardBias; set => downwardBias = Mathf.Clamp(value, 0f, 2f); }
        public float Range { get => range; set => range = Mathf.Max(0.01f, value); }
        public float SpotAngle { get => spotAngle; set => spotAngle = Mathf.Clamp(value, 1f, 179f); }
        public float InnerSpotRatio { get => innerSpotRatio; set => innerSpotRatio = Mathf.Clamp01(value); }
        public bool CastShadows { get => castShadows; set => castShadows = value; }
        public int CullingMask { get => cullingMask; set => cullingMask = value; }
        public RenderingLayerMask RenderingLayers { get => renderingLayers; set => renderingLayers = value; }
        public LightRenderMode RenderMode { get => renderMode; set => renderMode = value; }
        public float ShadowStrength { get => shadowStrength; set => shadowStrength = Mathf.Clamp01(value); }
        public float ShadowBias { get => shadowBias; set => shadowBias = Mathf.Clamp(value, 0f, 2f); }
        public float ShadowNormalBias { get => shadowNormalBias; set => shadowNormalBias = Mathf.Clamp(value, 0f, 3f); }
        public float ShadowNearPlane { get => shadowNearPlane; set => shadowNearPlane = Mathf.Max(0.01f, value); }
        public bool CustomShadowLayers { get => customShadowLayers; set => customShadowLayers = value; }
        public RenderingLayerMask ShadowRenderingLayers { get => shadowRenderingLayers; set => shadowRenderingLayers = value; }
        public Light[] GeneratedLights => lights;
        public RenderTexture CookieTexture => softCookie;
        public Vector2Int SamplingSize => allocatedSize;
        public int CompletedSamples { get; private set; }
        public int HistoryResets { get; private set; }
        public float LastReadbackMilliseconds { get; private set; }
        public string LastError { get; private set; }

        private void OnEnable()
        {
            panel = GetComponent<LedPanel>();
            readbackCallback = OnReadback;
            nextUpdate = 0f;
            CompletedSamples = 0;
            HistoryResets = 0;
            LastError = null;
            hasHistory = false;
        }

        private void OnValidate()
        {
            lightGrid = new Vector2Int(Mathf.Clamp(lightGrid.x, 1, 8), Mathf.Clamp(lightGrid.y, 1, 4));
            samplingResolution = Mathf.Clamp(samplingResolution, 64, 2048);
        }

        private void LateUpdate()
        {
            if (panel == null || !panel.isActiveAndEnabled) { SetLightsEnabled(false); hasHistory = false; return; }
            if (!SystemInfo.supportsAsyncGPUReadback || samplingShader == null)
            {
                LastError = !SystemInfo.supportsAsyncGPUReadback ? "Asynchronous GPU readback is unavailable on this graphics device." : "Assign the Lighting Sampler shader.";
                SetLightsEnabled(false);
                return;
            }
            var first = panel.PrimaryOutput != null ? panel.PrimaryOutput : Texture2D.blackTexture;
            var second = panel.SecondaryOutput != null ? panel.SecondaryOutput : first;
            EnsureResources(first, second);
            var weight = LedLightingResponse.InterpolationWeight(Time.unscaledDeltaTime, responseSeconds);
            for (var index = 0; index < colors.Length; index++) colors[index] = Color.LerpUnclamped(colors[index], targetColors[index], weight);
            using (LightMarker.Auto()) UpdateLightTransforms();
            if (requestPending || Time.unscaledTime < nextUpdate) return;
            var rect = panel.SamplingContentRect;
            pendingReset = !hasHistory || first != lastPrimary || second != lastSecondary || rect != lastRect || Mathf.Abs(panel.Transition - lastTransition) > 0.5f ||
                SourceChanged(panel.Source, lastPrimaryClip, lastPrimaryUrl, lastPrimaryFrame) || SourceChanged(panel.SecondarySource, lastSecondaryClip, lastSecondaryUrl, lastSecondaryFrame);
            lastPrimary = first;
            lastSecondary = second;
            lastRect = rect;
            lastTransition = panel.Transition;
            lastPrimaryClip = panel.Source != null ? panel.Source.clip : null;
            lastSecondaryClip = panel.SecondarySource != null ? panel.SecondarySource.clip : null;
            lastPrimaryUrl = panel.Source != null ? panel.Source.url : null;
            lastSecondaryUrl = panel.SecondarySource != null ? panel.SecondarySource.url : null;
            lastPrimaryFrame = panel.Source != null ? panel.Source.frame : -1;
            lastSecondaryFrame = panel.SecondarySource != null ? panel.SecondarySource.frame : -1;
            samplingMaterial.SetTexture(BlendMapId, second);
            samplingMaterial.SetFloat(TransitionId, panel.Transition);
            samplingMaterial.SetVector(ContentRectId, rect);
            samplingMaterial.SetVector(TintId, (Vector4)panel.EmissionTint);
            var previousTarget = RenderTexture.active;
            try
            {
                using (SamplingMarker.Auto())
                {
                    Graphics.Blit(first, reduction[0], samplingMaterial, 0);
                    for (var index = 1; index < reduction.Length; index++)
                    {
                        var source = reduction[index - 1];
                        var destination = reduction[index];
                        samplingMaterial.SetVector(ReductionStepId, new Vector4(source.width == destination.width ? 0f : 0.5f / source.width, source.height == destination.height ? 0f : 0.5f / source.height, 0, 0));
                        Graphics.Blit(source, destination, samplingMaterial, 1);
                    }
                }
            }
            finally { RenderTexture.active = previousTarget; }
            requestPending = true;
            submittedAt = Time.realtimeSinceStartup;
            pendingRequest = AsyncGPUReadback.Request(reduction[reduction.Length - 1], 0, TextureFormat.RGBAFloat, readbackCallback);
            nextUpdate = Time.unscaledTime + 1f / Mathf.Max(1f, updatesPerSecond);
        }

        private static bool SourceChanged(VideoPlayer source, VideoClip clip, string url, long frame)
        {
            if (source == null) return clip != null || url != null;
            if (source.clip != clip || source.url != url) return true;
            if (frame < 0 || source.frame < 0) return frame != source.frame;
            return source.frame < frame || source.frame - frame > Math.Max(3, source.frameRate * 0.75);
        }

        private void EnsureResources(Texture first, Texture second)
        {
            var grid = new Vector2Int(Mathf.Clamp(lightGrid.x, 1, 8), Mathf.Clamp(lightGrid.y, 1, 4));
            var maximum = Mathf.Clamp(samplingResolution, 64, 2048);
            var width = grid.x * Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Min(maximum, Mathf.Max(first.width, second.width)) / (float)grid.x));
            var height = grid.y * Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Min(maximum, Mathf.Max(first.height, second.height)) / (float)grid.y));
            var size = new Vector2Int(Mathf.Max(grid.x, width), Mathf.Max(grid.y, height));
            if (reduction != null && grid == allocatedGrid && size == allocatedSize && samplingMaterial.shader == samplingShader) return;
            ReleaseResources();
            hasHistory = false;
            allocatedGrid = grid;
            allocatedSize = size;
            samplingMaterial = new Material(samplingShader) { hideFlags = HideFlags.HideAndDontSave };
            var targets = new List<RenderTexture>();
            width = size.x;
            height = size.y;
            while (true)
            {
                var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) { name = "LED Area Reduction " + width + "x" + height, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                texture.Create();
                targets.Add(texture);
                if (width == grid.x && height == grid.y) break;
                width = Mathf.Max(grid.x, width / 2);
                height = Mathf.Max(grid.y, height / 2);
            }
            reduction = targets.ToArray();
            softCookie = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { name = "LED Soft Emitter Cookie", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            softCookie.Create();
            cookieAspect = -1f;
            lightRoot = new GameObject("LED Video Lights") { hideFlags = HideFlags.DontSave };
            lightRoot.transform.SetParent(transform, false);
            lights = new Light[grid.x * grid.y];
            lightData = new UniversalAdditionalLightData[lights.Length];
            colors = new Color[lights.Length];
            targetColors = new Color[lights.Length];
            newColors = new Color[lights.Length];
            for (var index = 0; index < lights.Length; index++)
            {
                var item = new GameObject("Emitter " + index) { hideFlags = HideFlags.DontSave };
                item.transform.SetParent(lightRoot.transform, false);
                lights[index] = item.AddComponent<Light>();
                lights[index].type = LightType.Spot;
                lights[index].enabled = false;
                lightData[index] = lights[index].GetUniversalAdditionalLightData();
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
            var aspect = Mathf.Clamp(right.magnitude * allocatedGrid.y / Mathf.Max(0.001f, up.magnitude * allocatedGrid.x), 0.25f, 4f);
            if (softDistribution && !Mathf.Approximately(aspect, cookieAspect))
            {
                cookieAspect = aspect;
                samplingMaterial.SetFloat(CookieAspectId, aspect);
                var previous = RenderTexture.active;
                try { Graphics.Blit(Texture2D.whiteTexture, softCookie, samplingMaterial, 2); }
                finally { RenderTexture.active = previous; }
            }
            for (var index = 0; index < lights.Length; index++)
            {
                var x = index % allocatedGrid.x;
                var y = index / allocatedGrid.x;
                var position = transform.position + right * ((x + 0.5f) / allocatedGrid.x - 0.5f) + up * ((y + 0.5f) / allocatedGrid.y - 0.5f) + normal * 0.15f;
                var light = lights[index];
                light.transform.SetPositionAndRotation(position, rotation);
                light.range = overrideLightParameters ? Mathf.Max(0.01f, range) : automaticRange;
                light.spotAngle = overrideLightParameters ? Mathf.Clamp(spotAngle, 1f, 179f) : 115f;
                light.innerSpotAngle = light.spotAngle * (overrideLightParameters ? Mathf.Clamp01(innerSpotRatio) : 0.72f);
                light.intensity = (overrideLightParameters ? intensity : 5f) * panel.BrightnessValue * area / lights.Length;
                light.cookie = softDistribution ? softCookie : null;
                light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
                light.cullingMask = cullingMask;
                light.renderMode = renderMode;
                light.shadowStrength = Mathf.Clamp01(shadowStrength);
                light.shadowBias = Mathf.Clamp(shadowBias, 0f, 2f);
                light.shadowNormalBias = Mathf.Clamp(shadowNormalBias, 0f, 3f);
                light.shadowNearPlane = Mathf.Max(0.01f, shadowNearPlane);
                lightData[index].renderingLayers = renderingLayers;
                lightData[index].customShadowLayers = customShadowLayers;
                lightData[index].shadowRenderingLayers = shadowRenderingLayers;
                light.color = QualitySettings.activeColorSpace == ColorSpace.Linear ? colors[index].gamma : colors[index];
                light.enabled = hasHistory && colors[index].maxColorComponent > 0.001f && area > 0.000001f;
            }
        }

        private void OnReadback(AsyncGPUReadbackRequest request)
        {
            requestPending = false;
            if (!isActiveAndEnabled || colors == null) return;
            if (request.hasError) { LastError = "LED lighting GPU readback failed."; hasHistory = false; SetLightsEnabled(false); return; }
            var data = request.GetData<Color>();
            if (data.Length != colors.Length) return;
            for (var index = 0; index < data.Length; index++) newColors[index] = data[index];
            var reset = pendingReset || LedLightingResponse.IsCut(targetColors, newColors, sceneCutThreshold);
            for (var index = 0; index < colors.Length; index++)
            {
                targetColors[index] = newColors[index];
                if (reset) colors[index] = newColors[index];
            }
            if (reset) HistoryResets++;
            hasHistory = true;
            CompletedSamples++;
            LastReadbackMilliseconds = (Time.realtimeSinceStartup - submittedAt) * 1000f;
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
            if (reduction != null)
            {
                foreach (var texture in reduction)
                {
                    if (RenderTexture.active == texture) RenderTexture.active = null;
                    texture.Release();
                    Destroy(texture);
                }
                reduction = null;
            }
            if (softCookie != null) { if (RenderTexture.active == softCookie) RenderTexture.active = null; softCookie.Release(); Destroy(softCookie); softCookie = null; }
            if (samplingMaterial != null) { Destroy(samplingMaterial); samplingMaterial = null; }
            if (lightRoot != null) { Destroy(lightRoot); lightRoot = null; }
            lights = null;
            lightData = null;
            colors = null;
            targetColors = null;
            newColors = null;
            hasHistory = false;
        }
    }
}

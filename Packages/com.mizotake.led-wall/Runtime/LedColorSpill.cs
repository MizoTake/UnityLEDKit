using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace Mizotake.LedWall
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(LedPanel))]
    [DefaultExecutionOrder(350)]
    public sealed class LedColorSpill : MonoBehaviour
    {
        [SerializeField] private ComputeShader reductionShader;
        [SerializeField] private Vector2Int emitterGrid = new Vector2Int(4, 2);
        [SerializeField, Range(64, 2048)] private int samplingResolution = 2048;
        [SerializeField, Range(1, 60)] private float updatesPerSecond = 30;
        [SerializeField, Range(0, 0.5f)] private float responseSeconds = 0.08f;
        [SerializeField, Range(0, 1)] private float sceneCutThreshold = 0.55f;
        [SerializeField] private LayerMask receiverMask = ~0;
        [SerializeField, Min(0)] private float strength = 1f;
        [SerializeField, Tooltip("Enable independent range and soft source distance. The default derives both from LedPanel geometry.")] private bool overrideDistribution;
        [SerializeField, Min(0.01f)] private float range = 12;
        [SerializeField, Min(0.01f)] private float softDistance = 0.6f;
        [SerializeField, Range(0, 1), Tooltip("Screen-depth occlusion strength. Off-screen occluders are not represented.")] private float screenOcclusion = 0.75f;
        private static readonly List<LedColorSpill> emitters = new List<LedColorSpill>();
        internal static IReadOnlyList<LedColorSpill> Emitters => emitters;
        private LedEmissionSampler sampler;
        private LedPanel panel;
        private Texture lastPrimary, lastSecondary;
        private Vector4 lastRect;
        private float nextUpdate, lastUpdate, lastTransition;
        private long firstFrame = -1, secondFrame = -1;
        private VideoClip firstClip, secondClip;
        private string firstUrl, secondUrl;
        public LedPanel Panel => panel;
        public RenderTexture EmissionTexture => sampler?.Output;
        internal UnityEngine.Rendering.RTHandle EmissionHandle => sampler?.OutputHandle;
        public long BufferBytes => sampler == null ? 0 : sampler.BufferBytes;
        public Vector2Int EmitterGrid { get => emitterGrid; set => emitterGrid = ClampGrid(value); }
        public int SamplingResolution { get => samplingResolution; set => samplingResolution = Mathf.Clamp(value, 64, 2048); }
        public int ReceiverMask { get => receiverMask; set => receiverMask = value; }
        public float Strength { get => strength; set => strength = Mathf.Max(0, value); }
        public bool OverrideDistribution { get => overrideDistribution; set => overrideDistribution = value; }
        public float Range { get => range; set => range = Mathf.Max(0.01f, value); }
        public float SoftDistance { get => softDistance; set => softDistance = Mathf.Max(0.01f, value); }
        public float ScreenOcclusion { get => screenOcclusion; set => screenOcclusion = Mathf.Clamp01(value); }
        public float ResponseSeconds { get => responseSeconds; set => responseSeconds = Mathf.Clamp(value, 0, 0.5f); }
        public float UpdatesPerSecond { get => updatesPerSecond; set => updatesPerSecond = Mathf.Clamp(value, 1, 60); }
        public string LastError { get; private set; }
        public int SubmittedSamples { get; private set; }
        internal bool Ready => isActiveAndEnabled && panel != null && panel.isActiveAndEnabled && sampler?.Output != null && strength > 0 && panel.BrightnessValue > 0;
        public float EffectiveRange => overrideDistribution ? range : Mathf.Max(0.01f, Mathf.Sqrt(transform.TransformVector(Vector3.right).sqrMagnitude + transform.TransformVector(Vector3.up).sqrMagnitude) * 1.3f);
        public float EffectiveSoftDistance => overrideDistribution ? softDistance : Mathf.Max(0.1f, Mathf.Sqrt(Vector3.Cross(transform.TransformVector(Vector3.right), transform.TransformVector(Vector3.up)).magnitude / (emitterGrid.x * emitterGrid.y)) * 0.5f);

        private static Vector2Int ClampGrid(Vector2Int value) => new Vector2Int(Mathf.Clamp(value.x, 1, 8), Mathf.Clamp(value.y, 1, 4));
        private void OnValidate() => emitterGrid = ClampGrid(emitterGrid);
        private void OnEnable()
        {
            panel = GetComponent<LedPanel>();
            if (!emitters.Contains(this)) emitters.Add(this);
            nextUpdate = 0; lastUpdate = 0; SubmittedSamples = 0; LastError = null;
        }

        private void LateUpdate()
        {
            if (panel == null || !panel.isActiveAndEnabled || Time.realtimeSinceStartup < nextUpdate) return;
            if (!SystemInfo.supportsComputeShaders) { LastError = "LED color spill requires Compute Shader support."; return; }
            if (reductionShader == null) reductionShader = LedEmissionSampler.DefaultShader;
            if (reductionShader == null) { LastError = "LED emission reduction Compute Shader is missing."; return; }
            if (sampler == null) sampler = new LedEmissionSampler();
            var first = panel.PrimaryOutput != null ? panel.PrimaryOutput : Texture2D.blackTexture;
            var second = panel.SecondaryOutput != null ? panel.SecondaryOutput : first;
            var changed = sampler.Ensure(reductionShader, ClampGrid(emitterGrid), LedEmissionSampler.CalculateSize(first, second, ClampGrid(emitterGrid), samplingResolution));
            var rect = panel.SamplingContentRect;
            var reset = changed || lastUpdate == 0 || first != lastPrimary || second != lastSecondary || rect != lastRect || Mathf.Abs(panel.Transition - lastTransition) > 0.5f ||
                SourceChanged(panel.Source, firstClip, firstUrl, firstFrame) || SourceChanged(panel.SecondarySource, secondClip, secondUrl, secondFrame);
            sampler.Sample(panel, LedLightingResponse.InterpolationWeight(Time.realtimeSinceStartup - lastUpdate, responseSeconds), reset, sceneCutThreshold);
            lastUpdate = Time.realtimeSinceStartup;
            nextUpdate = lastUpdate + 1f / Mathf.Max(1, updatesPerSecond);
            lastPrimary = first; lastSecondary = second; lastRect = rect; lastTransition = panel.Transition;
            firstFrame = panel.Source != null ? panel.Source.frame : -1; secondFrame = panel.SecondarySource != null ? panel.SecondarySource.frame : -1;
            firstClip = panel.Source != null ? panel.Source.clip : null; secondClip = panel.SecondarySource != null ? panel.SecondarySource.clip : null;
            firstUrl = panel.Source != null ? panel.Source.url : null; secondUrl = panel.SecondarySource != null ? panel.SecondarySource.url : null;
            SubmittedSamples++; LastError = null;
        }

        private static bool SourceChanged(VideoPlayer source, VideoClip clip, string url, long frame)
        {
            if (source == null) return clip != null || url != null;
            if (source.clip != clip || source.url != url) return true;
            if (frame < 0 || source.frame < 0) return frame != source.frame;
            return source.frame < frame || source.frame - frame > Math.Max(3, source.frameRate * 0.75);
        }

        private void OnDisable()
        {
            emitters.Remove(this);
            ((IDisposable)sampler)?.Dispose(); sampler = null;
        }
    }
}

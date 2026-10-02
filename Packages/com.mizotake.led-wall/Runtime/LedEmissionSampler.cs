using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mizotake.LedWall
{
    /// <summary>Two-stage area reduction. Owns only a partial-sum buffer and a small linear HDR region texture.</summary>
    public sealed class LedEmissionSampler : IDisposable
    {
        private ComputeShader shader;
        private ComputeBuffer partial;
        private CommandBuffer commands;
        private int tilesKernel;
        private int regionsKernel;
        private int tilesPerRegion;
        private static readonly ProfilerMarker Marker = new ProfilerMarker("LED Emission.Compute reduction");
        public RenderTexture Output { get; private set; }
        public RTHandle OutputHandle { get; private set; }
        public Vector2Int Grid { get; private set; }
        public Vector2Int SamplingSize { get; private set; }
        public long BufferBytes => partial == null ? 0 : (long)partial.count * partial.stride;
        public static ComputeShader DefaultShader => Resources.Load<ComputeShader>("Mizotake/LEDWall/EmissionReduction");

        public static Vector2Int CalculateSize(Texture first, Texture second, Vector2Int grid, int maximum)
        {
            maximum = Mathf.Clamp(maximum, 64, 2048);
            return new Vector2Int(
                grid.x * Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Min(maximum, Mathf.Max(first.width, second.width)) / (float)grid.x)),
                grid.y * Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Min(maximum, Mathf.Max(first.height, second.height)) / (float)grid.y)));
        }

        public bool Ensure(ComputeShader compute, Vector2Int grid, Vector2Int size)
        {
            if (Output != null && shader == compute && Grid == grid && SamplingSize == size) return false;
            Release();
            if (compute == null || !SystemInfo.supportsComputeShaders) throw new InvalidOperationException("LED emission requires a supported Compute Shader.");
            shader = compute;
            tilesKernel = shader.FindKernel("ReduceTiles");
            regionsKernel = shader.FindKernel("ReduceRegions");
            if (!shader.IsSupported(tilesKernel) || !shader.IsSupported(regionsKernel)) throw new InvalidOperationException("The LED reduction kernels are unsupported on this graphics device.");
            Grid = grid;
            SamplingSize = size;
            tilesPerRegion = Mathf.CeilToInt((size.x / grid.x) * (size.y / grid.y) / 256f);
            partial = new ComputeBuffer(tilesPerRegion * grid.x * grid.y, 16, ComputeBufferType.Structured) { name = "LED Emission Partial Sums" };
            Output = new RenderTexture(grid.x, grid.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
            {
                name = "LED Emission Region Colors", enableRandomWrite = true, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
            };
            Output.Create();
            OutputHandle = RTHandles.Alloc(Output);
            commands = new CommandBuffer { name = "LED Emission Area Reduction" };
            return true;
        }

        public void Sample(LedPanel panel, float responseWeight = 1f, bool reset = true, float cutThreshold = 0f)
        {
            var first = panel.PrimaryOutput != null ? panel.PrimaryOutput : Texture2D.blackTexture;
            var second = panel.SecondaryOutput != null ? panel.SecondaryOutput : first;
            var rect = panel.SamplingContentRect;
            using (Marker.Auto())
            {
                commands.Clear();
                commands.SetComputeVectorParam(shader, "_Grid", new Vector4(Grid.x, Grid.y, SamplingSize.x / Grid.x, SamplingSize.y / Grid.y));
                commands.SetComputeVectorParam(shader, "_ContentRect", rect);
                commands.SetComputeVectorParam(shader, "_Tint", (Vector4)panel.EmissionTint);
                commands.SetComputeVectorParam(shader, "_Response", new Vector4(responseWeight, reset ? 1 : 0, cutThreshold, 0));
                commands.SetComputeVectorParam(shader, "_SourceLod", new Vector4(SourceLod(first, rect), SourceLod(second, rect), 0, 0));
                commands.SetComputeFloatParam(shader, "_Transition", panel.Transition);
                commands.SetComputeIntParam(shader, "_TilesPerRegion", tilesPerRegion);
                commands.SetComputeTextureParam(shader, tilesKernel, "_SourceA", first);
                commands.SetComputeTextureParam(shader, tilesKernel, "_SourceB", second);
                commands.SetComputeBufferParam(shader, tilesKernel, "_Partial", partial);
                commands.DispatchCompute(shader, tilesKernel, tilesPerRegion, Grid.x * Grid.y, 1);
                commands.SetComputeBufferParam(shader, regionsKernel, "_Partial", partial);
                commands.SetComputeTextureParam(shader, regionsKernel, "_Average", Output);
                commands.DispatchCompute(shader, regionsKernel, Grid.x * Grid.y, 1, 1);
                Graphics.ExecuteCommandBuffer(commands);
            }
        }

        private float SourceLod(Texture source, Vector4 rect) => Mathf.Max(0, Mathf.Log(Mathf.Max(Mathf.Abs(rect.x) * source.width / SamplingSize.x, Mathf.Abs(rect.y) * source.height / SamplingSize.y, 0.0001f), 2));

        private void Release()
        {
            commands?.Release(); commands = null;
            partial?.Release(); partial = null;
            OutputHandle?.Release(); OutputHandle = null;
            if (Output != null) { Output.Release(); CoreUtils.Destroy(Output); Output = null; }
            shader = null;
        }

        void IDisposable.Dispose() => Release();
    }
}

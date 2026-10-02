using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mizotake.LedWall;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Temporary build instrumentation. Imported only by Build-LightingBenchmark.ps1; never part of the UPM sample.
[DefaultExecutionOrder(1000)]
public sealed class PlayerLightingBenchmark : MonoBehaviour
{
    [Serializable] private sealed class Result
    {
        public string mode, gpu, api, environment = "Windows Development Player, 1280x720, original video, 15 Hz aggregation, 3 second warmup, 12 second measurement";
        public int frames, gpuTimingSamples, generatedLights, ownedTextureCount;
        public long ownedTextureBytes, partialBufferBytes;
        public double gpuMeanMilliseconds, gpuP95Milliseconds, cpuMeanMilliseconds, blitSubmissionMeanMilliseconds, computeSubmissionMeanMilliseconds, lightUpdateMeanMilliseconds;
    }
    private readonly List<double> gpuTimes = new List<double>(20000);
    private readonly List<double> cpuTimes = new List<double>(20000);
    private readonly FrameTiming[] timing = new FrameTiming[1];
    private ProfilerRecorder blit, compute, lights;
    private RenderTexture output;
    private Camera renderCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-ledBenchmark") < 0) return;
        var item = new GameObject("LED Player Benchmark Instrumentation");
        DontDestroyOnLoad(item);
        item.AddComponent<PlayerLightingBenchmark>();
    }

    private IEnumerator Start()
    {
        var arguments = Environment.GetCommandLineArgs();
        var mode = arguments[Array.IndexOf(arguments, "-ledBenchmark") + 1];
        var path = arguments[Array.IndexOf(arguments, "-ledBenchmarkOutput") + 1];
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        var spillMode = mode == "Spill";
        if (!spillMode && !Enum.TryParse(mode, out LedSamplingBackend _)) throw new ArgumentException("Unknown benchmark mode: " + mode);
        foreach (var item in FindObjectsByType<LedColorSpill>(FindObjectsSortMode.None)) { item.enabled = spillMode; item.UpdatesPerSecond = 15; }
        foreach (var item in FindObjectsByType<LedPanelLighting>(FindObjectsSortMode.None))
        {
            item.enabled = !spillMode;
            if (!spillMode) item.SamplingBackend = (LedSamplingBackend)Enum.Parse(typeof(LedSamplingBackend), mode);
        }
        output = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        output.Create();
        renderCamera = Camera.main;
        renderCamera.enabled = false;
        yield return new WaitForSecondsRealtime(3);
        blit = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "LED Lighting.GPU area reduction", 32768);
        compute = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "LED Emission.Compute reduction", 32768);
        lights = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "LED Lighting.Update generated lights", 32768);
        var started = Time.realtimeSinceStartup;
        var frames = 0;
        ulong previousStamp = 0;
        var endOfFrame = new WaitForEndOfFrame();
        while (Time.realtimeSinceStartup - started < 12)
        {
            yield return endOfFrame;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].frameStartTimestamp != previousStamp)
            {
                previousStamp = timing[0].frameStartTimestamp;
                if (timing[0].gpuFrameTime > 0) gpuTimes.Add(timing[0].gpuFrameTime);
                if (timing[0].cpuFrameTime > 0) cpuTimes.Add(timing[0].cpuFrameTime);
            }
            frames++;
        }
        var targets = Resources.FindObjectsOfTypeAll<RenderTexture>().Where(t => t.name.StartsWith("LED Area Reduction") || t.name.StartsWith("LED Soft Emitter") || t.name.StartsWith("LED Emission")).ToArray();
        var result = new Result
        {
            mode = mode, gpu = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString(), frames = frames,
            gpuTimingSamples = gpuTimes.Count, gpuMeanMilliseconds = Mean(gpuTimes), gpuP95Milliseconds = Percentile(gpuTimes, 0.95), cpuMeanMilliseconds = Mean(cpuTimes),
            blitSubmissionMeanMilliseconds = RecorderMean(blit), computeSubmissionMeanMilliseconds = RecorderMean(compute), lightUpdateMeanMilliseconds = RecorderMean(lights),
            generatedLights = FindObjectsByType<LedPanelLighting>(FindObjectsSortMode.None).Sum(l => l.GeneratedLights == null ? 0 : l.GeneratedLights.Length),
            ownedTextureCount = targets.Length, ownedTextureBytes = targets.Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t)),
            partialBufferBytes = FindObjectsByType<LedPanelLighting>(FindObjectsSortMode.None).Sum(l => l.SamplingBufferBytes) + FindObjectsByType<LedColorSpill>(FindObjectsSortMode.None).Sum(s => s.BufferBytes)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(result, true), new System.Text.UTF8Encoding(false));
        var previous = RenderTexture.active;
        RenderTexture.active = output;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0, false); pixels.Apply();
        File.WriteAllBytes(Path.ChangeExtension(path, ".png"), pixels.EncodeToPNG());
        RenderTexture.active = previous;
        Destroy(pixels);
        Application.Quit(result.gpuTimingSamples == 0 ? 2 : 0);
    }

    private void LateUpdate()
    {
        // Hidden Windows players may skip their automatic camera loop. Explicitly render every measured frame.
        if (renderCamera != null && output != null) RenderPipeline.SubmitRenderRequest(renderCamera, new UniversalRenderPipeline.SingleCameraRequest { destination = output });
    }

    private static double Mean(List<double> values) => values.Count == 0 ? 0 : values.Average();
    private static double Percentile(List<double> values, double percentile)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(value => value).ToArray();
        return sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * percentile))];
    }
    private static double RecorderMean(ProfilerRecorder recorder)
    {
        if (!recorder.Valid) return 0;
        var samples = new List<ProfilerRecorderSample>(); recorder.CopyTo(samples);
        var count = samples.Sum(sample => (long)sample.Count);
        return count == 0 ? 0 : samples.Sum(sample => sample.Value) / (double)count / 1000000;
    }
    private void OnDestroy()
    {
        blit.Dispose(); compute.Dispose(); lights.Dispose();
        if (output != null) { output.Release(); Destroy(output); }
    }
}

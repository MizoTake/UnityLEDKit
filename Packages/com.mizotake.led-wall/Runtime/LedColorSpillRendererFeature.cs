using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Mizotake.LedWall
{
    /// <summary>Adds diffuse LED irradiance to selected opaque receivers before transparents and post processing.</summary>
    public sealed class LedColorSpillRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader spillShader;
        [SerializeField] private bool sceneView = true;
        private Material material;
        private SpillPass pass;
        public Shader SpillShader { get => spillShader; set => spillShader = value; }
        public int LastRenderedPanels => pass == null ? 0 : pass.RenderedPanels;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (spillShader == null) spillShader = Shader.Find("Hidden/Mizotake/LED Wall/Color Spill");
            material = spillShader != null ? CoreUtils.CreateEngineMaterial(spillShader) : null;
            pass = material != null ? new SpillPass(material) : null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || renderingData.cameraData.renderType != CameraRenderType.Base || renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection) return;
            if (!sceneView && renderingData.cameraData.isSceneViewCamera) return;
            var active = false;
            foreach (var emitter in LedColorSpill.Emitters) if (emitter != null && emitter.Ready && (emitter.ReceiverMask & renderingData.cameraData.camera.cullingMask) != 0) { active = true; break; }
            if (active) renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material); material = null; pass = null;
        }

        private sealed class SpillPass : ScriptableRenderPass
        {
            private readonly Material material;
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            private readonly Dictionary<int, TextureHandle> masks = new Dictionary<int, TextureHandle>();
            private static readonly List<ShaderTagId> Tags = new List<ShaderTagId> { new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("UniversalGBuffer") };
            private static readonly ProfilingSampler MaskSampler = new ProfilingSampler("LED Spill.Receiver mask");
            private static readonly ProfilingSampler SpillSampler = new ProfilingSampler("LED Spill.Diffuse integration");
            public int RenderedPanels { get; private set; }

            public SpillPass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            private sealed class MaskData { public RendererListHandle renderers; }
            private sealed class SpillData
            {
                public Material material;
                public MaterialPropertyBlock properties;
                public TextureHandle colors, mask, depth, normals;
                public Vector4 center, right, up, normal, grid, settings;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var light = frameData.Get<UniversalLightData>();
                RenderedPanels = 0;
                masks.Clear(); // Graph handles never survive the current recording.
                if (!resources.cameraDepthTexture.IsValid() || !resources.cameraNormalsTexture.IsValid()) return;
                foreach (var emitter in LedColorSpill.Emitters)
                {
                    if (emitter == null || !emitter.Ready) continue;
                    var receiverMask = emitter.ReceiverMask & camera.camera.cullingMask;
                    if (receiverMask == 0) continue;
                    if (!masks.TryGetValue(receiverMask, out var mask))
                    {
                        var descriptor = resources.activeColorTexture.GetDescriptor(graph);
                        descriptor.name = "LED Spill Receiver Mask";
                        descriptor.colorFormat = GraphicsFormat.R8_UNorm;
                        descriptor.depthBufferBits = DepthBits.None;
                        descriptor.msaaSamples = resources.activeDepthTexture.GetDescriptor(graph).msaaSamples;
                        descriptor.bindTextureMS = false;
                        descriptor.clearBuffer = true;
                        descriptor.clearColor = Color.black;
                        mask = graph.CreateTexture(descriptor);
                        masks.Add(receiverMask, mask);
                        var drawing = RenderingUtils.CreateDrawingSettings(Tags, rendering, camera, light, camera.defaultOpaqueSortFlags);
                        drawing.overrideMaterial = material;
                        drawing.overrideMaterialPassIndex = 1;
                        var filtering = new FilteringSettings(RenderQueueRange.opaque, receiverMask);
                        var rendererList = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
                        using (var builder = graph.AddRasterRenderPass<MaskData>("LED Spill Receiver Mask", out var data, MaskSampler))
                        {
                            data.renderers = rendererList;
                            builder.UseRendererList(rendererList);
                            builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                            builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                            builder.SetRenderFunc(static (MaskData data, RasterGraphContext context) => context.cmd.DrawRendererList(data.renderers));
                        }
                    }
                    var colors = graph.ImportTexture(emitter.EmissionHandle);
                    var transform = emitter.transform;
                    var right = transform.TransformVector(Vector3.right);
                    var up = transform.TransformVector(Vector3.up);
                    var area = Vector3.Cross(right, up).magnitude;
                    if (area < 0.000001f) continue;
                    using (var builder = graph.AddRasterRenderPass<SpillData>("LED Diffuse Color Spill", out var data, SpillSampler))
                    {
                        data.material = material;
                        data.properties = properties;
                        data.colors = colors; data.mask = mask; data.depth = resources.cameraDepthTexture; data.normals = resources.cameraNormalsTexture;
                        data.center = transform.position;
                        data.right = right; data.up = up; data.normal = -transform.forward;
                        data.grid = new Vector4(emitter.EmissionTexture.width, emitter.EmissionTexture.height, area / (emitter.EmissionTexture.width * emitter.EmissionTexture.height), 0);
                        data.settings = new Vector4(emitter.EffectiveRange, emitter.EffectiveSoftDistance, emitter.Panel.BrightnessValue * emitter.Strength, emitter.ScreenOcclusion);
                        builder.UseTexture(colors, AccessFlags.Read);
                        builder.UseTexture(mask, AccessFlags.Read);
                        builder.UseTexture(data.depth, AccessFlags.Read);
                        builder.UseTexture(data.normals, AccessFlags.Read);
                        // Additive framebuffer blending requires load/read access; no camera color copy is needed.
                        builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                        builder.SetRenderFunc(static (SpillData data, RasterGraphContext context) =>
                        {
                            var block = data.properties;
                            block.Clear();
                            RTHandle colors = data.colors, mask = data.mask, depth = data.depth, normals = data.normals;
                            block.SetTexture("_EmissionColors", colors.rt);
                            block.SetTexture("_ReceiverMask", mask.rt);
                            block.SetTexture("_CameraDepthTexture", depth.rt);
                            block.SetTexture("_CameraNormalsTexture", normals.rt);
                            block.SetVector("_EmitterCenter", data.center);
                            block.SetVector("_EmitterRight", data.right);
                            block.SetVector("_EmitterUp", data.up);
                            block.SetVector("_EmitterNormal", data.normal);
                            block.SetVector("_EmitterGrid", data.grid);
                            block.SetVector("_SpillSettings", data.settings);
                            context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, block);
                        });
                    }
                    RenderedPanels++;
                }
            }
        }
    }
}

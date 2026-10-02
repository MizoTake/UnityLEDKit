using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mizotake.LedWall
{
    [ExecuteAlways]
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class LedPlanarReflection : MonoBehaviour
    {
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private Shader blurShader;
        [SerializeField] private LayerMask reflectedLayers = ~(1 << 4);
        [SerializeField, Range(0.1f, 1f)] private float resolutionScale = 0.5f;
        [SerializeField, Range(256, 2048)] private int maximumResolution = 1024;
        [SerializeField, Range(0f, 6f)] private float blurRadius = 1.4f;
        [SerializeField, Range(0f, 1f)] private float strength = 0.22f;
        [SerializeField, Min(0.001f)] private float clipOffset = 0.03f;
        [SerializeField] private bool renderInEditMode = true;
        private Camera reflectionCamera;
        private RenderTexture raw;
        private RenderTexture filtered;
        private Material blurMaterial;
        private Renderer target;
        private MaterialPropertyBlock properties;
        private bool rendering;
        private readonly UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();
        private static readonly int ReflectionTexture = Shader.PropertyToID("_PlanarReflectionTexture");
        private static readonly int ReflectionMatrix = Shader.PropertyToID("_PlanarReflectionVP");
        private static readonly int ReflectionStrength = Shader.PropertyToID("_ReflectionStrength");
        private static readonly int ReflectionValid = Shader.PropertyToID("_ReflectionValid");
        private static readonly int BlurDirection = Shader.PropertyToID("_BlurDirection");

        public Camera SourceCamera { get => sourceCamera; set => sourceCamera = value; }
        public Shader BlurShader { get => blurShader; set => blurShader = value; }
        public float Strength { get => strength; set => strength = Mathf.Clamp01(value); }
        public float BlurRadius { get => blurRadius; set => blurRadius = Mathf.Clamp(value, 0f, 6f); }
        public float ResolutionScale { get => resolutionScale; set => resolutionScale = Mathf.Clamp(value, 0.1f, 1f); }
        public RenderTexture ReflectionTextureValue => filtered;

        private void LateUpdate()
        {
            if (!Application.isPlaying && !renderInEditMode) return;
            RenderReflection(sourceCamera != null ? sourceCamera : Camera.main);
        }

        public bool RenderReflection(Camera camera)
        {
            if (rendering || camera == null || camera == reflectionCamera || camera.stereoEnabled || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) return false;
            if (Vector3.Dot(camera.transform.position - transform.position, transform.up) <= clipOffset) { Invalidate(); return false; }
            EnsureResources(camera);
            if (blurMaterial == null || !RenderPipeline.SupportsRenderRequest(reflectionCamera, request)) { Invalidate(); return false; }
            reflectionCamera.CopyFrom(camera);
            reflectionCamera.enabled = false;
            reflectionCamera.cameraType = CameraType.Reflection;
            reflectionCamera.cullingMask = reflectedLayers.value;
            reflectionCamera.useOcclusionCulling = false;
            reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
            reflectionCamera.backgroundColor = camera.backgroundColor;
            reflectionCamera.allowHDR = true;
            reflectionCamera.allowMSAA = false;
            var cameraData = reflectionCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            cameraData.volumeLayerMask = 0;
            cameraData.antialiasing = AntialiasingMode.None;
            var reflection = ReflectionMath.CreateReflection(transform.position, transform.up);
            reflectionCamera.transform.SetPositionAndRotation(reflection.MultiplyPoint(camera.transform.position), Quaternion.LookRotation(reflection.MultiplyVector(camera.transform.forward), reflection.MultiplyVector(camera.transform.up)));
            reflectionCamera.worldToCameraMatrix = camera.worldToCameraMatrix * reflection;
            var clipPosition = reflectionCamera.worldToCameraMatrix.MultiplyPoint(transform.position + transform.up * clipOffset);
            var clipNormal = reflectionCamera.worldToCameraMatrix.MultiplyVector(transform.up).normalized;
            reflectionCamera.projectionMatrix = camera.projectionMatrix;
            reflectionCamera.projectionMatrix = reflectionCamera.CalculateObliqueMatrix(new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPosition, clipNormal)));
            var previousCulling = GL.invertCulling;
            var previousRenderingOff = target.forceRenderingOff;
            var previousTarget = RenderTexture.active;
            rendering = true;
            try
            {
                GL.invertCulling = !previousCulling;
                target.forceRenderingOff = true;
                request.destination = raw;
                RenderPipeline.SubmitRenderRequest(reflectionCamera, request);
            }
            finally
            {
                GL.invertCulling = previousCulling;
                target.forceRenderingOff = previousRenderingOff;
                rendering = false;
                RenderTexture.active = previousTarget;
            }
            var intermediateDescriptor = filtered.descriptor;
            intermediateDescriptor.useMipMap = false;
            intermediateDescriptor.autoGenerateMips = false;
            var intermediate = RenderTexture.GetTemporary(intermediateDescriptor);
            try
            {
                blurMaterial.SetVector(BlurDirection, new Vector4(blurRadius / raw.width, 0f, 0f, 0f));
                Graphics.Blit(raw, intermediate, blurMaterial);
                blurMaterial.SetVector(BlurDirection, new Vector4(0f, blurRadius / raw.height, 0f, 0f));
                Graphics.Blit(intermediate, filtered, blurMaterial);
                filtered.GenerateMips();
            }
            finally { RenderTexture.active = previousTarget; RenderTexture.ReleaseTemporary(intermediate); }
            target.GetPropertyBlock(properties);
            properties.SetTexture(ReflectionTexture, filtered);
            properties.SetMatrix(ReflectionMatrix, GL.GetGPUProjectionMatrix(reflectionCamera.projectionMatrix, true) * reflectionCamera.worldToCameraMatrix);
            properties.SetFloat(ReflectionStrength, strength);
            properties.SetFloat(ReflectionValid, 1f);
            target.SetPropertyBlock(properties);
            return true;
        }

        private void EnsureResources(Camera camera)
        {
            if (target == null) target = GetComponent<Renderer>();
            if (properties == null) properties = new MaterialPropertyBlock();
            if (reflectionCamera == null)
            {
                var cameraObject = new GameObject("LED Planar Reflection Camera") { hideFlags = HideFlags.HideAndDontSave };
                reflectionCamera = cameraObject.AddComponent<Camera>();
                reflectionCamera.enabled = false;
            }
            if (blurShader != null && (blurMaterial == null || blurMaterial.shader != blurShader))
            {
                DestroyOwned(blurMaterial);
                blurMaterial = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
            }
            var size = ReflectionMath.GetTargetSize(camera.pixelWidth, camera.pixelHeight, resolutionScale, maximumResolution);
            if (raw != null && raw.width == size.x && raw.height == size.y) return;
            ReleaseTextures();
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            raw = new RenderTexture(size.x, size.y, 24, format, RenderTextureReadWrite.Linear) { name = "LED Reflection Raw", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            filtered = new RenderTexture(size.x, size.y, 0, format, RenderTextureReadWrite.Linear) { name = "LED Reflection Filtered", filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, useMipMap = true, autoGenerateMips = false };
            raw.Create();
            filtered.Create();
        }

        private void Invalidate()
        {
            if (target == null) target = GetComponent<Renderer>();
            if (target == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            target.GetPropertyBlock(properties);
            properties.SetFloat(ReflectionValid, 0f);
            properties.SetTexture(ReflectionTexture, Texture2D.blackTexture);
            target.SetPropertyBlock(properties);
        }

        private void OnDisable()
        {
            Invalidate();
            ReleaseTextures();
            if (reflectionCamera != null) DestroyOwned(reflectionCamera.gameObject);
            DestroyOwned(blurMaterial);
            reflectionCamera = null;
            blurMaterial = null;
        }

        private void ReleaseTextures()
        {
            if (RenderTexture.active == raw || RenderTexture.active == filtered) RenderTexture.active = null;
            if (raw != null) { raw.Release(); DestroyOwned(raw); raw = null; }
            if (filtered != null) { filtered.Release(); DestroyOwned(filtered); filtered = null; }
            request.destination = null;
        }

        private static void DestroyOwned(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}

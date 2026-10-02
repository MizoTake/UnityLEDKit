using UnityEngine;
using UnityEngine.Video;

namespace Mizotake.LedWall
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class LedPanel : MonoBehaviour
    {
        [SerializeField] private VideoPlayer source;
        [SerializeField] private VideoPlayer secondarySource;
        [SerializeField] private Texture texture;
        [SerializeField] private Texture secondaryTexture;
        [SerializeField, Range(0f, 1f)] private float transition;
        [SerializeField] private Vector2Int resolution = new Vector2Int(256, 144);
        [SerializeField, Range(0f, 20f)] private float brightness = 2.5f;
        [SerializeField, Range(0f, 1f)] private float rgbSeparation = 1f;
        [SerializeField, Range(0.2f, 0.95f)] private float fill = 0.72f;
        [SerializeField, Range(0f, 1f)] private float diffusion = 0.25f;
        [SerializeField] private Vector4 contentRect = new Vector4(1f, 1f, 0f, 0f);
        private Renderer target;
        private MaterialPropertyBlock properties;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BlendMap = Shader.PropertyToID("_BlendMap");
        private static readonly int TransitionId = Shader.PropertyToID("_Transition");
        private static readonly int Grid = Shader.PropertyToID("_LedResolution");
        private static readonly int Brightness = Shader.PropertyToID("_Brightness");
        private static readonly int RgbSeparation = Shader.PropertyToID("_RgbSeparation");
        private static readonly int Fill = Shader.PropertyToID("_Fill");
        private static readonly int Diffusion = Shader.PropertyToID("_Diffusion");
        private static readonly int ContentRectId = Shader.PropertyToID("_ContentRect");

        public VideoPlayer Source { get => source; set => source = value; }
        public VideoPlayer SecondarySource { get => secondarySource; set => secondarySource = value; }
        public Texture Texture { get => texture; set => texture = value; }
        public Texture SecondaryTexture { get => secondaryTexture; set => secondaryTexture = value; }
        public float Transition { get => transition; set => transition = Mathf.Clamp01(value); }
        public Texture PrimaryOutput => GetVideoTexture(source, texture);
        public Texture SecondaryOutput => GetVideoTexture(secondarySource, secondaryTexture != null ? secondaryTexture : PrimaryOutput);
        public Vector4 ContentRect => contentRect;
        public Vector2Int Resolution { get => resolution; set => resolution = new Vector2Int(Mathf.Max(1, value.x), Mathf.Max(1, value.y)); }
        public float BrightnessValue { get => brightness; set => brightness = Mathf.Max(0f, value); }
        public float RgbSeparationValue { get => rgbSeparation; set => rgbSeparation = Mathf.Clamp01(value); }
        public float DiffusionValue { get => diffusion; set => diffusion = Mathf.Clamp01(value); }

        private void OnEnable() => Apply();
        private void LateUpdate() => Apply();
        private void OnValidate() { resolution = new Vector2Int(Mathf.Max(1, resolution.x), Mathf.Max(1, resolution.y)); brightness = Mathf.Max(0f, brightness); }

        private static Texture GetVideoTexture(VideoPlayer player, Texture fallback) => player != null && player.isPrepared && player.frame >= 0 && player.texture != null ? player.texture : fallback;

        public void Apply()
        {
            if (target == null) target = GetComponent<Renderer>();
            if (target == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            target.GetPropertyBlock(properties);
            var content = PrimaryOutput;
            properties.SetTexture(BaseMap, content != null ? content : Texture2D.blackTexture);
            properties.SetTexture(BlendMap, SecondaryOutput != null ? SecondaryOutput : Texture2D.blackTexture);
            properties.SetFloat(TransitionId, transition);
            properties.SetVector(Grid, new Vector4(Mathf.Max(1, resolution.x), Mathf.Max(1, resolution.y), 0f, 0f));
            properties.SetFloat(Brightness, brightness);
            properties.SetFloat(RgbSeparation, rgbSeparation);
            properties.SetFloat(Fill, fill);
            properties.SetFloat(Diffusion, diffusion);
            properties.SetVector(ContentRectId, contentRect);
            target.SetPropertyBlock(properties);
        }
    }
}

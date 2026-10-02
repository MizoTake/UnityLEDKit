using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mizotake.LedWall.Tests
{
    public sealed class LedAssetTests
    {
        [Test]
        public void PanelTintAndPhysicalSurfacePropertiesAreAvailable()
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Quad);
            try
            {
                var panel = item.AddComponent<LedPanel>();
                Assert.That(new SerializedObject(panel).FindProperty("tint"), Is.Not.Null, "Tint must be controlled by LedPanel so display and lights share the value.");
                foreach (var name in new[] { "Mizotake/LED Wall/RGB LED", "Mizotake/LED Wall/Diffused LED" })
                {
                    var material = new Material(Shader.Find(name));
                    try
                    {
                        foreach (var property in new[] { "_LensCurvature", "_LensSmoothness", "_HousingSmoothness", "_SurfaceLighting" }) Assert.That(material.HasProperty(property), Is.True, name + ": " + property);
                    }
                    finally { Object.DestroyImmediate(material); }
                }
            }
            finally { Object.DestroyImmediate(item); }
        }

        [TestCase("Mizotake/LED Wall/RGB LED")]
        [TestCase("Mizotake/LED Wall/Diffused LED")]
        [TestCase("Mizotake/LED Wall/Reflective Floor")]
        [TestCase("Hidden/Mizotake/LED Wall/Reflection Blur")]
        [TestCase("Hidden/Mizotake/LED Wall/Lighting Sampler")]
        public void ShadersExistAndHaveNoCompilerErrors(string name)
        {
            var shader = Shader.Find(name);
            Assert.That(shader, Is.Not.Null, name);
            Assert.That(shader.isSupported, Is.True, name);
            var errors = ShaderUtil.GetShaderMessages(shader).Where(message => message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(message => message.message).ToArray();
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [Test]
        public void PanelClampsInvalidResolutionAndPreservesOtherPropertyBlockValues()
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Quad);
            try
            {
                var renderer = item.GetComponent<Renderer>();
                var block = new MaterialPropertyBlock();
                block.SetFloat("_UnrelatedValue", 42f);
                renderer.SetPropertyBlock(block);
                var panel = item.AddComponent<LedPanel>();
                panel.Resolution = new Vector2Int(0, -7);
                panel.Texture = Texture2D.whiteTexture;
                panel.Apply();
                renderer.GetPropertyBlock(block);
                Assert.That(panel.Resolution, Is.EqualTo(Vector2Int.one));
                Assert.That(block.GetTexture("_BaseMap"), Is.SameAs(Texture2D.whiteTexture));
                Assert.That(block.GetFloat("_UnrelatedValue"), Is.EqualTo(42f));
            }
            finally { Object.DestroyImmediate(item); }
        }

        [Test]
        public void ReflectionWithNoCameraDoesNotAllocateRenderTargets()
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Plane);
            try
            {
                var reflection = item.AddComponent<LedPlanarReflection>();
                Assert.That(reflection.RenderReflection(null), Is.False);
                Assert.That(reflection.ReflectionTextureValue, Is.Null);
            }
            finally { Object.DestroyImmediate(item); }
        }

        [Test]
        public void PanelAcceptsStandardVideoPlayersForBothSources()
        {
            Assert.That(typeof(LedPanel).GetProperty("Source").PropertyType, Is.EqualTo(typeof(UnityEngine.Video.VideoPlayer)));
            Assert.That(typeof(LedPanel).GetProperty("SecondarySource").PropertyType, Is.EqualTo(typeof(UnityEngine.Video.VideoPlayer)));
        }

        [Test]
        public void GeneratedLightSettingsAreEditableInTheInspector()
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Quad);
            try
            {
                var lighting = item.AddComponent<LedPanelLighting>();
                var settings = new SerializedObject(lighting);
                Assert.That(settings.FindProperty("overrideLightParameters"), Is.Not.Null);
                Assert.That(settings.FindProperty("overrideLightParameters").boolValue, Is.False, "Automatic panel-derived lighting must be the default.");
                foreach (var name in new[] { "cullingMask", "renderingLayers", "renderMode", "innerSpotRatio", "shadowStrength", "shadowBias", "shadowNormalBias", "shadowNearPlane", "customShadowLayers", "shadowRenderingLayers" }) Assert.That(settings.FindProperty(name), Is.Not.Null, "Generated light setting is missing: " + name);
            }
            finally { Object.DestroyImmediate(item); }
        }
    }
}

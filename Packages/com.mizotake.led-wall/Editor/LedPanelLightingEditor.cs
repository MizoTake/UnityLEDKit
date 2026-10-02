using UnityEditor;
using UnityEngine;

namespace Mizotake.LedWall.Editor
{
    [CustomEditor(typeof(LedPanelLighting)), CanEditMultipleObjects]
    public sealed class LedPanelLightingEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            Draw("samplingShader", "lightGrid", "updatesPerSecond", "samplingResolution", "responseSeconds", "sceneCutThreshold", "softDistribution");
            EditorGUILayout.Space();
            var overrides = serializedObject.FindProperty("overrideLightParameters");
            EditorGUILayout.PropertyField(overrides);
            if (!overrides.boolValue && !overrides.hasMultipleDifferentValues) EditorGUILayout.HelpBox("Automatic: video color, tint, crop, crossfade, brightness, size and orientation follow LedPanel. Enable overrides to adjust emission manually.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!overrides.boolValue && !overrides.hasMultipleDifferentValues)) Draw("intensity", "range", "spotAngle", "innerSpotRatio", "downwardBias");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Receiver filtering", EditorStyles.boldLabel);
            Draw("cullingMask", "renderingLayers", "renderMode");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shadows", EditorStyles.boldLabel);
            Draw("castShadows");
            using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("castShadows").boolValue)) Draw("shadowStrength", "shadowBias", "shadowNormalBias", "shadowNearPlane", "customShadowLayers");
            using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("castShadows").boolValue || !serializedObject.FindProperty("customShadowLayers").boolValue)) Draw("shadowRenderingLayers");
            serializedObject.ApplyModifiedProperties();
            if (Application.isPlaying && targets.Length == 1)
            {
                var lighting = (LedPanelLighting)target;
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Sampling", lighting.SamplingSize.x + " x " + lighting.SamplingSize.y);
                EditorGUILayout.LabelField("Completed / History resets", lighting.CompletedSamples + " / " + lighting.HistoryResets);
                EditorGUILayout.LabelField("Last GPU readback", lighting.LastReadbackMilliseconds.ToString("F1") + " ms");
                if (!string.IsNullOrEmpty(lighting.LastError)) EditorGUILayout.HelpBox(lighting.LastError, MessageType.Error);
            }
        }

        private void Draw(params string[] names) { foreach (var name in names) EditorGUILayout.PropertyField(serializedObject.FindProperty(name)); }
    }
}

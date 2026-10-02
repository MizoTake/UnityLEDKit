using UnityEditor;
using UnityEngine;

namespace Mizotake.LedWall.Editor
{
    [CustomEditor(typeof(LedColorSpill)), CanEditMultipleObjects]
    public sealed class LedColorSpillEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "range", "softDistance");
            using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("overrideDistribution").boolValue))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("range"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("softDistance"));
            }
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Add LED Color Spill Renderer Feature to the active URP Renderer. Emission follows LedPanel. This adds diffuse color to opaque receivers; material BRDF and off-screen shadows are not evaluated.", MessageType.Info);
            if (targets.Length == 1)
            {
                var spill = (LedColorSpill)target;
                EditorGUILayout.LabelField("Submitted GPU samples", spill.SubmittedSamples.ToString());
                if (!string.IsNullOrEmpty(spill.LastError)) EditorGUILayout.HelpBox(spill.LastError, MessageType.Error);
            }
        }
    }
}

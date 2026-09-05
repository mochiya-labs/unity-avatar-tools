using UnityEditor;
using UnityEngine;

namespace Mochiya.LilToon.Exporter.Editor
{
    [CustomEditor(typeof(MochiyaExportProfile))]
    public sealed class MochiyaExportProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var isDefault = target == MochiyaExportProfile.Default;
            if (isDefault)
            {
                EditorGUILayout.HelpBox("Ready to export: uses the object's name, Mochiya VRM Exporter as author, and sparse morph targets. Create a copy to save different settings.", MessageType.Info);
                if (GUILayout.Button("Create editable copy")) MochiyaExportProfileGUI.CreateCopy((MochiyaExportProfile)target);
            }
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(isDefault))
            {
                EditorGUILayout.LabelField("VRM metadata", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MochiyaExportProfile.UseAttachedVrmMetadata)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MochiyaExportProfile.Metadata)), true);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("VRM export settings", EditorStyles.boldLabel);
                DrawPropertiesExcluding(serializedObject, "m_Script", nameof(MochiyaExportProfile.Metadata),
                    nameof(MochiyaExportProfile.UseAttachedVrmMetadata), nameof(MochiyaExportProfile.GlbSettings));
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MochiyaExportProfile.GlbSettings)), new GUIContent("GLB export settings"), true);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

    internal static class MochiyaExportProfileGUI
    {
        internal static void Draw(ref MochiyaExportProfile profile)
        {
            if (profile == null) profile = MochiyaExportProfile.Default;
            profile = (MochiyaExportProfile)EditorGUILayout.ObjectField("Export profile", profile, typeof(MochiyaExportProfile), false);
            if (profile == null) profile = MochiyaExportProfile.Default;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Inspect profile")) Selection.activeObject = profile;
                if (GUILayout.Button("New profile…"))
                {
                    var created = CreateCopy(profile);
                    if (created != null) profile = created;
                }
            }
        }

        internal static MochiyaExportProfile CreateCopy(MochiyaExportProfile source)
        {
            var path = EditorUtility.SaveFilePanelInProject("Create export profile", "Mochiya Export Profile", "asset", "Save reusable export settings.");
            if (string.IsNullOrEmpty(path)) return null;
            var copy = Object.Instantiate(source);
            copy.hideFlags = HideFlags.None;
            // Never replace an existing profile when making a new one.
            AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();
            Selection.activeObject = copy;
            EditorGUIUtility.PingObject(copy);
            return copy;
        }
    }
}

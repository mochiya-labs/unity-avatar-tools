using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mochiya.LilToon.Exporter.Editor
{
    public sealed class MochiyaAvatarToolsWindow : EditorWindow
    {
        [SerializeField] private GameObject target;
        [SerializeField] private MochiyaExportProfile profile;
        [SerializeField] private bool showProfile;
        [SerializeField] private bool showWarnings;
        private MochiyaConversionReport completedReport;
        private Vector2 scroll;
        private string status;
        private bool failed;
        private double nextValidation;
        private MochiyaAvatarTarget detected;
        private MochiyaConversionReport vrmReport, glbReport;

        [MenuItem("Mochiya/Avatar Tools")]
        public static void Open()
        {
            var window = GetWindow<MochiyaAvatarToolsWindow>("Mochiya Avatar Tools");
            window.minSize = new Vector2(440, 280);
            if (window.target == null) window.target = Selection.activeGameObject;
            window.Show();
        }

        private void OnEnable() { if (profile == null) profile = MochiyaExportProfile.Default; }
        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Convert or export your model", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Choose an avatar, or an attachment placed directly under its avatar.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(10);
            EditorGUI.BeginChangeCheck();
            target = (GameObject)EditorGUILayout.ObjectField("Avatar or attachment", target, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) { nextValidation = 0; status = null; completedReport = null; }
            if (vrmReport == null || EditorApplication.timeSinceStartup >= nextValidation)
            {
                detected = MochiyaAvatarWorkflow.Detect(target);
                vrmReport = MochiyaAvatarWorkflow.Validate(target, true, profile);
                glbReport = MochiyaAvatarWorkflow.Validate(target, false, profile);
                nextValidation = EditorApplication.timeSinceStartup + .5;
            }
            if (detected.Kind != MochiyaTargetKind.Invalid)
            {
                EditorGUILayout.LabelField("Detected asset", detected.Kind.ToString(), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(detected.Reason, EditorStyles.wordWrappedLabel);
                if (detected.Kind == MochiyaTargetKind.Attachment && !detected.IsPrepared)
                    EditorGUILayout.LabelField("Reference avatar", detected.ReferenceAvatar.name);
            }
            else EditorGUILayout.LabelField("Detected asset", "Neither", EditorStyles.boldLabel);
            foreach (var error in vrmReport.Errors.Union(glbReport.Errors)) EditorGUILayout.HelpBox(error, MessageType.Error);
            showWarnings = EditorGUILayout.Foldout(showWarnings, "Compatibility warnings (optional)", true);
            if (showWarnings)
            {
                var warnings = CompatibilityWarnings(vrmReport, glbReport, completedReport);
                if (warnings.Length == 0) EditorGUILayout.LabelField("No compatibility warnings reported.", EditorStyles.miniLabel);
                foreach (var warning in warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }
            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!vrmReport.CanConvert || detected.IsPrepared))
                    if (GUILayout.Button("Convert to VRM GameObject", GUILayout.Height(36))) ConvertSelected();
                using (new EditorGUI.DisabledScope(!vrmReport.CanConvert && !glbReport.CanConvert))
                    if (GUILayout.Button("Export VRM / GLB…", GUILayout.Height(36))) ShowExportMenu();
            }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, failed ? MessageType.Error : MessageType.Info);
            EditorGUILayout.Space(12);
            showProfile = EditorGUILayout.Foldout(showProfile, "Export settings (optional)", true);
            if (showProfile)
            {
                EditorGUI.BeginChangeCheck();
                MochiyaExportProfileGUI.Draw(ref profile);
                if (EditorGUI.EndChangeCheck()) nextValidation = 0;
            }
            EditorGUILayout.EndScrollView();
        }

        private void ConvertSelected()
        {
            try
            {
                var converted = MochiyaAvatarWorkflow.ConvertToVrmGameObject(target, profile);
                completedReport = converted.Report;
                status = "Created " + converted.Root.name + " in the Hierarchy.";
                failed = false;
            }
            catch (Exception exception) { status = exception.Message; failed = true; Debug.LogException(exception); }
        }

        private void ShowExportMenu()
        {
            var menu = new GenericMenu();
            if (vrmReport.CanConvert) menu.AddItem(new GUIContent("Export VRM…"), false, () => ExportSelected("vrm"));
            else menu.AddDisabledItem(new GUIContent("Export VRM…"));
            if (glbReport.CanConvert) menu.AddItem(new GUIContent("Export GLB…"), false, () => ExportSelected("glb"));
            else menu.AddDisabledItem(new GUIContent("Export GLB…"));
            menu.ShowAsContext();
        }

        private void ExportSelected(string extension)
        {
            var path = MochiyaExportDialog.ChoosePath(target, extension);
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                EditorUtility.DisplayProgressBar("Mochiya Export", "Exporting " + target.name + "…", .5f);
                completedReport = MochiyaAvatarWorkflow.Export(target, path, profile);
                MochiyaExportDialog.RememberPath(path);
                status = "Exported " + Path.GetFileName(path) + "\n" + path;
                failed = false;
            }
            catch (Exception exception) { status = exception.Message; failed = true; Debug.LogException(exception); }
            finally { EditorUtility.ClearProgressBar(); Repaint(); }
        }

        internal static string[] CompatibilityWarnings(params MochiyaConversionReport[] reports) => reports.Where(r => r != null)
            .SelectMany(r => r.Unsupported.Select(w => "Unsupported: " + w).Concat(r.Warnings)).Distinct().ToArray();
    }

    internal static class MochiyaExportDialog
    {
        private const string DirectoryKey = "Mochiya.LilToonExporter.LastDirectory";
        internal static string ChoosePath(GameObject root, string extension) => EditorUtility.SaveFilePanel(
            "Export " + extension.ToUpperInvariant(), EditorPrefs.GetString(DirectoryKey, Application.dataPath), root != null ? root.name : "Avatar", extension);
        internal static void RememberPath(string path) => EditorPrefs.SetString(DirectoryKey, Path.GetDirectoryName(path) ?? Application.dataPath);
    }
}

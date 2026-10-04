using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor
{
    public sealed class MochiyaAvatarToolsWindow : EditorWindow
    {
        [SerializeField] private GameObject target;
        [SerializeField] private MochiyaExportProfile profile;
        [SerializeField] private bool showProfile;
        [SerializeField] private bool showWarnings;
        [SerializeField] private AnimationClip animationClip;
        [SerializeField] private string locale = "en";
        private string animationStatus;
        private bool animationFailed;
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

        private string T(string text) => MochiyaPanelText.Get(locale, text);

        private void OnGUI()
        {
            var previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * .36f, 185, 240);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var selectedLocale = MochiyaPanelText.LanguageField(locale);
            if (selectedLocale != locale) { locale = selectedLocale; status = null; animationStatus = null; }
            EditorGUILayout.LabelField(T(MochiyaPanelText.ToolsIntro), EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(T("Convert or export your model"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(T("Choose an avatar, or an attachment placed directly under its avatar."), EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(10);
            EditorGUI.BeginChangeCheck();
            target = (GameObject)EditorGUILayout.ObjectField(T("Avatar or attachment"), target, typeof(GameObject), true);
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
                EditorGUILayout.LabelField(T("Detected asset"), T(detected.Kind.ToString()), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(T(detected.Reason), EditorStyles.wordWrappedLabel);
                if (detected.Kind == MochiyaTargetKind.Attachment && !detected.IsPrepared)
                    EditorGUILayout.LabelField(T("Reference avatar"), detected.ReferenceAvatar.name);
            }
            else EditorGUILayout.LabelField(T("Detected asset"), T("Neither"), EditorStyles.boldLabel);
            foreach (var error in vrmReport.Errors.Union(glbReport.Errors)) EditorGUILayout.HelpBox(T(error), MessageType.Error);
            showWarnings = EditorGUILayout.Foldout(showWarnings, T("Compatibility warnings (optional)"), true);
            if (showWarnings)
            {
                var warnings = CompatibilityWarnings(vrmReport, glbReport, completedReport);
                if (warnings.Length == 0) EditorGUILayout.LabelField(T("No compatibility warnings reported."), EditorStyles.miniLabel);
                foreach (var warning in warnings) EditorGUILayout.HelpBox(T(warning), MessageType.Warning);
            }
            EditorGUILayout.Space(10);
            showProfile = EditorGUILayout.Foldout(showProfile, T("Export settings (optional)"), true);
            if (showProfile)
            {
                EditorGUI.BeginChangeCheck();
                MochiyaExportProfileGUI.Draw(ref profile, locale);
                if (EditorGUI.EndChangeCheck()) nextValidation = 0;
            }
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!vrmReport.CanConvert || detected.IsPrepared))
                    if (GUILayout.Button(T("Convert to VRM GameObject"), GUILayout.Height(36))) ConvertSelected();
                using (new EditorGUI.DisabledScope(!vrmReport.CanConvert && !glbReport.CanConvert))
                    if (GUILayout.Button(T("Export VRM / GLB…"), GUILayout.Height(36))) ShowExportMenu();
            }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, failed ? MessageType.Error : MessageType.Info);
            EditorGUILayout.Space(16);
            EditorGUILayout.LabelField(T("Export animation"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(T(MochiyaPanelText.AnimationIntro), EditorStyles.wordWrappedLabel);
            EditorGUI.BeginChangeCheck();
            animationClip = (AnimationClip)EditorGUILayout.ObjectField(T("Animation Clip"), animationClip, typeof(AnimationClip), false);
            if (EditorGUI.EndChangeCheck()) animationStatus = null;
            if (animationClip != null && (animationClip.legacy || !animationClip.isHumanMotion))
                EditorGUILayout.HelpBox(T("Choose a Humanoid clip. Generic and Legacy clips are not supported."), MessageType.Warning);
            using (new EditorGUI.DisabledScope(animationClip == null || animationClip.legacy || !animationClip.isHumanMotion || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button(T("Export VRM Animation (.vrma)…"), GUILayout.Height(32))) ExportAnimation();
            if (!string.IsNullOrEmpty(animationStatus))
                EditorGUILayout.HelpBox(animationStatus, animationFailed ? MessageType.Error : MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        private void ExportAnimation()
        {
            var path = EditorUtility.SaveFilePanel(T("Export VRM Animation"), "", animationClip.name, "vrma");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                MochiyaAnimationExporter.Export(animationClip, path, progress =>
                {
                    if (EditorUtility.DisplayCancelableProgressBar(T("Export VRM Animation"), string.Format(T("Sampling {0}…"), animationClip.name), progress))
                        throw new OperationCanceledException();
                });
                animationFailed = false; animationStatus = string.Format(T("Exported {0}"), Path.GetFileName(path)) + "\n" + path;
            }
            catch (OperationCanceledException) { animationFailed = false; animationStatus = T("Animation export cancelled."); }
            catch (Exception error) { animationFailed = true; animationStatus = T(error.Message); Debug.LogException(error); }
            finally { EditorUtility.ClearProgressBar(); Repaint(); }
        }

        private void ConvertSelected()
        {
            try
            {
                var converted = MochiyaAvatarWorkflow.ConvertToVrmGameObject(target, profile);
                completedReport = converted.Report;
                status = string.Format(T("Created {0} in the Hierarchy."), converted.Root.name);
                failed = false;
            }
            catch (Exception exception) { status = T(exception.Message); failed = true; Debug.LogException(exception); }
        }

        private void ShowExportMenu()
        {
            var menu = new GenericMenu();
            if (vrmReport.CanConvert) menu.AddItem(new GUIContent(T("Export VRM…")), false, () => ExportSelected("vrm"));
            else menu.AddDisabledItem(new GUIContent(T("Export VRM…")));
            if (glbReport.CanConvert) menu.AddItem(new GUIContent(T("Export GLB…")), false, () => ExportSelected("glb"));
            else menu.AddDisabledItem(new GUIContent(T("Export GLB…")));
            menu.ShowAsContext();
        }

        private void ExportSelected(string extension)
        {
            var path = MochiyaExportDialog.ChoosePath(target, extension, locale);
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                EditorUtility.DisplayProgressBar(T("Mochiya Export"), string.Format(T("Exporting {0}…"), target.name), .5f);
                completedReport = MochiyaAvatarWorkflow.Export(target, path, profile);
                MochiyaExportDialog.RememberPath(path);
                status = string.Format(T("Exported {0}"), Path.GetFileName(path)) + "\n" + path;
                failed = false;
            }
            catch (Exception exception) { status = T(exception.Message); failed = true; Debug.LogException(exception); }
            finally { EditorUtility.ClearProgressBar(); Repaint(); }
        }

        internal static string[] CompatibilityWarnings(params MochiyaConversionReport[] reports) => reports.Where(r => r != null)
            .SelectMany(r => r.Unsupported.Select(w => "Unsupported: " + w).Concat(r.Warnings)).Distinct().ToArray();
    }

    internal static class MochiyaExportDialog
    {
        private const string DirectoryKey = "Mochiya.LilToonExporter.LastDirectory";
        internal static string ChoosePath(GameObject root, string extension, string locale = "en") => EditorUtility.SaveFilePanel(
            string.Format(MochiyaPanelText.Get(locale, "Export {0}"), extension.ToUpperInvariant()), EditorPrefs.GetString(DirectoryKey, Application.dataPath), root != null ? root.name : "Avatar", extension);
        internal static void RememberPath(string path) => EditorPrefs.SetString(DirectoryKey, Path.GetDirectoryName(path) ?? Application.dataPath);
    }
}

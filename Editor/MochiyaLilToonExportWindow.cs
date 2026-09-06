using System;
using System.IO;
using System.Linq;
using UniVRM10;
using UnityEditor;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor
{
    public sealed class MochiyaLilToonExportWindow : EditorWindow
    {
        private enum ExportFormat { Glb, Vrm }
        [SerializeField] private GameObject _root;
        [SerializeField] private ExportFormat _format;
        [SerializeField] private MochiyaExportProfile _profile;
        [SerializeField] private bool _showProfile;
        private Vector2 _scroll;
        private string _status;
        private bool _failed;

        [MenuItem("Mochiya/Export GLB or VRM with lilToon...")]
        public static void Open()
        {
            var window = GetWindow<MochiyaLilToonExportWindow>("Mochiya lilToon Export");
            window.minSize = new Vector2(440, 280);
            window.Show();
        }
        public static void OpenFor(GameObject root)
        {
            Open();
            var window = GetWindow<MochiyaLilToonExportWindow>();
            window._root = root;
            window._format = root != null && root.GetComponent<Vrm10Instance>() != null ? ExportFormat.Vrm : ExportFormat.Glb;
        }
        private void OnEnable()
        {
            if (_profile == null) _profile = MochiyaExportProfile.Default;
            if (_root == null) _root = Selection.activeGameObject;
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Export your model", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _root = (GameObject)EditorGUILayout.ObjectField("Model", _root, typeof(GameObject), true);
            _format = (ExportFormat)EditorGUILayout.EnumPopup("Format", _format);
            if (EditorGUI.EndChangeCheck()) _status = null;
            if (_profile == null) _profile = MochiyaExportProfile.Default;
            var asVrm = _format == ExportFormat.Vrm;
            // Raw authoring setups use the same automatic workflow as Avatar Tools.
            var convert = _root != null && MochiyaAvatarWorkflow.HasAuthoringComponents(_root);
            var errors = convert ? MochiyaAvatarWorkflow.Validate(_root, asVrm, _profile).Errors.ToArray()
                : MochiyaExportValidation.Validate(_root, asVrm, _profile.CreateMetadata(_root))
                    .Where(issue => issue.Severity == MochiyaExportIssueSeverity.Error).Select(issue => issue.Message).ToArray();
            foreach (var error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            using (new EditorGUI.DisabledScope(errors.Length > 0 || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button(asVrm ? "Export VRM…" : "Export GLB…", GUILayout.Height(36))) ExportSelected(convert);
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, _failed ? MessageType.Error : MessageType.Info);
            EditorGUILayout.Space(12);
            _showProfile = EditorGUILayout.Foldout(_showProfile, "Export settings (optional)", true);
            if (_showProfile) MochiyaExportProfileGUI.Draw(ref _profile);
            EditorGUILayout.EndScrollView();
        }

        private void ExportSelected(bool convert)
        {
            var path = MochiyaExportDialog.ChoosePath(_root, _format == ExportFormat.Vrm ? "vrm" : "glb");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                EditorUtility.DisplayProgressBar("Mochiya Export", "Exporting " + _root.name + "…", .5f);
                if (convert) MochiyaAvatarWorkflow.Export(_root, path, _profile);
                else MochiyaLilToonExporter.ExportWithProfile(_root, path, _profile);
                MochiyaExportDialog.RememberPath(path);
                _status = "Exported " + Path.GetFileName(path) + "\n" + path; _failed = false;
            }
            catch (Exception exception) { _status = exception.Message; _failed = true; Debug.LogException(exception); }
            finally { EditorUtility.ClearProgressBar(); }
        }
    }
}

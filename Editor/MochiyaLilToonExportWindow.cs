using System;
using System.IO;
using System.Linq;
using UniGLTF;
using UniVRM10;
using UnityEditor;
using UnityEngine;

namespace Mochiya.LilToon.Exporter.Editor
{
    public sealed class MochiyaLilToonExportWindow : EditorWindow
    {
        private enum ExportFormat
        {
            Glb,
            Vrm,
        }

        private const string LastDirectoryKey = "Mochiya.LilToonExporter.LastDirectory";

        [SerializeField] private GameObject _root;
        [SerializeField] private ExportFormat _format;
        [SerializeField] private GltfExportSettings _glbSettings = new GltfExportSettings();
        [SerializeField] private bool _useSparseMorphTargets = true;
        [SerializeField] private bool _useAttachedVrmMeta = true;
        [SerializeField] private VRM10ObjectMeta _vrmMeta = new VRM10ObjectMeta();

        private SerializedObject _serializedWindow;

        [MenuItem("Mochiya/Export GLB or VRM with lilToon...")]
        public static void Open()
        {
            var window = GetWindow<MochiyaLilToonExportWindow>();
            window.titleContent = new GUIContent("Mochiya lilToon Export");
            window.minSize = new Vector2(480, 520);
            window.Show();
        }

        private void OnEnable()
        {
            _serializedWindow = new SerializedObject(this);
            if (_root == null) _root = Selection.activeGameObject;
        }

        private void OnGUI()
        {
            _serializedWindow.Update();
            EditorGUILayout.LabelField("Mochiya lilToon Exporter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "UniVRM exports the model/avatar. Mochiya adds the lilToon material extension consumed by three-liltoon.",
                MessageType.Info);

            EditorGUILayout.PropertyField(_serializedWindow.FindProperty(nameof(_root)));
            EditorGUILayout.PropertyField(_serializedWindow.FindProperty(nameof(_format)));
            EditorGUILayout.Space();

            if (_format == ExportFormat.Glb)
            {
                EditorGUILayout.LabelField("UniGLTF settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_serializedWindow.FindProperty(nameof(_glbSettings)), true);
            }
            else
            {
                DrawVrmSettings();
            }

            _serializedWindow.ApplyModifiedProperties();
            EditorGUILayout.Space();
            DrawValidationAndExport();
        }

        private void DrawVrmSettings()
        {
            EditorGUILayout.LabelField("VRM 1.0 settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_serializedWindow.FindProperty(nameof(_useSparseMorphTargets)));

            var attachedMeta = GetAttachedMeta();
            if (attachedMeta != null)
            {
                EditorGUILayout.PropertyField(_serializedWindow.FindProperty(nameof(_useAttachedVrmMeta)));
                if (_useAttachedVrmMeta)
                {
                    EditorGUILayout.HelpBox(
                        "Using the VRM metadata already attached to the selected Vrm10Instance. Expressions, look-at, first-person, and spring bones are also preserved by UniVRM.",
                        MessageType.Info);
                    return;
                }
            }

            EditorGUILayout.LabelField("VRM metadata", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_serializedWindow.FindProperty(nameof(_vrmMeta)), true);
        }

        private void DrawValidationAndExport()
        {
            var asVrm = _format == ExportFormat.Vrm;
            var issues = MochiyaExportValidation.Validate(_root, asVrm, asVrm ? GetSelectedMeta() : null);
            foreach (var issue in issues)
            {
                EditorGUILayout.HelpBox(
                    issue.Message,
                    issue.Severity == MochiyaExportIssueSeverity.Error ? MessageType.Error : MessageType.Warning);
            }

            var hasErrors = issues.Any(x => x.Severity == MochiyaExportIssueSeverity.Error);
            using (new EditorGUI.DisabledScope(hasErrors))
            {
                if (GUILayout.Button(asVrm ? "Export .vrm" : "Export .glb", GUILayout.Height(34)))
                {
                    ExportSelected();
                }
            }
        }

        private void ExportSelected()
        {
            var asVrm = _format == ExportFormat.Vrm;
            var extension = asVrm ? "vrm" : "glb";
            var directory = EditorPrefs.GetString(LastDirectoryKey, Application.dataPath);
            var path = EditorUtility.SaveFilePanel(
                asVrm ? "Export VRM 1.0 with Mochiya lilToon" : "Export GLB with Mochiya lilToon",
                directory,
                _root.name,
                extension);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                EditorUtility.DisplayProgressBar("Mochiya lilToon Export", "Exporting with UniVRM...", 0.5f);
                if (asVrm)
                    MochiyaLilToonExporter.ExportVrm(_root, path, GetSelectedMeta(), _useSparseMorphTargets);
                else
                    MochiyaLilToonExporter.ExportGlb(_root, path, _glbSettings);

                EditorPrefs.SetString(LastDirectoryKey, Path.GetDirectoryName(path) ?? Application.dataPath);
                Debug.Log($"Mochiya lilToon export complete: {path}");
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Mochiya export failed", exception.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private VRM10ObjectMeta GetSelectedMeta()
        {
            var attached = GetAttachedMeta();
            return _useAttachedVrmMeta && attached != null ? attached : _vrmMeta;
        }

        private VRM10ObjectMeta GetAttachedMeta()
        {
            if (_root != null && _root.TryGetComponent<Vrm10Instance>(out var instance) && instance.Vrm != null)
                return instance.Vrm.Meta;
            return null;
        }
    }
}

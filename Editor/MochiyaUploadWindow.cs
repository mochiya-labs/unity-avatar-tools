using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor
{
    /// <summary>Item fields must match mochiya-site. Read AGENTS.md and Documentation~/ITEM_UPLOAD.md before editing.</summary>
    public sealed class MochiyaUploadWindow : EditorWindow
    {
        [SerializeField] private GameObject target;
        [SerializeField] private MochiyaExportProfile profile;
        [SerializeField] private MochiyaItemInput item = new MochiyaItemInput();
        [SerializeField] private string cover = "", tagDraft = "", locale = "en";
        [SerializeField] private List<string> gallery = new List<string>(), additional = new List<string>();
        [SerializeField] private int outputFormat;
        [SerializeField] private double price;
        [SerializeField] private bool details, exportSettings, warnings;
        [SerializeField] private string recoveryId = "", productId = "";
        private string token = "", message = "", temporaryDirectory, requestJson;
        private string origin = MochiyaUploadClient.ProductionOrigin;
        private bool busy, connected, failed, closing;
        private float progress;
        private Vector2 scroll;
        private MochiyaUploadClient client;
        private CancellationTokenSource cancellation;
        private MochiyaUploadContract contract;
        private MochiyaUploadCapabilities capabilities;
        private readonly Dictionary<string, string> labels = new Dictionary<string, string>();
        private readonly Dictionary<string, string> uploadPaths = new Dictionary<string, string>();
        private MochiyaUploadRequest requestData;
        private Texture2D coverPreview;
        private string previewPath;
        private static readonly string[] Locales = { "en", "ja", "zh-CN", "ko" };
        private static readonly string[] LocaleNames = { "English", "日本語", "简体中文", "한국어" };

        [MenuItem("Mochiya/Upload to Mochiya")]
        public static void Open()
        {
            var window = GetWindow<MochiyaUploadWindow>("Upload to Mochiya");
            window.minSize = new Vector2(460, 420); window.Show();
            if (window.target == null) window.target = Selection.activeGameObject;
        }
        private void OnEnable()
        {
            closing = false;
            if (profile == null) profile = MochiyaExportProfile.Default;
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/org.mochiya.avatar-tools/Editor/UploadContract.json");
            if (asset != null) { contract = JsonUtility.FromJson<MochiyaUploadContract>(asset.text); SetLocale(); }
        }
        private void SetLocale()
        {
            labels.Clear();
            var selected = contract?.locales.FirstOrDefault(v => v.name == locale);
            if (selected != null) foreach (var pair in selected.labels) labels[pair.name] = pair.value;
        }
        private string L(string name) => labels.TryGetValue(name, out var text) ? text : name;
        private void OnDisable()
        {
            closing = true;
            cancellation?.Cancel(); client?.Dispose(); connected = false; token = "";
            if (coverPreview != null) DestroyImmediate(coverPreview);
            // Keep the non-secret recovery ID through assembly reload, but never serialize credentials.
            if (!busy) CleanupLocal();
        }
        private void OnGUI()
        {
            if (contract == null) { EditorGUILayout.HelpBox("Upload contract is missing. Reinstall Mochiya Avatar Tools.", MessageType.Error); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(busy))
            {
                int language = EditorGUILayout.Popup(L("language"), Math.Max(0, Array.IndexOf(Locales, locale)), LocaleNames);
                if (Locales[language] != locale) { locale = Locales[language]; SetLocale(); connected = false; }
                // Development origin is opt-in, never received from remote content or persisted with a token.
                var development = Environment.GetEnvironmentVariable("MOCHIYA_UPLOAD_API_URL");
                if (!string.IsNullOrEmpty(development)) { origin = development.TrimEnd('/'); EditorGUILayout.HelpBox(origin, MessageType.Info); }
                token = EditorGUILayout.PasswordField(L("token"), token);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(L(connected ? "disconnect" : "connect")))
                    { if (connected) { client?.Dispose(); client = null; connected = false; token = ""; } else _ = Connect(); }
                    if (GUILayout.Button(L("profile"))) Application.OpenURL(origin + "/profile");
                }
                if (connected) EditorGUILayout.LabelField(L("connected"), capabilities.displayName);
                EditorGUILayout.Space(12);
                EditorGUI.BeginChangeCheck();
                item.title = EditorGUILayout.TextField(L("title") + " *", item.title);
                int category = Math.Max(0, Array.IndexOf(contract.categories, item.category));
                item.category = contract.categories[EditorGUILayout.Popup(L("category") + " *", category, contract.categories.Select(L).ToArray())];
                using (new EditorGUILayout.HorizontalScope())
                {
                    tagDraft = EditorGUILayout.TextField(L("tags"), tagDraft);
                    if (GUILayout.Button(L("addTag"), GUILayout.Width(80)))
                    {
                        var tag = System.Text.RegularExpressions.Regex.Replace(tagDraft.Trim(), @"\s+", " ");
                        if (tag.Length > 0 && tag.Length <= contract.limits.tag && item.tags.Length < contract.limits.tags && !item.tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) { item.tags = item.tags.Concat(new[] { tag }).ToArray(); tagDraft = ""; GUI.changed = true; }
                    }
                }
                for (int i = 0; i < item.tags.Length; i++) using (new EditorGUILayout.HorizontalScope())
                { EditorGUILayout.LabelField(item.tags[i]); if (GUILayout.Button("×", GUILayout.Width(28))) { item.tags = item.tags.Where((_, n) => n != i).ToArray(); GUI.changed = true; break; } }
                item.purchaseMode = EditorGUILayout.Popup(L("availability"), item.purchaseMode == "private" ? 0 : 1, new[] { L("privateItem"), L("sellOnMochiya") }) == 0 ? "private" : "mochiya";
                if (item.purchaseMode == "mochiya")
                {
                    price = EditorGUILayout.DoubleField(L("price") + " (USD)", price);
                    if (capabilities?.pricing != null && price >= 0 && price <= 900000)
                    {
                        var cents = Math.Floor(price * 100 + .5); var fees = capabilities.pricing;
                        var processing = cents == 0 ? 0 : Math.Floor(cents * fees.processingBasisPoints / 10000 + .5) + fees.processingFixedCents;
                        var commission = Math.Floor(cents * fees.commissionBasisPoints / 10000);
                        EditorGUILayout.HelpBox(L("processingFee") + ": " + Money(processing) + "\n" + L("commissionFee") + ": " + Money(commission) + "\n" + L("earnings") + ": " + Money(cents - processing - commission), MessageType.Info);
                    }
                }
                cover = FileField(L("cover") + " *", cover, "png,jpg,jpeg");
                DrawCover();
                EditorGUILayout.Space(8);
                target = (GameObject)EditorGUILayout.ObjectField(L("source") + " *", target, typeof(GameObject), true);
                if (GUILayout.Button(L("useSelection"))) { target = Selection.activeGameObject; GUI.changed = true; }
                if (string.IsNullOrEmpty(item.title) && target != null) item.title = target.name.Substring(0, Math.Min(target.name.Length, contract.limits.title));
                outputFormat = EditorGUILayout.Popup(L("format"), outputFormat, new[] { "VRM", "GLB" });
                if (target != null)
                {
                    var detected = MochiyaAvatarWorkflow.Detect(target);
                    EditorGUILayout.HelpBox(detected.Error ?? detected.Reason, detected.Kind == MochiyaTargetKind.Invalid ? MessageType.Error : MessageType.Info);
                }
                exportSettings = FormFoldout(exportSettings, L("exportSettings"));
                if (exportSettings) MochiyaExportProfileGUI.Draw(ref profile);
                warnings = FormFoldout(warnings, L("warnings"));
                if (warnings && target != null)
                {
                    var report = MochiyaAvatarWorkflow.Validate(target, outputFormat == 0, profile);
                    foreach (var error in report.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
                    foreach (var warning in report.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
                details = FormFoldout(details, L("details"));
                if (details)
                {
                    FileList("gallery", gallery, "png,jpg,jpeg", contract.limits.gallery);
                    FileList("additionalFiles", additional, "", contract.limits.additionalFiles);
                    item.specifications = TextArea("specifications", item.specifications);
                    item.requirements = TextArea("requirements", item.requirements);
                    item.credits = TextArea("credits", item.credits);
                    item.licenseText = TextArea("licenseText", item.licenseText);
                }
                if (EditorGUI.EndChangeCheck() && (requestJson != null || !string.IsNullOrEmpty(recoveryId))) { CleanupLocal(); requestJson = null; requestData = null; recoveryId = ""; }
                EditorGUILayout.HelpBox(L("limits"), MessageType.None);
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, failed ? MessageType.Error : MessageType.Info);
            if (busy) { var rect = EditorGUILayout.GetControlRect(false, 20); EditorGUI.ProgressBar(rect, progress, message); if (GUILayout.Button(L("cancel"))) cancellation?.Cancel(); }
            else using (new EditorGUI.DisabledScope(!connected))
            {
                if (GUILayout.Button(L(requestJson != null || !string.IsNullOrEmpty(recoveryId) ? "retry" : item.purchaseMode == "private" ? "privateUpload" : "publishButton"), GUILayout.Height(36))) _ = Upload();
                if ((requestJson != null || !string.IsNullOrEmpty(recoveryId)) && GUILayout.Button(L("prepareAgain")))
                { CleanupLocal(); requestJson = null; requestData = null; recoveryId = ""; message = ""; }
            }
            if (!string.IsNullOrEmpty(productId) && GUILayout.Button(L("view"))) Application.OpenURL(origin + "/products/" + Uri.EscapeDataString(productId));
            EditorGUILayout.EndScrollView();
        }
        private static bool FormFoldout(bool value, string label)
        {
            var changed = GUI.changed; var result = EditorGUILayout.Foldout(value, label, true);
            GUI.changed = changed; return result;
        }
        private static string Money(double cents) => "$" + (cents / 100).ToString("F2", CultureInfo.InvariantCulture) + " USD";
        private string TextArea(string key, string value) { EditorGUILayout.LabelField(L(key)); return EditorGUILayout.TextArea(value, GUILayout.MinHeight(65)); }
        private string FileField(string label, string value, string extension)
        {
            EditorGUILayout.LabelField(label);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(value) ? "—" : Path.GetFileName(value), GUILayout.Height(18));
                if (GUILayout.Button(L("choose"), GUILayout.Width(120))) { var path = EditorUtility.OpenFilePanel(label, "", extension); if (!string.IsNullOrEmpty(path)) { value = path; GUI.changed = true; } }
            }
            return value;
        }
        private void FileList(string label, List<string> files, string extension, int max)
        {
            EditorGUILayout.LabelField(L(label), EditorStyles.boldLabel);
            for (int i = 0; i < files.Count; i++) using (new EditorGUILayout.HorizontalScope())
            { EditorGUILayout.LabelField(Path.GetFileName(files[i])); if (GUILayout.Button(L("remove"), GUILayout.Width(80))) { files.RemoveAt(i); GUI.changed = true; break; } }
            using (new EditorGUI.DisabledScope(files.Count >= max)) if (GUILayout.Button(L("add")))
            { var path = EditorUtility.OpenFilePanel(L(label), "", extension); if (!string.IsNullOrEmpty(path)) { files.Add(path); GUI.changed = true; } }
        }
        private void DrawCover()
        {
            if (previewPath != cover)
            {
                if (coverPreview != null) DestroyImmediate(coverPreview);
                previewPath = cover;
                if (File.Exists(cover) && new FileInfo(cover).Length <= contract.limits.imageBytes)
                { coverPreview = new Texture2D(2, 2); if (!coverPreview.LoadImage(File.ReadAllBytes(cover))) { DestroyImmediate(coverPreview); coverPreview = null; } }
            }
            if (coverPreview != null) GUI.DrawTexture(EditorGUILayout.GetControlRect(false, 100), coverPreview, ScaleMode.ScaleToFit);
        }
        private async Task Connect()
        {
            busy = true; failed = false; cancellation = new CancellationTokenSource();
            try
            {
                client?.Dispose(); client = new MochiyaUploadClient(token.Trim(), origin);
                capabilities = await client.Api<MochiyaUploadCapabilities>("/api/v1/creator/upload-capabilities?locale=" + locale, "GET", null, cancellation.Token);
                if (capabilities.limits.version != contract.limits.version) throw new InvalidOperationException("Update Mochiya Avatar Tools to use the current upload form.");
                contract.limits = capabilities.limits;
                foreach (var pair in capabilities.labels) labels[pair.name] = pair.value;
                connected = true; message = L("connected") + " " + capabilities.displayName;
            }
            catch (Exception error) { failed = true; connected = false; message = error.Message; }
            finally { busy = false; if (closing) CleanupLocal(); if (this != null) Repaint(); }
        }
        private void Prepare()
        {
            if (target == null) throw new InvalidOperationException(L("noSource"));
            if (string.IsNullOrWhiteSpace(item.title) || item.title.Length > contract.limits.title) throw new InvalidOperationException(L("title") + ": 1–" + contract.limits.title);
            if (item.specifications.Length > contract.limits.specifications || item.requirements.Length > contract.limits.requirements || item.credits.Length > contract.limits.credits || item.licenseText.Length > contract.limits.licenseText) throw new InvalidOperationException("An item detail exceeds the form's text limit.");
            if (double.IsNaN(price) || double.IsInfinity(price) || price < 0 || price > 900000 || (item.purchaseMode == "mochiya" && price > 0 && price < .5)) throw new InvalidOperationException(L("priceInvalid"));
            item.priceCents = item.purchaseMode == "private" ? 0 : (int)Math.Floor(price * 100 + .5);
            message = L("prepare"); Repaint();
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "MochiyaUpload-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporaryDirectory);
            var modelPath = Path.Combine(temporaryDirectory, "model" + (outputFormat == 0 ? ".vrm" : ".glb"));
            MochiyaAvatarWorkflow.Export(target, modelPath, profile);
            uploadPaths.Clear(); var files = new List<MochiyaUploadFile>();
            Action<string, string> add = (role, path) =>
            {
                var info = new FileInfo(path); var image = role == "cover" || role == "gallery";
                if (!info.Exists || info.Length <= 0 || info.Length > (image ? contract.limits.imageBytes : contract.limits.fileBytes)) throw new InvalidOperationException(L("limits"));
                var ext = info.Extension.ToLowerInvariant(); var type = ext == ".png" ? "image/png" : ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : "application/octet-stream";
                if (image && type == "application/octet-stream") throw new InvalidOperationException(L("coverInvalid"));
                var id = role + "-" + files.Count;
                files.Add(new MochiyaUploadFile { id = id, role = role, name = info.Name, size = info.Length, contentType = type }); uploadPaths[id] = path;
            };
            add("cover", cover); add("model", modelPath);
            foreach (var path in gallery) add("gallery", path);
            foreach (var path in additional) add("additional", path);
            if (files.Sum(f => f.size) > contract.limits.totalBytes) throw new InvalidOperationException(L("limits"));
            requestData = new MochiyaUploadRequest { idempotencyKey = Guid.NewGuid().ToString(), item = JsonUtility.FromJson<MochiyaItemInput>(JsonUtility.ToJson(item)), files = files.ToArray() };
            requestJson = requestData.ToJson();
        }
        private async Task Upload()
        {
            busy = true; failed = false; progress = 0; cancellation = new CancellationTokenSource();
            try
            {
                MochiyaUploadSession session;
                if (!string.IsNullOrEmpty(recoveryId)) session = await client.Api<MochiyaUploadSession>("/api/v1/creator/uploads/" + recoveryId, "GET", null, cancellation.Token);
                else
                {
                    if (requestJson == null) { await Task.Yield(); Prepare(); }
                    cancellation.Token.ThrowIfCancellationRequested();
                    session = await client.Api<MochiyaUploadSession>("/api/v1/creator/uploads", "POST", requestJson, cancellation.Token); recoveryId = session.id;
                }
                productId = session.productId;
                if (!session.complete)
                {
                    if (session.targets.Length > 0 && requestData == null) { recoveryId = ""; throw new InvalidOperationException("Reconnect succeeded. Some files were not uploaded before the editor restarted. Upload again to prepare a new copy."); }
                    for (int i = 0; i < session.targets.Length; i++)
                    {
                        var transfer = session.targets[i]; var file = requestData.files.First(f => f.id == transfer.id);
                        message = L("uploading") + " " + file.name; int index = i;
                        await client.Upload(transfer, uploadPaths[transfer.id], file.size, p => { progress = (index + Math.Max(0, p)) / session.targets.Length; Repaint(); }, cancellation.Token);
                    }
                    message = L("finalizing"); Repaint();
                    await client.Api<MochiyaUploadSession>("/api/v1/creator/uploads/" + session.id + "/finalize", "POST", "{}", cancellation.Token);
                }
                message = L("done"); progress = 1; requestJson = null; requestData = null; recoveryId = ""; CleanupLocal();
            }
            catch (OperationCanceledException) { message = L("retry"); }
            catch (Exception error) { failed = true; message = L("failed") + "\n" + error.Message; }
            finally { busy = false; if (closing) CleanupLocal(); if (this != null) Repaint(); }
        }
        private void CleanupLocal()
        {
            if (string.IsNullOrEmpty(temporaryDirectory)) return;
            var full = Path.GetFullPath(temporaryDirectory); var parent = Path.GetFullPath(Path.GetTempPath());
            if (full.StartsWith(parent, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("MochiyaUpload-", StringComparison.Ordinal) && Directory.Exists(full)) Directory.Delete(full, true);
            temporaryDirectory = null;
        }
    }
}

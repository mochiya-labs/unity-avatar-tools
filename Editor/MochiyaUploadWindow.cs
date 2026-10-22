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
    /// <summary>Uploads use the shared Mochiya item contract.</summary>
    public sealed class MochiyaUploadWindow : EditorWindow
    {
        [SerializeField] private GameObject target;
        [SerializeField] private MochiyaExportProfile profile;
        [SerializeField] private MochiyaItemInput item = new MochiyaItemInput();
        [SerializeField] private string cover = "", tagDraft = "", locale = "en";
        [SerializeField] private List<string> gallery = new List<string>(), additional = new List<string>();
        [SerializeField] private int outputFormat;
        [SerializeField] private double price;
        [SerializeField] private bool details;
        [SerializeField] private bool exportSettings, warnings;
        [SerializeField] private string recoveryId = "", productId = "";
        [NonSerialized] private string token = "", message = "", temporaryDirectory, requestJson;
        private string origin => MochiyaUploadClient.ConfiguredOrigin;
        private bool busy, connected, failed, closing;
        private bool refreshing, refreshFailed, incompatibleContract;
        private double lastRefreshAttempt = double.NegativeInfinity;
        private CancellationTokenSource refreshCancellation;
        private float progress;
        private Vector2 scroll;
        private MochiyaUploadClient client;
        private MochiyaUploadConnection connection;
        private string activeOrigin;
        private bool cleanupAfterOperation;
        private CancellationTokenSource cancellation;
        private MochiyaUploadContract contract;
        private MochiyaUploadCapabilities capabilities;
        private readonly Dictionary<string, string> labels = new Dictionary<string, string>();
        private readonly Dictionary<string, string> uploadPaths = new Dictionary<string, string>();
        private MochiyaUploadRequest requestData;
        private Texture2D coverPreview;
        private string previewPath;
        private GUIStyle wrappedTextArea;
        private GUIStyle sectionTitle, sectionFoldout, sectionDescription;
        private bool sectionStylesDark;
        private float formHeight;
        private float FormWidth => Mathf.Max(1, position.width - (formHeight > position.height ? GUI.skin.verticalScrollbar.fixedWidth : 0));
        private MochiyaUploadAnalysis analysis;

        [MenuItem("Mochiya/Upload to Mochiya")]
        public static void Open()
        {
            var window = GetWindow<MochiyaUploadWindow>("Upload to Mochiya");
            window.minSize = new Vector2(460, 420); window.Show();
            if (window.target == null) window.target = Selection.activeGameObject;
        }
        private void OnEnable()
        {
            analysis?.Dispose();
            analysis = new MochiyaUploadAnalysis();
            EditorApplication.update -= UpdateAnalysis;
            EditorApplication.update += UpdateAnalysis;
            closing = false;
            connected = false; busy = false; activeOrigin = null;
            connection = new MochiyaUploadConnection(new MochiyaCredentialStore(Path.GetDirectoryName(Application.dataPath)));
            if (profile == null) profile = MochiyaExportProfile.Default;
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/org.mochiya.avatar-tools/Editor/UploadContract.json");
            if (asset != null) { contract = JsonUtility.FromJson<MochiyaUploadContract>(asset.text); SetLocale(); }
            EditorApplication.delayCall += RestoreConnection;
        }
        private void UpdateAnalysis()
        {
            if (!closing && !busy && analysis != null &&
                analysis.Refresh(target, profile, outputFormat == 0, warnings, EditorApplication.timeSinceStartup)) Repaint();
        }
        private void RestoreConnection()
        {
            if (closing || this == null || contract == null || busy) return;
            try
            {
                if (connection.Restore(origin)) { token = connection.Token; _ = Connect(); }
                else { recoveryId = ""; productId = ""; }
            }
            catch (Exception error) { failed = true; message = error.Message; }
        }
        private bool SignOut()
        {
            refreshCancellation?.Cancel(); refreshFailed = false; incompatibleContract = false;
            cancellation?.Cancel(); client?.Dispose(); client = null;
            connected = false; token = ""; capabilities = null; activeOrigin = null;
            recoveryId = ""; productId = ""; requestJson = null; requestData = null;
            cleanupAfterOperation = busy;
            if (!busy) CleanupLocal();
            try { connection?.SignOut(); return true; }
            catch (Exception error) { failed = true; message = error.Message; return false; }
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
            refreshCancellation?.Cancel();
            EditorApplication.update -= UpdateAnalysis;
            analysis?.Dispose(); analysis = null;
            EditorApplication.delayCall -= RestoreConnection;
            cancellation?.Cancel(); client?.Dispose(); connected = false; token = "";
            if (coverPreview != null) DestroyImmediate(coverPreview);
            // Keep the non-secret recovery ID through assembly reload, but never serialize credentials.
            if (!busy) CleanupLocal();
        }
        private void OnGUI()
        {
            if (contract == null) { EditorGUILayout.HelpBox("Upload contract is missing. Reinstall Mochiya Avatar Tools.", MessageType.Error); return; }
            if (activeOrigin != null && activeOrigin != origin) SignOut();
            scroll.x = 0;
            scroll = EditorGUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            scroll.x = 0;
            EditorGUILayout.BeginVertical(GUILayout.Width(FormWidth));
            using (new EditorGUI.DisabledScope(busy))
            {
                using (new EditorGUI.DisabledScope(refreshing))
                {
                    var selectedLocale = MochiyaPanelText.LanguageField(locale);
                    if (selectedLocale != locale) { locale = selectedLocale; SetLocale(); if (connected) _ = RefreshConnection(true); }
                    EditorGUILayout.LabelField(MochiyaPanelText.Get(locale, MochiyaPanelText.UploadIntro), EditorStyles.wordWrappedLabel);
                    EditorGUILayout.Space(10);
                    if (origin != MochiyaUploadClient.ProductionOrigin) EditorGUILayout.HelpBox(origin, MessageType.Info);
                    if (!connected) token = EditorGUILayout.PasswordField(L("token"), token);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(L(refreshing ? "checkingConnection" : connected ? "disconnect" : "connect")))
                        { if (connected) { if (SignOut()) message = ""; } else _ = Connect(); }
                        if (GUILayout.Button(L("profile"))) Application.OpenURL(origin + "/profile");
                    }
                }
                if (connected) EditorGUILayout.LabelField(L("connected"), capabilities.displayName);
                if (!refreshing && refreshFailed)
                {
                    EditorGUILayout.HelpBox(L("connectionRefreshFailed"), MessageType.Warning);
                    if (GUILayout.Button(L("retry"))) _ = RefreshConnection(true);
                }
                EditorGUILayout.Space(12);
                EditorGUI.BeginChangeCheck();
                SectionHeading("listingBasics", "listingBasicsDescription");
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
                item.status = EditorGUILayout.Popup(L("availability"), item.status == "private" ? 0 : 1, new[] { L("privateItem"), L("sellOnMochiya") }) == 0 ? "private" : "public";
                EditorGUILayout.HelpBox(L(item.status == "private" ? "personalUseHint" : "marketplaceListingHint") + " " + L("availabilityHint"), MessageType.None);
                if (item.status == "public")
                {
                    price = EditorGUILayout.DoubleField(L("price") + " (USD)", price);
                    if (capabilities?.pricing != null && price >= 0 && price <= 900000)
                    {
                        var cents = Math.Floor(price * 100 + .5); var fees = capabilities.pricing;
                        var processing = cents == 0 ? 0 : Math.Floor(cents * fees.processingBasisPoints / 10000 + .5) + fees.processingFixedCents;
                        var commission = Math.Floor(cents * fees.commissionBasisPoints / 10000);
                        EditorGUILayout.HelpBox(L("processingFee") + " (" + (fees.processingBasisPoints / 100.0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "% + " + Money(fees.processingFixedCents) + "): " + Money(processing) + "\n" + L("commissionFee") + " (" + (fees.commissionBasisPoints / 100.0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%): " + Money(commission) + "\n" + L("earnings") + ": " + Money(cents - processing - commission), MessageType.Info);
                    }
                }
                EditorGUILayout.Space(16);
                SectionHeading("modelAndCover", "modelAndCoverDescription");
                cover = FileField(L("cover") + " *", cover, "png,jpg,jpeg");
                DrawCover();
                EditorGUI.BeginChangeCheck();
                var sourceLabel = new GUIContent(L("source") + " *");
                var sourceLabelWidth = EditorStyles.label.CalcSize(sourceLabel).x + 8;
                if (sourceLabelWidth + 180 > FormWidth)
                {
                    EditorGUILayout.LabelField(sourceLabel, EditorStyles.wordWrappedLabel);
                    target = (GameObject)EditorGUILayout.ObjectField(GUIContent.none, target, typeof(GameObject), true);
                }
                else
                {
                    var previousLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = Mathf.Max(previousLabelWidth, sourceLabelWidth);
                    target = (GameObject)EditorGUILayout.ObjectField(sourceLabel, target, typeof(GameObject), true);
                    EditorGUIUtility.labelWidth = previousLabelWidth;
                }
                if (EditorGUI.EndChangeCheck() && target != null) item.title = target.name.Substring(0, Math.Min(target.name.Length, contract.limits.title));
                exportSettings = FormFoldout(exportSettings, L("exportSettings"));
                if (exportSettings)
                {
                    outputFormat = EditorGUILayout.Popup(L("format"), outputFormat, new[] { "VRM", "GLB" });
                    if (target != null && analysis?.Target != null && analysis.Target.Root == target)
                    {
                        var detected = analysis.Target;
                        EditorGUILayout.HelpBox(detected.Error ?? detected.Reason, detected.Kind == MochiyaTargetKind.Invalid ? MessageType.Error : MessageType.Info);
                    }
                    MochiyaExportProfileGUI.Draw(ref profile, locale);
                }
                warnings = FormFoldout(warnings, L("warnings"));
                if (warnings && target != null && analysis?.Report != null && analysis.Target?.Root == target)
                {
                    var report = analysis.Report;
                    foreach (var error in report.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
                    foreach (var warning in report.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
                EditorGUILayout.Space(16);
                var changedBeforeDetails = GUI.changed;
                EnsureSectionStyles();
                var detailsRect = EditorGUILayout.GetControlRect(false, 26);
                details = EditorGUI.Foldout(detailsRect, details, L("additionalItemDetails"), true, sectionFoldout);
                GUI.changed = changedBeforeDetails;
                SectionDescription("additionalItemDetailsDescription");
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
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, failed ? MessageType.Error : MessageType.Info);
            if (connected && item.status == "public" && price > 0 && capabilities.sellerState != "approved")
            {
                var restricted = capabilities.sellerState == "restricted";
                EditorGUILayout.HelpBox(L(restricted ? "seller_not_ready" : "payoutVerificationNotice"), restricted ? MessageType.Error : MessageType.Warning);
                if (!restricted && GUILayout.Button(L("completeSellerVerification"))) Application.OpenURL(origin + "/profile?tab=creator");
            }
            if (busy) { var rect = EditorGUILayout.GetControlRect(false, 20); EditorGUI.ProgressBar(rect, progress, message); if (GUILayout.Button(L("cancel"))) cancellation?.Cancel(); }
            else using (new EditorGUI.DisabledScope(!connected || refreshing || incompatibleContract))
            {
                if (GUILayout.Button(L(requestJson != null || !string.IsNullOrEmpty(recoveryId) ? "retry" : item.status == "private" ? "privateUpload" : "publishButton"), GUILayout.Height(36))) _ = Upload();
                if ((requestJson != null || !string.IsNullOrEmpty(recoveryId)) && GUILayout.Button(L("prepareAgain")))
                { CleanupLocal(); requestJson = null; requestData = null; recoveryId = ""; message = ""; }
            }
            if (!string.IsNullOrEmpty(productId) && GUILayout.Button(L("view"))) Application.OpenURL(origin + "/products/" + Uri.EscapeDataString(productId));
            EditorGUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                var height = GUILayoutUtility.GetLastRect().height;
                if (!Mathf.Approximately(formHeight, height)) { formHeight = height; Repaint(); }
            }
            EditorGUILayout.EndScrollView();
        }
        private void SectionHeading(string title, string description)
        {
            EnsureSectionStyles();
            EditorGUILayout.LabelField(L(title), sectionTitle, GUILayout.Height(26));
            SectionDescription(description);
        }
        private void EnsureSectionStyles()
        {
            var dark = EditorGUIUtility.isProSkin;
            if (sectionTitle != null && sectionStylesDark == dark) return;
            sectionStylesDark = dark;
            var accent = dark ? new Color(0.72f, 0.9f, 0.35f) : new Color(0.30f, 0.43f, 0.10f);
            sectionTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            sectionFoldout = new GUIStyle(EditorStyles.foldoutHeader) { fontSize = 14, fontStyle = FontStyle.Bold, fixedHeight = 0 };
            foreach (var style in new[] { sectionTitle, sectionFoldout })
                foreach (var state in new[] { style.normal, style.hover, style.active, style.focused, style.onNormal, style.onHover, style.onActive, style.onFocused })
                    state.textColor = accent;
            sectionDescription = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 12, wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8)
            };
            sectionDescription.normal.textColor = dark ? new Color(0.8f, 0.82f, 0.77f) : new Color(0.28f, 0.31f, 0.25f);
        }
        private void SectionDescription(string description)
        {
            var content = new GUIContent(L(description));
            var width = Mathf.Max(1, FormWidth - 4);
            var rect = GUILayoutUtility.GetRect(width, sectionDescription.CalcHeight(content, width), GUILayout.ExpandWidth(false));
            rect.x += 2;
            GUI.Label(rect, content, sectionDescription);
            EditorGUILayout.Space(6);
        }
        private static bool FormFoldout(bool value, string label)
        {
            var changed = GUI.changed; var result = EditorGUILayout.Foldout(value, label, true);
            GUI.changed = changed; return result;
        }
        private static string Money(double cents) => "$" + (cents / 100).ToString("F2", CultureInfo.InvariantCulture) + " USD";
        private string TextArea(string key, string value)
        {
            EditorGUILayout.LabelField(L(key));
            if (wrappedTextArea == null) wrappedTextArea = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            var width = Mathf.Max(1, FormWidth - 4);
            var height = Mathf.Max(65, wrappedTextArea.CalcHeight(new GUIContent(value ?? ""), width));
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
            rect.x += 2;
            var controlName = "MochiyaUpload-" + key;
            GUI.SetNextControlName(controlName);
            var result = EditorGUI.TextArea(rect, value, wrappedTextArea);
            if (string.IsNullOrEmpty(result) && GUI.GetNameOfFocusedControl() != controlName && Event.current.type == EventType.Repaint)
            {
                var placeholder = new GUIStyle(wrappedTextArea);
                placeholder.normal.background = null;
                placeholder.normal.textColor = EditorGUIUtility.isProSkin
                    ? new Color(0.62f, 0.62f, 0.62f) : new Color(0.42f, 0.42f, 0.42f);
                GUI.Label(rect, L(key == "licenseText" ? "licenseDefault" : key + "Placeholder"), placeholder);
            }
            return result;
        }
        private string FileField(string label, string value, string extension)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label);
                if (GUILayout.Button(L("choose"), GUILayout.Width(120))) { var path = EditorUtility.OpenFilePanel(label, "", extension); if (!string.IsNullOrEmpty(path)) { value = path; GUI.changed = true; } }
            }
            if (!string.IsNullOrEmpty(value))
                EditorGUILayout.SelectableLabel(value, EditorStyles.wordWrappedLabel, GUILayout.Height(Mathf.Max(18, EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(value), Mathf.Max(1, FormWidth - 4)))));
            return value;
        }
        private void FileList(string label, List<string> files, string extension, int max)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(L(label), EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(files.Count >= max)) if (GUILayout.Button(L("add"), GUILayout.Width(120)))
                { var path = EditorUtility.OpenFilePanel(L(label), "", extension); if (!string.IsNullOrEmpty(path)) { files.Add(path); GUI.changed = true; } }
            }
            for (int i = 0; i < files.Count; i++) using (new EditorGUILayout.HorizontalScope())
            { EditorGUILayout.LabelField(Path.GetFileName(files[i])); if (GUILayout.Button(L("remove"), GUILayout.Width(80))) { files.RemoveAt(i); GUI.changed = true; break; } }
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
        private void OnFocus()
        {
            if (connected && !busy) _ = RefreshConnection();
        }
        private async Task RefreshConnection(bool force = false)
        {
            if (!connected || busy || refreshing || closing || client == null) return;
            if (!force && EditorApplication.timeSinceStartup - lastRefreshAttempt < 60) return;
            lastRefreshAttempt = EditorApplication.timeSinceStartup;
            refreshing = true; refreshFailed = false;
            var refreshOrigin = activeOrigin;
            var refreshToken = token;
            var refreshLocale = locale;
            var refreshClient = client;
            var source = refreshCancellation = new CancellationTokenSource();
            bool Current() => !closing && !source.IsCancellationRequested && connected
                && client == refreshClient && token == refreshToken && origin == refreshOrigin && locale == refreshLocale;
            try
            {
                var result = await refreshClient.Api<MochiyaUploadCapabilities>("/api/v1/creator/upload-capabilities?locale=" + refreshLocale, "GET", null, source.Token);
                if (!Current()) return;
                if (result?.limits == null || result.labels == null) throw new InvalidOperationException("Missing upload capabilities.");
                incompatibleContract = result.limits.version != contract.limits.version;
                if (incompatibleContract) { failed = true; message = L("updateUploadTool"); return; }
                capabilities = result;
                contract.limits = result.limits;
                foreach (var pair in result.labels) labels[pair.name] = pair.value;
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (!Current()) return;
                if (error is MochiyaUploadApiException apiError && apiError.AuthenticationRejected)
                { SignOut(); failed = true; message = error.Message; }
                else refreshFailed = true;
            }
            finally
            {
                if (refreshCancellation == source) { refreshCancellation = null; refreshing = false; }
                source.Dispose();
                if (!closing && this != null) Repaint();
            }
        }
        private async Task Connect()
        {
            if (busy || refreshing) return;
            busy = true; connected = false; failed = false; cancellation = new CancellationTokenSource();
            var cancel = cancellation.Token;
            try
            {
                activeOrigin = origin;
                token = token.Trim();
                if (connection.Token != null && connection.Token != token)
                {
                    recoveryId = ""; productId = ""; requestJson = null; requestData = null; CleanupLocal();
                }
                client?.Dispose(); client = new MochiyaUploadClient(token, activeOrigin);
                capabilities = await connection.Authenticate(token, activeOrigin, async () =>
                {
                    var result = await client.Api<MochiyaUploadCapabilities>("/api/v1/creator/upload-capabilities?locale=" + locale, "GET", null, cancel);
                    if (activeOrigin != origin) throw new InvalidOperationException("The Mochiya server changed. Connect again.");
                    if (result?.limits == null || result.labels == null || result.limits.version != contract.limits.version)
                        throw new InvalidOperationException("Update Mochiya Avatar Tools to use the current upload form.");
                    return result;
                }, cancel);
                contract.limits = capabilities.limits;
                foreach (var pair in capabilities.labels) labels[pair.name] = pair.value;
                connected = true; incompatibleContract = false; refreshFailed = false; lastRefreshAttempt = EditorApplication.timeSinceStartup; message = "";
            }
            catch (OperationCanceledException) { connected = false; client?.Dispose(); }
            catch (Exception error) { SignOut(); failed = true; message = error.Message; }
            finally { busy = false; if (closing || cleanupAfterOperation) CleanupLocal(); cleanupAfterOperation = false; if (this != null) Repaint(); }
        }
        private void CheckText(string field, string value, int limit)
        {
            if ((value ?? "").Length > limit)
                throw new InvalidOperationException(L("textTooLong").Replace("{field}", L(field)).Replace("{limit}", limit.ToString()).Replace("{count}", value.Length.ToString()));
        }
        private void Prepare()
        {
            if (target == null) throw new InvalidOperationException(L("noSource"));
            if (string.IsNullOrEmpty(cover)) throw new InvalidOperationException(L("coverRequired"));
            if (string.IsNullOrWhiteSpace(item.title)) throw new InvalidOperationException(L("title") + ": " + L("required"));
            CheckText("title", item.title, contract.limits.title);
            CheckText("specifications", item.specifications, contract.limits.specifications);
            CheckText("requirements", item.requirements, contract.limits.requirements);
            CheckText("credits", item.credits, contract.limits.credits);
            CheckText("licenseText", item.licenseText, contract.limits.licenseText);
            if (double.IsNaN(price) || double.IsInfinity(price) || price < 0 || price > 900000 || (item.status == "public" && price > 0 && price < .5)) throw new InvalidOperationException(L("priceInvalid"));
            item.priceCents = item.status == "private" ? 0 : (int)Math.Floor(price * 100 + .5);
            message = L("prepare"); Repaint();
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "MochiyaUpload-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporaryDirectory);
            var modelPath = Path.Combine(temporaryDirectory, "model" + (outputFormat == 0 ? ".vrm" : ".glb"));
            MochiyaAvatarWorkflow.Export(target, modelPath, profile);
            uploadPaths.Clear(); var files = new List<MochiyaUploadFile>();
            Action<string, string> add = (role, path) =>
            {
                var info = new FileInfo(path); var image = role == "cover" || role == "gallery";
                if (!info.Exists || info.Length <= 0) throw new InvalidOperationException(L("fileMissingOrEmpty").Replace("{name}", info.Name));
                var limit = image ? contract.limits.imageBytes : contract.limits.fileBytes;
                if (info.Length > limit) throw new InvalidOperationException(L("fileTooLarge").Replace("{name}", info.Name).Replace("{limit}", (limit / (1024 * 1024)).ToString()));
                var ext = info.Extension.ToLowerInvariant(); var type = ext == ".png" ? "image/png" : ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : "application/octet-stream";
                if (image && type == "application/octet-stream") throw new InvalidOperationException(info.Name + ": " + L(image && role == "gallery" ? "galleryInvalid" : "coverInvalid"));
                var id = role + "-" + files.Count;
                files.Add(new MochiyaUploadFile { id = id, role = role, name = info.Name, size = info.Length, contentType = type }); uploadPaths[id] = path;
            };
            add("cover", cover); add("model", modelPath);
            foreach (var path in gallery) add("gallery", path);
            foreach (var path in additional) add("additional", path);
            if (files.Sum(f => f.size) > contract.limits.totalBytes) throw new InvalidOperationException(L("totalFilesTooLarge").Replace("{limit}", (contract.limits.totalBytes / (1024 * 1024 * 1024)).ToString()));
            requestData = new MochiyaUploadRequest { idempotencyKey = Guid.NewGuid().ToString(), item = JsonUtility.FromJson<MochiyaItemInput>(JsonUtility.ToJson(item)), files = files.ToArray() };
            requestJson = requestData.ToJson();
        }
        private async Task Upload()
        {
            if (busy || refreshing || incompatibleContract || !connected) return;
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
            catch (Exception error)
            {
                if (error is MochiyaUploadApiException apiError && apiError.InvalidatesConnection) SignOut();
                failed = true; message = L("failed") + "\n" + error.Message;
            }
            finally { busy = false; if (closing || cleanupAfterOperation) CleanupLocal(); cleanupAfterOperation = false; if (this != null) Repaint(); }
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

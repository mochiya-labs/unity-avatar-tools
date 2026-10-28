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
        [Serializable] private sealed class ModelSource
        {
            public string id = Guid.NewGuid().ToString("N");
            public GameObject source;
            public int format;
            public MochiyaExportProfile profile;
            public bool exportSettings, warnings;
            public string[] bodies = Array.Empty<string>();
        }
        [SerializeField] private List<ModelSource> modelSources = new List<ModelSource>();
        private bool bodySelectionChanged;
        private string[] bodyCatalog = Array.Empty<string>();
        private CancellationTokenSource catalogCancellation;
        [SerializeField] private GameObject target;
        [SerializeField] private MochiyaExportProfile profile;
        [SerializeField] private MochiyaItemInput item = new MochiyaItemInput();
        [SerializeField] private string cover = "", tagDraft = "", locale = "en";
        [SerializeField] private List<string> gallery = new List<string>(), additional = new List<string>();
        [SerializeField] private int outputFormat;
        [SerializeField] private double price;
        [SerializeField] private bool details;
        [SerializeField] private bool exportSettings, warnings;
        private bool showModelHelp;
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
        private readonly Dictionary<string, Texture2D> galleryPreviews = new Dictionary<string, Texture2D>();
        private string previewPath;
        private GUIStyle wrappedTextArea;
        private GUIStyle sectionTitle, sectionFoldout, sectionDescription;
        private bool sectionStylesDark;
        private bool dismissCompletion;
        private float formHeight;
        private float FormWidth => Mathf.Max(1, position.width - (formHeight > position.height ? GUI.skin.verticalScrollbar.fixedWidth : 0));
        private readonly Dictionary<string, MochiyaUploadAnalysis> analyses = new Dictionary<string, MochiyaUploadAnalysis>();

        [MenuItem("Mochiya/Upload to Mochiya")]
        public static void Open()
        {
            var window = GetWindow<MochiyaUploadWindow>("Upload to Mochiya");
            window.minSize = new Vector2(460, 420); window.Show();
            if (window.target == null) window.target = Selection.activeGameObject;
        }
        private void OnEnable()
        {
            DisposeAnalyses();
            EditorApplication.update -= UpdateAnalysis;
            EditorApplication.update += UpdateAnalysis;
            closing = false;
            connected = false; busy = false; activeOrigin = null;
            connection = new MochiyaUploadConnection(new MochiyaCredentialStore(Path.GetDirectoryName(Application.dataPath)));
            if (profile == null) profile = MochiyaExportProfile.Default;
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/org.mochiya.avatar-tools/Editor/UploadContract.json");
            if (asset != null) { contract = JsonUtility.FromJson<MochiyaUploadContract>(asset.text); SetLocale(); }
            if (modelSources.Count == 0) modelSources.Add(new ModelSource { source = target, format = outputFormat, bodies = new[] { "any" } });
            foreach (var row in modelSources)
            {
                // Preserve the previous shared profile for existing serialized window drafts.
                if (row.profile == null) { row.profile = profile; row.exportSettings = exportSettings; row.warnings = warnings; }
            }
            bodyCatalog = contract?.bodyCatalog?.tags ?? Array.Empty<string>();
            _ = LoadBodyCatalog();
            EditorApplication.delayCall += RestoreConnection;
        }
        private void UpdateAnalysis()
        {
            if (closing || busy) return;
            bool changed = false;
            foreach (var row in modelSources)
            {
                if (!row.exportSettings && !row.warnings) continue;
                if (!analyses.TryGetValue(row.id, out var analysis))
                    analyses[row.id] = analysis = new MochiyaUploadAnalysis();
                changed |= analysis.Refresh(row.source, row.profile, row.format == 0, row.warnings, EditorApplication.timeSinceStartup);
            }
            if (changed) Repaint();
        }
        private void DisposeAnalyses()
        {
            foreach (var analysis in analyses.Values) analysis.Dispose();
            analyses.Clear();
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
            var showCompletion = labels.TryGetValue("done", out var previousCompletion) && message == previousCompletion;
            labels.Clear();
            var selected = contract?.locales.FirstOrDefault(v => v.name == locale);
            if (selected != null) foreach (var pair in selected.labels) labels[pair.name] = pair.value;
            if (showCompletion) message = L("done");
        }
        private string L(string name) => labels.TryGetValue(name, out var text) ? text : name;
        private void OnDisable()
        {
            closing = true;
            catalogCancellation?.Cancel();
            refreshCancellation?.Cancel();
            EditorApplication.update -= UpdateAnalysis;
            DisposeAnalyses();
            EditorApplication.delayCall -= RestoreConnection;
            cancellation?.Cancel(); client?.Dispose(); connected = false; token = "";
            if (coverPreview != null) DestroyImmediate(coverPreview);
            foreach (var preview in galleryPreviews.Values) if (preview != null) DestroyImmediate(preview);
            galleryPreviews.Clear();
            // Keep the non-secret recovery ID through assembly reload, but never serialize credentials.
            if (!busy) CleanupLocal();
        }
        private void OnGUI()
        {
            if (contract == null) { EditorGUILayout.HelpBox("Upload contract is missing. Reinstall Mochiya Avatar Tools.", MessageType.Error); return; }
            if (dismissCompletion && Event.current.type == EventType.Layout)
            {
                if (!busy && !failed && message == L("done")) message = "";
                dismissCompletion = false;
            }
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
                var focusedBeforeForm = GUIUtility.keyboardControl;
                EditorGUI.BeginChangeCheck();
                SectionHeading("listingBasics", "listingBasicsDescription");
                item.title = EditorGUI.TextField(RequiredFieldRect(L("title")), item.title);
                int category = Math.Max(0, Array.IndexOf(contract.categories, item.category));
                var oldCategory = item.category;
                item.category = contract.categories[EditorGUI.Popup(RequiredFieldRect(L("category")), category, contract.categories.Select(L).ToArray())];
                if (oldCategory != item.category)
                {
                    if (oldCategory == "base_avatar" && modelSources.Count == 1)
                        modelSources[0].bodies = new[] { string.IsNullOrEmpty(item.baseBodyType) ? "any" : item.baseBodyType };
                    item.baseBodyType = "";
                }
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
                EditorGUILayout.HelpBox(L("tagsHint").Replace("{count}", item.tags.Length.ToString()), MessageType.None);
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
                cover = FileField(L("cover"), cover, "png,jpg,jpeg");
                EditorGUILayout.LabelField(L("coverHint"), EditorStyles.wordWrappedLabel);
                DrawCover();
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(RequiredLabel(L("modelAsset")), RequiredLabelStyle(), GUILayout.ExpandWidth(false));
                    var help = new GUIContent(EditorGUIUtility.IconContent("_Help")) { tooltip = L("modelSupportHint") };
                    var changedBeforeHelp = GUI.changed;
                    if (GUILayout.Button(help, EditorStyles.iconButton, GUILayout.Width(22))) showModelHelp = !showModelHelp;
                    GUI.changed = changedBeforeHelp;
                    GUILayout.FlexibleSpace();
                    if (item.category != "base_avatar") using (new EditorGUI.DisabledScope(modelSources.Count >= contract.limits.modelVariants))
                        if (GUILayout.Button(L("addVariant"), GUILayout.ExpandWidth(false))) { modelSources.Add(new ModelSource { profile = MochiyaExportProfile.Default }); GUI.changed = true; }
                }
                if (showModelHelp) EditorGUILayout.HelpBox(L("modelSupportHint"), MessageType.Info);
                DrawModelVariants();
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
                var formChanged = EditorGUI.EndChangeCheck() || bodySelectionChanged;
                bodySelectionChanged = false;
                if (formChanged && (requestJson != null || !string.IsNullOrEmpty(recoveryId))) { CleanupLocal(); requestJson = null; requestData = null; recoveryId = ""; }
                if (!busy && !failed && message == L("done") &&
                    (formChanged || (GUIUtility.keyboardControl != 0 && GUIUtility.keyboardControl != focusedBeforeForm)))
                {
                    dismissCompletion = true;
                    Repaint();
                }
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
        private async Task LoadBodyCatalog()
        {
            var source = catalogCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                using (var publicClient = new MochiyaUploadClient(null, origin))
                {
                    var catalog = await publicClient.Api<MochiyaBodyCatalog>("/api/avatar-body-tags", "GET", null, source.Token);
                    if (!closing && catalog?.schemaVersion == 1 && catalog.tags != null && catalog.tags.Length <= 2000
                        && catalog.tags.All(t => MochiyaBodyTypes.Concrete(t, contract.limits.bodyType))
                        && catalog.tags.Distinct().Count() == catalog.tags.Length) bodyCatalog = catalog.tags;
                }
            }
            catch (Exception) { /* Suggestions are optional; bundled tags remain available. */ }
            finally { if (catalogCancellation == source) catalogCancellation = null; source.Dispose(); if (!closing && this != null) Repaint(); }
        }
        private void OpenBodyPicker(Rect anchor, ModelSource row, bool single)
        {
            var excluded = single ? Array.Empty<string>() : modelSources.Where(v => v != row).SelectMany(v => v.bodies).ToArray();
            PopupWindow.Show(anchor, new BodyPicker(this, single, row.bodies, excluded, selected =>
            {
                if (busy || closing) return;
                if (single) item.baseBodyType = selected ?? "";
                else row.bodies = row.bodies.Contains(selected) ? row.bodies.Where(v => v != selected).ToArray() : selected == "any" ? new[] { "any" } : row.bodies.Where(v => v != "any").Concat(new[] { selected }).ToArray();
                bodySelectionChanged = true;
                Repaint();
            }));
        }
        private sealed class BodyPicker : PopupWindowContent
        {
            private readonly MochiyaUploadWindow owner;
            private readonly bool single;
            private readonly string[] excluded, selected;
            private Vector2 scroll;
            private readonly Action<string> select;
            private string query = "";
            private bool focus = true;
            public BodyPicker(MochiyaUploadWindow owner, bool single, string[] selected, string[] excluded, Action<string> select)
            { this.owner = owner; this.single = single; this.excluded = excluded; this.selected = selected; this.select = select; }
            public override Vector2 GetWindowSize() => new Vector2(Mathf.Clamp(owner.position.width - 24, 360, 720), 280);
            public override void OnGUI(Rect rect)
            {
                EditorGUILayout.LabelField(owner.L("bodyPlaceholder"), EditorStyles.wordWrappedLabel);
                GUI.SetNextControlName("bodySearch");
                query = EditorGUILayout.TextField(GUIContent.none, query);
                EditorGUILayout.Space(4);
                if (focus) { EditorGUI.FocusTextInControl("bodySearch"); focus = false; }
                var normalized = MochiyaBodyTypes.Normalize(query);
                var matches = owner.bodyCatalog.Where(t => t.Contains(normalized) && !excluded.Contains(t) && (single || !selected.Contains(t)))
                    .OrderByDescending(t => t.StartsWith(normalized, StringComparison.Ordinal)).ThenBy(t => t, StringComparer.Ordinal).ToArray();
                bool custom = MochiyaBodyTypes.Concrete(normalized, owner.contract.limits.bodyType) && !excluded.Contains(normalized) && !matches.Contains(normalized) && (single || !selected.Contains(normalized));
                if (custom) Option(owner.L("useBody").Replace("{body}", normalized), normalized);
                scroll = EditorGUILayout.BeginScrollView(scroll);
                if (!single) foreach (var tag in selected.Where(t => t.Contains(normalized))) Option((tag == "any" ? owner.L("bodyAny") : tag) + " ×", tag);
                if (query.Length == 0 && single) Option(owner.L("bodyNone"), null);
                if (!single && !excluded.Contains("any") && !selected.Contains("any") && (query.Length == 0 || "any".Contains(normalized))) Option(owner.L("bodyAny"), "any");
                foreach (var tag in matches) Option(tag, tag);
                if (!custom && query.Length > 0 && matches.Length == 0 && normalized != "any") EditorGUILayout.LabelField(owner.L("invalidBody"), EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndScrollView();
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { editorWindow.Close(); Event.current.Use(); }
            }
            private void Option(string label, string value)
            {
                if (GUILayout.Button(label, EditorStyles.miniButton)) { select(value); editorWindow.Close(); GUIUtility.ExitGUI(); }
            }
        }
        private bool ModelAction(string icon, string label, bool destructive = false)
        {
            var style = new GUIStyle(EditorStyles.miniButton);
            if (destructive) style.hover.textColor = new Color(0.9f, 0.25f, 0.25f);
            return GUILayout.Button(new GUIContent(icon, L(label)), style, GUILayout.Width(28));
        }
        private void DrawModelVariants()
        {
            modelSources[0].source = target; modelSources[0].format = outputFormat;
            bool isBase = item.category == "base_avatar";
            if (!isBase) EditorGUILayout.LabelField(L("bodyMatchingHint"), EditorStyles.wordWrappedLabel);
            if (isBase && modelSources.Count != 1) EditorGUILayout.HelpBox(L("baseOneModel"), MessageType.Error);
            int remove = -1, move = -1, direction = 0;
            for (int i = 0; i < modelSources.Count; i++)
            {
                var row = modelSources[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    // First line: one body dropdown for a base; removable body chips for attachments.
                    var firstRow = EditorGUILayout.GetControlRect();
                    float sourceWidth = Mathf.Min(180, firstRow.width * 0.5f);
                    var tagRect = new Rect(firstRow.x, firstRow.y, firstRow.width - sourceWidth - 8, firstRow.height);
                    var sourceRect = new Rect(tagRect.xMax + 8, firstRow.y, sourceWidth, firstRow.height);
                    EditorGUI.BeginChangeCheck();
                    row.source = (GameObject)EditorGUI.ObjectField(sourceRect, GUIContent.none, row.source, typeof(GameObject), true);
                    if (EditorGUI.EndChangeCheck() && i == 0 && row.source != null) item.title = row.source.name.Substring(0, Math.Min(row.source.name.Length, contract.limits.title));
                    if (isBase)
                    {
                        if (EditorGUI.DropdownButton(tagRect, new GUIContent(string.IsNullOrEmpty(item.baseBodyType) ? L("selectBodyType") : item.baseBodyType), FocusType.Keyboard)) OpenBodyPicker(tagRect, row, true);
                    }
                    else
                    {
                        // Keep a single line even for many tags; remaining tags are available in the popup.
                        float x = tagRect.x;
                        float addWidth = EditorStyles.miniButton.CalcSize(new GUIContent(L("addBody"))).x + 20;
                        foreach (var body in row.bodies)
                        {
                            string text = (body == "any" ? L("bodyAny") : body) + " ×";
                            float width = EditorStyles.miniButton.CalcSize(new GUIContent(text)).x;
                            if (x + width > tagRect.xMax - addWidth) break;
                            if (GUI.Button(new Rect(x, tagRect.y, width, tagRect.height), new GUIContent(text, L("removeBody").Replace("{body}", body)), EditorStyles.miniButton))
                            { row.bodies = row.bodies.Where(t => t != body).ToArray(); GUI.changed = true; }
                            x += width + 3;
                        }
                        if (EditorGUI.DropdownButton(new Rect(x, tagRect.y, tagRect.xMax - x, tagRect.height), new GUIContent(L("addBody"), L("bodyMatchingHint") + "\n" + string.Join(", ", row.bodies)), FocusType.Keyboard)) OpenBodyPicker(tagRect, row, false);
                    }
                    // Selected source and a text-labelled preview appear only after selection.
                    if (row.source != null) using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(row.source.name, EditorStyles.label);
                        if (GUILayout.Button(L("previewModel"), GUILayout.Width(sourceWidth))) { Selection.activeGameObject = row.source; SceneView.FrameLastActiveSceneView(); }
                    }
                    DrawModelExportSettings(row);
                    // Keep card actions separate from the original export-settings controls.
                    if (!isBase || modelSources.Count > 1) using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (!isBase) {
                        using (new EditorGUI.DisabledScope(i == 0)) if (ModelAction("↑", "moveUp")) { move = i; direction = -1; }
                        using (new EditorGUI.DisabledScope(i == modelSources.Count - 1)) if (ModelAction("↓", "moveDown")) { move = i; direction = 1; }
                        }
                        if (!isBase || modelSources.Count > 1) using (new EditorGUI.DisabledScope(modelSources.Count == 1)) if (ModelAction("×", "removeVariant", true)) remove = i;
                    }
                }
            }
            if (move >= 0) { var row = modelSources[move]; modelSources[move] = modelSources[move + direction]; modelSources[move + direction] = row; }
            if (remove >= 0)
            {
                var id = modelSources[remove].id;
                if (analyses.TryGetValue(id, out var removedAnalysis)) { removedAnalysis.Dispose(); analyses.Remove(id); }
                modelSources.RemoveAt(remove);
            }
            target = modelSources[0].source; outputFormat = modelSources[0].format;
            if (move >= 0 || remove >= 0) GUI.changed = true;

            if (!isBase && modelSources.Count >= contract.limits.modelVariants)
            {
                var limitStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                limitStyle.normal.textColor = new Color(0.9f, 0.25f, 0.25f);
                EditorGUILayout.LabelField(L("variantLimits"), limitStyle);
            }
        }
        private void DrawModelExportSettings(ModelSource row)
        {
            analyses.TryGetValue(row.id, out var analysis);
            row.exportSettings = FormFoldout(row.exportSettings, L("exportSettings"));
            if (row.exportSettings)
            {
                row.format = EditorGUILayout.Popup(L("format"), row.format, new[] { "VRM", "GLB" });
                if (row.source != null && analysis?.Target?.Root == row.source)
                {
                    var detected = analysis.Target;
                    EditorGUILayout.HelpBox(detected.Error ?? detected.Reason, detected.Kind == MochiyaTargetKind.Invalid ? MessageType.Error : MessageType.Info);
                }
                var previous = row.profile;
                MochiyaExportProfileGUI.Draw(ref row.profile, locale);
                if (previous != row.profile) GUI.changed = true;
            }
            row.warnings = FormFoldout(row.warnings, L("warnings"));
            if (row.warnings && row.source != null && analysis?.Target?.Root == row.source && analysis.Report != null)
            {
                foreach (var error in analysis.Report.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
                foreach (var warning in analysis.Report.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }
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
        private static GUIStyle RequiredLabelStyle() => new GUIStyle(EditorStyles.label) { richText = true };
        private static string RequiredLabel(string label) => label + (EditorGUIUtility.isProSkin
            ? " <color=#FF6B6B>*</color>" : " <color=#C62828>*</color>");
        private static Rect RequiredFieldRect(string label)
        {
            var rect = EditorGUILayout.GetControlRect();
            return EditorGUI.PrefixLabel(rect, new GUIContent(RequiredLabel(label)), RequiredLabelStyle());
        }
        private string FileField(string label, string value, string extension)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(RequiredLabel(label), RequiredLabelStyle());
                if (GUILayout.Button(L("choose"), GUILayout.Width(120))) { var path = EditorUtility.OpenFilePanel(label, "", extension); if (!string.IsNullOrEmpty(path)) { value = path; GUI.changed = true; } }
            }
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
            if (label == "gallery") { GalleryGrid(files); return; }
            for (int i = 0; i < files.Count; i++) using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(Path.GetFileName(files[i]));
                if (GUILayout.Button(L("remove"), GUILayout.Width(80))) { files.RemoveAt(i); GUI.changed = true; break; }
            }
        }
        private void GalleryGrid(List<string> files)
        {
            if (files.Count == 0) return;
            const float gap = 6;
            var width = Mathf.Max(1, FormWidth - 4);
            var columns = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (110 + gap)));
            var size = (width - gap * (columns - 1)) / columns;
            var rows = Mathf.CeilToInt(files.Count / (float)columns);
            var grid = GUILayoutUtility.GetRect(width, rows * (size + gap) - gap, GUILayout.ExpandWidth(false));
            grid.x += 2;
            var removeIndex = -1;
            for (int i = 0; i < files.Count; i++)
            {
                var path = files[i];
                var rect = new Rect(grid.x + (i % columns) * (size + gap), grid.y + (i / columns) * (size + gap), size, size);
                if (ImageThumbnail(rect, GalleryPreview(path), path))
                    removeIndex = i;
            }
            if (removeIndex >= 0)
            {
                var path = files[removeIndex];
                files.RemoveAt(removeIndex);
                if (!files.Contains(path) && galleryPreviews.TryGetValue(path, out var preview))
                { if (preview != null) DestroyImmediate(preview); galleryPreviews.Remove(path); }
                GUI.changed = true;
            }
        }
        private bool ImageThumbnail(Rect rect, Texture2D preview, string path)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            var imageRect = new Rect(rect.x + 3, rect.y + 3, rect.width - 6, rect.height - 6);
            if (preview != null) GUI.DrawTexture(imageRect, preview, ScaleMode.ScaleToFit);
            else GUI.Label(imageRect, EditorGUIUtility.IconContent("Texture Icon"));
            GUI.Label(rect, new GUIContent("", Path.GetFileName(path)));
            return GUI.Button(new Rect(rect.xMax - 25, rect.y + 3, 22, 22), new GUIContent("×", L("remove") + ": " + Path.GetFileName(path)));
        }
        private Texture2D GalleryPreview(string path)
        {
            if (galleryPreviews.TryGetValue(path, out var cached)) return cached;
            Texture2D preview = null;
            try
            {
                var file = new FileInfo(path);
                if (file.Exists && file.Length > 0 && file.Length <= contract.limits.imageBytes)
                {
                    preview = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                    if (!preview.LoadImage(File.ReadAllBytes(path), true)) { DestroyImmediate(preview); preview = null; }
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is UnityException)
            { if (preview != null) DestroyImmediate(preview); preview = null; }
            galleryPreviews[path] = preview;
            return preview;
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
            if (string.IsNullOrEmpty(cover)) return;
            var width = Mathf.Max(1, FormWidth - 4);
            var columns = Mathf.Max(1, Mathf.FloorToInt((width + 6) / 116));
            var size = (width - 6 * (columns - 1)) / columns;
            var row = GUILayoutUtility.GetRect(width, size, GUILayout.ExpandWidth(false));
            var rect = new Rect(row.x + 2 + (width - size) / 2, row.y, size, size);
            if (ImageThumbnail(rect, coverPreview, cover))
            {
                cover = ""; previewPath = null;
                if (coverPreview != null) DestroyImmediate(coverPreview);
                coverPreview = null;
                GUI.changed = true;
            }
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
                var result = await refreshClient.Api<MochiyaUploadCapabilities>("/api/v1/creator/upload-capabilities?contractVersion=3&locale=" + refreshLocale, "GET", null, source.Token);
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
                    var result = await client.Api<MochiyaUploadCapabilities>("/api/v1/creator/upload-capabilities?contractVersion=3&locale=" + locale, "GET", null, cancel);
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
        internal static string ModelFileName(string title, string category, string baseBody, string[] bodies, bool vrm, HashSet<string> usedNames)
        {
            string Clean(string value)
            {
                var cleaned = System.Text.RegularExpressions.Regex.Replace((value ?? "").Normalize(), @"[^\p{L}\p{M}\p{N}_]+", "_");
                return System.Text.RegularExpressions.Regex.Replace(cleaned, "_+", "_").Trim('_');
            }
            string Limit(string value, int bytes)
            {
                int length = 0, count = 0;
                while (length < value.Length)
                {
                    int size = char.IsHighSurrogate(value[length]) && length + 1 < value.Length ? 2 : 1;
                    int next = System.Text.Encoding.UTF8.GetByteCount(value.Substring(length, size));
                    if (count + next > bytes) break;
                    count += next; length += size;
                }
                return value.Substring(0, length).TrimEnd('_');
            }
            var name = Clean(title);
            if (name.Length == 0) name = "item";
            var tags = category == "base_avatar"
                ? new[] { string.IsNullOrEmpty(baseBody) ? "none" : baseBody }
                : bodies;
            var bodyName = Clean(string.Join("_", tags));
            var shortened = Limit(bodyName, 100);
            if (shortened != bodyName) shortened += "_more";
            var stem = Limit(name, 100) + "_" + shortened;
            var extension = vrm ? ".vrm" : ".glb";
            var result = stem + extension;
            for (int suffix = 2; !usedNames.Add(result); suffix++) result = stem + "_" + suffix + extension;
            return result;
        }
        private async Task Prepare()
        {
            CleanupLocal();
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
            modelSources[0].source = target; modelSources[0].format = outputFormat;
            item.modelVariants = modelSources.Select(v => new MochiyaModelVariant { id = v.id, bodyTypes = item.category == "base_avatar" ? Array.Empty<string>() : v.bodies }).ToArray();
            if (item.category != "base_avatar" && item.modelVariants.Any(v => v.bodyTypes.Length == 0)) throw new InvalidOperationException(L("variantBodyRequired"));
            if (!MochiyaBodyTypes.Valid(item.category, item.baseBodyType, item.modelVariants, contract.limits)) throw new InvalidOperationException(L("variantInvalid"));
            if (modelSources.Any(v => v.source == null)) throw new InvalidOperationException(L("noSource"));
            uploadPaths.Clear(); var files = new List<MochiyaUploadFile>();
            Action<string, string, string> add = (role, path, slotId) =>
            {
                var info = new FileInfo(path); var image = role == "cover" || role == "gallery";
                if (!info.Exists || info.Length <= 0) throw new InvalidOperationException(L("fileMissingOrEmpty").Replace("{name}", info.Name));
                var limit = image ? contract.limits.imageBytes : contract.limits.fileBytes;
                if (info.Length > limit) throw new InvalidOperationException(L("fileTooLarge").Replace("{name}", info.Name).Replace("{limit}", (limit / (1024 * 1024)).ToString()));
                var ext = info.Extension.ToLowerInvariant(); var type = ext == ".png" ? "image/png" : ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : "application/octet-stream";
                if (image && type == "application/octet-stream") throw new InvalidOperationException(info.Name + ": " + L(image && role == "gallery" ? "galleryInvalid" : "coverInvalid"));
                var id = slotId ?? role + "-" + files.Count;
                files.Add(new MochiyaUploadFile { id = id, role = role, name = info.Name, size = info.Length, contentType = type }); uploadPaths[id] = path;
                if (files.Sum(f => f.size) > contract.limits.totalBytes) throw new InvalidOperationException(L("totalFilesTooLarge").Replace("{limit}", (contract.limits.totalBytes / (1024 * 1024 * 1024)).ToString()));
            };
            add("cover", cover, null);
            var modelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var variant in modelSources)
            {
                await Task.Yield();
                cancellation.Token.ThrowIfCancellationRequested();
                var modelName = ModelFileName(item.title, item.category, item.baseBodyType, variant.bodies, variant.format == 0, modelNames);
                var modelPath = Path.Combine(temporaryDirectory, modelName);
                message = L("prepare") + " — " + variant.source.name; Repaint();
                try
                {
                    MochiyaAvatarWorkflow.Export(variant.source, modelPath, variant.profile);
                    add("model", modelPath, variant.id);
                }
                catch (Exception error) when (!(error is OperationCanceledException))
                {
                    throw new InvalidOperationException(variant.source.name + ": " + error.Message, error);
                }
            }
            foreach (var path in gallery) add("gallery", path, null);
            foreach (var path in additional) add("additional", path, null);
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
                    if (requestJson == null) { await Prepare(); }
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

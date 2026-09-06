using System;
using System.IO;
using System.Linq;
using Mochiya.AvatarComposition;
using UniVRM10;
using UnityEditor;
using UnityEngine;

namespace Mochiya.LilToon.Exporter.Editor
{
    public enum MochiyaTargetKind { Invalid, Avatar, Attachment }

    public sealed class MochiyaAvatarTarget
    {
        public GameObject Root { get; internal set; }
        public GameObject ReferenceAvatar { get; internal set; }
        public MochiyaTargetKind Kind { get; internal set; }
        public bool IsPrepared { get; internal set; }
        public bool UsesParentRig { get; internal set; }
        public string Reason { get; internal set; }
        public string Error { get; internal set; }
    }

    /// <summary>The single-target creator workflow. Explicit converter/exporter APIs remain available separately.</summary>
    public static class MochiyaAvatarWorkflow
    {
        public static MochiyaAvatarTarget Detect(GameObject root)
        {
            var result = new MochiyaAvatarTarget { Root = root };
            if (root == null) { result.Error = "Choose an avatar or attachment from the Hierarchy."; return result; }
            if (EditorUtility.IsPersistent(root) || !root.scene.IsValid())
            { result.Error = "Place the prefab in a scene, then select its root in the Hierarchy."; return result; }
            if (!root.GetComponentsInChildren<Renderer>(true).Any(r => r is MeshRenderer || r is SkinnedMeshRenderer))
            { result.Error = "This object has no avatar or attachment meshes. Select the model's root."; return result; }

            var asset = root.GetComponent<MochiyaAvatarComposition>();
            if (asset != null && !HasAuthoringComponents(root))
            {
                // Extracted attachments carry a full reference humanoid. Their explicit kind wins over that Animator.
                result.Kind = asset.Kind == AssetKind.Avatar ? MochiyaTargetKind.Avatar : MochiyaTargetKind.Attachment;
                result.ReferenceAvatar = root;
                result.IsPrepared = !HasAuthoringComponents(root);
                result.Reason = "Previously converted asset; its saved kind is preserved.";
                return result;
            }
            var dependency = MochiyaParentDependency.Inspect(root);
            if (dependency.Errors.Count > 0)
            { result.Error = string.Join("\n", dependency.Errors.Distinct()); return result; }
            var ownRig = MochiyaAvatarConverter.HasValidHumanoid(root);
            if (ownRig && dependency.Reasons.Count == 0)
            {
                result.Kind = MochiyaTargetKind.Avatar;
                result.ReferenceAvatar = root;
                result.IsPrepared = root.GetComponent<Vrm10Instance>() != null && !HasAuthoringComponents(root);
                result.Reason = "Has its own VRM humanoid and no MA dependency outside this asset.";
                return result;
            }
            var parent = root.transform.parent != null ? root.transform.parent.gameObject : null;
            if (parent != null && MochiyaAvatarConverter.HasValidHumanoid(parent) && Detect(parent).Kind == MochiyaTargetKind.Avatar)
            {
                result.Kind = MochiyaTargetKind.Attachment;
                result.ReferenceAvatar = parent;
                result.UsesParentRig = !ownRig;
                result.Reason = !ownRig ? "Needs its direct parent's humanoid for VRM export."
                    : "Has its own humanoid, but MA depends on objects or settings outside this asset.";
                return result;
            }
            result.Error = ownRig
                ? "This asset has an external MA dependency. Its direct parent must be a valid independent VRM avatar."
                : "This asset has no valid VRM humanoid. Place it directly under a valid independent avatar to convert it as an attachment, or configure its own Humanoid Animator.";
            return result;
        }

        internal static bool HasAuthoringComponents(GameObject root) => root.GetComponentsInChildren<Component>(true).Any(c => c != null &&
            (c.GetType().Namespace == "nadena.dev.modular_avatar.core" || (c.GetType().Namespace ?? "").StartsWith("VRC.", StringComparison.Ordinal)));

        public static MochiyaConversionReport Validate(GameObject root, bool asVrm = true, MochiyaExportProfile profile = null)
        {
            var detected = Detect(root);
            if (detected.Kind == MochiyaTargetKind.Invalid)
            {
                var invalid = new MochiyaConversionReport(); invalid.Errors.Add(detected.Error); return invalid;
            }
            if (!detected.IsPrepared)
                return MochiyaAvatarConverter.Validate(detected.ReferenceAvatar,
                    detected.Kind == MochiyaTargetKind.Attachment ? root : null,
                    new MochiyaConversionOptions { AllowGenericRig = !asVrm });

            var report = new MochiyaConversionReport { CanExportVrm = MochiyaAvatarConverter.HasValidHumanoid(root) };
            if (EditorApplication.isPlayingOrWillChangePlaymode) report.Errors.Add("Export requires Edit Mode.");
            profile = profile != null ? profile : MochiyaExportProfile.Default;
            foreach (var issue in MochiyaExportValidation.Validate(root, asVrm, profile.CreateMetadata(root)))
                (issue.Severity == MochiyaExportIssueSeverity.Error ? report.Errors : report.Warnings).Add(issue.Message);
            var saved = root.GetComponent<MochiyaAvatarComposition>()?.ConversionReport;
            if (!string.IsNullOrEmpty(saved)) report.Warnings.AddRange(saved.Split('\n').Where(line => line.StartsWith("Warning:") || line.StartsWith("Unsupported:")));
            return report;
        }

        public static MochiyaConversionResult ConvertToVrmGameObject(GameObject root, MochiyaExportProfile profile = null)
        {
            var detected = Detect(root);
            RequireValid(root, true, profile);
            if (detected.IsPrepared) throw new InvalidOperationException("This model is already prepared for export.");
            var result = detected.Kind == MochiyaTargetKind.Attachment
                ? MochiyaAvatarConverter.ConvertAttachmentInScene(detected.ReferenceAvatar, root)
                : MochiyaAvatarConverter.ConvertAvatarInScene(root);
            try { ApplyMetadata(result.Root, root, profile); return result; }
            catch { result.Dispose(); throw; }
        }

        public static MochiyaConversionReport Export(GameObject root, string path, MochiyaExportProfile profile = null)
        {
            var extension = Path.GetExtension(path);
            var asVrm = string.Equals(extension, ".vrm", StringComparison.OrdinalIgnoreCase);
            if (!asVrm && !string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose a .vrm or .glb output path.", nameof(path));
            var detected = Detect(root);
            RequireValid(root, asVrm, profile);
            profile = profile != null ? profile : MochiyaExportProfile.Default;
            if (detected.IsPrepared) { MochiyaLilToonExporter.ExportWithProfile(root, path, profile); return Validate(root, asVrm, profile); }

            using (var result = MochiyaAvatarConverter.ConvertForExport(detected.ReferenceAvatar,
                detected.Kind == MochiyaTargetKind.Attachment ? root : null, new MochiyaConversionOptions { AllowGenericRig = !asVrm }))
            {
                // Use the selected source name/metadata, not the temporary duplicate's generated suffix.
                if (asVrm) MochiyaLilToonExporter.ExportVrm(result.Root, path, profile.CreateMetadata(root), profile);
                else MochiyaLilToonExporter.ExportGlb(result.Root, path, profile.GlbSettings);
                return result.Report;
            }
        }

        private static void RequireValid(GameObject root, bool asVrm, MochiyaExportProfile profile)
        {
            var report = Validate(root, asVrm, profile);
            if (!report.CanConvert) throw new InvalidOperationException(string.Join("\n", report.Errors));
        }

        private static void ApplyMetadata(GameObject result, GameObject source, MochiyaExportProfile profile)
        {
            profile = profile != null ? profile : MochiyaExportProfile.Default;
            result.GetComponent<Vrm10Instance>().Vrm.Meta = profile.CreateMetadata(source);
            result.GetComponent<MochiyaSceneResources>().Capture();
        }
    }
}

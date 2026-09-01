using System;
using System.Collections.Generic;
using System.Linq;
using UniVRM10;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mochiya.LilToon.Exporter.Editor
{
    public enum MochiyaExportIssueSeverity
    {
        Warning,
        Error,
    }

    public sealed class MochiyaExportIssue
    {
        public MochiyaExportIssueSeverity Severity { get; }
        public string Message { get; }

        public MochiyaExportIssue(MochiyaExportIssueSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    public static class MochiyaExportValidation
    {
        public static IReadOnlyList<MochiyaExportIssue> Validate(
            GameObject root,
            bool asVrm,
            VRM10ObjectMeta meta = null)
        {
            var issues = new List<MochiyaExportIssue>();
            if (root == null)
            {
                issues.Add(new MochiyaExportIssue(MochiyaExportIssueSeverity.Error, "Choose an export root."));
                return issues;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(false)
                .Where(x => x.enabled && x.gameObject.activeInHierarchy)
                .ToArray();
            if (renderers.Length == 0)
            {
                issues.Add(new MochiyaExportIssue(MochiyaExportIssueSeverity.Error, "The export root has no enabled renderers."));
            }

            var lilToonCount = 0;
            foreach (var material in renderers.SelectMany(x => x.sharedMaterials).Distinct())
            {
                if (material == null)
                {
                    issues.Add(new MochiyaExportIssue(MochiyaExportIssueSeverity.Warning, "A renderer has an empty material slot."));
                    continue;
                }
                if (!LilToonMaterialSupport.IsLilToonMaterial(material)) continue;

                ++lilToonCount;
                if (!LilToonMaterialSupport.IsSupported(material, out var reason))
                {
                    issues.Add(new MochiyaExportIssue(
                        MochiyaExportIssueSeverity.Error,
                        $"Material '{material.name}': {reason}"));
                    continue;
                }

                ValidateTextureDimensions(material, issues);
            }

            if (lilToonCount == 0)
            {
                issues.Add(new MochiyaExportIssue(
                    MochiyaExportIssueSeverity.Warning,
                    "No lilToon materials were found. The file will be a normal UniVRM export without the Mochiya extension."));
            }

            if (asVrm)
            {
                var animator = root.GetComponent<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                {
                    issues.Add(new MochiyaExportIssue(
                        MochiyaExportIssueSeverity.Error,
                        "VRM export requires an Animator with a valid Humanoid avatar on the export root."));
                }
                if (meta == null)
                {
                    issues.Add(new MochiyaExportIssue(MochiyaExportIssueSeverity.Error, "VRM 1.0 metadata is required."));
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(meta.Name))
                        issues.Add(new MochiyaExportIssue(MochiyaExportIssueSeverity.Error, "VRM metadata requires a name."));
                    if (meta.Authors == null || meta.Authors.All(string.IsNullOrWhiteSpace))
                        issues.Add(new MochiyaExportIssue(MochiyaExportIssueSeverity.Error, "VRM metadata requires at least one author."));
                }
            }

            return issues;
        }

        public static void ThrowIfInvalid(GameObject root, bool asVrm, VRM10ObjectMeta meta = null)
        {
            var errors = Validate(root, asVrm, meta)
                .Where(x => x.Severity == MochiyaExportIssueSeverity.Error)
                .Select(x => x.Message)
                .ToArray();
            if (errors.Length > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        private static void ValidateTextureDimensions(Material material, ICollection<MochiyaExportIssue> issues)
        {
            var shader = material.shader;
            for (var index = 0; index < shader.GetPropertyCount(); ++index)
            {
                if (shader.GetPropertyType(index) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                var propertyName = shader.GetPropertyName(index);
                var texture = material.GetTexture(propertyName);
                if (texture == null || texture.dimension == TextureDimension.Tex2D) continue;

                issues.Add(new MochiyaExportIssue(
                    MochiyaExportIssueSeverity.Error,
                    $"Material '{material.name}' texture '{propertyName}' is {texture.dimension}; glTF can embed only 2D material textures."));
            }
        }
    }
}

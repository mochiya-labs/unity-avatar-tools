using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Mochiya.AvatarTools.Editor
{
    internal static class MochiyaBodyTypes
    {
        internal static string Normalize(string value) => Regex.Replace(
            Regex.Replace((value ?? "").Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant(), @"[^a-z0-9-]+", "-"), "-+", "-").Trim('-');

        internal static bool Concrete(string value, int limit) => !string.IsNullOrEmpty(value)
            && value.Length <= limit && value != "any" && value != "none"
            && Regex.IsMatch(value, @"^[a-z0-9]+(?:-[a-z0-9]+)*$");

        internal static bool Valid(string category, string body, MochiyaModelVariant[] variants, MochiyaUploadLimits limits)
        {
            if (variants == null || variants.Length == 0 || variants.Length > limits.modelVariants
                || variants.Any(v => v == null || v.bodyTypes == null || !Regex.IsMatch(v.id ?? "", @"^[a-zA-Z0-9_-]{1,60}$"))
                || variants.Select(v => v.id).Distinct().Count() != variants.Length) return false;
            if (category == "base_avatar") return variants.Length == 1 && variants[0].bodyTypes.Length == 0
                && (string.IsNullOrEmpty(body) || Concrete(body, limits.bodyType));
            if (!string.IsNullOrEmpty(body)) return false;
            var tags = variants.SelectMany(v => v.bodyTypes).ToArray();
            return variants.All(v => v.bodyTypes.Length > 0 && (v.bodyTypes.SequenceEqual(new[] { "any" })
                    || v.bodyTypes.All(t => Concrete(t, limits.bodyType))))
                && tags.Distinct().Count() == tags.Length && tags.Count(t => t != "any") <= limits.bodyTypes;
        }
    }
}

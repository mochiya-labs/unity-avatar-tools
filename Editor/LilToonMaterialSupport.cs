using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor
{
    /// <summary>
    /// Shader families accepted by the current Three.js runtime. Appearance has renderer-specific limits.
    /// </summary>
    public static class LilToonMaterialSupport
    {
        private static readonly Dictionary<string, (string Mode, string Transparency)> SupportedShaders =
            new Dictionary<string, (string Mode, string Transparency)>(StringComparer.Ordinal)
        {
            { "lilToon", ("opaque", "normal") },
            { "Hidden/lilToonOutline", ("opaque", "normal") },
            { "Hidden/lilToonCutout", ("cutout", "normal") },
            { "Hidden/lilToonCutoutOutline", ("cutout", "normal") },
            { "Hidden/lilToonTransparent", ("transparent", "normal") },
            { "Hidden/lilToonTransparentOutline", ("transparent", "normal") },
            { "Hidden/lilToonOnePassTransparent", ("transparent", "one-pass") },
            { "Hidden/lilToonOnePassTransparentOutline", ("transparent", "one-pass") },
            { "Hidden/lilToonTwoPassTransparent", ("transparent", "two-pass") },
            { "Hidden/lilToonTwoPassTransparentOutline", ("transparent", "two-pass") },
            { "Hidden/lilToonRefraction", ("refraction", "normal") },
            { "Hidden/lilToonRefractionBlur", ("refraction-blur", "normal") },
            { "Hidden/lilToonFur", ("fur", "normal") },
            { "Hidden/lilToonFurCutout", ("fur-cutout", "normal") },
            { "Hidden/lilToonFurTwoPass", ("fur-two-pass", "normal") },
            { "Hidden/lilToonGem", ("gem", "normal") },
        };

        public static bool IsLilToonShaderName(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return false;
            return shaderName.IndexOf("lilToon", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsSupportedShaderName(string shaderName)
        {
            return shaderName != null && SupportedShaders.ContainsKey(shaderName);
        }

        public static bool IsLilToonMaterial(Material material)
        {
            return material != null && material.shader != null && IsLilToonShaderName(material.shader.name);
        }

        public static bool IsSupported(Material material, out string reason)
        {
            reason = null;
            if (!IsLilToonMaterial(material)) return false;

            var shaderName = material.shader.name;
            if (!IsSupportedShaderName(shaderName))
            {
                reason = $"Shader '{shaderName}' is a specialized lilToon variant that three-liltoon does not yet reproduce.";
                return false;
            }

            return true;
        }

        public static string GetRenderMode(Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            return GetShaderModes(material).Mode;
        }

        public static string GetTransparencyMode(Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            return GetShaderModes(material).Transparency;
        }
        private static (string Mode, string Transparency) GetShaderModes(Material material)
        {
            var name = material.shader != null ? material.shader.name : string.Empty;
            if (SupportedShaders.TryGetValue(name, out var modes)) return modes;
            throw new NotSupportedException($"Unsupported lilToon shader '{name}'.");
        }

    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor
{
    /// <summary>
    /// The deliberately small lilToon surface that the current Three.js port can render faithfully.
    /// </summary>
    public static class LilToonMaterialSupport
    {
        private static readonly HashSet<string> SupportedShaderNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "lilToon",
            "Hidden/lilToonOutline",
            "Hidden/lilToonCutout",
            "Hidden/lilToonCutoutOutline",
            "Hidden/lilToonTransparent",
            "Hidden/lilToonTransparentOutline",
        };

        public static bool IsLilToonShaderName(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return false;
            return shaderName.IndexOf("lilToon", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsSupportedShaderName(string shaderName)
        {
            return SupportedShaderNames.Contains(shaderName);
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

            if (material.HasProperty("_TransparentMode"))
            {
                var mode = Mathf.RoundToInt(material.GetFloat("_TransparentMode"));
                if (mode < 0 || mode > 2)
                {
                    reason = $"Material rendering mode {mode} is outside opaque, cutout, and transparent.";
                    return false;
                }
            }

            return true;
        }

        public static string GetRenderMode(Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            var shaderName = material.shader != null ? material.shader.name : string.Empty;
            if (shaderName.IndexOf("Cutout", StringComparison.OrdinalIgnoreCase) >= 0) return "cutout";
            if (shaderName.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0) return "transparent";

            if (material.HasProperty("_TransparentMode"))
            {
                switch (Mathf.RoundToInt(material.GetFloat("_TransparentMode")))
                {
                    case 1: return "cutout";
                    case 2: return "transparent";
                }
            }

            return "opaque";
        }
    }
}

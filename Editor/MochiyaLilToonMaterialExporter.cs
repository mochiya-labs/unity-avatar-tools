using System;
using UniGLTF;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor
{
    internal sealed class MochiyaLilToonMaterialExporter : IMaterialExporter
    {
        private readonly IMaterialExporter _fallback;

        public bool HasExportedLilToonMaterial { get; private set; }

        public MochiyaLilToonMaterialExporter(IMaterialExporter fallback)
        {
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        }

        public glTFMaterial ExportMaterial(
            Material material,
            ITextureExporter textureExporter,
            GltfExportSettings settings)
        {
            var destination = _fallback.ExportMaterial(material, textureExporter, settings);
            if (!LilToonMaterialSupport.IsLilToonMaterial(material)) return destination;
            if (!LilToonMaterialSupport.IsSupported(material, out var reason))
            {
                throw new NotSupportedException($"Cannot export lilToon material '{material.name}': {reason}");
            }

            ApplyCoreFallback(destination, material, textureExporter);
            LilToonMaterialSerializer.Attach(destination, material, textureExporter);
            HasExportedLilToonMaterial = true;
            return destination;
        }

        private static void ApplyCoreFallback(
            glTFMaterial destination,
            Material material,
            ITextureExporter textureExporter)
        {
            var renderMode = LilToonMaterialSupport.GetRenderMode(material);
            destination.alphaMode = renderMode == "cutout" ? "MASK" : renderMode == "transparent" ? "BLEND" : "OPAQUE";
            if (renderMode == "cutout" && material.HasProperty("_Cutoff"))
            {
                destination.alphaCutoff = material.GetFloat("_Cutoff");
            }
            destination.doubleSided = material.HasProperty("_Cull") && Mathf.RoundToInt(material.GetFloat("_Cull")) == 0;

            if (destination.pbrMetallicRoughness == null)
            {
                destination.pbrMetallicRoughness = new glTFPbrMetallicRoughness();
            }
            if (material.HasProperty("_Color"))
            {
                var color = material.GetColor("_Color").linear;
                destination.pbrMetallicRoughness.baseColorFactor = new[] { color.r, color.g, color.b, color.a };
            }
            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null)
            {
                destination.pbrMetallicRoughness.baseColorTexture = new glTFMaterialBaseColorTextureInfo
                {
                    index = textureExporter.RegisterExportingAsSRgb(
                        material.GetTexture("_MainTex"),
                        renderMode != "opaque")
                };
                GltfMaterialExportUtils.ExportTextureTransform(
                    material,
                    destination.pbrMetallicRoughness.baseColorTexture,
                    "_MainTex");
            }
        }
    }
}

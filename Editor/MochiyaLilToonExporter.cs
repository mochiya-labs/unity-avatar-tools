using System;
using System.IO;
using System.Linq;
using UniGLTF;
using UniVRM10;
using UnityEditor;
using UnityEngine;
using VrmLib;

namespace Mochiya.LilToon.Exporter.Editor
{
    /// <summary>
    /// Public editor API for deterministic GLB and VRM 1.0 exports.
    /// </summary>
    public static class MochiyaLilToonExporter
    {
        public static void ExportGlb(GameObject root, string path, GltfExportSettings settings = null)
        {
            RequireExtension(path, ".glb");
            MochiyaExportValidation.ThrowIfInvalid(root, false);
            settings = settings ?? new GltfExportSettings();

            var data = new ExportingGltfData();
            var materialExporter = new MochiyaLilToonMaterialExporter(
                MaterialExporterUtility.GetValidGltfMaterialExporter());
            using (var exporter = new gltfExporter(
                data,
                settings,
                progress: new EditorProgress(),
                animationExporter: new EditorAnimationExporter(),
                materialExporter: materialExporter,
                textureSerializer: new EditorTextureSerializer()))
            {
                exporter.Prepare(root);
                exporter.Export();
            }

            AddExtensionUsed(data.Gltf, materialExporter.HasExportedLilToonMaterial);
            File.WriteAllBytes(path, data.ToGlbBytes());
            RefreshAssetDatabase(path);
        }

        public static void ExportVrm(
            GameObject root,
            string path,
            VRM10ObjectMeta meta = null,
            bool useSparseMorphTargets = true)
        {
            RequireExtension(path, ".vrm");
            meta = ResolveMeta(root, meta);
            MochiyaExportValidation.ThrowIfInvalid(root, true, meta);

            var settings = new GltfExportSettings
            {
                UseSparseAccessorForMorphTarget = useSparseMorphTargets,
                ExportOnlyBlendShapePosition = true,
                DivideVertexBuffer = true,
            };

            var exportRoot = UnityEngine.Object.Instantiate(root);
            exportRoot.name = root.name;
            exportRoot.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                using (var arrayManager = new NativeArrayManager())
                {
                    var converter = new ModelExporter();
                    var model = converter.Export(settings, arrayManager, exportRoot);
                    model.ConvertCoordinate(Coordinates.Vrm1, ignoreVrm: false);

                    var materialExporter = new MochiyaLilToonMaterialExporter(
                        Vrm10MaterialExporterUtility.GetValidVrm10MaterialExporter());
                    using (var exporter = new Vrm10Exporter(
                        settings,
                        materialExporter: materialExporter,
                        textureSerializer: new EditorTextureSerializer()))
                    {
                        exporter.Export(
                            exportRoot,
                            model,
                            converter,
                            new ExportArgs { sparse = useSparseMorphTargets },
                            meta);
                        AddExtensionUsed(exporter.Storage.Gltf, materialExporter.HasExportedLilToonMaterial);
                        File.WriteAllBytes(path, exporter.Storage.ToGlbBytes());
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(exportRoot);
            }

            RefreshAssetDatabase(path);
        }

        private static VRM10ObjectMeta ResolveMeta(GameObject root, VRM10ObjectMeta explicitMeta)
        {
            if (explicitMeta != null) return explicitMeta;
            if (root != null && root.TryGetComponent<Vrm10Instance>(out var instance))
            {
                return instance.Vrm != null ? instance.Vrm.Meta : null;
            }
            return null;
        }

        private static void AddExtensionUsed(glTF gltf, bool used)
        {
            if (!used || gltf.extensionsUsed.Contains(LilToonMaterialSerializer.ExtensionName)) return;
            gltf.extensionsUsed.Add(LilToonMaterialSerializer.ExtensionName);
        }

        private static void RequireExtension(string path, string requiredExtension)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An output path is required.", nameof(path));
            if (!string.Equals(Path.GetExtension(path), requiredExtension, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"The output path must end in {requiredExtension}.", nameof(path));
        }

        private static void RefreshAssetDatabase(string path)
        {
            var fullPath = Path.GetFullPath(path).Replace('\\', '/');
            var assetsPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            if (fullPath.StartsWith(assetsPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.Refresh();
            }
        }
    }
}

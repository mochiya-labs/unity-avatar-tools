using System;
using System.IO;
using System.Linq;
using UniGLTF;
using UniGLTF.MeshUtility;
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
            var exportSettings = ScriptableObject.CreateInstance<VRM10ExportSettings>();
            exportSettings.MorphTargetUseSparse = useSparseMorphTargets;
            try
            {
                ExportVrm(root, path, meta, exportSettings);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(exportSettings);
            }
        }

        public static void ExportVrm(
            GameObject root,
            string path,
            VRM10ObjectMeta meta,
            VRM10ExportSettings exportSettings)
        {
            RequireExtension(path, ".vrm");
            meta = ResolveMeta(root, meta);
            MochiyaExportValidation.ThrowIfInvalid(root, true, meta);
            if (exportSettings == null) throw new ArgumentNullException(nameof(exportSettings));
            var settings = exportSettings.MeshExportSettings;

            var exportRoot = UnityEngine.Object.Instantiate(root);
            exportRoot.name = root.name;
            exportRoot.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                if (exportSettings.FreezeMesh)
                {
                    FreezeVrmMesh(exportRoot, exportSettings);
                }

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
                            new ExportArgs { sparse = exportSettings.MorphTargetUseSparse },
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

        private static void FreezeVrmMesh(GameObject exportRoot, VRM10ExportSettings settings)
        {
            var vrmInstance = exportRoot.GetComponent<Vrm10Instance>();
            if (vrmInstance != null)
            {
                vrmInstance.UpdateType = Vrm10Instance.UpdateTypes.None;
            }

            Action freeze = () =>
            {
                var newMeshMap = BoneNormalizer.NormalizeHierarchyFreezeMesh(
                    exportRoot,
                    settings.FreezeMeshUseCurrentBlendShapeWeight);
                BoneNormalizer.Replace(exportRoot, newMeshMap, settings.FreezeMeshKeepRotation);
            };

            // UniVRM protects coordinate-dependent VRM data while transforms
            // are baked. Plain Humanoid exports have no such data to restore.
            if (vrmInstance != null && vrmInstance.Vrm != null)
            {
                using (new Vrm10GeometryBackup(exportRoot))
                {
                    freeze();
                }
            }
            else
            {
                freeze();
            }
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

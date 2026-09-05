using System.Collections.Generic;
using System.Linq;
using UniGLTF;
using UniVRM10;
using UnityEditor;
using UnityEngine;

namespace Mochiya.LilToon.Exporter.Editor
{
    /// <summary>Reusable metadata and native export settings shared by both Mochiya windows.</summary>
    [CreateAssetMenu(menuName = "Mochiya/Export Profile", fileName = "Mochiya Export Profile")]
    public sealed class MochiyaExportProfile : VRM10ExportSettings
    {
        public const string DefaultAuthor = "Mochiya VRM Exporter";
        private const string DefaultAssetGuid = "12059905c92844348a9cce8995e2f402";
        private static MochiyaExportProfile fallback;

        [Tooltip("Use metadata from the selected VRM when available. Otherwise use this profile.")]
        public bool UseAttachedVrmMetadata;
        [Tooltip("An empty name uses the selected object's name. Empty authors use Mochiya VRM Exporter.")]
        public VRM10ObjectMeta Metadata = new VRM10ObjectMeta
        {
            Version = "1.0",
            Authors = new List<string> { DefaultAuthor }
        };
        public GltfExportSettings GlbSettings = new GltfExportSettings { UseSparseAccessorForMorphTarget = true };

        public static MochiyaExportProfile Default
        {
            get
            {
                var profile = AssetDatabase.LoadAssetAtPath<MochiyaExportProfile>(AssetDatabase.GUIDToAssetPath(DefaultAssetGuid));
                if (profile != null) return profile;
                // Keep conversion usable while the package's default asset is being imported.
                if (fallback == null)
                {
                    fallback = CreateInstance<MochiyaExportProfile>();
                    fallback.name = "Default Mochiya Export Profile";
                    fallback.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                }
                return fallback;
            }
        }

        public VRM10ObjectMeta CreateMetadata(GameObject root)
        {
            var instance = root != null ? root.GetComponent<Vrm10Instance>() : null;
            var source = UseAttachedVrmMetadata && instance != null && instance.Vrm != null ? instance.Vrm.Meta : Metadata;
            return CompleteMetadata(root, source);
        }

        internal static VRM10ObjectMeta CompleteMetadata(GameObject root, VRM10ObjectMeta source)
        {
            // Clone all serialized fields, including permission flags and lists, without changing the profile/source.
            var meta = source != null ? JsonUtility.FromJson<VRM10ObjectMeta>(JsonUtility.ToJson(source)) : new VRM10ObjectMeta();
            if (string.IsNullOrWhiteSpace(meta.Name)) meta.Name = root != null ? root.name : "Mochiya Avatar";
            if (string.IsNullOrWhiteSpace(meta.Version)) meta.Version = "1.0";
            meta.Authors = meta.Authors?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
            if (meta.Authors.Count == 0) meta.Authors.Add(DefaultAuthor);
            return meta;
        }
    }
}

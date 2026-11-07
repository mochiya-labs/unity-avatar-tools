using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor
{
    // Keep importer changes scoped to synchronous export, including failure/cancellation cleanup.
    internal sealed class MochiyaReadableMeshes : IDisposable
    {
        private readonly List<string> changed = new List<string>();

        internal MochiyaReadableMeshes(GameObject root)
        {
            var paths = new HashSet<string>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh
                    : renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
                if (mesh == null || mesh.isReadable) continue;
                var path = AssetDatabase.GetAssetPath(mesh);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter))
                    throw new InvalidOperationException($"Mesh '{mesh.name}' on '{AnimationUtility.CalculateTransformPath(renderer.transform, null)}' is not readable and has no model importer. Enable mesh Read/Write in its source before exporting.");
                paths.Add(path);
            }
            try
            {
                foreach (var path in paths)
                {
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    if (importer.isReadable) continue;
                    // Record before reimport so a failed import is also restored.
                    changed.Add(path);
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
            }
            catch (Exception error)
            {
                try { Dispose(); }
                catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
        }

        public void Dispose()
        {
            var failures = new List<Exception>();
            foreach (var path in changed.AsEnumerable().Reverse())
            {
                try
                {
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null) throw new InvalidOperationException("Model importer is unavailable.");
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
                catch (Exception error)
                {
                    failures.Add(new InvalidOperationException($"Could not restore Read/Write to disabled for '{path}'. Restore it in Model import settings. {error.Message}", error));
                }
            }
            changed.Clear();
            if (failures.Count > 0) throw new AggregateException("Failed to restore temporary mesh import settings.", failures);
        }
    }
}

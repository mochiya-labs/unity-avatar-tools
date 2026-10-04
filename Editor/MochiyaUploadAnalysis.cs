using System;
using UnityEditor;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor
{
    // GUI events only consume these results. Scene inspection runs on EditorApplication.update.
    internal sealed class MochiyaUploadAnalysis : IDisposable
    {
        private readonly Func<GameObject, MochiyaAvatarTarget> detect;
        private readonly Func<GameObject, bool, MochiyaExportProfile, MochiyaConversionReport> validate;
        private GameObject target;
        private MochiyaExportProfile profile;
        private bool asVrm, warnings, dirty = true, disposed;
        private double nextRefresh;
        internal MochiyaAvatarTarget Target { get; private set; }
        internal MochiyaConversionReport Report { get; private set; }

        internal MochiyaUploadAnalysis(
            Func<GameObject, MochiyaAvatarTarget> detect = null,
            Func<GameObject, bool, MochiyaExportProfile, MochiyaConversionReport> validate = null)
        {
            this.detect = detect ?? MochiyaAvatarWorkflow.Detect;
            this.validate = validate ?? MochiyaAvatarWorkflow.Validate;
            EditorApplication.hierarchyChanged += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
            ObjectChangeEvents.changesPublished += ObjectsChanged;
        }

        private void ObjectsChanged(ref ObjectChangeEventStream changes)
        {
            // EditorWindow state (typing/scrolling) must not invalidate scene analysis.
            for (int i = 0; i < changes.length; i++)
            {
                if (changes.GetEventType(i) == ObjectChangeKind.ChangeAssetObjectProperties)
                {
                    changes.GetChangeAssetObjectPropertiesEvent(i, out var change);
                    if (EditorUtility.InstanceIDToObject(change.instanceId) is EditorWindow) continue;
                }
                Invalidate();
                break;
            }
        }

        internal void Invalidate() { dirty = true; }

        internal bool Refresh(GameObject source, MochiyaExportProfile settings, bool vrm, bool showWarnings, double now)
        {
            if (disposed) return false;
            if (target != source || profile != settings || asVrm != vrm || warnings != showWarnings)
            {
                target = source; profile = settings; asVrm = vrm; warnings = showWarnings;
                Target = null; Report = null; dirty = true;
            }
            if (!dirty || now < nextRefresh) return false;
            dirty = false;
            nextRefresh = now + .25;
            try
            {
                Target = target != null ? detect(target) : null;
                Report = warnings && target != null ? validate(target, asVrm, profile) : null;
            }
            catch (Exception error)
            {
                Target = new MochiyaAvatarTarget { Root = target, Kind = MochiyaTargetKind.Invalid, Error = error.Message };
                Report = null;
            }
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            EditorApplication.hierarchyChanged -= Invalidate;
            EditorApplication.projectChanged -= Invalidate;
            Undo.undoRedoPerformed -= Invalidate;
            ObjectChangeEvents.changesPublished -= ObjectsChanged;
        }
    }
}

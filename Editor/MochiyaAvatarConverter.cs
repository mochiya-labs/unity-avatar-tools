using System;
using System.Collections.Generic;
using System.Linq;
using Mochiya.AvatarComposition;
using UniVRM10;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mochiya.AvatarTools.Editor
{
    public sealed class MochiyaConversionOptions
    {
        /// <summary>Omit unsupported source features by default. API callers can opt into strict diagnostics.</summary>
        public bool AllowUnsupportedFeatures = true;
        /// <summary>Legacy option. Avatar/attachment classification still requires a valid owned or parent humanoid; use ExportGlb for ordinary props.</summary>
        public bool AllowGenericRig;
        /// <summary>Avatar mode only: omit separately exported attachment roots from the duplicate.</summary>
        public GameObject[] ExcludeObjects = Array.Empty<GameObject>();
    }

    public sealed class MochiyaConversionReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Unsupported = new List<string>();
        public bool CanConvert => Errors.Count == 0;
        public bool CanExportVrm;
        public override string ToString() => string.Join("\n", Errors.Select(x => "Error: " + x)
            .Concat(Unsupported.Select(x => "Unsupported: " + x)).Concat(Warnings.Select(x => "Warning: " + x)));
    }

    public sealed class MochiyaConversionResult : IDisposable
    {
        public GameObject Root { get; internal set; }
        public MochiyaConversionReport Report { get; internal set; }
        internal List<Object> Resources;
        /// <summary>Release an owned conversion, including its generated VRM data. Source assets are never disposed.</summary>
        public void Dispose()
        {
            if (Root != null) Object.DestroyImmediate(Root);
            if (Resources != null) foreach (var resource in Resources) if (resource != null) Object.DestroyImmediate(resource);
            Root = null;
            Resources = null;
        }
    }

    public static class MochiyaAvatarConverter
    {
        internal static bool HasValidHumanoid(GameObject root)
        {
            var animator = root != null ? root.GetComponent<Animator>() : null;
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) return false;
            var required = new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head,
                HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot };
            var seen = new HashSet<Transform>();
            try
            {
                foreach (var name in required)
                {
                    var bone = animator.GetBoneTransform(name);
                    if (bone == null || !bone.IsChildOf(root.transform) || !seen.Add(bone)) return false;
                }
            }
            catch (InvalidOperationException) { return false; }
            if (root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(mesh =>
                mesh.bones.Any(bone => bone != null && !bone.IsChildOf(root.transform)))) return false;
            return true;
        }

        public static MochiyaConversionReport Validate(GameObject avatarRoot, GameObject attachment = null, MochiyaConversionOptions options = null)
        {
            options = options ?? new MochiyaConversionOptions();
            var report = new MochiyaConversionReport();
            if (EditorApplication.isPlayingOrWillChangePlaymode) report.Errors.Add("Conversion requires Edit Mode.");
            if (avatarRoot == null || EditorUtility.IsPersistent(avatarRoot)) report.Errors.Add("Choose an avatar instance in a scene.");
            if (avatarRoot == null) return report;
            var scope = attachment ?? avatarRoot;
            var detected = MochiyaAvatarWorkflow.Detect(scope);
            if (detected.Kind == MochiyaTargetKind.Invalid) report.Errors.Add(detected.Error);
            else if (attachment != null && (detected.Kind != MochiyaTargetKind.Attachment || detected.ReferenceAvatar != avatarRoot))
                report.Errors.Add("This target is not an attachment dependent on the supplied direct parent. Use automatic detection for this asset.");
            else if (attachment == null && detected.Kind != MochiyaTargetKind.Avatar)
                report.Errors.Add("This asset depends on its parent. Convert it as an attachment using automatic detection.");
            var rigRoot = attachment != null && HasValidHumanoid(attachment) ? attachment : avatarRoot;
            var animator = rigRoot.GetComponent<Animator>();
            report.CanExportVrm = HasValidHumanoid(rigRoot);
            if (!report.CanExportVrm)
            {
                report.Errors.Add("VRM conversion requires a valid Humanoid Animator with all required bones in its hierarchy. Use the lilToon GLB exporter for ordinary props.");
            }
            var excluded = options.ExcludeObjects.Where(x => x != null).Select(x => x.transform).ToArray();
            if (attachment != null && excluded.Length > 0) report.Errors.Add("Content exclusions apply to avatar conversion, not attachment conversion.");
            if (excluded.Any(x => x == avatarRoot.transform || !x.IsChildOf(avatarRoot.transform))) report.Errors.Add("Excluded attachments must be descendants of the selected avatar.");
            if (report.CanExportVrm)
                for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
                { var bone = animator.GetBoneTransform((HumanBodyBones)i); if (bone != null && excluded.Any(x => bone.IsChildOf(x))) report.Errors.Add("An exclusion contains a required humanoid bone."); }
            if (!scope.GetComponentsInChildren<Renderer>(true).Any(x => (x is SkinnedMeshRenderer || x is MeshRenderer) && !excluded.Any(t => x.transform.IsChildOf(t))))
                report.Errors.Add("The conversion scope contains no mesh renderers.");
            OptionalAvatarReaders.Inspect(scope, report, excluded);
            if (!options.AllowUnsupportedFeatures && report.Unsupported.Count > 0)
                report.Errors.Add("Resolve the unsupported features, or explicitly accept the listed omissions before conversion.");
            return report;
        }

        public static MochiyaConversionResult ConvertAvatarInScene(GameObject root, MochiyaConversionOptions options = null)
            => Convert(root, null, options);

        public static MochiyaConversionResult ConvertAttachmentInScene(GameObject referenceAvatar, GameObject attachment, MochiyaConversionOptions options = null)
        {
            if (attachment == null) throw new ArgumentNullException(nameof(attachment));
            return Convert(referenceAvatar, attachment, options);
        }

        [Obsolete("Use ConvertAttachmentInScene instead.")]
        public static MochiyaConversionResult ConvertOutfitInScene(GameObject referenceAvatar, GameObject outfit, MochiyaConversionOptions options = null)
            => ConvertAttachmentInScene(referenceAvatar, outfit, options);

        internal static MochiyaConversionResult ConvertForExport(GameObject source, GameObject attachment, MochiyaConversionOptions options)
            => Convert(source, attachment, options, false);

        private static MochiyaConversionResult Convert(GameObject source, GameObject attachment, MochiyaConversionOptions options, bool keepInScene = true)
        {
            var report = Validate(source, attachment, options);
            if (!report.CanConvert) throw new InvalidOperationException(report.ToString());
            GameObject duplicate = null;
            var allocated = new List<Object>();
            try
            {
                var scope = attachment ?? source;
                // Parent context resolves external MA references. Copy the target's own rig whenever it is sufficient.
                var rigRoot = attachment != null && HasValidHumanoid(attachment) ? attachment : source;
                var excluded = (options?.ExcludeObjects ?? Array.Empty<GameObject>()).Where(x => x != null).Select(x => x.transform).ToArray();
                var included = new HashSet<Transform>(scope.GetComponentsInChildren<Transform>(true).Where(t => !excluded.Any(x => t.IsChildOf(x))));
                Action<Transform> includePath = t => { while (t != null && t.IsChildOf(rigRoot.transform)) { included.Add(t); t = t.parent; } };
                includePath(scope.transform);
                var sourceAnimator = rigRoot.GetComponent<Animator>();
                if (report.CanExportVrm)
                    for (var i = 0; i < (int)HumanBodyBones.LastBone; i++) includePath(sourceAnimator.GetBoneTransform((HumanBodyBones)i));
                foreach (var mesh in scope.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(m => included.Contains(m.transform)))
                {
                    if (mesh.bones.Any(b => b != null && excluded.Any(x => b.IsChildOf(x)))) throw new InvalidOperationException("A retained mesh depends on bones inside an excluded attachment.");
                    foreach (var bone in mesh.bones) includePath(bone);
                    includePath(mesh.rootBone);
                }
                // Source authoring scripts are never instantiated: ExecuteAlways/NDMF callbacks cannot run on the duplicate.
                duplicate = new GameObject((attachment != null ? attachment.name : source.name) + " (Mochiya)");
                duplicate.SetActive(false);
                duplicate.transform.SetPositionAndRotation(rigRoot.transform.position, rigRoot.transform.rotation);
                duplicate.transform.localScale = rigRoot.transform.lossyScale;
                var map = new Dictionary<Object, Object> { [rigRoot] = duplicate, [rigRoot.transform] = duplicate.transform };
                foreach (var t in rigRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (t == rigRoot.transform || !included.Contains(t)) continue;
                    var go = new GameObject(t.name);
                    go.transform.SetParent((Transform)map[t.parent], false);
                    go.transform.localPosition = t.localPosition; go.transform.localRotation = t.localRotation; go.transform.localScale = t.localScale;
                    go.SetActive(t.gameObject.activeSelf);
                    map[t] = go.transform; map[t.gameObject] = go;
                }
                var copied = new List<Component>();
                GameObject rootGeometry = null;
                foreach (var component in scope.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || !map.TryGetValue(component.gameObject, out var mapped)) continue;
                    var type = component.GetType();
                    if (!(component is Renderer || component is MeshFilter || component is VRM10SpringBoneCollider ||
                          component is VRM10SpringBoneColliderGroup || component is VRM10SpringBoneJoint ||
                          type.Namespace == "UniVRM10" && type.Name.EndsWith("Constraint", StringComparison.Ordinal))) continue;
                    if (component is Renderer && !(component is MeshRenderer || component is SkinnedMeshRenderer)) continue;
                    var destination = (GameObject)mapped;
                    if (component.gameObject == rigRoot && (component is Renderer || component is MeshFilter))
                    {
                        if (rootGeometry == null) { rootGeometry = new GameObject(source.name + " Geometry"); rootGeometry.transform.SetParent(duplicate.transform, false); }
                        destination = rootGeometry;
                    }
                    var copy = destination.AddComponent(type);
                    EditorUtility.CopySerialized(component, copy);
                    copied.Add(copy); map[component] = copy;
                }
                // Remap all serialized component references, including skin bones and VRM colliders/constraints.
                foreach (var component in copied) RemapReferences(component, map, source.transform, report);
                if (sourceAnimator != null)
                {
                    var animator = duplicate.AddComponent<Animator>();
                    animator.avatar = sourceAnimator.avatar;
                    animator.applyRootMotion = false;
                }
                var asset = duplicate.AddComponent<MochiyaAvatarComposition>();
                asset.Kind = attachment != null ? AssetKind.Attachment : AssetKind.Avatar;
                asset.ArmatureKeywords = report.CanExportVrm ? new[] { sourceAnimator.GetBoneTransform(HumanBodyBones.Hips)?.parent?.name ?? "Armature" } : Array.Empty<string>();
                foreach (var pair in map.Where(x => x.Key is Transform && x.Key != rigRoot.transform))
                {
                    var original = (Transform)pair.Key; var node = (Transform)pair.Value;
                    var renderer = original.GetComponent<Renderer>();
                    asset.Nodes.Add(new AssetNodeState { Node = node, Active = original.gameObject.activeSelf && (renderer == null || renderer.enabled), Aliases = new[] { original.name } });
                }
                Vrm10Instance instance = null;
                Action finalizeVrmPaths = null;
                if (report.CanExportVrm)
                {
                    instance = duplicate.AddComponent<Vrm10Instance>();
                    instance.UpdateType = Vrm10Instance.UpdateTypes.LateUpdate;
                    var original = rigRoot.GetComponent<Vrm10Instance>();
                    instance.Vrm = original != null && original.Vrm != null ? Object.Instantiate(original.Vrm) : ScriptableObject.CreateInstance<VRM10Object>();
                    allocated.Add(instance.Vrm);
                    instance.Vrm.name = duplicate.name;
                    if (original == null || original.Vrm == null) instance.Vrm.Meta.Name = scope.name;
                    instance.Vrm.Meta = MochiyaExportProfile.CompleteMetadata(scope, instance.Vrm.Meta);
                    if (original != null && original.Vrm != null)
                        finalizeVrmPaths = CopyOwnedVrmBindings(rigRoot.transform, duplicate.transform, original.Vrm, instance.Vrm, map, allocated);
                    if (original != null)
                    {
                        foreach (var group in original.SpringBone.ColliderGroups)
                            if (group != null && map.TryGetValue(group, out var cloned)) instance.SpringBone.ColliderGroups.Add((VRM10SpringBoneColliderGroup)cloned);
                        foreach (var spring in original.SpringBone.Springs)
                        {
                            var joints = spring.Joints.Where(j => j != null && map.ContainsKey(j)).Select(j => (VRM10SpringBoneJoint)map[j]).ToList();
                            if (joints.Count == 0) continue;
                            instance.SpringBone.Springs.Add(new Vrm10InstanceSpringBone.Spring(spring.Name) {
                                Joints = joints, ColliderGroups = spring.ColliderGroups.Where(g => g != null && map.ContainsKey(g)).Select(g => (VRM10SpringBoneColliderGroup)map[g]).ToList(),
                                Center = spring.Center != null && map.TryGetValue(spring.Center, out var center) ? (Transform)center : null });
                        }
                    }
                }
                var context = new ConversionContext(source, scope, attachment != null, map, asset, instance, report, allocated, rigRoot);
                OptionalAvatarReaders.Convert(context);
                finalizeVrmPaths?.Invoke();
                if (attachment != null)
                {
                    var mappedBones = new HashSet<Transform>(asset.Joints.Select(x => x.Source));
                    foreach (var bone in scope.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(x => x.bones).Where(x => x != null).Distinct())
                        if (map.TryGetValue(bone, out var local) && mappedBones.Add((Transform)local))
                            asset.Joints.Add(new AssetJointMapping { Source = (Transform)local, Target = context.Select(bone, forceBase: true, bone: true) });
                    foreach (var original in included.Where(t => t != source.transform && !t.IsChildOf(scope.transform)))
                        if (map.TryGetValue(original, out var reference) && mappedBones.Add((Transform)reference))
                            asset.Joints.Add(new AssetJointMapping { Source = (Transform)reference, Target = context.Select(original, forceBase: true, bone: true) });
                }
                asset.ConversionReport = report.ToString();
                var resources = duplicate.AddComponent<MochiyaSceneResources>(); resources.Capture();
                duplicate.SetActive(true);
                if (keepInScene)
                {
                    Undo.RegisterCreatedObjectUndo(duplicate, "Convert Mochiya asset");
                    Selection.activeGameObject = duplicate;
                }
                else duplicate.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                return new MochiyaConversionResult { Root = duplicate, Report = report, Resources = allocated };
            }
            catch
            {
                if (duplicate != null) Object.DestroyImmediate(duplicate);
                foreach (var obj in allocated) if (obj != null) Object.DestroyImmediate(obj);
                throw;
            }
        }

        private static Action CopyOwnedVrmBindings(Transform source, Transform destination, VRM10Object original, VRM10Object copy,
            Dictionary<Object, Object> map, List<Object> allocated)
        {
            Renderer Resolve(string path)
            {
                var node = string.IsNullOrEmpty(path) ? source : source.Find(path);
                var renderer = node != null ? node.GetComponent<Renderer>() : null;
                return renderer != null && map.TryGetValue(renderer, out var owned) ? (Renderer)owned : null;
            }
            var materials = new HashSet<string>(map.Values.OfType<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name));
            var finalize = new List<Action>();
            copy.Expression = new VRM10ObjectExpression();
            foreach (var entry in original.Expression.Clips)
            {
                if (entry.Clip == null) continue;
                var bindings = entry.Clip.MorphTargetBindings.Select(b => (binding: b, renderer: Resolve(b.RelativePath))).Where(x => x.renderer != null).ToArray();
                var colors = entry.Clip.MaterialColorBindings.Where(b => materials.Contains(b.MaterialName)).ToArray();
                var uvs = entry.Clip.MaterialUVBindings.Where(b => materials.Contains(b.MaterialName)).ToArray();
                if (bindings.Length + colors.Length + uvs.Length == 0) continue;
                var clip = Object.Instantiate(entry.Clip); allocated.Add(clip);
                clip.MaterialColorBindings = colors; clip.MaterialUVBindings = uvs;
                copy.Expression.AddClip(entry.Preset, clip);
                // MA can reparent renderers. Resolve paths only after hierarchy conversion finishes.
                finalize.Add(() => clip.MorphTargetBindings = bindings.Select(x => new MorphTargetBinding(
                    AnimationUtility.CalculateTransformPath(x.renderer.transform, destination), x.binding.Index, x.binding.Weight)).ToArray());
            }
            var firstPerson = original.FirstPerson.Renderers.Select(b => (binding: b, renderer: Resolve(b.Renderer))).Where(x => x.renderer != null).ToArray();
            copy.FirstPerson = new VRM10ObjectFirstPerson();
            finalize.Add(() => copy.FirstPerson.Renderers = firstPerson.Select(x => new RendererFirstPersonFlags {
                Renderer = AnimationUtility.CalculateTransformPath(x.renderer.transform, destination), FirstPersonFlag = x.binding.FirstPersonFlag }).ToList());
            return () => { foreach (var action in finalize) action(); };
        }

        private static void RemapReferences(Component component, Dictionary<Object, Object> map, Transform source, MochiyaConversionReport report)
        {
            var serialized = new SerializedObject(component);
            var iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference || iterator.propertyPath == "m_Script") continue;
                var value = iterator.objectReferenceValue;
                if (value == null) continue;
                if (map.TryGetValue(value, out var replacement)) iterator.objectReferenceValue = replacement;
                else if (value is Component c && c.transform.IsChildOf(source) || value is GameObject go && go.transform.IsChildOf(source))
                {
                    iterator.objectReferenceValue = null;
                    report.Warnings.Add($"{component.name}: reference outside the conversion scope was omitted ({iterator.propertyPath}).");
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

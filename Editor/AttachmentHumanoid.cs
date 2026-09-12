using System;
using System.Collections.Generic;
using System.Linq;
using Mochiya.AvatarComposition;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mochiya.AvatarTools.Editor
{
    /// <summary>Complete an attachment's authored rig without retaining a parallel reference humanoid.</summary>
    internal static class AttachmentHumanoid
    {
        internal static void Complete(GameObject reference, GameObject attachment, GameObject output,
            HashSet<Transform> included, Dictionary<Object, Object> map, MochiyaConversionReport report)
        {
            var owned = attachment.GetComponentsInChildren<Transform>(true);
            var external = included.Where(t => t != reference.transform && !t.IsChildOf(attachment.transform)).ToArray();
            var context = new ConversionContext(reference, attachment, true, map, null, null, report, new List<Object>());
            var merges = attachment.GetComponentsInChildren<Component>(true).Where(c => c != null &&
                c.GetType().FullName == "nadena.dev.modular_avatar.core.ModularAvatarMergeArmature" &&
                (!(c is Behaviour b) || b.enabled)).Select(c => new {
                    Source = c.transform,
                    Target = OptionalAvatarReaders.Reference(OptionalAvatarReaders.Read(c, "mergeTarget"), context, c),
                    Prefix = OptionalAvatarReaders.String(c, "prefix"), Suffix = OptionalAvatarReaders.String(c, "suffix")
                }).Where(c => c.Target != null && external.Contains(c.Target)).ToArray();
            var pairs = new Dictionary<Transform, Transform>();
            var used = new HashSet<Transform>();
            string Strip(string name, string prefix, string suffix) => name.StartsWith(prefix, StringComparison.Ordinal) &&
                name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > prefix.Length + suffix.Length
                ? name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length) : name;
            bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            void Pair(Transform target, Transform source, string prefix, string suffix)
            {
                if (pairs.TryGetValue(target, out var previous))
                {
                    if (previous != source) throw new InvalidOperationException($"Several attachment rigs target '{target.name}'. A unique humanoid rig is required for VRM completion.");
                    return;
                }
                if (!used.Add(source)) throw new InvalidOperationException($"Attachment bone '{source.name}' has conflicting humanoid targets.");
                pairs[target] = source;
                var pool = owned.Where(t => t != source && t.IsChildOf(source) && t.GetComponent<Renderer>() == null &&
                    !merges.Any(m => m.Source != source && t.IsChildOf(m.Source))).ToArray();
                void Walk(Transform parent, Transform matchedParent)
                {
                    foreach (Transform child in parent)
                    {
                        if (!external.Contains(child)) continue;
                        var matches = pool.Where(t => !used.Contains(t) && Same(Strip(t.name, prefix, suffix), child.name)).ToArray();
                        var direct = matches.Where(t => t.parent == matchedParent).ToArray();
                        if (direct.Length > 0) matches = direct;
                        if (matches.Length > 1) throw new InvalidOperationException($"Ambiguous attachment bone '{child.name}'. Use distinct names or MA Merge Armature roots.");
                        var match = matches.SingleOrDefault();
                        if (match != null) { pairs[child] = match; used.Add(match); }
                        Walk(child, match);
                    }
                }
                Walk(target, source);
            }
            foreach (var merge in merges) Pair(merge.Target, merge.Source, merge.Prefix, merge.Suffix);
            foreach (var root in external.Where(t => !external.Contains(t.parent)))
            {
                if (pairs.ContainsKey(root)) continue;
                // With no authored root instruction, accept only a unique named rig container.
                var candidates = owned.Where(t => t != attachment.transform && !used.Contains(t) &&
                    t.GetComponent<Renderer>() == null && Same(t.name, root.name)).ToArray();
                if (candidates.Length == 0 && merges.Length == 0)
                {
                    // Imported clothing often calls its container something other than Armature.
                    // Derive that container from actual skin ownership, never from mesh names.
                    var skinBranches = attachment.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .SelectMany(m => m.bones).Where(t => t != null && t != attachment.transform && t.IsChildOf(attachment.transform))
                        .Select(t => { while (t.parent != attachment.transform) t = t.parent; return t; }).Distinct().ToArray();
                    var referenceBones = reference.GetComponent<Animator>();
                    var humanNames = Enumerable.Range(0, (int)HumanBodyBones.LastBone)
                        .Select(i => referenceBones.GetBoneTransform((HumanBodyBones)i)).Where(t => t != null).Select(t => t.name).ToArray();
                    candidates = skinBranches.Where(t => t.GetComponent<Renderer>() == null && !humanNames.Any(n => Same(n, t.name))).ToArray();
                    if (candidates.Length == 0 && skinBranches.Length == 1 && humanNames.Any(n => Same(n, skinBranches[0].name)))
                    {
                        // Some imports put Hips directly under the attachment instead of using
                        // a separate container. Its root is already the common rig container.
                        Pair(root, attachment.transform, "", "");
                        continue;
                    }
                }
                if (candidates.Length > 1) throw new InvalidOperationException($"Ambiguous attachment armature '{root.name}'. Configure its MA Merge Armature target.");
                if (candidates.Length == 1) Pair(root, candidates[0], "", "");
            }

            // Original attachment becomes the model root. Keep the avatar coordinate frame so root
            // normalization during export does not erase the attachment's authored placement.
            var wrapper = (Transform)map[attachment.transform];
            foreach (var child in wrapper.Cast<Transform>().ToArray()) child.SetParent(output.transform, true);
            map[attachment] = output; map[attachment.transform] = output.transform;
            Object.DestroyImmediate(wrapper.gameObject);

            // Map all correspondences before changing hierarchy, so missing intermediate bones can
            // receive existing descendants without replacing those descendants or their skin bindings.
            var replacements = pairs.ToDictionary(p => (Transform)map[p.Key], p => (Transform)map[p.Value]);
            foreach (var pair in pairs)
            {
                var old = (Transform)map[pair.Key]; var replacement = (Transform)map[pair.Value];
                map[pair.Key] = replacement; map[pair.Key.gameObject] = replacement.gameObject;
                foreach (var child in old.Cast<Transform>().ToArray())
                    if (!replacements.ContainsKey(child)) child.SetParent(replacement, true);
            }
            // Insert only missing reference ancestors. Existing authored intermediate transforms
            // remain intact when their mapped humanoid ancestry is already correct.
            foreach (var original in external)
            {
                if (!pairs.ContainsKey(original)) continue;
                var node = (Transform)map[original];
                var parent = original.parent == reference.transform ? output.transform : (Transform)map[original.parent];
                if (node != parent && !node.IsChildOf(parent))
                {
                    if (parent.IsChildOf(node)) throw new InvalidOperationException($"Attachment humanoid hierarchy conflicts at '{original.name}'.");
                    node.SetParent(parent, true);
                }
            }
            foreach (var old in replacements.Keys)
            {
                // A replaced descendant may still be below its replaced parent. Detach first so
                // destroying one placeholder never destroys another before its children are moved.
                old.SetParent(null, true);
            }
            foreach (var old in replacements.Keys) Object.DestroyImmediate(old.gameObject);
        }

        internal static Avatar BuildAvatar(Animator source, GameObject output, Dictionary<Object, Object> map, List<Object> allocated)
        {
            var description = source.avatar.humanDescription;
            var human = new List<HumanBone>();
            var transforms = output.GetComponentsInChildren<Transform>(true);
            var mappedHumans = Enumerable.Range(0, (int)HumanBodyBones.LastBone).Select(i => source.GetBoneTransform((HumanBodyBones)i))
                .Where(t => t != null && map.ContainsKey(t)).Select(t => (Transform)map[t]).ToHashSet();
            // UniVRM's standard humanoid preparation also requires unique transform names.
            // Give the actual mapped bones priority over same-named collider/helper objects;
            // the composition node aliases retain every original authoring name.
            UniGLTF.Utils.ForceTransformUniqueName.Process(transforms.OrderByDescending(mappedHumans.Contains).ToArray(),
                t => t.name, (t, name) => t.name = name, t => t.parent != null ? t.parent.name + "-" + t.name : null);
            for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var original = source.GetBoneTransform((HumanBodyBones)i);
                if (original == null || !map.TryGetValue(original, out var mapped)) continue;
                var bone = (Transform)mapped;
                var entry = description.human.FirstOrDefault(h => h.humanName == HumanTrait.BoneName[i]);
                entry.humanName = HumanTrait.BoneName[i]; entry.boneName = bone.name;
                human.Add(entry);
            }
            description.human = human.ToArray();
            description.skeleton = transforms.Select(t => new SkeletonBone {
                name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale
            }).ToArray();
            var avatar = AvatarBuilder.BuildHumanAvatar(output, description);
            if (avatar != null) allocated.Add(avatar);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("The completed attachment rig is not a valid Unity humanoid. Check the attachment's bone hierarchy and rest pose.");
            avatar.name = output.name + " Humanoid";
            return avatar;
        }

        internal static void ValidateMapping(Animator source, Animator output, Dictionary<Object, Object> map)
        {
            for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var original = source.GetBoneTransform((HumanBodyBones)i);
                if (original != null && map.TryGetValue(original, out var expected) && output.GetBoneTransform((HumanBodyBones)i) != expected)
                    throw new InvalidOperationException($"Unity could not map attachment humanoid bone '{original.name}' to its intended transform. Check conflicting bone names and hierarchy.");
            }
        }
    }
}

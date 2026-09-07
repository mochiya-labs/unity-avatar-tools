using System;
using System.Collections.Generic;
using System.Linq;
using Mochiya.AvatarComposition;
using UniVRM10;
using UnityEditor;
using UnityEngine;
using static Mochiya.AvatarTools.Editor.OptionalAvatarReaders;

namespace Mochiya.AvatarTools.Editor
{
    internal static class VrcAvatarReader
    {
        internal static void Convert(ConversionContext c, Component[] components)
        {
            if (c.Vrm == null)
            {
                if (components.Any(x => (x.GetType().Namespace ?? "").StartsWith("VRC.")))
                    c.Report.Warnings.Add("Generic GLB conversion does not include VRM expressions or spring physics.");
                return;
            }
            var descriptor = c.RigRoot.GetComponents<Component>().FirstOrDefault(x => x != null && x.GetType().FullName == "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");
            if (descriptor != null) Expressions(c, descriptor);
            var colliders = new Dictionary<Component, VRM10SpringBoneCollider>();
            foreach (var component in components.Where(x => x.GetType().FullName == "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider" && PhysicsActive(c, x))) Collider(c, component, colliders);
            var sources = components.Where(x => x.GetType().FullName == "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone").ToArray();
            var active = sources.Where(x => PhysicsActive(c, x)).ToArray();
            if (active.Length != sources.Length)
                c.Report.Warnings.Add("Inactive PhysBone presets were omitted. Conversion captures the current physics setup; runtime preset switching is not exported.");
            var roots = new HashSet<Transform>(active.Select(PhysBoneRoot));
            var claimed = new HashSet<Transform>(c.Vrm.SpringBone.Springs.SelectMany(s => s.Joints).Where(j => j != null).Select(j => j.transform));
            foreach (var component in active) Spring(c, component, colliders, roots, claimed);
        }

        // Ignore inactive preset hosts beneath the selected avatar, but allow
        // conversion of an avatar whose scene root itself is inactive.
        private static bool PhysicsActive(ConversionContext c, Component component)
        {
            if (component is Behaviour behaviour && !behaviour.enabled) return false;
            for (var t = component.transform; t != null && t != c.RigRoot.transform; t = t.parent)
                if (!t.gameObject.activeSelf) return false;
            return true;
        }

        private static void Expressions(ConversionContext c, Component descriptor)
        {
            var ownsDescriptor = !c.Attachment || c.RigRoot == c.Scope;
            if (ownsDescriptor)
            {
                var head = c.RigRoot.GetComponent<Animator>()?.GetBoneTransform(HumanBodyBones.Head);
                if (head != null && Read(descriptor, "ViewPosition") is Vector3 viewpoint)
                    c.Vrm.Vrm.LookAt.OffsetFromHead = head.InverseTransformPoint(c.RigRoot.transform.TransformPoint(viewpoint));
                if (c.Vrm.Vrm.FirstPerson.Renderers.Count == 0) c.Vrm.Vrm.FirstPerson.SetDefault(c.Asset.transform);
            }
            var renderer = Read(descriptor, "VisemeSkinnedMesh") as SkinnedMeshRenderer;
            var shapes = Read(descriptor, "VisemeBlendShapes") as string[];
            if (renderer != null && c.Local(renderer.transform) != null && (!c.Attachment || renderer.transform.IsChildOf(c.Scope.transform)))
            {
                var presets = new[] { ExpressionPreset.aa, ExpressionPreset.ih, ExpressionPreset.ou, ExpressionPreset.ee, ExpressionPreset.oh };
                var indices = new[] { 10, 12, 14, 11, 13 };
                if (String(descriptor, "lipSync") == "VisemeBlendShape" && shapes != null)
                    for (var i = 0; i < indices.Length; i++) if (indices[i] < shapes.Length) AddExpression(c, renderer, shapes[indices[i]], presets[i]);
                else if (!c.Attachment) c.Report.Warnings.Add("Only blendshape visemes are converted. Jaw-bone/parameter lip sync needs VRM expression authoring.");
            }
            if (!Bool(descriptor, "enableEyeLook")) return;
            var settings = Read(descriptor, "customEyeLookSettings");
            var eyelids = Read(settings, "eyelidsSkinnedMesh") as SkinnedMeshRenderer;
            var eyelidShapes = Read(settings, "eyelidsBlendshapes") as int[];
            if (eyelids != null && eyelids.sharedMesh != null && c.Local(eyelids.transform) != null && (!c.Attachment || eyelids.transform.IsChildOf(c.Scope.transform)) && eyelidShapes != null && eyelidShapes.Length > 0)
            {
                var index = eyelidShapes[0];
                if (index >= 0 && index < eyelids.sharedMesh.blendShapeCount) AddExpression(c, eyelids, eyelids.sharedMesh.GetBlendShapeName(index), ExpressionPreset.blink);
            }
            if (!ownsDescriptor) return;
            // VRChat stores per-eye rotations. VRM stores symmetric degree ranges; retain the larger excursion.
            var straight = Read(settings, "eyesLookingStraight");
            float Angle(string direction)
            {
                var rotation = Read(settings, direction); float result = 0;
                foreach (var eye in new[] { "left", "right" })
                    if (Read(straight, eye) is Quaternion a && Read(rotation, eye) is Quaternion b) result = Mathf.Max(result, Quaternion.Angle(a, b));
                return result;
            }
            var horizontal = Mathf.Max(Angle("eyesLookingLeft"), Angle("eyesLookingRight"));
            if (horizontal > 0) { c.Vrm.Vrm.LookAt.HorizontalInner = new CurveMapper(90, horizontal); c.Vrm.Vrm.LookAt.HorizontalOuter = new CurveMapper(90, horizontal); }
            var up = Angle("eyesLookingUp"); if (up > 0) c.Vrm.Vrm.LookAt.VerticalUp = new CurveMapper(90, up);
            var down = Angle("eyesLookingDown"); if (down > 0) c.Vrm.Vrm.LookAt.VerticalDown = new CurveMapper(90, down);
        }
        private static void AddExpression(ConversionContext c, SkinnedMeshRenderer renderer, string shape, ExpressionPreset preset)
        {
            if (renderer.sharedMesh == null || string.IsNullOrEmpty(shape)) return;
            var index = renderer.sharedMesh.GetBlendShapeIndex(shape);
            if (index < 0) { c.Report.Warnings.Add($"{renderer.name}: expression blendshape '{shape}' was not found."); return; }
            var clip = ScriptableObject.CreateInstance<VRM10Expression>(); c.Allocated.Add(clip); clip.name = preset.ToString();
            clip.MorphTargetBindings = new[] { new MorphTargetBinding(AnimationUtility.CalculateTransformPath(c.Local(renderer.transform), c.Asset.transform), index, 1) };
            c.Vrm.Vrm.Expression.AddClip(preset, clip);
        }
        private static Transform PhysBoneRoot(Component source)
        {
            var root = Read(source, "rootTransform") as Transform;
            // Serialized unassigned/destroyed Unity objects can be non-null CLR wrappers.
            // Use Unity's equality operator; ?? would skip the component-transform fallback.
            return root != null ? root : source.transform;
        }
        private static VRM10SpringBoneCollider Collider(ConversionContext c, Component source, Dictionary<Component, VRM10SpringBoneCollider> cache)
        {
            if (source == null || !PhysicsActive(c, source)) return null;
            if (cache.TryGetValue(source, out var existing)) return existing;
            var root = PhysBoneRoot(source);
            var localRoot = c.Local(root);
            if (localRoot == null)
            {
                // Capture a referenced base collider on an attachment-owned anchor and bind that anchor by keywords at runtime.
                if (!root.IsChildOf(c.Source.transform)) { c.Report.Warnings.Add($"{source.name}: collider outside the reference avatar was omitted."); return null; }
                var anchor = new GameObject(root.name + " Collider Anchor").transform;
                anchor.SetParent(c.Asset.transform, false);
                anchor.position = root.position; anchor.rotation = root.rotation; anchor.localScale = Vector3.Scale(root.lossyScale, new Vector3(1 / c.Asset.transform.lossyScale.x, 1 / c.Asset.transform.lossyScale.y, 1 / c.Asset.transform.lossyScale.z));
                localRoot = anchor;
                if (c.Attachment) c.Add(new AssetComponent { Kind = ComponentKind.BoneProxy, Source = anchor, Target = c.Select(root, forceBase: true, bone: true), Origin = ComponentOrigin.ColliderAnchor });
            }
            var shape = String(source, "shapeType");
            if (shape != "Sphere" && shape != "Capsule") { c.Report.Warnings.Add($"{source.name}: {shape} collider has no core VRM sphere/capsule representation and was omitted."); return null; }
            var collider = localRoot.gameObject.AddComponent<VRM10SpringBoneCollider>();
            collider.ColliderType = shape == "Capsule" ? VRM10SpringBoneColliderTypes.Capsule : VRM10SpringBoneColliderTypes.Sphere;
            collider.Radius = Mathf.Max(0, Number(source, "radius"));
            var position = Read(source, "position") is Vector3 p ? p : Vector3.zero;
            var rotation = Read(source, "rotation") is Quaternion q ? q : Quaternion.identity;
            var axis = rotation * Vector3.up * Mathf.Max(0, Number(source, "height") / 2 - collider.Radius);
            collider.Offset = shape == "Capsule" ? position - axis : position; collider.Tail = position + axis;
            if (Bool(source, "insideBounds")) c.Report.Warnings.Add($"{source.name}: inside-bounds collision is approximated as an outside collider; inspect the result.");
            cache[source] = collider; return collider;
        }
        private static void Spring(ConversionContext c, Component source, Dictionary<Component, VRM10SpringBoneCollider> cache,
            HashSet<Transform> roots, HashSet<Transform> claimed)
        {
            var root = PhysBoneRoot(source);
            if (c.Local(root) == null) { c.Report.Warnings.Add($"{source.name}: spring root is outside the conversion scope."); return; }
            if (claimed.Contains(c.Local(root))) { c.Report.Warnings.Add($"{source.name}: overlapping spring root '{root.name}' already has a physics owner; the additional preset was omitted."); return; }
            var ignored = new HashSet<Transform>(List(source, "ignoreTransforms").OfType<Transform>());
            // VRChat defaults to excluding nested PhysBone roots. Even when that
            // option is disabled, VRM cannot have two solvers writing one joint.
            foreach (var other in roots.Where(t => t != root && t.IsChildOf(root))) ignored.Add(other);
            if (Read(source, "ignoreOtherPhysBones") is bool ignoreOthers && !ignoreOthers && roots.Any(t => t != root && t.IsChildOf(root)))
                c.Report.Warnings.Add($"{source.name}: nested PhysBone ownership was separated despite Ignore Other Phys Bones being disabled; overlapping simulation is not supported by VRM.");
            var group = c.Local(root).gameObject.AddComponent<VRM10SpringBoneColliderGroup>(); group.Name = source.name;
            foreach (var collider in List(source, "colliders").OfType<Component>())
            { var converted = Collider(c, collider, cache); if (converted != null) group.Colliders.Add(converted); }
            if (group.Colliders.Count > 0) c.Vrm.SpringBone.ColliderGroups.Add(group);
            var paths = new List<List<Transform>>();
            var depths = new Dictionary<Transform, int>();
            var mode = String(source, "multiChildType");
            if (mode == "Average") c.Report.Warnings.Add($"{source.name}: Multi-Child Average is approximated by First; VRM has no averaged branch solver.");
            if (Number(source, "gravityFalloff") != 0 && Number(source, "gravity") != 0)
                c.Report.Warnings.Add($"{source.name}: Gravity Falloff is baked into the rest-pose gravity strength; VRM does not reproduce its angle-dependent response.");
            void Visit(Transform t, List<Transform> path)
            {
                if (ignored.Contains(t) || c.Local(t) == null) return;
                if (claimed.Contains(c.Local(t))) { c.Report.Warnings.Add($"{source.name}: overlapping joint '{t.name}' already has a physics owner and was omitted."); if (path.Count > 0) paths.Add(path); return; }
                depths[t] = t == root ? 0 : depths[t.parent] + 1;
                var next = new List<Transform>(path) { t };
                var children = t.Cast<Transform>().Where(x => !ignored.Contains(x) && c.Local(x) != null).ToArray();
                if (children.Length == 0) paths.Add(next);
                else if (children.Length == 1) Visit(children[0], next);
                else if (mode == "Ignore")
                {
                    // A branch can be the preceding chain's terminal point,
                    // but must not itself rotate toward just one of its children.
                    if (next.Count > 1) paths.Add(next);
                    foreach (var child in children) Visit(child, new List<Transform>());
                }
                else
                {
                    Visit(children[0], next);
                    foreach (var child in children.Skip(1)) Visit(child, new List<Transform>());
                }
            }
            Visit(root, new List<Transform>());
            var maxDepth = depths.Count > 0 ? depths.Values.Max() : 0;
            foreach (var path in paths)
            {
                var spring = new Vrm10InstanceSpringBone.Spring(source.name + "/" + paths.IndexOf(path));
                if (group.Colliders.Count > 0) spring.ColliderGroups.Add(group);
                for (var i = 0; i < path.Count; i++)
                {
                    var local = c.Local(path[i]);
                    var joint = local.GetComponent<VRM10SpringBoneJoint>() ?? local.gameObject.AddComponent<VRM10SpringBoneJoint>();
                    var ratio = maxDepth > 0 ? (float)depths[path[i]] / maxDepth : 0;
                    float Value(string name, float fallback = 0)
                    {
                        var curve = Read(source, name + "Curve") as AnimationCurve;
                        return Number(source, name, fallback) * (curve != null && curve.length > 0 ? curve.Evaluate(ratio) : 1);
                    }
                    joint.m_stiffnessForce = Mathf.Max(0, Value("pull", 0.2f) * 4 + Value("stiffness"));
                    joint.m_dragForce = Mathf.Clamp01(1 - Value("spring", 0.5f));
                    var gravity = Value("gravity");
                    joint.m_gravityPower = Mathf.Abs(gravity) * (1 - Mathf.Clamp01(Value("gravityFalloff")));
                    joint.m_gravityDir = gravity < 0 ? Vector3.up : Vector3.down;
                    joint.m_jointRadius = Mathf.Max(0, Value("radius"));
                    spring.Joints.Add(joint);
                }
                var endpoint = Read(source, "endpointPosition") is Vector3 end ? end : Vector3.zero;
                // A branch used as a terminal point must remain fixed in Ignore
                // mode. Only actual leaves get the source's virtual endpoint.
                var leaf = path.Last();
                var hasChildren = leaf.Cast<Transform>().Any(x => !ignored.Contains(x) && c.Local(x) != null);
                if (endpoint.sqrMagnitude > 0 && !hasChildren)
                {
                    var endNode = new GameObject(leaf.name + " End").transform; endNode.SetParent(c.Local(leaf), false); endNode.localPosition = endpoint;
                    spring.Joints.Add(endNode.gameObject.AddComponent<VRM10SpringBoneJoint>());
                }
                if (spring.Joints.Count > 1)
                {
                    foreach (var joint in spring.Joints) claimed.Add(joint.transform);
                    c.Vrm.SpringBone.Springs.Add(spring);
                }
                else c.Report.Warnings.Add($"{source.name}: '{leaf.name}' has no spring segment; add an Endpoint Position or child bone to simulate it.");
            }
        }
    }
}

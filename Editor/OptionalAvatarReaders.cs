using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mochiya.AvatarComposition;
using UniVRM10;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mochiya.AvatarTools.Editor
{
    internal sealed class ConversionContext
    {
        public readonly GameObject Source, Scope, RigRoot;
        public readonly bool Attachment;
        public readonly Dictionary<Object, Object> Map;
        public readonly MochiyaAvatarComposition Asset;
        public readonly Vrm10Instance Vrm;
        public readonly MochiyaConversionReport Report;
        public readonly List<Object> Allocated;
        public ConversionContext(GameObject source, GameObject scope, bool attachment, Dictionary<Object, Object> map,
            MochiyaAvatarComposition asset, Vrm10Instance vrm, MochiyaConversionReport report, List<Object> allocated, GameObject rigRoot = null)
        { Source = source; Scope = scope; RigRoot = rigRoot != null ? rigRoot : source; Attachment = attachment; Map = map; Asset = asset; Vrm = vrm; Report = report; Allocated = allocated; }
        public Transform Local(Transform t) => t != null && Map.TryGetValue(t, out var local) ? (Transform)local : null;
        public AssetSelector Select(Transform target, string shape = null, bool forceBase = false, bool bone = false)
        {
            if (target == null) throw new InvalidOperationException("An authoring reference could not be resolved. Repair it before conversion.");
            var external = forceBase || Attachment && !target.IsChildOf(Scope.transform);
            var result = new AssetSelector { Base = external, Node = external ? null : Local(target),
                BoneKeywords = bone ? new[] { target.name } : Array.Empty<string>(),
                NodeKeywords = !bone && shape == null ? new[] { target.name } : Array.Empty<string>(),
                MeshKeywords = !bone ? new[] { target.name } : Array.Empty<string>(),
                ParentKeywords = target.parent != null ? new[] { target.parent.name } : Array.Empty<string>() };
            if (!external && !bone && target == RigRoot.transform && target.TryGetComponent<Renderer>(out var sourceRenderer) && Map.TryGetValue(sourceRenderer, out var copyRenderer))
                result.Node = ((Renderer)copyRenderer).transform;
            if (shape != null)
            {
                result.BlendshapeKeywords = new[] { shape };
                if (!external)
                {
                    var renderer = target.GetComponent<SkinnedMeshRenderer>();
                    result.MorphIndex = renderer != null && renderer.sharedMesh != null ? renderer.sharedMesh.GetBlendShapeIndex(shape) : -1;
                    if (result.MorphIndex < 0) throw new InvalidOperationException($"Local blendshape '{shape}' is missing on '{target.name}'.");
                }
            }
            if (!external && result.Node == null) throw new InvalidOperationException("A local target is outside the converted hierarchy.");
            return result;
        }
        public void Add(AssetAction action) { action.UseCondition = action.Condition != null; action.SourceOrder = Asset.Actions.Count; action.Id = "action-" + action.SourceOrder; Asset.Actions.Add(action); }
    }

    /// <summary>Optional package adapters inspect serialized public data by full type name. No optional assembly references leak into the core.</summary>
    internal static class OptionalAvatarReaders
    {
        private const string MaNamespace = "nadena.dev.modular_avatar.core";
        private static readonly HashSet<string> SupportedMa = new HashSet<string> {
            "ModularAvatarMergeArmature", "ModularAvatarBoneProxy", "ModularAvatarBlendshapeSync", "ModularAvatarShapeChanger",
            "ModularAvatarObjectToggle", "ModularAvatarMaterialSetter", "ModularAvatarMenuItem", "ModularAvatarParameters",
            "ModularAvatarMenuInstaller", "ModularAvatarMenuGroup", "ModularAvatarMenuInstallTarget" };
        internal static object Read(object obj, string name)
        {
            if (obj == null) return null;
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                var field = type.GetField(name, flags); if (field != null) return field.GetValue(obj);
                var property = type.GetProperty(name, flags); if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(obj);
            }
            return null;
        }
        internal static float Number(object obj, string name, float fallback = 0) => Read(obj, name) is IConvertible number ? number.ToSingle(null) : fallback;
        internal static bool Bool(object obj, string name) => Read(obj, name) is bool value && value;
        internal static string String(object obj, string name) => Read(obj, name)?.ToString() ?? "";
        internal static IEnumerable<object> List(object obj, string name) => (Read(obj, name) as IEnumerable)?.Cast<object>() ?? Enumerable.Empty<object>();
        private static object MenuControl(Component component) => Read(component, "Control");
        private static string MenuMode(object control) => (Read(control, "type") ?? Read(control, "Type"))?.ToString() ?? "";
        private static string MenuParameter(object control) => Read(control, "parameter") is object parameter ? String(parameter, "name") : String(control, "Parameter");
        internal static Transform Reference(object reference, ConversionContext c, Component owner = null) =>
            MochiyaParentDependency.Resolve(reference, owner != null ? MochiyaParentDependency.ReferenceRoot(owner, c.Source) : c.Source.transform);
        internal static void Inspect(GameObject scope, MochiyaConversionReport report, Transform[] excluded)
        {
            foreach (var component in scope.GetComponentsInChildren<Component>(true))
            {
                if (component == null) { report.Errors.Add("The hierarchy contains a missing script. Remove or restore it first."); continue; }
                if (excluded.Any(x => component.transform.IsChildOf(x))) continue;
                if (component is Behaviour b && !b.enabled) continue;
                var type = component.GetType();
                if (type.Namespace == MaNamespace)
                {
                    if (!SupportedMa.Contains(type.Name)) report.Unsupported.Add($"{component.name}: {type.Name} has no portable conversion adapter.");
                    if (type.Name == "ModularAvatarShapeChanger" && List(component, "Shapes").Any(s => String(s, "ChangeType") == "Delete"))
                        report.Unsupported.Add($"{component.name}: Shape Changer Delete requires geometry processing; only Set is exported.");
                    if (type.Name == "ModularAvatarBoneProxy" && (String(component, "attachmentMode") == "AsChildKeepRotation" || String(component, "attachmentMode") == "AsChildKeepPosition" || Bool(component, "matchScale")))
                        report.Unsupported.Add($"{component.name}: Bone Proxy partial-pose/scale matching requires a dedicated adapter; use Keep World Pose or At Root.");
                    if (type.Name == "ModularAvatarMenuItem")
                    {
                        var control = MenuControl(component); var mode = MenuMode(control);
                        if (mode != "Toggle" && mode != "Button" && mode != "SubMenu" && mode != "") report.Unsupported.Add($"{component.name}: {mode} menu control is not a binary object control.");
                        if (!string.IsNullOrEmpty(MenuParameter(control))) report.Unsupported.Add($"{component.name}: shared/named menu parameters require parameter-group conversion; only independent automatic object controls are supported.");
                        if (mode == "Button") report.Warnings.Add($"{component.name}: the menu button is exposed as a persistent binary control; applications may supply momentary input.");
                    }
                    if (type.Name == "ModularAvatarParameters") report.Warnings.Add($"{component.name}: VRChat parameter networking, saving and renaming are not exported; portable object controls are scoped to each asset instance.");
                    if (type.Name == "ModularAvatarMenuInstaller" && Read(component, "menuToAppend") != null) report.Unsupported.Add($"{component.name}: an external expression menu is not converted into portable object controls.");
                }
                else if (type.FullName == "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor")
                {
                    if (List(component, "baseAnimationLayers").Concat(List(component, "specialAnimationLayers")).Any(l => Read(l, "animatorController") != null && !Bool(l, "isDefault")))
                        report.Unsupported.Add($"{component.name}: custom VRChat Animator state machines are not executed by VRM. Their clips must be authored as VRM expressions or Mochiya actions.");
                    report.Warnings.Add("VRChat visemes, eyelid blendshapes and eye ranges are translated to VRM expressions/look-at where configured.");
                }
                else if (type.FullName == "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone")
                    report.Warnings.Add($"{component.name}: PhysBone motion is approximated with VRM springs. Grabbing, stretching, limits and parameter output have no equivalent in this profile.");
                else if ((type.Namespace ?? "").StartsWith("VRC.", StringComparison.Ordinal) && type.Name != "VRCPhysBoneCollider" && type.Name != "PipelineManager")
                    report.Unsupported.Add($"{component.name}: {type.Name} has no portable adapter.");
            }
        }

        internal static void Convert(ConversionContext c)
        {
            var components = c.Scope.GetComponentsInChildren<Component>(true).Where(x => x != null && c.Map.ContainsKey(x.gameObject) && (!(x is Behaviour b) || b.enabled)).ToArray();
            foreach (var component in components.Where(x => x.GetType().Namespace == MaNamespace && x.GetType().Name == "ModularAvatarMenuItem")) ConvertMenu(component, c);
            foreach (var component in components.Where(x => x.GetType().Namespace == MaNamespace)) ConvertMa(component, c);
            VrcAvatarReader.Convert(c, components);
        }

        private static AssetCondition Condition(Component component, ConversionContext c)
        {
            if (component.transform == c.Source.transform) return null;
            return new AssetCondition { Node = c.Local(component.transform), Inverse = Bool(component, "Inverted") };
        }
        private static void ConvertMenu(Component component, ConversionContext c)
        {
            var control = MenuControl(component);
            if (control == null || !(MenuMode(control) == "Toggle" || MenuMode(control) == "Button") || !string.IsNullOrEmpty(MenuParameter(control))) return;
            // MA Menu Item drives its own GameObject active state; expose an instance-scoped portable control.
            var id = "menu-" + c.Asset.Controls.Count;
            c.Asset.Controls.Add(new AssetControl { Id = id, Label = string.IsNullOrEmpty(String(component, "label")) ? component.name : String(component, "label"), DefaultValue = Bool(component, "isDefault") ? 1 : 0 });
            c.Add(new AssetAction { Kind = ActionKind.NodeActive, Target = c.Select(component.transform), Active = true, Condition = new AssetCondition { Control = id, Value = 1 } });
            c.Add(new AssetAction { Kind = ActionKind.NodeActive, Target = c.Select(component.transform), Active = false, Condition = new AssetCondition { Control = id, Value = 0 } });
        }
        private static void ConvertMa(Component component, ConversionContext c)
        {
            switch (component.GetType().Name)
            {
                case "ModularAvatarBlendshapeSync":
                    foreach (var binding in List(component, "Bindings"))
                    {
                        var driver = Reference(Read(binding, "ReferenceMesh"), c, component);
                        var shape = String(binding, "Blendshape"); var local = String(binding, "LocalBlendshape");
                        var curve = Read(binding, "RemapCurve") as AnimationCurve;
                        var normalized = Bool(binding, "RemapCurveIsValid") && curve != null && curve.length >= 2
                            ? new AnimationCurve(curve.keys.Select(k => new Keyframe(k.time / 100, k.value / 100)).ToArray()) : AnimationCurve.Linear(0, 0, 1, 1);
                        c.Add(new AssetAction { Kind = ActionKind.MorphSync, Driver = c.Select(driver, shape), Target = c.Select(component.transform, string.IsNullOrEmpty(local) ? shape : local), Curve = normalized });
                    }
                    break;
                case "ModularAvatarShapeChanger":
                    foreach (var shape in List(component, "Shapes"))
                    {
                        if (String(shape, "ChangeType") != "Set") continue;
                        c.Add(new AssetAction { Kind = ActionKind.MorphOverride, Target = c.Select(Reference(Read(shape, "Object"), c, component), String(shape, "ShapeName")), Value = Number(shape, "Value") / 100, Condition = Condition(component, c) });
                    }
                    break;
                case "ModularAvatarObjectToggle":
                    foreach (var item in List(component, "Objects")) c.Add(new AssetAction { Kind = ActionKind.NodeActive, Target = c.Select(Reference(Read(item, "Object"), c, component)), Active = Bool(item, "Active"), Condition = Condition(component, c) });
                    break;
                case "ModularAvatarMaterialSetter":
                    foreach (var item in List(component, "Objects")) c.Add(new AssetAction { Kind = ActionKind.MaterialSwap, Target = c.Select(Reference(Read(item, "Object"), c, component)), Material = Read(item, "Material") as Material, MaterialSlot = (int)Number(item, "MaterialIndex"), Condition = Condition(component, c) });
                    break;
                case "ModularAvatarMergeArmature":
                    var target = Reference(Read(component, "mergeTarget"), c, component);
                    if (target == null) throw new InvalidOperationException($"{component.name}: MA Merge Armature target is missing.");
                    if (!c.Attachment || target.IsChildOf(c.Scope.transform))
                    { c.Report.Warnings.Add($"{component.name}: MA Merge Armature was not applied. The armatures remain separate; use Modular Avatar to merge them in Unity."); break; }
                    var prefix = String(component, "prefix"); var suffix = String(component, "suffix");
                    foreach (var bone in component.GetComponentsInChildren<Transform>(true))
                    {
                        var pointer = target;
                        var path = AnimationUtility.CalculateTransformPath(bone, component.transform);
                        if (path.Length > 0) foreach (var segment in path.Split('/'))
                        {
                            if (!segment.StartsWith(prefix, StringComparison.Ordinal) || !segment.EndsWith(suffix, StringComparison.Ordinal) || segment.Length < prefix.Length + suffix.Length) { pointer = null; break; }
                            pointer = pointer?.Find(segment.Substring(prefix.Length, segment.Length - prefix.Length - suffix.Length));
                        }
                        if (pointer != null) AttachBone(c, bone, pointer, false, false);
                    }
                    break;
                case "ModularAvatarBoneProxy":
                    var mode = String(component, "attachmentMode");
                    if (mode == "AsChildKeepRotation" || mode == "AsChildKeepPosition" || Bool(component, "matchScale")) break;
                    var proxyTarget = MochiyaParentDependency.ProxyTarget(component, MochiyaParentDependency.ReferenceRoot(component, c.Source));
                    if (proxyTarget == null) throw new InvalidOperationException($"{component.name}: Bone Proxy target is missing.");
                    AttachBone(c, component.transform, proxyTarget, true, mode == "AsChildAtRoot" || mode == "Unset");
                    break;
            }
        }
        private static void AttachBone(ConversionContext c, Transform source, Transform target, bool attachment, bool snap)
        {
            var local = c.Local(source); if (local == null) return;
            if (c.Attachment && !target.IsChildOf(c.Scope.transform))
            {
                c.Asset.Joints.RemoveAll(x => x.Source == local);
                c.Asset.Joints.Add(new AssetJointMapping { Source = local, Target = c.Select(target, forceBase: true, bone: true), Attachment = attachment, Snap = snap });
            }
            else
            {
                c.Report.Warnings.Add($"{source.name}: local MA attachment was not applied. Hierarchy and pose are preserved; use Modular Avatar for Unity attachment.");
            }
        }
    }
}

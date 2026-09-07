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
                BaseRoot = external && target == Source.transform,
                Path = external ? AnimationUtility.CalculateTransformPath(target, Source.transform).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries) : null,
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
        public void Add(AssetComponent component) { component.UseCondition = component.Condition != null; component.Id = "component-" + Asset.Components.Count; Asset.Components.Add(component); }
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
                        report.Warnings.Add($"{component.name}: Shape Changer Delete is preserved; the web runtime uses blendshape weight zero instead of geometry deletion.");
                    if (type.Name == "ModularAvatarBoneProxy" && (String(component, "attachmentMode") == "AsChildKeepRotation" || String(component, "attachmentMode") == "AsChildKeepPosition" || Bool(component, "matchScale")))
                        report.Warnings.Add($"{component.name}: Bone Proxy partial-pose/scale settings are preserved; the web runtime falls back to Keep World Pose.");
                    if (type.Name == "ModularAvatarMenuItem")
                    {
                        var control = MenuControl(component); var mode = MenuMode(control);
                        if (mode != "Toggle" && mode != "Button" && mode != "SubMenu" && mode != "") report.Unsupported.Add($"{component.name}: {mode} menu control is not a binary object control.");
                        if (!string.IsNullOrEmpty(MenuParameter(control))) report.Warnings.Add($"{component.name}: menu parameter metadata is preserved; arbitrary Animator effects are not executed on the web.");
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
            return new AssetCondition { Node = c.Local(component.transform), Inverse = Bool(component, "Inverted") };
        }
        private static AssetComponent Record(Component component, ConversionContext c, ComponentKind kind, bool conditional = true)
        {
            var record = new AssetComponent { Kind = kind, Source = c.Local(component.transform), Origin = ComponentOrigin.ModularAvatar, Condition = conditional ? Condition(component, c) : null };
            c.Add(record); return record;
        }
        private static void ConvertMenu(Component component, ConversionContext c)
        {
            var control = MenuControl(component);
            if (control == null || !(MenuMode(control) == "Toggle" || MenuMode(control) == "Button")) return;
            var record = Record(component, c, ComponentKind.MenuItem, false);
            record.ControlType = MenuMode(control) == "Button" ? MenuControlType.Button : MenuControlType.Toggle;
            record.Label = string.IsNullOrEmpty(String(component, "label")) ? component.name : String(component, "label");
            record.Parameter = MenuParameter(control);
            record.Automatic = string.IsNullOrEmpty(record.Parameter);
            record.Value = record.Automatic ? 1 : Number(control, "value", 1);
            record.DefaultValue = record.ControlType == MenuControlType.Button ? 0 : Bool(component, "isDefault") ? record.Value : 0;
        }
        private static void ConvertMa(Component component, ConversionContext c)
        {
            switch (component.GetType().Name)
            {
                case "ModularAvatarBlendshapeSync":
                    var sync = Record(component, c, ComponentKind.BlendshapeSync, false);
                    foreach (var binding in List(component, "Bindings"))
                    {
                        var driver = Reference(Read(binding, "ReferenceMesh"), c, component);
                        var shape = String(binding, "Blendshape"); var local = String(binding, "LocalBlendshape");
                        var curve = Read(binding, "RemapCurve") as AnimationCurve;
                        var normalized = Bool(binding, "RemapCurveIsValid") && curve != null && curve.length >= 2
                            ? new AnimationCurve(curve.keys.Select(k => new Keyframe(k.time / 100, k.value / 100, k.inTangent, k.outTangent)).ToArray()) : AnimationCurve.Linear(0, 0, 1, 1);
                        // MA's remapper uses key coordinates with linear interpolation/extrapolation;
                        // AnimationCurve tangents and wrap modes do not affect its mapping.
                        sync.Entries.Add(new AssetEntry { Driver = c.Select(driver, shape), Target = c.Select(component.transform, string.IsNullOrEmpty(local) ? shape : local), Curve = normalized });
                    }
                    break;
                case "ModularAvatarShapeChanger":
                    var changer = Record(component, c, ComponentKind.ShapeChanger);
                    foreach (var shape in List(component, "Shapes"))
                        changer.Entries.Add(new AssetEntry { ChangeType = String(shape, "ChangeType") == "Delete" ? ShapeChangeType.Delete : ShapeChangeType.Set, Target = c.Select(Reference(Read(shape, "Object"), c, component), String(shape, "ShapeName")), Value = Number(shape, "Value") / 100 });
                    break;
                case "ModularAvatarObjectToggle":
                    var toggle = Record(component, c, ComponentKind.ObjectToggle);
                    foreach (var item in List(component, "Objects")) toggle.Entries.Add(new AssetEntry { Target = c.Select(Reference(Read(item, "Object"), c, component)), Active = Bool(item, "Active") });
                    break;
                case "ModularAvatarMaterialSetter":
                    var setter = Record(component, c, ComponentKind.MaterialSetter);
                    foreach (var item in List(component, "Objects")) setter.Entries.Add(new AssetEntry { Target = c.Select(Reference(Read(item, "Object"), c, component)), Material = Read(item, "Material") as Material, MaterialSlot = (int)Number(item, "MaterialIndex") });
                    break;
                case "ModularAvatarMergeArmature":
                    var target = Reference(Read(component, "mergeTarget"), c, component);
                    if (target == null) throw new InvalidOperationException($"{component.name}: MA Merge Armature target is missing.");
                    var merge = Record(component, c, ComponentKind.MergeArmature, false);
                    merge.Target = c.Select(target, bone: true); merge.Prefix = String(component, "prefix"); merge.Suffix = String(component, "suffix"); merge.MangleNames = Bool(component, "mangleNames");
                    var mode = String(component, "LockMode");
                    merge.LockMode = mode == "NotLocked" ? PositionLockMode.NotLocked : mode == "BidirectionalExact" || mode == "Legacy" && Bool(component, "legacyLocked") ? PositionLockMode.Bidirectional : PositionLockMode.Unidirectional;
                    break;
                case "ModularAvatarBoneProxy":
                    var proxyTarget = MochiyaParentDependency.ProxyTarget(component, MochiyaParentDependency.ReferenceRoot(component, c.Source));
                    if (proxyTarget == null) throw new InvalidOperationException($"{component.name}: Bone Proxy target is missing.");
                    var proxy = Record(component, c, ComponentKind.BoneProxy, false);
                    proxy.Target = c.Select(proxyTarget, bone: true); proxy.MatchScale = Bool(component, "matchScale");
                    if (proxy.Target.Base && Read(component, "boneReference") is HumanBodyBones unityBone && unityBone != HumanBodyBones.LastBone)
                    {
                        var humanoid = Vrm10HumanoidBoneSpecification.ConvertFromUnityBone(unityBone).ToString();
                        proxy.Target.HumanBone = char.ToLowerInvariant(humanoid[0]) + humanoid.Substring(1);
                        proxy.Target.HumanBonePath = String(component, "subPath").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    }
                    switch (String(component, "attachmentMode")) {
                        case "AsChildAtRoot": case "Unset": proxy.AttachmentMode = ProxyAttachmentMode.AtRoot; break;
                        case "AsChildKeepPosition": proxy.AttachmentMode = ProxyAttachmentMode.KeepPosition; break;
                        case "AsChildKeepRotation": proxy.AttachmentMode = ProxyAttachmentMode.KeepRotation; break;
                        default: proxy.AttachmentMode = ProxyAttachmentMode.KeepWorldPose; break;
                    }
                    break;
            }
        }
    }
}

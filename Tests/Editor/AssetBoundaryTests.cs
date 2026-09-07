using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mochiya.AvatarComposition;
using NUnit.Framework;
using UniVRM10;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mochiya.AvatarTools.Editor.Tests
{
    public sealed class AssetBoundaryTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();
        private GameObject Avatar(string name)
        { var root = AvatarConversionTests.CreateAvatar(); root.name = name; roots.Add(root); return root; }
        private static Type Ma(string name)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("nadena.dev.modular_avatar.core." + name)).FirstOrDefault(t => t != null);
            if (type == null) Assert.Ignore("Optional Modular Avatar package is not installed.");
            return type;
        }
        private static void Field(object owner, string name, object value) => owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(owner, value);
        private static object Reference(Type type, GameObject target, string path = null)
        {
            var value = Activator.CreateInstance(type);
            Field(value, "targetObject", target); Field(value, "referencePath", path ?? ""); return value;
        }
        private static Component Shape(GameObject owner, GameObject target, string path = null)
        {
            var component = owner.AddComponent(Ma("ModularAvatarShapeChanger"));
            var list = (IList)component.GetType().GetProperty("Shapes").GetValue(component);
            var item = Activator.CreateInstance(list.GetType().GetGenericArguments()[0]);
            Field(item, "Object", Reference(item.GetType().GetField("Object").FieldType, target, path));
            Field(item, "ShapeName", "Body_Slim"); Field(item, "Value", 65f);
            Field(item, "ChangeType", Enum.Parse(item.GetType().GetField("ChangeType").FieldType, "Set"));
            list.Add(item); return component;
        }
        private static Component Merge(GameObject owner, Transform target)
        {
            var component = owner.AddComponent(Ma("ModularAvatarMergeArmature"));
            Field(component, "mergeTarget", Reference(component.GetType().GetField("mergeTarget").FieldType, target.gameObject));
            return component;
        }
        [TearDown] public void Cleanup() { foreach (var root in roots) if (root != null) Object.DestroyImmediate(root); roots.Clear(); }

        [TestCase("BaseToMerge", PositionLockMode.Unidirectional)]
        [TestCase("BidirectionalExact", PositionLockMode.Bidirectional)]
        [TestCase("NotLocked", PositionLockMode.NotLocked)]
        public void MergeExportsAuthoredRootSettingsWithoutPrecomputingBonePairs(string mode, PositionLockMode expected)
        {
            var parent = Avatar("Base"); var clothing = Avatar("Coat"); clothing.transform.SetParent(parent.transform, false);
            var source = clothing.transform.Find("Armature"); var target = parent.transform.Find("Armature");
            var ma = Merge(source.gameObject, target);
            Field(ma, "prefix", "Coat_"); Field(ma, "suffix", "_end"); Field(ma, "mangleNames", false);
            Field(ma, "LockMode", Enum.Parse(ma.GetType().GetField("LockMode").FieldType, mode));
            var before = EditorJsonUtility.ToJson(ma);
            using (var copy = MochiyaAvatarWorkflow.ConvertToVrmGameObject(clothing))
            {
                var record = copy.Root.GetComponent<MochiyaAvatarComposition>().Components.Single();
                Assert.That(record.Kind, Is.EqualTo(ComponentKind.MergeArmature));
                Assert.That(record.Source, Is.SameAs(copy.Root.transform.Find("Armature")));
                Assert.That(record.Target.Path, Is.EqualTo(new[] { "Armature" }));
                Assert.That(record.Target.Base, Is.True); Assert.That(record.Prefix, Is.EqualTo("Coat_"));
                Assert.That(record.Suffix, Is.EqualTo("_end")); Assert.That(record.MangleNames, Is.False);
                Assert.That(record.LockMode, Is.EqualTo(expected)); Assert.That(record.Entries, Is.Empty);
                Directory.CreateDirectory("MochiyaTests");
                var path = Path.GetFullPath("MochiyaTests/merge-" + mode + ".glb"); MochiyaAvatarWorkflow.Export(copy.Root, path);
                var bytes = File.ReadAllBytes(path); var json = System.Text.Encoding.UTF8.GetString(bytes, 20, BitConverter.ToInt32(bytes, 12));
                StringAssert.Contains("\"type\":\"mergeArmature\"", json); StringAssert.Contains("\"origin\":\"modularAvatar\"", json);
                StringAssert.DoesNotContain("jointMappings", json); StringAssert.DoesNotContain("\"actions\"", json);
            }
            Assert.That(EditorJsonUtility.ToJson(ma), Is.EqualTo(before));
        }

        [Test] public void ShapeChangerPreservesGroupedDeleteAndSetEntriesWithTheirSourceCondition()
        {
            var parent = Avatar("Base"); var clothing = Avatar("Coat"); clothing.transform.SetParent(parent.transform, false);
            var ma = Shape(clothing, parent.GetComponentInChildren<SkinnedMeshRenderer>().gameObject);
            ma.GetType().GetProperty("Threshold").SetValue(ma, .03f);
            var list = (IList)ma.GetType().GetProperty("Shapes").GetValue(ma);
            var deleted = Activator.CreateInstance(list[0].GetType());
            foreach (var field in deleted.GetType().GetFields()) field.SetValue(deleted, field.GetValue(list[0]));
            Field(deleted, "ChangeType", Enum.Parse(deleted.GetType().GetField("ChangeType").FieldType, "Delete")); list.Add(deleted);
            using (var copy = MochiyaAvatarWorkflow.ConvertToVrmGameObject(clothing))
            {
                var record = copy.Root.GetComponent<MochiyaAvatarComposition>().Components.Single();
                Assert.That(record.Kind, Is.EqualTo(ComponentKind.ShapeChanger)); Assert.That(record.Entries.Count, Is.EqualTo(2));
                Assert.That(record.Threshold, Is.EqualTo(.03f));
                Assert.That(record.Entries[0].ChangeType, Is.EqualTo(ShapeChangeType.Set));
                Assert.That(record.Entries[1].ChangeType, Is.EqualTo(ShapeChangeType.Delete));
                Assert.That(record.Entries[1].Value, Is.EqualTo(.65f)); Assert.That(record.Entries[0].Target.Path, Is.EqualTo(new[] { "Body" }));
                Assert.That(record.Source, Is.SameAs(copy.Root.transform)); Assert.That(record.Condition.Node, Is.SameAs(record.Source));
                Directory.CreateDirectory("MochiyaTests"); var path = Path.GetFullPath("MochiyaTests/grouped-shapes.vrm"); MochiyaAvatarWorkflow.Export(copy.Root, path);
                var bytes = File.ReadAllBytes(path); var json = System.Text.Encoding.UTF8.GetString(bytes, 20, BitConverter.ToInt32(bytes, 12));
                StringAssert.Contains("\"changeType\":\"delete\"", json); StringAssert.Contains("\"sourceNode\":", json);
                StringAssert.Contains("\"threshold\":0.03", json);
                WriteDeletionOracle(parent.GetComponentInChildren<SkinnedMeshRenderer>(), .03f, "grouped-shapes");
            }
            Assert.That(list.Count, Is.EqualTo(2));
        }

        [Serializable] private sealed class DeletionOracle { public int originalTriangles, remainingTriangles; public float threshold; }
        private static void WriteDeletionOracle(SkinnedMeshRenderer renderer, float threshold, string name)
        {
            Type Find(string type) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("nadena.dev.modular_avatar.core.editor." + type)).FirstOrDefault(t => t != null);
            var selectorType = Find("VertexFilterByShape");
            var constructor = selectorType.GetConstructors().Single(c => c.GetParameters().Length == 3 && c.GetParameters()[0].ParameterType == typeof(string));
            var modeType = constructor.GetParameters()[2].ParameterType;
            var selector = constructor.Invoke(new object[] { "Body_Slim", threshold, Enum.Parse(modeType, "AnyVertex") });
            var selectors = Array.CreateInstance(Find("IMeshSelector"), 1); selectors.SetValue(selector, 0);
            var filtered = (Mesh)Find("RemoveVerticesFromMesh").GetMethod("FilterPrimitivesOnly").Invoke(null, new object[] { renderer, renderer.sharedMesh, selectors });
            try
            {
                // MA inserts [0,0,0] for an empty submesh. Three uses a zero draw count.
                var remaining = Enumerable.Range(0, filtered.subMeshCount).Sum(sm => {
                    var indices = filtered.GetIndices(sm);
                    return indices.Length == 3 && indices.All(i => i == 0) ? 0 : indices.Length / 3;
                });
                var oracle = new DeletionOracle { originalTriangles = renderer.sharedMesh.triangles.Length / 3, remainingTriangles = remaining, threshold = threshold };
                Assert.That(oracle.remainingTriangles, Is.LessThan(oracle.originalTriangles));
                File.WriteAllText(Path.GetFullPath("MochiyaTests/" + name + ".json"), JsonUtility.ToJson(oracle));
            }
            finally { Object.DestroyImmediate(filtered); }
        }

        [TestCase(false, false, .01f)] [TestCase(false, true, .03f)]
        [TestCase(true, false, .03f)] [TestCase(true, true, .01f)]
        public void DeleteThresholdSurvivesPreparedCopyAndSparseFrozenExport(bool freeze, bool sparse, float threshold)
        {
            var avatar = Avatar("Deletion Base"); var renderer = avatar.GetComponentInChildren<SkinnedMeshRenderer>();
            var ma = Shape(avatar, renderer.gameObject); ma.GetType().GetProperty("Threshold").SetValue(ma, threshold);
            var entry = ((IList)ma.GetType().GetProperty("Shapes").GetValue(ma))[0];
            Field(entry, "ChangeType", Enum.Parse(entry.GetType().GetField("ChangeType").FieldType, "Delete"));
            var sourceMesh = renderer.sharedMesh; var sourceIndices = sourceMesh.triangles; var before = EditorJsonUtility.ToJson(ma);
            Directory.CreateDirectory("MochiyaTests"); var name = "delete-" + freeze + "-" + sparse;
            using (var copy = MochiyaAvatarWorkflow.ConvertToVrmGameObject(avatar))
            {
                var record = copy.Root.GetComponent<MochiyaAvatarComposition>().Components.Single();
                Assert.That(record.Threshold, Is.EqualTo(threshold));
                var settings = ScriptableObject.CreateInstance<VRM10ExportSettings>();
                try
                {
                    settings.FreezeMesh = freeze; settings.FreezeMeshUseCurrentBlendShapeWeight = false; settings.MorphTargetUseSparse = sparse;
                    MochiyaLilToonExporter.ExportVrm(copy.Root, Path.GetFullPath("MochiyaTests/" + name + ".vrm"), null, settings);
                    MochiyaLilToonExporter.ExportGlb(copy.Root, Path.GetFullPath("MochiyaTests/" + name + ".glb"));
                }
                finally { Object.DestroyImmediate(settings); }
                Assert.That(record.Threshold, Is.EqualTo(threshold));
            }
            WriteDeletionOracle(renderer, threshold, name);
            Assert.That(renderer.sharedMesh, Is.SameAs(sourceMesh)); Assert.That(sourceMesh.triangles, Is.EqualTo(sourceIndices));
            Assert.That(EditorJsonUtility.ToJson(ma), Is.EqualTo(before));
        }

        [Test] public void IndependentNestedHumanoidIsAvatarAndDoesNotCopyParent()
        {
            var parent = Avatar("Parent"); var target = Avatar("Independent"); target.transform.SetParent(parent.transform, false);
            var detected = MochiyaAvatarWorkflow.Detect(target);
            Assert.That(detected.Kind, Is.EqualTo(MochiyaTargetKind.Avatar)); Assert.That(detected.ReferenceAvatar, Is.SameAs(target));
            using (var copy = MochiyaAvatarWorkflow.ConvertToVrmGameObject(target))
            {
                Assert.That(copy.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.EqualTo(1));
                Assert.That(copy.Root.GetComponent<MochiyaAvatarComposition>().Components.Where(c => c.Kind == ComponentKind.MergeArmature), Is.Empty);
                Assert.That(copy.Root.transform.Find("Parent"), Is.Null);
            }
            Assert.Throws<InvalidOperationException>(() => MochiyaAvatarConverter.ConvertAttachmentInScene(parent, target));
        }

        [TestCase("Toggle", "Shared", 2f)]
        [TestCase("Button", "Shared", 2f)]
        [TestCase("Button", "", 1f)]
        public void MenuItemPreservesControlTypeParameterValueAndDefaults(string mode, string parameter, float value)
        {
            var avatar = Avatar("Avatar"); var owner = new GameObject("Option"); owner.transform.SetParent(avatar.transform, false);
            var menu = owner.AddComponent(Ma("ModularAvatarMenuItem")); var field = menu.GetType().GetField("Control");
            if (field == null) Assert.Ignore("VRChat expression menu backing is not installed.");
            var control = Activator.CreateInstance(field.FieldType);
            Field(control, "type", Enum.Parse(control.GetType().GetField("type").FieldType, mode)); Field(control, "value", value);
            var input = Activator.CreateInstance(control.GetType().GetField("parameter").FieldType); Field(input, "name", parameter); Field(control, "parameter", input);
            field.SetValue(menu, control); Field(menu, "label", "Option Label"); Field(menu, "isDefault", true);
            using (var copy = MochiyaAvatarWorkflow.ConvertToVrmGameObject(avatar))
            {
                var record = copy.Root.GetComponent<MochiyaAvatarComposition>().Components.Single();
                Assert.That(record.ControlType.ToString(), Is.EqualTo(mode)); Assert.That(record.Parameter, Is.EqualTo(parameter));
                Assert.That(record.Value, Is.EqualTo(value)); Assert.That(record.Label, Is.EqualTo("Option Label"));
                Assert.That(record.Automatic, Is.EqualTo(parameter.Length == 0)); Assert.That(record.DefaultValue, Is.EqualTo(mode == "Button" ? 0 : value));
            }
        }

        [Test] public void BlendshapeSyncPreservesGroupedReferenceDirectionAndMaLinearPoints()
        {
            var parent = Avatar("Base"); var clothing = Avatar("Coat"); clothing.transform.SetParent(parent.transform, false);
            var mesh = clothing.GetComponentInChildren<SkinnedMeshRenderer>(); var sync = mesh.gameObject.AddComponent(Ma("ModularAvatarBlendshapeSync"));
            var bindings = (IList)sync.GetType().GetField("Bindings").GetValue(sync);
            var binding = Activator.CreateInstance(bindings.GetType().GetGenericArguments()[0]);
            Field(binding, "ReferenceMesh", Reference(binding.GetType().GetField("ReferenceMesh").FieldType, parent.GetComponentInChildren<SkinnedMeshRenderer>().gameObject));
            Field(binding, "Blendshape", "Body_Slim"); Field(binding, "LocalBlendshape", "Body_Slim"); Field(binding, "RemapCurveIsValid", true);
            Field(binding, "RemapCurve", new AnimationCurve(new Keyframe(0, 10, 7, 8), new Keyframe(50, 20, 9, 10), new Keyframe(100, 80, 11, 12)));
            bindings.Add(binding);
            using (var copy = MochiyaAvatarWorkflow.ConvertToVrmGameObject(clothing))
            {
                var record = copy.Root.GetComponent<MochiyaAvatarComposition>().Components.Single(); var entry = record.Entries.Single();
                Assert.That(record.Kind, Is.EqualTo(ComponentKind.BlendshapeSync)); Assert.That(record.UseCondition, Is.False);
                Assert.That(entry.Driver.Base, Is.True); Assert.That(entry.Driver.Path, Is.EqualTo(new[] { "Body" }));
                Assert.That(entry.Target.Base, Is.False); Assert.That(entry.Target.MorphIndex, Is.EqualTo(0));
                Assert.That(entry.Curve.keys.Select(k => k.time), Is.EqualTo(new[] { 0f, .5f, 1f }));
                Assert.That(entry.Curve.keys.Select(k => k.value), Is.EqualTo(new[] { .1f, .2f, .8f }));
                Directory.CreateDirectory("MochiyaTests"); var path = Path.GetFullPath("MochiyaTests/ma-sync.glb"); MochiyaAvatarWorkflow.Export(copy.Root, path);
                var bytes = File.ReadAllBytes(path); var json = System.Text.Encoding.UTF8.GetString(bytes, 20, BitConverter.ToInt32(bytes, 12));
                StringAssert.Contains("\"interpolation\":\"linear\"", json); StringAssert.DoesNotContain("\"tangents\"", json);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void OwnHumanoidWithExternalMaUsesOwnRigAndExportsBaseAction(bool pathReference)
        {
            var parent = Avatar("Parent"); var target = Avatar("Dependent"); target.transform.SetParent(parent.transform, false);
            var sibling = new GameObject("Sibling"); sibling.transform.SetParent(parent.transform, false);
            var mesh = AvatarConversionTests.AddMesh(sibling.transform, parent.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips), "Sibling Body", Color.red, 1);
            var owner = new GameObject("Fit"); owner.transform.SetParent(target.transform, false);
            Shape(owner, pathReference ? null : mesh.gameObject, pathReference ? "Sibling/Sibling Body" : null);
            var snapshot = parent.GetComponentsInChildren<Component>(true).ToDictionary(c => c, EditorJsonUtility.ToJson);
            var detected = MochiyaAvatarWorkflow.Detect(target);
            Assert.That(detected.Kind, Is.EqualTo(MochiyaTargetKind.Attachment)); Assert.That(detected.UsesParentRig, Is.False);
            Assert.That(MochiyaAvatarWorkflow.Detect(parent).Kind, Is.EqualTo(MochiyaTargetKind.Avatar));
            Assert.Throws<InvalidOperationException>(() => MochiyaAvatarConverter.ConvertAvatarInScene(target));
            using (var copy = MochiyaAvatarWorkflow.ConvertToVrmGameObject(target))
            {
                Assert.That(copy.Root.GetComponent<Animator>().avatar, Is.SameAs(target.GetComponent<Animator>().avatar));
                Assert.That(copy.Root.transform.Find("Armature"), Is.Not.Null);
                Assert.That(copy.Root.transform.Find("Dependent"), Is.Null, "No parent scaffold is needed for an owned humanoid.");
                Assert.That(copy.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.EqualTo(1));
                var action = copy.Root.GetComponent<MochiyaAvatarComposition>().Components.Single().Entries.Single();
                Assert.That(action.Target.Base, Is.True); Assert.That(action.Target.MeshKeywords, Does.Contain("Sibling Body"));
                Directory.CreateDirectory("MochiyaTests");
                foreach (var extension in new[] { "vrm", "glb" })
                {
                    var path = Path.GetFullPath("MochiyaTests/owned-attachment." + extension);
                    MochiyaAvatarWorkflow.Export(copy.Root, path);
                    var bytes = File.ReadAllBytes(path);
                    var json = System.Text.Encoding.UTF8.GetString(bytes, 20, BitConverter.ToInt32(bytes, 12));
                    StringAssert.Contains("\"assetKind\":\"attachment\"", json); StringAssert.Contains("\"asset\":\"base\"", json);
                    StringAssert.DoesNotContain("\"name\":\"Sibling Body\"", json);
                }
            }
            Assert.That(snapshot.All(p => EditorJsonUtility.ToJson(p.Key) == p.Value), Is.True);
        }

        [Test] public void InternalMaReferenceDoesNotCreateParentDependency()
        {
            var parent = Avatar("Parent"); var target = Avatar("Independent"); target.transform.SetParent(parent.transform, false);
            Shape(target, target.GetComponentInChildren<SkinnedMeshRenderer>().gameObject);
            Assert.That(MochiyaAvatarWorkflow.Detect(target).Kind, Is.EqualTo(MochiyaTargetKind.Avatar));
            using (var copy = MochiyaAvatarWorkflow.ConvertToVrmGameObject(target))
                Assert.That(copy.Root.GetComponent<MochiyaAvatarComposition>().Components.Single().Entries.Single().Target.Base, Is.False);
        }

        [Test] public void InvalidOrDependentParentCannotSupplyAnAttachmentReference()
        {
            var parent = Avatar("Parent"); var target = Avatar("Dependent"); target.transform.SetParent(parent.transform, false);
            Shape(target, parent.GetComponentInChildren<SkinnedMeshRenderer>().gameObject);
            Object.DestroyImmediate(parent.GetComponent<Animator>());
            Assert.That(MochiyaAvatarWorkflow.Detect(target).Kind, Is.EqualTo(MochiyaTargetKind.Invalid));
            Assert.That(MochiyaAvatarWorkflow.Validate(target, false).CanConvert, Is.False);
        }

        [Test] public void DescriptorWithoutHumanoidDoesNotDeclareAnAvatar()
        {
            var root = Avatar("Malformed"); Object.DestroyImmediate(root.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head).gameObject);
            root.AddComponent<Vrm10Instance>().UpdateType = Vrm10Instance.UpdateTypes.None;
            Assert.That(MochiyaAvatarWorkflow.Detect(root).Kind, Is.EqualTo(MochiyaTargetKind.Invalid));
        }

        [Test] public void CompleteAvatarPreservesEveryUnmergedArmatureAndSkinBinding()
        {
            var parent = Avatar("Parent"); var clothing = Avatar("Clothing"); clothing.transform.SetParent(parent.transform, false);
            var sourceHips = clothing.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
            Object.DestroyImmediate(clothing.GetComponent<Animator>());
            Merge(clothing.transform.Find("Armature").gameObject, parent.transform.Find("Armature"));
            var before = parent.GetComponentsInChildren<Transform>(true).ToDictionary(t => AnimationUtility.CalculateTransformPath(t, parent.transform),
                t => (t.localPosition, t.localRotation, t.localScale));
            using (var copy = MochiyaAvatarWorkflow.ConvertToVrmGameObject(parent))
            {
                foreach (var pair in before.Where(p => p.Key.Length > 0))
                {
                    var node = copy.Root.transform.Find(pair.Key); Assert.That(node, Is.Not.Null, pair.Key);
                    Assert.That((node.localPosition, node.localRotation, node.localScale), Is.EqualTo(pair.Value), pair.Key);
                }
                var mesh = copy.Root.transform.Find("Clothing/Body").GetComponent<SkinnedMeshRenderer>();
                Assert.That(mesh.bones[0], Is.SameAs(copy.Root.transform.Find("Clothing/Armature/Hips")));
                Assert.That(copy.Root.GetComponent<MochiyaAvatarComposition>().Components.Single(c => c.Kind == ComponentKind.MergeArmature).Target.Base, Is.False);
            }
            Assert.That(sourceHips.parent, Is.SameAs(clothing.transform.Find("Armature")));
            using (var attachment = MochiyaAvatarWorkflow.ConvertToVrmGameObject(clothing))
            {
                Assert.That(attachment.Root.transform.Find("Clothing/Armature/Hips"), Is.Not.Null);
                Assert.That(attachment.Root.GetComponent<MochiyaAvatarComposition>().Components.Where(c => c.Kind == ComponentKind.MergeArmature).Count(), Is.GreaterThan(0));
            }
        }

        [Test] public void LocalBoneProxyDoesNotMoveOrSnapTheConvertedObject()
        {
            var root = Avatar("Avatar"); var prop = new GameObject("Prop"); prop.transform.SetParent(root.transform, false); prop.transform.localPosition = Vector3.one;
            var proxy = prop.AddComponent(Ma("ModularAvatarBoneProxy"));
            Field(proxy, "boneReference", HumanBodyBones.Head);
            var before = prop.transform.localPosition;
            using (var copy = MochiyaAvatarWorkflow.ConvertToVrmGameObject(root))
            {
                Assert.That(copy.Root.transform.Find("Prop"), Is.Not.Null);
                Assert.That(copy.Root.transform.Find("Prop").localPosition, Is.EqualTo(before));
            }
        }

        [Test] public void WarningsIncludeUnsupportedAndConversionDetailsWithoutErrors()
        {
            var report = new MochiyaConversionReport(); report.Warnings.Add("Physics approximation"); report.Unsupported.Add("MA controller"); report.Errors.Add("Invalid source");
            Assert.That(MochiyaAvatarToolsWindow.CompatibilityWarnings(report), Is.EquivalentTo(new[] { "Physics approximation", "Unsupported: MA controller" }));
        }
    }
}

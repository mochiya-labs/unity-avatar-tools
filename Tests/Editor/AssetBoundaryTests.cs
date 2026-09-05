using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mochiya.AvatarAssets;
using NUnit.Framework;
using UniVRM10;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mochiya.LilToon.Exporter.Editor.Tests
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

        [Test] public void IndependentNestedHumanoidIsAvatarAndDoesNotCopyParent()
        {
            var parent = Avatar("Parent"); var target = Avatar("Independent"); target.transform.SetParent(parent.transform, false);
            var detected = MochiyaAvatarWorkflow.Detect(target);
            Assert.That(detected.Kind, Is.EqualTo(MochiyaTargetKind.Avatar)); Assert.That(detected.ReferenceAvatar, Is.SameAs(target));
            using (var copy = MochiyaAvatarWorkflow.ConvertToVrmGameObject(target))
            {
                Assert.That(copy.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.EqualTo(1));
                Assert.That(copy.Root.GetComponent<MochiyaAvatarAsset>().Joints, Is.Empty);
                Assert.That(copy.Root.transform.Find("Parent"), Is.Null);
            }
            Assert.Throws<InvalidOperationException>(() => MochiyaAvatarConverter.ConvertAttachmentInScene(parent, target));
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
                var action = copy.Root.GetComponent<MochiyaAvatarAsset>().Actions.Single();
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
                Assert.That(copy.Root.GetComponent<MochiyaAvatarAsset>().Actions.Single().Target.Base, Is.False);
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
                Assert.That(copy.Root.GetComponent<MochiyaAvatarAsset>().Joints, Is.Empty);
                Assert.That(copy.Report.Warnings.Any(w => w.Contains("armatures remain separate")), Is.True);
            }
            Assert.That(sourceHips.parent, Is.SameAs(clothing.transform.Find("Armature")));
            using (var attachment = MochiyaAvatarWorkflow.ConvertToVrmGameObject(clothing))
            {
                Assert.That(attachment.Root.transform.Find("Clothing/Armature/Hips"), Is.Not.Null);
                Assert.That(attachment.Root.GetComponent<MochiyaAvatarAsset>().Joints.Count, Is.GreaterThan(0));
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

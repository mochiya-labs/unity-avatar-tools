using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mochiya.AvatarComposition;
using NUnit.Framework;
using UniGLTF;
using UniVRM10;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mochiya.AvatarTools.Editor.Tests
{
    public class AvatarConversionTests
    {
        private readonly List<Object> objects = new List<Object>();
        private string output;
        [SetUp] public void SetUp() { output = Path.GetFullPath("MochiyaTests"); Directory.CreateDirectory(output); }
        [TearDown] public void TearDown() { foreach (var obj in objects.AsEnumerable().Reverse()) if (obj != null) Object.DestroyImmediate(obj); objects.Clear(); }
        private GameObject Keep(GameObject go) { objects.Add(go); return go; }

        private sealed class PathReference { public GameObject targetObject; public string referencePath; }
        [Test] public void MaReferenceFallsBackToPathWhenDirectObjectIsUnityNull()
        {
            var source = Keep(CreateAvatar());
            var missing = new GameObject("Deleted direct target"); Object.DestroyImmediate(missing);
            var context = new ConversionContext(source, source, false, new Dictionary<Object, Object>(), null, null, new MochiyaConversionReport(), new List<Object>());
            var reference = new PathReference { targetObject = missing, referencePath = "Armature/Hips" };
            Assert.That(ReferenceEquals(reference.targetObject, null), Is.False);
            Assert.That(OptionalAvatarReaders.Reference(reference, context), Is.SameAs(source.transform.Find("Armature/Hips")));
            Assert.That(OptionalAvatarReaders.Reference(missing, context), Is.Null);
        }

        internal static GameObject CreateAvatar()
        {
            var root = new GameObject("Mochiya Test Avatar");
            var bones = new Dictionary<HumanBodyBones, Transform>();
            Transform Bone(HumanBodyBones type, Transform parent, Vector3 position)
            {
                var bone = new GameObject(type.ToString()).transform; bone.SetParent(parent, false); bone.localPosition = position; bones[type] = bone; return bone;
            }
            var armature = new GameObject("Armature").transform; armature.SetParent(root.transform, false);
            var hips = Bone(HumanBodyBones.Hips, armature, new Vector3(0, 1, 0));
            var spine = Bone(HumanBodyBones.Spine, hips, new Vector3(0, .2f, 0));
            var chest = Bone(HumanBodyBones.Chest, spine, new Vector3(0, .2f, 0));
            var neck = Bone(HumanBodyBones.Neck, chest, new Vector3(0, .15f, 0));
            Bone(HumanBodyBones.Head, neck, new Vector3(0, .15f, 0));
            foreach (var left in new[] { true, false })
            {
                var side = left ? 1 : -1;
                var thigh = Bone(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg, hips, new Vector3(.1f * side, -.05f, 0));
                var shin = Bone(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg, thigh, new Vector3(0, -.4f, 0));
                Bone(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot, shin, new Vector3(0, -.4f, .05f));
                var upper = Bone(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm, chest, new Vector3(.2f * side, .05f, 0));
                var lower = Bone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm, upper, new Vector3(.3f * side, 0, 0));
                Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, lower, new Vector3(.25f * side, 0, 0));
            }
            var description = new HumanDescription {
                human = bones.Select(x => new HumanBone { boneName = x.Value.name, humanName = HumanTrait.BoneName[(int)x.Key], limit = new HumanLimit { useDefaultValues = true } }).ToArray(),
                skeleton = root.GetComponentsInChildren<Transform>().Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray(),
                upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f, armStretch = .05f, legStretch = .05f, feetSpacing = 0
            };
            var animator = root.AddComponent<Animator>(); animator.avatar = AvatarBuilder.BuildHumanAvatar(root, description);
            AddMesh(root.transform, hips, "Body", new Color(.65f, .8f, .6f), 1);
            return root;
        }
        internal static SkinnedMeshRenderer AddMesh(Transform parent, Transform bone, string name, Color color, float scale)
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var mesh = Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh); Object.DestroyImmediate(primitive);
            mesh.name = name;
            var vertices = mesh.vertices.Select(v => v * scale + new Vector3(0, 1, 0)).ToArray(); mesh.vertices = vertices;
            mesh.boneWeights = vertices.Select(v => new BoneWeight { boneIndex0 = 0, weight0 = 1 }).ToArray();
            mesh.bindposes = new[] { bone.worldToLocalMatrix * parent.localToWorldMatrix };
            mesh.AddBlendShapeFrame("Body_Slim", 100, vertices.Select(v => new Vector3(-v.x * .3f, 0, 0)).ToArray(), new Vector3[vertices.Length], new Vector3[vertices.Length]);
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh; renderer.bones = new[] { bone }; renderer.rootBone = bone;
            renderer.sharedMaterial = new Material(Shader.Find("Standard")) { name = name + " Material", color = color };
            return renderer;
        }
        [Test] public void AvatarConversionPreservesSourceAndCreatesVrmComponentsWithoutWritingAssets()
        {
            var source = Keep(CreateAvatar()); var before = EditorJsonUtility.ToJson(source.GetComponent<Animator>());
            var assetsBefore = AssetDatabase.GetAllAssetPaths().Length;
            var result = MochiyaAvatarConverter.ConvertAvatarInScene(source); Keep(result.Root);
            Assert.That(result.Root.GetComponent<Vrm10Instance>(), Is.Not.Null);
            Assert.That(result.Root.GetComponent<MochiyaAvatarComposition>().Kind, Is.EqualTo(AssetKind.Avatar));
            Assert.That(result.Root.GetComponent<Animator>().avatar.isHuman, Is.True);
            Assert.That(EditorJsonUtility.ToJson(source.GetComponent<Animator>()), Is.EqualTo(before));
            Assert.That(source.GetComponent<Vrm10Instance>(), Is.Null);
            Assert.That(AssetDatabase.GetAllAssetPaths().Length, Is.EqualTo(assetsBefore));
            var original = source.GetComponentInChildren<SkinnedMeshRenderer>(); var copy = result.Root.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(copy.bones[0].IsChildOf(result.Root.transform), Is.True);
            Assert.That(copy.bones[0], Is.Not.SameAs(original.bones[0]));
            Assert.That(copy.sharedMaterial, Is.SameAs(original.sharedMaterial));
        }
        [Test] public void AttachmentGetsReferenceHumanoidWithoutBaseBodyOrOtherAttachmentMeshes()
        {
            var source = Keep(CreateAvatar()); var attachment = new GameObject("Coat"); attachment.transform.SetParent(source.transform, false);
            var renderer = AddMesh(attachment.transform, source.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips), "Coat Mesh", Color.blue, 1.1f);
            var result = MochiyaAvatarConverter.ConvertAttachmentInScene(source, attachment); Keep(result.Root);
            Assert.That(result.Root.GetComponentsInChildren<Renderer>(true).Select(x => x.name), Is.EqualTo(new[] { renderer.name }));
            Assert.That(result.Root.GetComponent<Animator>().avatar.isHuman, Is.True);
            Assert.That(result.Root.GetComponent<MochiyaAvatarComposition>().Components.Where(c => c.Kind == ComponentKind.MergeArmature).Count(), Is.GreaterThan(0));
            Assert.That(attachment.transform.parent, Is.SameAs(source.transform));
        }
        [Test] public void InvalidHumanoidIsNotAnAvatarAndOrdinaryPropsUseGlbExporter()
        {
            var malformed = Keep(CreateAvatar());
            Object.DestroyImmediate(malformed.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head).gameObject);
            Assert.That(MochiyaAvatarConverter.Validate(malformed).CanExportVrm, Is.False);
            var source = Keep(GameObject.CreatePrimitive(PrimitiveType.Cube));
            Assert.That(MochiyaAvatarConverter.Validate(source).CanConvert, Is.False);
            Assert.Throws<InvalidOperationException>(() => MochiyaAvatarConverter.ConvertAvatarInScene(source, new MochiyaConversionOptions { AllowGenericRig = true }));
            MochiyaLilToonExporter.ExportGlb(source, Path.Combine(output, "generic.glb"));
        }
        [Test] public void DetachedAttachmentIsRejected()
        {
            var source = Keep(CreateAvatar()); var attachment = Keep(GameObject.CreatePrimitive(PrimitiveType.Cube));
            Assert.Throws<InvalidOperationException>(() => MochiyaAvatarConverter.ConvertAttachmentInScene(source, attachment));
        }
        [Test] public void BaseExclusionsAndAttachmentExtractionKeepOnlyOwnedVrmBindings()
        {
            var source = Keep(CreateAvatar()); var attachment = new GameObject("Coat"); attachment.transform.SetParent(source.transform, false);
            AddMesh(attachment.transform, source.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips), "Coat Mesh", Color.blue, 1.1f);
            var original = source.AddComponent<Vrm10Instance>(); original.UpdateType = Vrm10Instance.UpdateTypes.None;
            original.Vrm = ScriptableObject.CreateInstance<VRM10Object>(); objects.Add(original.Vrm);
            var clip = ScriptableObject.CreateInstance<VRM10Expression>(); objects.Add(clip);
            clip.MorphTargetBindings = new[] { new MorphTargetBinding("Body", 0, .5f), new MorphTargetBinding("Coat/Coat Mesh", 0, .6f) };
            original.Vrm.Expression.AddClip(ExpressionPreset.happy, clip);
            original.Vrm.FirstPerson.Renderers.Add(new RendererFirstPersonFlags { Renderer = "Body" });
            original.Vrm.FirstPerson.Renderers.Add(new RendererFirstPersonFlags { Renderer = "Coat/Coat Mesh" });
            var baseOnly = MochiyaAvatarConverter.ConvertAvatarInScene(source, new MochiyaConversionOptions { ExcludeObjects = new[] { attachment } }); Keep(baseOnly.Root);
            var extracted = MochiyaAvatarConverter.ConvertAttachmentInScene(source, attachment); Keep(extracted.Root);
            Assert.That(baseOnly.Root.GetComponentsInChildren<Renderer>(true).Select(r => r.name), Is.EqualTo(new[] { "Body" }));
            Assert.That(baseOnly.Root.GetComponent<Vrm10Instance>().Vrm.Expression.Happy.MorphTargetBindings.Single().RelativePath, Is.EqualTo("Body"));
            Assert.That(extracted.Root.GetComponent<Vrm10Instance>().Vrm.Expression.Happy.MorphTargetBindings.Single().RelativePath, Is.EqualTo("Coat/Coat Mesh"));
            Assert.That(extracted.Root.GetComponent<Vrm10Instance>().Vrm.FirstPerson.Renderers.Single().Renderer, Is.EqualTo("Coat/Coat Mesh"));
            Assert.That(clip.MorphTargetBindings.Length, Is.EqualTo(2)); Assert.That(original.Vrm.FirstPerson.Renderers.Count, Is.EqualTo(2));
        }
        [Test] public void ExportsRealGlbAndVrmWithInactiveVariantsAlternateMaterialsAndFinalReferences()
        {
            var source = Keep(CreateAvatar()); var result = MochiyaAvatarConverter.ConvertAvatarInScene(source); Keep(result.Root);
            var asset = result.Root.GetComponent<MochiyaAvatarComposition>(); var mesh = result.Root.GetComponentInChildren<SkinnedMeshRenderer>();
            mesh.gameObject.SetActive(false);
            var alternate = new Material(Shader.Find("Standard")) { name = "Alternate", color = Color.magenta }; objects.Add(alternate);
            asset.Components.Add(new AssetComponent { Id = "fit", Kind = ComponentKind.ShapeChanger, Source = asset.transform, Entries = new List<AssetEntry> { new AssetEntry { Target = new AssetSelector { Node = mesh.transform, MorphIndex = 0, BlendshapeKeywords = new[] { "Body_Slim" } }, Value = .5f } } });
            asset.Components.Add(new AssetComponent { Id = "swap", Kind = ComponentKind.MaterialSetter, Source = asset.transform, Entries = new List<AssetEntry> { new AssetEntry { Target = new AssetSelector { Node = mesh.transform }, Material = alternate } } });
            var meta = result.Root.GetComponent<Vrm10Instance>().Vrm.Meta; meta.Authors = new List<string> { "Mochiya test fixture" };
            foreach (var extension in new[] { ".glb", ".vrm" })
            {
                var path = Path.Combine(output, "avatar" + extension);
                if (extension == ".glb") MochiyaLilToonExporter.ExportGlb(result.Root, path);
                else MochiyaLilToonExporter.ExportVrm(result.Root, path, meta);
                var bytes = File.ReadAllBytes(path); Assert.That(BitConverter.ToUInt32(bytes, 0), Is.EqualTo(0x46546c67));
                var json = System.Text.Encoding.UTF8.GetString(bytes, 20, BitConverter.ToInt32(bytes, 12));
                StringAssert.Contains("MOCHIYA_avatar_composition", json); StringAssert.Contains("\"morphIndex\":0", json); StringAssert.Contains("\"active\":false", json); StringAssert.Contains("Alternate", json);
                if (extension == ".vrm") StringAssert.Contains("VRMC_vrm", json);
            }
            Assert.That(mesh.gameObject.activeSelf, Is.False);
        }
        [Test] public void SceneDataRestoresTransientVrmExpressionsAfterReferencesAreLost()
        {
            var source = Keep(CreateAvatar()); var result = MochiyaAvatarConverter.ConvertAvatarInScene(source); Keep(result.Root);
            var instance = result.Root.GetComponent<Vrm10Instance>();
            var clip = ScriptableObject.CreateInstance<VRM10Expression>(); clip.name = "Test"; clip.MorphTargetBindings = new[] { new MorphTargetBinding("Body", 0, .8f) };
            instance.Vrm.Expression.AddClip(ExpressionPreset.happy, clip);
            var resources = result.Root.GetComponent<MochiyaSceneResources>(); resources.Capture();
            instance.Vrm = null; result.Root.GetComponent<Animator>().avatar = null; resources.RestoreIfNeeded();
            Assert.That(instance.Vrm.Expression.Happy.MorphTargetBindings[0].Weight, Is.EqualTo(.8f));
            Assert.That(result.Root.GetComponent<Animator>().avatar.isHuman, Is.True);
        }
        [Test] public void PreparedHierarchySurvivesSceneSaveAndReload()
        {
            if (!Application.isBatchMode || new DirectoryInfo(Path.GetDirectoryName(Application.dataPath)).Name != "unity-project")
                Assert.Ignore("Scene round-trip testing requires the isolated batch unity-project fixture.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var source = CreateAvatar(); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(source, scene);
            var result = MochiyaAvatarConverter.ConvertAvatarInScene(source);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(result.Root, scene);
            var path = "Assets/MochiyaSceneRoundTrip.unity";
            try
            {
                Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                EditorSceneManager.CloseScene(scene, true);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var restored = scene.GetRootGameObjects().First(x => x.GetComponent<MochiyaAvatarComposition>() != null);
                Assert.That(restored.GetComponent<Animator>().avatar.isHuman, Is.True);
                Assert.That(restored.GetComponent<Vrm10Instance>().Vrm, Is.Not.Null);
                Assert.That(restored.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh, Is.Not.Null);
                Assert.That(restored.GetComponent<MochiyaAvatarComposition>().Nodes.All(x => x.Node != null), Is.True);
            }
            finally { if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
        [Test] public void WritesPortableViewerFixtures()
        {
            var source = Keep(CreateAvatar()); var avatar = MochiyaAvatarConverter.ConvertAvatarInScene(source); Keep(avatar.Root);
            var meta = avatar.Root.GetComponent<Vrm10Instance>().Vrm.Meta; meta.Authors = new List<string> { "Mochiya" };
            MochiyaLilToonExporter.ExportVrm(avatar.Root, Path.Combine(output, "viewer-avatar.vrm"), meta);
            var attachment = new GameObject("Test Vest"); attachment.transform.SetParent(source.transform, false);
            var hips = source.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips);
            AddMesh(attachment.transform, hips, "Vest", Color.blue, 1.1f);
            var converted = MochiyaAvatarConverter.ConvertAttachmentInScene(source, attachment); Keep(converted.Root);
            var asset = converted.Root.GetComponent<MochiyaAvatarComposition>(); var mesh = converted.Root.GetComponentInChildren<SkinnedMeshRenderer>();
            asset.Components.Add(new AssetComponent { Id = "fit", Kind = ComponentKind.ShapeChanger, Source = asset.transform, Entries = new List<AssetEntry> { new AssetEntry { Target = new AssetSelector { Base = true, MeshKeywords = new[] { "Body" }, BlendshapeKeywords = new[] { "Body_Slim" } }, Value = .4f } } });
            asset.Components.Add(new AssetComponent { Id = "sync", Kind = ComponentKind.BlendshapeSync, Source = asset.transform, Entries = new List<AssetEntry> { new AssetEntry { Driver = new AssetSelector { Base = true, MeshKeywords = new[] { "Body" }, BlendshapeKeywords = new[] { "Body_Slim" } }, Target = new AssetSelector { Node = mesh.transform, MorphIndex = 0, BlendshapeKeywords = new[] { "Body_Slim" } } } } });
            MochiyaLilToonExporter.ExportVrm(converted.Root, Path.Combine(output, "viewer-outfit.vrm"), meta);
            MochiyaLilToonExporter.ExportGlb(converted.Root, Path.Combine(output, "viewer-outfit.glb"));
        }
        [Test] public void ReadsRealModularAvatarComponentsWhenInstalled()
        {
            var shapeType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("nadena.dev.modular_avatar.core.ModularAvatarShapeChanger")).FirstOrDefault(t => t != null);
            if (shapeType == null) Assert.Ignore("Optional Modular Avatar package is not installed in this test configuration.");
            var source = Keep(CreateAvatar()); var attachment = new GameObject("MA Attachment"); attachment.transform.SetParent(source.transform, false);
            AddMesh(attachment.transform, source.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips), "MA Mesh", Color.cyan, 1.1f);
            var component = attachment.AddComponent(shapeType);
            var list = (System.Collections.IList)shapeType.GetProperty("Shapes").GetValue(component);
            var changed = Activator.CreateInstance(list.GetType().GetGenericArguments()[0]);
            var referenceField = changed.GetType().GetField("Object"); var reference = Activator.CreateInstance(referenceField.FieldType);
            reference.GetType().GetField("targetObject", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(reference, source.GetComponentInChildren<SkinnedMeshRenderer>().gameObject);
            referenceField.SetValue(changed, reference); changed.GetType().GetField("ShapeName").SetValue(changed, "Body_Slim");
            changed.GetType().GetField("ChangeType").SetValue(changed, Enum.Parse(changed.GetType().GetField("ChangeType").FieldType, "Set"));
            changed.GetType().GetField("Value").SetValue(changed, 65f); list.Add(changed);
            var result = MochiyaAvatarConverter.ConvertAttachmentInScene(source, attachment); Keep(result.Root);
            var record = result.Root.GetComponent<MochiyaAvatarComposition>().Components.Single(c => c.Kind == ComponentKind.ShapeChanger);
            var action = record.Entries.Single();
            Assert.That(action.Target.Base, Is.True); Assert.That(action.Value, Is.EqualTo(.65f)); Assert.That(record.UseCondition, Is.True);
            Assert.That(attachment.GetComponent(shapeType), Is.SameAs(component)); Assert.That(result.Root.GetComponentInChildren(shapeType), Is.Null);
        }
        [TestCase("empty")]
        [TestCase("serializedEmpty")]
        [TestCase("destroyed")]
        [TestCase("assigned")]
        public void ReadsRealVrchatVisemesPhysbonesAndCollidersWhenInstalled(string rootState)
        {
            Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);
            var descriptorType = Find("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");
            if (descriptorType == null) Assert.Ignore("Optional VRChat SDK is not installed in this test configuration.");
            var source = Keep(CreateAvatar()); var descriptor = source.AddComponent(descriptorType);
            var viewField = descriptorType.GetField("ViewPosition"); viewField.SetValue(descriptor, new Vector3(0,1.75f,.08f));
            var renderer = source.GetComponentInChildren<SkinnedMeshRenderer>();
            descriptorType.GetField("VisemeSkinnedMesh").SetValue(descriptor, renderer);
            descriptorType.GetField("VisemeBlendShapes").SetValue(descriptor, Enumerable.Repeat("Body_Slim", 15).ToArray());
            var lipSync = descriptorType.GetField("lipSync"); lipSync.SetValue(descriptor, Enum.Parse(lipSync.FieldType, "VisemeBlendShape"));
            var hair = new GameObject("Hair"); hair.transform.SetParent(source.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head), false);
            var tip = new GameObject("HairTip"); tip.transform.SetParent(hair.transform, false); tip.transform.localPosition = new Vector3(0,.1f,0);
            var physboneType = Find("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone"); var physbone = hair.AddComponent(physboneType);
            var colliderType = Find("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider"); var collider = source.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head).gameObject.AddComponent(colliderType);
            var expectedSpringRoot = hair.transform;
            var expectedColliderRoot = collider.transform;
            foreach (var component in new[] { physbone, collider })
            {
                var field = component.GetType().GetField("rootTransform");
                if (rootState == "serializedEmpty")
                {
                    // Editor deserialization can produce a managed wrapper that compares equal to Unity null.
                    EditorJsonUtility.FromJsonOverwrite("{\"rootTransform\":{\"fileID\":0}}", component);
                    Assert.That((Transform)field.GetValue(component) == null, Is.True);
                }
                else if (rootState == "destroyed")
                {
                    var missing = new GameObject("Deleted root").transform;
                    field.SetValue(component, missing); Object.DestroyImmediate(missing.gameObject);
                    Assert.That(ReferenceEquals(field.GetValue(component), null), Is.False);
                    Assert.That((Transform)field.GetValue(component) == null, Is.True);
                }
                else if (rootState == "assigned")
                {
                    var assigned = new GameObject(component == physbone ? "Explicit spring root" : "Explicit collider root").transform;
                    assigned.SetParent(component.transform, false); assigned.localPosition = new Vector3(.03f,.04f,.05f);
                    field.SetValue(component, assigned);
                    if (component == physbone) { expectedSpringRoot = assigned; tip.transform.SetParent(assigned, false); }
                    else expectedColliderRoot = assigned;
                }
            }
            colliderType.GetField("radius").SetValue(collider, .05f);
            ((System.Collections.IList)physboneType.GetField("colliders").GetValue(physbone)).Add(collider);
            var originalPhysbone = EditorJsonUtility.ToJson(physbone);
            var originalCollider = EditorJsonUtility.ToJson(collider);
            var result = MochiyaAvatarConverter.ConvertAvatarInScene(source); Keep(result.Root);
            var vrm = result.Root.GetComponent<Vrm10Instance>();
            Assert.That(vrm.Vrm.Expression.Aa.MorphTargetBindings.Single().Index, Is.EqualTo(0));
            Assert.That(Vector3.Distance(vrm.Vrm.LookAt.OffsetFromHead, new Vector3(0,.05f,.08f)), Is.LessThan(.00001f));
            Assert.That(vrm.SpringBone.Springs.Count, Is.GreaterThan(0));
            Assert.That(vrm.SpringBone.Springs[0].Joints[0].m_stiffnessForce, Is.GreaterThan(0));
            Assert.That(vrm.SpringBone.ColliderGroups[0].Colliders[0].Radius, Is.EqualTo(.05f));
            Assert.That(AnimationUtility.CalculateTransformPath(vrm.SpringBone.Springs[0].Joints[0].transform, result.Root.transform),
                Is.EqualTo(AnimationUtility.CalculateTransformPath(expectedSpringRoot, source.transform)));
            Assert.That(AnimationUtility.CalculateTransformPath(vrm.SpringBone.ColliderGroups[0].Colliders[0].transform, result.Root.transform),
                Is.EqualTo(AnimationUtility.CalculateTransformPath(expectedColliderRoot, source.transform)));
            Assert.That(EditorJsonUtility.ToJson(physbone), Is.EqualTo(originalPhysbone));
            Assert.That(EditorJsonUtility.ToJson(collider), Is.EqualTo(originalCollider));
            Assert.That(result.Root.GetComponentInChildren(physboneType), Is.Null); Assert.That(hair.GetComponent(physboneType), Is.SameAs(physbone));
        }
    }
}

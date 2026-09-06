using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mochiya.AvatarComposition;
using NUnit.Framework;
using UniGLTF;
using UniVRM10;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mochiya.AvatarTools.Editor.Tests
{
    public sealed class AvatarWorkflowTests
    {
        private readonly List<Object> objects = new List<Object>();
        private readonly List<string> assets = new List<string>();
        private T Keep<T>(T value) where T : Object { objects.Add(value); return value; }
        private GameObject Avatar() => Keep(AvatarConversionTests.CreateAvatar());
        private static GameObject Attachment(GameObject avatar)
        {
            var attachment = new GameObject("Coat"); attachment.transform.SetParent(avatar.transform, false);
            AvatarConversionTests.AddMesh(attachment.transform, avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips), "Coat Mesh", Color.blue, 1.1f);
            return attachment;
        }
        private static string Json(string path)
        {
            var bytes = File.ReadAllBytes(path);
            Assert.That(BitConverter.ToUInt32(bytes, 0), Is.EqualTo(0x46546c67));
            return Encoding.UTF8.GetString(bytes, 20, BitConverter.ToInt32(bytes, 12));
        }
        [SetUp] public void SetUp() => Directory.CreateDirectory("MochiyaTests");
        [TearDown] public void TearDown()
        {
            foreach (var value in objects.AsEnumerable().Reverse()) if (value != null) Object.DestroyImmediate(value);
            objects.Clear();
            foreach (var path in assets) AssetDatabase.DeleteAsset(path);
            assets.Clear();
        }

        [TestCase(0, AssetKind.Avatar)]
        [TestCase(1, AssetKind.Attachment)]
        [TestCase(2, AssetKind.Attachment)]
        public void LoadsSavedKindsUsingOnlyAvatarAndAttachmentChoices(int storedKind, AssetKind expected)
        {
            var data = Avatar().AddComponent<MochiyaAvatarComposition>();
            JsonUtility.FromJsonOverwrite("{\"Kind\":" + storedKind + "}", data);
            Assert.That(data.Kind, Is.EqualTo(expected));
            Assert.That(new SerializedObject(data).FindProperty("Kind").enumDisplayNames,
                Is.EqualTo(new[] { "Avatar", "Attachment" }));
            Assert.That(MochiyaAvatarWorkflow.Detect(data.gameObject).Kind,
                Is.EqualTo(expected == AssetKind.Avatar ? MochiyaTargetKind.Avatar : MochiyaTargetKind.Attachment));
            var path = Path.GetFullPath("MochiyaTests/migrated-kind-" + storedKind + ".glb");
            MochiyaAvatarWorkflow.Export(data.gameObject, path);
            var json = Json(path);
            StringAssert.Contains("\"assetKind\":\"" + (expected == AssetKind.Avatar ? "avatar" : "attachment") + "\"", json);
            StringAssert.DoesNotContain("\"accessory\"", json);
            StringAssert.DoesNotContain("\"outfit\"", json);
        }

        [Test] public void DetectsAvatarAndDirectChildAttachmentWithoutMaEvenWithAttachmentAnimator()
        {
            var avatar = Avatar(); var attachment = Attachment(avatar);
            attachment.AddComponent<Animator>().avatar = avatar.GetComponent<Animator>().avatar;
            Assert.That(MochiyaAvatarWorkflow.Detect(avatar).Kind, Is.EqualTo(MochiyaTargetKind.Avatar));
            var detected = MochiyaAvatarWorkflow.Detect(attachment);
            Assert.That(detected.Kind, Is.EqualTo(MochiyaTargetKind.Attachment));
            Assert.That(detected.ReferenceAvatar, Is.SameAs(avatar));
            Assert.That(MochiyaAvatarWorkflow.Validate(attachment).CanConvert, Is.True);
        }

        [Test] public void InvalidSelectionAndNestedAttachmentDoNotGuessAnAncestor()
        {
            var avatar = Avatar(); var attachment = Attachment(avatar);
            var group = new GameObject("Group"); group.transform.SetParent(avatar.transform, false);
            attachment.transform.SetParent(group.transform, false);
            Assert.That(MochiyaAvatarWorkflow.Detect(attachment).Kind, Is.EqualTo(MochiyaTargetKind.Invalid));
            Assert.That(MochiyaAvatarWorkflow.Validate(attachment).CanConvert, Is.False);
            Assert.That(MochiyaAvatarWorkflow.Detect(Keep(GameObject.CreatePrimitive(PrimitiveType.Cube))).Kind, Is.EqualTo(MochiyaTargetKind.Invalid));
            Assert.That(MochiyaAvatarWorkflow.Validate(null).Errors, Is.Not.Empty);
        }

        [Test] public void ConversionKeepsIncludedAttachmentsAndSuppliesMetadataWithoutCreatingAssets()
        {
            var avatar = Avatar(); Attachment(avatar);
            var before = AssetDatabase.GetAllAssetPaths();
            using (var result = MochiyaAvatarWorkflow.ConvertToVrmGameObject(avatar))
            {
                Assert.That(result.Root.GetComponentsInChildren<Renderer>(true).Select(r => r.name), Is.EquivalentTo(new[] { "Body", "Coat Mesh" }));
                Assert.That(result.Root.GetComponent<Vrm10Instance>().Vrm.Meta.Authors, Is.EqualTo(new[] { MochiyaExportProfile.DefaultAuthor }));
                Assert.That(result.Root.GetComponent<Vrm10Instance>().Vrm.Meta.Name, Is.EqualTo(avatar.name));
                Assert.That(AssetDatabase.GetAllAssetPaths(), Is.EquivalentTo(before));
                Assert.That(avatar.GetComponent<Vrm10Instance>(), Is.Null);
            }
        }

        [TestCase("vrm", false)] [TestCase("glb", false)]
        [TestCase("vrm", true)] [TestCase("glb", true)]
        public void DirectExportUsesDefaultsAndLeavesNoConversionObjects(string extension, bool extractAttachment)
        {
            var avatar = Avatar(); var attachment = Attachment(avatar); var source = extractAttachment ? attachment : avatar;
            var snapshot = avatar.GetComponentsInChildren<Component>(true).ToDictionary(c => c, EditorJsonUtility.ToJson);
            var roots = avatar.scene.GetRootGameObjects(); Selection.activeGameObject = source;
            var profileBefore = EditorJsonUtility.ToJson(MochiyaExportProfile.Default);
            var path = Path.GetFullPath("MochiyaTests/workflow-" + (extractAttachment ? "attachment" : "avatar") + "." + extension);
            MochiyaAvatarWorkflow.Export(source, path);
            var json = Json(path);
            StringAssert.Contains("\"assetKind\":\"" + (extractAttachment ? "attachment" : "avatar") + "\"", json);
            StringAssert.Contains("\"role\":\"" + (extractAttachment ? "attachmentReference" : "avatar") + "\"", json);
            StringAssert.Contains("Coat Mesh", json);
            if (extractAttachment) StringAssert.DoesNotContain("\"name\":\"Body\"", json);
            else StringAssert.Contains("\"name\":\"Body\"", json);
            StringAssert.Contains("\"sparse\":", json);
            if (extension == "vrm")
            {
                StringAssert.Contains("\"authors\":[\"Mochiya VRM Exporter\"]", json);
                StringAssert.Contains("\"name\":\"" + source.name + "\"", json);
            }
            Assert.That(avatar.scene.GetRootGameObjects(), Is.EquivalentTo(roots));
            Assert.That(Selection.activeGameObject, Is.SameAs(source));
            Assert.That(snapshot.All(p => EditorJsonUtility.ToJson(p.Key) == p.Value), Is.True);
            Assert.That(EditorJsonUtility.ToJson(MochiyaExportProfile.Default), Is.EqualTo(profileBefore));
        }

        [Test] public void FailedDirectExportAlsoCleansUpTemporaryConversion()
        {
            var avatar = Avatar(); var roots = avatar.scene.GetRootGameObjects(); Selection.activeGameObject = avatar;
            var missing = Path.Combine("MochiyaTests", Guid.NewGuid().ToString("N"), "missing.vrm");
            Assert.Throws<DirectoryNotFoundException>(() => MochiyaAvatarWorkflow.Export(avatar, missing));
            Assert.That(avatar.scene.GetRootGameObjects(), Is.EquivalentTo(roots));
            Assert.That(Selection.activeGameObject, Is.SameAs(avatar));
        }

        [Test] public void PreparedAttachmentRetainsItsKindAndActionsWhenExportedAgain()
        {
            var avatar = Avatar(); var attachment = Attachment(avatar);
            using (var result = MochiyaAvatarWorkflow.ConvertToVrmGameObject(attachment))
            {
                var data = result.Root.GetComponent<MochiyaAvatarComposition>();
                data.Actions.Add(new AssetAction { Id = "fit", Kind = ActionKind.MorphOverride,
                    Target = new AssetSelector { Base = true, MeshKeywords = new[] { "Body" }, BlendshapeKeywords = new[] { "Body_Slim" } }, Value = .4f });
                Assert.That(MochiyaAvatarWorkflow.Detect(result.Root).Kind, Is.EqualTo(MochiyaTargetKind.Attachment));
                Assert.That(MochiyaAvatarWorkflow.Detect(result.Root).IsPrepared, Is.True);
                var path = Path.GetFullPath("MochiyaTests/prepared-attachment.vrm");
                MochiyaAvatarWorkflow.Export(result.Root, path);
                StringAssert.Contains("\"assetKind\":\"attachment\"", Json(path));
                StringAssert.Contains("morph.override", Json(path));
                Assert.That(data.Actions.Count, Is.EqualTo(1));
            }
        }

        [Test] public void UnsupportedSourceComponentsDoNotRequireAcceptance()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator")).FirstOrDefault(t => t != null);
            if (type == null) Assert.Ignore("Optional MA adapter test.");
            var avatar = Avatar(); var unsupported = avatar.AddComponent(type);
            var report = MochiyaAvatarWorkflow.Validate(avatar);
            Assert.That(report.Unsupported, Is.Not.Empty); Assert.That(report.CanConvert, Is.True);
            using (var result = MochiyaAvatarWorkflow.ConvertToVrmGameObject(avatar))
            {
                Assert.That(result.Root.GetComponent(type), Is.Null);
                Assert.That(avatar.GetComponent(type), Is.SameAs(unsupported));
            }
        }

        [Test] public void DefaultIsBundledAndProfilesPersistNativeSettingsIndependently()
        {
            var defaults = MochiyaExportProfile.Default;
            Assert.That(EditorUtility.IsPersistent(defaults), Is.True, "The package must ship its default asset.");
            Assert.That(defaults.Metadata.Authors, Is.EqualTo(new[] { MochiyaExportProfile.DefaultAuthor }));
            Assert.That(defaults.MorphTargetUseSparse && defaults.GlbSettings.UseSparseAccessorForMorphTarget, Is.True);
            Assert.That(defaults.GlbSettings.InverseAxis, Is.EqualTo(Axes.Z));
            var custom = Object.Instantiate(defaults);
            custom.Metadata.Authors = new List<string> { "Creator" };
            custom.Metadata.AntisocialOrHateUsage = true;
            custom.FreezeMesh = true; custom.MorphTargetUseSparse = false; custom.GlbSettings.UseSparseAccessorForMorphTarget = false;
            var path = "Assets/MochiyaProfile-" + Guid.NewGuid().ToString("N") + ".asset"; assets.Add(path);
            AssetDatabase.CreateAsset(custom, path); AssetDatabase.SaveAssets(); Resources.UnloadAsset(custom);
            var restored = AssetDatabase.LoadAssetAtPath<MochiyaExportProfile>(path);
            Assert.That(restored.FreezeMesh, Is.True);
            Assert.That(restored.MorphTargetUseSparse || restored.GlbSettings.UseSparseAccessorForMorphTarget, Is.False);
            var avatar = Avatar(); var meta = restored.CreateMetadata(avatar);
            Assert.That(meta.Name, Is.EqualTo(avatar.name)); Assert.That(meta.Authors, Is.EqualTo(new[] { "Creator" }));
            Assert.That(meta.AntisocialOrHateUsage, Is.True);
            meta.Authors.Add("Transient"); Assert.That(restored.Metadata.Authors, Is.EqualTo(new[] { "Creator" }));
            Assert.That(defaults.Metadata.Authors, Is.EqualTo(new[] { MochiyaExportProfile.DefaultAuthor }));
            var output = Path.GetFullPath("MochiyaTests/custom-profile.vrm");
            MochiyaAvatarWorkflow.Export(avatar, output, restored);
            StringAssert.Contains("\"authors\":[\"Creator\"]", Json(output));
            StringAssert.DoesNotContain("\"sparse\":", Json(output));
        }

        [Test] public void ProfileDefaultsFillOnlyMissingMetadataWithoutEditingAttachedVrm()
        {
            var avatar = Avatar(); var instance = avatar.AddComponent<Vrm10Instance>(); instance.UpdateType = Vrm10Instance.UpdateTypes.None;
            instance.Vrm = Keep(ScriptableObject.CreateInstance<VRM10Object>());
            instance.Vrm.Meta.Authors = new List<string> { "Original creator" };
            var profile = Keep(ScriptableObject.CreateInstance<MochiyaExportProfile>()); profile.UseAttachedVrmMetadata = true;
            var before = JsonUtility.ToJson(instance.Vrm.Meta);
            var meta = profile.CreateMetadata(avatar);
            Assert.That(meta.Name, Is.EqualTo(avatar.name)); Assert.That(meta.Authors, Is.EqualTo(new[] { "Original creator" }));
            Assert.That(JsonUtility.ToJson(instance.Vrm.Meta), Is.EqualTo(before));
            profile.UseAttachedVrmMetadata = false; profile.Metadata.Authors.Clear();
            Assert.That(profile.CreateMetadata(avatar).Authors, Is.EqualTo(new[] { MochiyaExportProfile.DefaultAuthor }));
            Assert.That(profile.Metadata.Authors, Is.Empty);
        }
    }
}

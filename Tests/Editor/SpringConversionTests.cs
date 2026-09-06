using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UniVRM10;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.TestTools;
using UnityEditor.TestTools;
using UnityEditor.SceneManagement;

namespace Mochiya.AvatarTools.Editor.Tests
{
    public class SpringConversionTests
    {
        private readonly List<Object> objects = new List<Object>();
        private GameObject Keep(GameObject go) { objects.Add(go); return go; }
        private static Transform Bone(string name, Transform parent, Vector3 offset)
        {
            var bone = new GameObject(name).transform; bone.SetParent(parent, false); bone.localPosition = offset; return bone;
        }
        private static Component PhysBone(Transform host, Transform root)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone")).FirstOrDefault(t => t != null);
            if (type == null) Assert.Ignore("Install VRC SDK to exercise its adapter.");
            var component = host.gameObject.AddComponent(type); Set(component, "rootTransform", root); return component;
        }
        private static void Set(Component component, string field, object value)
        {
            var target = component.GetType().GetField(field);
            Assert.That(target, Is.Not.Null, field);
            target.SetValue(component, target.FieldType.IsEnum ? Enum.ToObject(target.FieldType, value) : value);
        }
        [TearDown] public void TearDown() { foreach (var obj in objects.AsEnumerable().Reverse()) if (obj != null) Object.DestroyImmediate(obj); objects.Clear(); }

        [Test] public void InactivePhysBonePresetsDoNotCompeteWithActivePreset()
        {
            var source = Keep(AvatarConversionTests.CreateAvatar());
            var root = Bone("Breast", source.transform, Vector3.up);
            Bone("Breast tip", root, Vector3.up * .1f);
            var inactive = Bone("Inactive preset", source.transform, Vector3.zero);
            var skipped = PhysBone(inactive, root); Set(skipped, "pull", .9f); inactive.gameObject.SetActive(false);
            var active = PhysBone(Bone("Active preset", source.transform, Vector3.zero), root); Set(active, "pull", .1f); Set(active, "stiffness", 0f);
            var result = MochiyaAvatarConverter.ConvertAvatarInScene(source); Keep(result.Root);
            var vrm = result.Root.GetComponent<Vrm10Instance>();
            Assert.That(vrm.UpdateType, Is.EqualTo(Vrm10Instance.UpdateTypes.LateUpdate));
            Assert.That(vrm.SpringBone.Springs.Count, Is.EqualTo(1));
            Assert.That(vrm.SpringBone.Springs[0].Joints[0].m_stiffnessForce, Is.EqualTo(.4f).Within(.00001f));
            Assert.That(inactive.gameObject.activeSelf, Is.False);
        }

        [Test] public void NestedAndOverlappingPhysBonesHaveUniqueJointOwnership()
        {
            var source = Keep(AvatarConversionTests.CreateAvatar());
            var root = Bone("Outer", source.transform, Vector3.up);
            var child = Bone("Inner", root, Vector3.up * .1f);
            Bone("Tip", child, Vector3.up * .1f);
            var outer = PhysBone(root, root); Set(outer, "endpointPosition", Vector3.up * .1f);
            PhysBone(child, child);
            PhysBone(Bone("Duplicate preset", source.transform, Vector3.zero), child);
            var result = MochiyaAvatarConverter.ConvertAvatarInScene(source); Keep(result.Root);
            var joints = result.Root.GetComponent<Vrm10Instance>().SpringBone.Springs.SelectMany(s => s.Joints).Select(j => j.transform).ToArray();
            Assert.That(joints.Distinct().Count(), Is.EqualTo(joints.Length));
            Assert.That(joints.Count(t => t.name == "Inner"), Is.EqualTo(1));
            Assert.That(result.Report.Warnings.Any(w => w.Contains("overlap") || w.Contains("already")), Is.True);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void BranchesRespectMultiChildModeAndHaveContinuousUniqueChains(int mode)
        {
            var source = Keep(AvatarConversionTests.CreateAvatar());
            var root = Bone("Hair hub", source.transform, Vector3.up);
            var left = Bone("Left strand", root, new Vector3(-.1f, -.1f, 0)); Bone("Left tip", left, Vector3.down * .1f);
            var right = Bone("Right strand", root, new Vector3(.1f, -.1f, 0)); Bone("Right tip", right, Vector3.down * .1f);
            var physics = PhysBone(root, root); Set(physics, "multiChildType", mode);
            var result = MochiyaAvatarConverter.ConvertAvatarInScene(source); Keep(result.Root);
            var springs = result.Root.GetComponent<Vrm10Instance>().SpringBone.Springs;
            var heads = springs.SelectMany(s => s.Joints.Take(s.Joints.Count - 1)).ToArray();
            Assert.That(heads.Any(j => j.name == "Hair hub"), Is.EqualTo(mode != 0));
            var joints = springs.SelectMany(s => s.Joints).Select(j => j.transform).ToArray();
            Assert.That(joints.Distinct().Count(), Is.EqualTo(joints.Length));
            Assert.That(heads.Any(j => j.name == "Left strand"), Is.True);
            Assert.That(heads.Any(j => j.name == "Right strand"), Is.True);
            foreach (var spring in springs)
                for (var i = 1; i < spring.Joints.Count; i++) Assert.That(spring.Joints[i].transform.IsChildOf(spring.Joints[i-1].transform), Is.True);
        }

        [TestCase(0f, .5f)] [TestCase(.5f, .25f)] [TestCase(1f, 0f)]
        public void GravityFalloffPreservesRestStrength(float falloff, float expected)
        {
            var source = Keep(AvatarConversionTests.CreateAvatar());
            var root = Bone("Tail", source.transform, Vector3.up); Bone("Tip", root, Vector3.down * .1f);
            var physics = PhysBone(root, root); Set(physics, "gravity", .5f); Set(physics, "gravityFalloff", falloff);
            var result = MochiyaAvatarConverter.ConvertAvatarInScene(source); Keep(result.Root);
            var joint = result.Root.GetComponent<Vrm10Instance>().SpringBone.Springs.Single().Joints[0];
            Assert.That(joint.m_gravityPower, Is.EqualTo(expected));
            Assert.That(joint.m_gravityDir, Is.EqualTo(Vector3.down));
        }

        [UnityTest]
        public System.Collections.IEnumerator ConvertedSpringsAnimateAutomaticallyInPlayMode()
        {
            if (!Application.isBatchMode || new DirectoryInfo(Path.GetDirectoryName(Application.dataPath)).Name != "unity-project")
                Assert.Ignore("Play Mode transition requires the isolated batch unity-project fixture.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var source = AvatarConversionTests.CreateAvatar();
            var root = Bone("Moving tail", source.transform, Vector3.up); Bone("Tip", root, Vector3.down * .2f);
            PhysBone(root, root);
            var result = MochiyaAvatarConverter.ConvertAvatarInScene(source); result.Root.name = "Spring play fixture";
            Object.DestroyImmediate(source);
            yield return new EnterPlayMode();
            var instance = GameObject.Find("Spring play fixture").GetComponent<Vrm10Instance>();
            Assert.That(instance.UpdateType, Is.EqualTo(Vrm10Instance.UpdateTypes.LateUpdate));
            var joint = instance.SpringBone.Springs.Single().Joints[0].transform;
            var initial = joint.localRotation;
            var angle = 0f;
            for (var i = 0; i < 30; i++)
            {
                instance.transform.position = Vector3.right * Mathf.Sin(i * .3f) * .2f;
                yield return null;
                angle = Mathf.Max(angle, Quaternion.Angle(initial, joint.localRotation));
            }
            Assert.That(angle, Is.GreaterThan(1f), "The runtime must actually simulate motion, not only contain spring components.");
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        [Serializable] private class Document { public Extensions extensions; public ExportedNode[] nodes; }
        [Serializable] private class Extensions { public SpringExtension VRMC_springBone; }
        [Serializable] private class SpringExtension { public ExportedSpring[] springs; public ExportedCollider[] colliders; }
        [Serializable] private class ExportedSpring { public ExportedJoint[] joints; }
        [Serializable] private class ExportedJoint { public float[] gravityDir; public float gravityPower; }
        [Serializable] private class ExportedCollider { public int node; public ExportedShape shape; }
        [Serializable] private class ExportedShape { public Capsule capsule; }
        [Serializable] private class Capsule { public float[] offset; public float[] tail; }
        [Serializable] private class ExportedNode { public int[] children; public float[] translation, rotation, scale, matrix; }
        private static Matrix4x4 WorldMatrix(ExportedNode[] nodes, int index)
        {
            var n = nodes[index]; var matrix = Matrix4x4.identity;
            if (n.matrix != null && n.matrix.Length == 16) { for (var i = 0; i < 16; i++) matrix[i] = n.matrix[i]; }
            else matrix = Matrix4x4.TRS(n.translation != null ? new Vector3(n.translation[0], n.translation[1], n.translation[2]) : Vector3.zero,
                n.rotation != null ? new Quaternion(n.rotation[0], n.rotation[1], n.rotation[2], n.rotation[3]) : Quaternion.identity,
                n.scale != null ? new Vector3(n.scale[0], n.scale[1], n.scale[2]) : Vector3.one);
            var parent = Array.FindIndex(nodes, other => other.children != null && other.children.Contains(index));
            return parent >= 0 ? WorldMatrix(nodes, parent) * matrix : matrix;
        }

        [TestCase(false)] [TestCase(true)]
        public void ExportPreservesWorldGravityWithRotatedBones(bool freeze)
        {
            var source = Keep(AvatarConversionTests.CreateAvatar());
            var result = MochiyaAvatarConverter.ConvertAvatarInScene(source); Keep(result.Root);
            var vrm = result.Root.GetComponent<Vrm10Instance>();
            var root = Bone("Tail", result.Root.transform, Vector3.up); root.localRotation = Quaternion.Euler(130, 25, 15);
            var tip = Bone("Tail tip", root, Vector3.up * .1f);
            var joint = root.gameObject.AddComponent<VRM10SpringBoneJoint>(); joint.m_gravityDir = Vector3.down; joint.m_gravityPower = .5f;
            vrm.SpringBone.Springs.Add(new Vrm10InstanceSpringBone.Spring("Tail") { Joints = new List<VRM10SpringBoneJoint> { joint, tip.gameObject.AddComponent<VRM10SpringBoneJoint>() } });
            var collider = root.gameObject.AddComponent<VRM10SpringBoneCollider>(); collider.ColliderType = VRM10SpringBoneColliderTypes.Capsule;
            collider.Offset = new Vector3(.02f, .1f, .03f); collider.Tail = new Vector3(.04f, .4f, .05f); collider.Radius = .03f;
            var group = root.gameObject.AddComponent<VRM10SpringBoneColliderGroup>(); group.Colliders.Add(collider);
            vrm.SpringBone.ColliderGroups.Add(group); vrm.SpringBone.Springs[0].ColliderGroups.Add(group);
            var expectedOffset = root.TransformPoint(collider.Offset); expectedOffset.x *= -1;
            var expectedTail = root.TransformPoint(collider.Tail); expectedTail.x *= -1;
            vrm.Vrm.Meta.Authors = new List<string> { "Mochiya test fixture" };
            var settings = ScriptableObject.CreateInstance<VRM10ExportSettings>(); objects.Add(settings); settings.FreezeMesh = freeze;
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".vrm");
            try
            {
                MochiyaLilToonExporter.ExportVrm(result.Root, path, vrm.Vrm.Meta, settings);
                var bytes = File.ReadAllBytes(path);
                var json = System.Text.Encoding.UTF8.GetString(bytes, 20, BitConverter.ToInt32(bytes, 12));
                var document = JsonUtility.FromJson<Document>(json);
                var exported = document.extensions.VRMC_springBone.springs[0].joints[0];
                Assert.That(exported.gravityDir, Is.EqualTo(new[] { 0f, -1f, 0f }).Within(.00001f));
                Assert.That(exported.gravityPower, Is.EqualTo(.5f));
                Assert.That(joint.m_gravityDir, Is.EqualTo(Vector3.down));
                Assert.That(Quaternion.Angle(root.localRotation, Quaternion.Euler(130, 25, 15)), Is.LessThan(.001f));
                var shape = document.extensions.VRMC_springBone.colliders[0];
                var matrix = WorldMatrix(document.nodes, shape.node);
                var offset = shape.shape.capsule.offset; var tail = shape.shape.capsule.tail;
                Assert.That(Vector3.Distance(matrix.MultiplyPoint3x4(new Vector3(offset[0], offset[1], offset[2])), expectedOffset), Is.LessThan(.00001f));
                Assert.That(Vector3.Distance(matrix.MultiplyPoint3x4(new Vector3(tail[0], tail[1], tail[2])), expectedTail), Is.LessThan(.00001f));
                Assert.That(collider.Tail, Is.EqualTo(new Vector3(.04f, .4f, .05f)));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}

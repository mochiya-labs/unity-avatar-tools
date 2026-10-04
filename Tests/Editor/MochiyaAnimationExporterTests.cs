using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UniGLTF;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mochiya.AvatarTools.Editor.Tests
{
    public sealed class MochiyaAnimationExporterTests
    {
        internal static AnimationClip Clip(float duration)
        {
            var clip = new AnimationClip { name = "Humanoid test", frameRate = 30 };
            void Curve(string name, float start, float end) => AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Animator), name),
                duration == 0 ? new AnimationCurve(new Keyframe(0, start)) : AnimationCurve.Linear(0, start, duration, end));
            Curve("Spine Front-Back", .1f, .4f);
            Curve("RootT.x", .1f, .3f);
            Curve("RootT.y", 1, .8f);
            Curve("RootT.z", .2f, .6f);
            Curve("RootQ.x", 0, 0); Curve("RootQ.y", 0, .3826834f);
            Curve("RootQ.z", 0, 0); Curve("RootQ.w", 1, .9238795f);
            return clip;
        }
        private static glTF Parse(byte[] bytes) => GltfDeserializer.Deserialize(UniJSON.JsonParser.Parse(
            Encoding.UTF8.GetString(bytes, 20, BitConverter.ToInt32(bytes, 12))));
        private static float[] Values(byte[] bytes, glTF gltf, int accessorId)
        {
            var accessor = gltf.accessors[accessorId]; var view = gltf.bufferViews[accessor.bufferView.Value];
            var width = accessor.type == "VEC4" ? 4 : accessor.type == "VEC3" ? 3 : 1;
            var offset = 28 + BitConverter.ToInt32(bytes, 12) + view.byteOffset + (accessor.byteOffset ?? 0);
            return Enumerable.Range(0, accessor.count * width).Select(i => BitConverter.ToSingle(bytes, offset + 4 * i)).ToArray();
        }

        [TestCase(0f)] [TestCase(.071f)] [TestCase(1f)]
        public void ExportsValidReferenceAndEveryEndpointWithUnitySampledTransforms(float duration)
        {
            var clip = Clip(duration);
            try
            {
                Assert.That(clip.isHumanMotion, Is.True);
                var before = EditorJsonUtility.ToJson(clip);
                var bytes = MochiyaAnimationExporter.Create(clip);
                var gltf = Parse(bytes);
                var hips = gltf.nodes.FindIndex(n => n.name == "Hips");
                Assert.That(gltf.nodes[hips].translation, Is.EqualTo(new[] { 0f, 1f, 0f }).Within(.00001f));
                var animation = gltf.animations.Single();
                var times = Values(bytes, gltf, animation.samplers[0].input);
                Assert.That(times.First(), Is.Zero);
                Assert.That(times.Last(), Is.EqualTo(duration).Within(.000001));
                Assert.That(times.Length, Is.EqualTo(duration == 0 ? 1 : Math.Ceiling((double)duration * 30) + 1));
                for (int i = 1; i < times.Length; i++) Assert.That(times[i], Is.GreaterThan(times[i - 1]));
                // Independent sampling instance, comparing both positions and every bone rotation.
                var scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    using (var rig = new MochiyaAnimationRig(scene))
                    {
                        for (int i = 0; i < times.Length; i++)
                        {
                            clip.SampleAnimation(rig.Root, times[i]);
                            foreach (var channel in animation.channels)
                            {
                                var values = Values(bytes, gltf, animation.samplers[channel.sampler].output);
                                var name = gltf.nodes[channel.target.node].name;
                                var bone = (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), name);
                                var node = rig.Bones[bone];
                                if (channel.target.path == "translation")
                                {
                                    var actual = new Vector3(values[i * 3], values[i * 3 + 1], values[i * 3 + 2]);
                                    var p = node.position;
                                    Assert.That(Vector3.Distance(actual, new Vector3(-p.x, p.y, p.z)), Is.LessThan(.00001f));
                                    Assert.That(actual.magnitude, Is.LessThan(5), "Hips must remain human-sized.");
                                }
                                else
                                {
                                    var q = bone == HumanBodyBones.Hips ? node.rotation : Quaternion.Inverse(rig.Parent(bone).rotation) * node.rotation;
                                    var actual = new Quaternion(values[i * 4], values[i * 4 + 1], values[i * 4 + 2], values[i * 4 + 3]);
                                    Assert.That(Quaternion.Angle(actual, new Quaternion(q.x, -q.y, -q.z, q.w)), Is.LessThan(.1f), name);
                                }
                            }
                        }
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
                Assert.That(EditorJsonUtility.ToJson(clip), Is.EqualTo(before));
                Assert.That(MochiyaAnimationExporter.Create(clip), Is.EqualTo(bytes), "Repeated exports must be deterministic.");
                Directory.CreateDirectory("MochiyaTests");
                File.WriteAllBytes("MochiyaTests/animation-" + (duration == 0 ? "pose" : "motion") + ".vrma", bytes);
            }
            finally { Object.DestroyImmediate(clip); }
        }

        [Test] public void CancellationReleasesAllTemporaryRigsAndAvatars()
        {
            var clip = Clip(1);
            try
            {
                int scenes = EditorSceneManager.previewSceneCount;
                int avatars = Resources.FindObjectsOfTypeAll<Avatar>().Length;
                int objects = Resources.FindObjectsOfTypeAll<GameObject>().Length;
                Assert.Throws<OperationCanceledException>(() => MochiyaAnimationExporter.Create(clip, p => { if (p > .2f) throw new OperationCanceledException(); }));
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(scenes));
                Assert.That(Resources.FindObjectsOfTypeAll<Avatar>().Length, Is.EqualTo(avatars));
                Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Length, Is.EqualTo(objects));
            }
            finally { Object.DestroyImmediate(clip); }
        }

        [TestCase(1f / 30f)] [TestCase(2f / 30f)] [TestCase(.0001f)]
        public void SamplingNeverDuplicatesAnEndpoint(float duration)
        {
            var times = MochiyaAnimationExporter.SampleTimes(duration);
            Assert.That(times.First(), Is.Zero);
            Assert.That(times.Last(), Is.EqualTo(duration));
            for (int i = 1; i < times.Length; i++) Assert.That(times[i], Is.GreaterThan(times[i - 1]));
        }

        [Test] public void RejectsGenericAndMixedObjectAnimation()
        {
            var generic = new AnimationClip(); var mixed = Clip(1);
            try
            {
                Assert.Throws<ArgumentException>(() => MochiyaAnimationExporter.Create(generic));
                AnimationUtility.SetEditorCurve(mixed, EditorCurveBinding.FloatCurve("Mesh", typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Linear(0, 0, 1, 1));
                Assert.Throws<ArgumentException>(() => MochiyaAnimationExporter.Create(mixed));
                Assert.Throws<ArgumentException>(() => MochiyaAnimationExporter.Export(mixed, "wrong.glb"));
            }
            finally { Object.DestroyImmediate(generic); Object.DestroyImmediate(mixed); }
        }

        [Test] public void ExportsOptionalCreatorClipWithoutAnAvatar()
        {
            var path = Environment.GetEnvironmentVariable("MOCHIYA_TEST_ANIMATION_CLIP");
            if (string.IsNullOrEmpty(path)) Assert.Ignore("Set MOCHIYA_TEST_ANIMATION_CLIP to a project AnimationClip asset for optional creator verification.");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            Assert.That(clip, Is.Not.Null);
            var before = EditorJsonUtility.ToJson(clip);
            var bytes = MochiyaAnimationExporter.Create(clip);
            var gltf = Parse(bytes);
            var hips = gltf.nodes.FindIndex(n => n.name == "Hips");
            Assert.That(gltf.nodes[hips].translation[1], Is.EqualTo(1).Within(.00001));
            var animation = gltf.animations.Single();
            foreach (var channel in animation.channels)
            {
                var values = Values(bytes, gltf, animation.samplers[channel.sampler].output);
                Assert.That(values.All(v => !float.IsNaN(v) && !float.IsInfinity(v)), Is.True);
                if (channel.target.path == "translation") Assert.That(values.All(v => Mathf.Abs(v) < 10), Is.True);
            }
            Assert.That(EditorJsonUtility.ToJson(clip), Is.EqualTo(before));
            Directory.CreateDirectory("MochiyaTests");
            File.WriteAllBytes("MochiyaTests/animation-creator.vrma", bytes);
        }
    }
}

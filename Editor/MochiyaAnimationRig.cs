using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UniVRM10;

namespace Mochiya.AvatarTools.Editor
{
    // Original procedural proportions; no model, mesh or model-derived reference pose is embedded.
    internal sealed class MochiyaAnimationRig : IDisposable
    {
        internal GameObject Root { get; }
        internal Animator Animator { get; private set; }
        internal readonly Dictionary<HumanBodyBones, Transform> Bones = new Dictionary<HumanBodyBones, Transform>();
        private Avatar avatar;

        internal MochiyaAnimationRig(Scene scene)
        {
            Root = new GameObject("MochiyaAnimationReference");
            SceneManager.MoveGameObjectToScene(Root, scene);
            try
            {
                Add(HumanBodyBones.Hips, new Vector3(0, 1, 0));
                Add(HumanBodyBones.Spine, new Vector3(0, .15f, 0));
                Add(HumanBodyBones.Chest, new Vector3(0, .15f, 0));
                Add(HumanBodyBones.UpperChest, new Vector3(0, .12f, 0));
                Add(HumanBodyBones.Neck, new Vector3(0, .12f, 0));
                Add(HumanBodyBones.Head, new Vector3(0, .12f, 0));
                Side("Left", -1);
                Side("Right", 1);
                var human = Bones.Select(pair => new HumanBone
                {
                    boneName = pair.Key.ToString(),
                    humanName = HumanTrait.BoneName[(int)pair.Key],
                    limit = new HumanLimit { useDefaultValues = true }
                }).ToArray();
                // Include the actual root and the actual hips rest transform in the Avatar definition.
                var skeleton = Root.GetComponentsInChildren<Transform>().Select(t => new SkeletonBone
                { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
                avatar = AvatarBuilder.BuildHumanAvatar(Root, new HumanDescription
                {
                    human = human, skeleton = skeleton,
                    armStretch = .05f, legStretch = .05f,
                    upperArmTwist = .5f, lowerArmTwist = .5f,
                    upperLegTwist = .5f, lowerLegTwist = .5f,
                    feetSpacing = 0, hasTranslationDoF = false
                });
                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                    throw new InvalidOperationException("Could not build the animation reference humanoid.");
                avatar.hideFlags = HideFlags.HideAndDontSave;
                Animator = Root.AddComponent<Animator>();
                Animator.avatar = avatar;
                Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            catch { Dispose(); throw; }
        }

        private void Add(HumanBodyBones bone, Vector3 position)
        {
            var node = new GameObject(bone.ToString()).transform;
            node.SetParent(Parent(bone), false);
            node.localPosition = position;
            Bones.Add(bone, node);
        }

        internal Transform Parent(HumanBodyBones bone)
        {
            if (bone == HumanBodyBones.Hips) return Root.transform;
            var vrmBone = Vrm10HumanoidBoneSpecification.ConvertFromUnityBone(bone);
            var parent = Vrm10HumanoidBoneSpecification.GetDefine(vrmBone).ParentBone.Value;
            return Bones[Vrm10HumanoidBoneSpecification.ConvertToUnityBone(parent)];
        }

        private void Side(string side, float sign)
        {
            void Bone(string name, float x, float y, float z = 0) =>
                Add((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + name), new Vector3(sign * x, y, z));
            Bone("UpperLeg", .1f, -.05f);
            Bone("LowerLeg", 0, -.43f);
            Bone("Foot", 0, -.43f);
            Bone("Toes", 0, -.09f, .14f);
            Bone("Shoulder", .08f, .08f);
            Bone("UpperArm", .1f, 0);
            Bone("LowerArm", .28f, 0);
            Bone("Hand", .25f, 0);
            Bone("ThumbProximal", .025f, 0, .035f);
            Bone("ThumbIntermediate", .025f, 0, .025f);
            Bone("ThumbDistal", .02f, 0, .015f);
            var fingers = new[] { "Index", "Middle", "Ring", "Little" };
            for (int i = 0; i < fingers.Length; i++)
            {
                Bone(fingers[i] + "Proximal", .07f, 0, .03f - .02f * i);
                Bone(fingers[i] + "Intermediate", .03f, 0);
                Bone(fingers[i] + "Distal", .02f, 0);
            }
        }

        public void Dispose()
        {
            if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
            if (avatar != null) UnityEngine.Object.DestroyImmediate(avatar);
        }
    }
}

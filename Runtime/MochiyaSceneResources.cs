using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniVRM10;

namespace Mochiya.AvatarComposition
{
    /// <summary>Serializes generated VRM objects inside the scene, without creating .asset files.</summary>
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class MochiyaSceneResources : MonoBehaviour
    {
        [Serializable] public sealed class ExpressionSnapshot { public ExpressionPreset Preset; public string Name; public string Json; }
        [SerializeField, HideInInspector] private string vrmJson;
        [SerializeField, HideInInspector] private string avatarJson;
        [SerializeField, HideInInspector] private List<ExpressionSnapshot> expressions = new List<ExpressionSnapshot>();
        [Serializable] private sealed class HumanEntry { public string BoneName; public string HumanName; }
        [Serializable] private sealed class SkeletonEntry { public string Name; public Vector3 Position; public Quaternion Rotation; public Vector3 Scale; }
        [Serializable] private sealed class HumanoidSnapshot { public HumanEntry[] Human; public SkeletonEntry[] Skeleton; }

        public void Capture()
        {
            var instance = GetComponent<Vrm10Instance>();
            if (instance != null && instance.Vrm != null)
            {
                vrmJson = JsonUtility.ToJson(instance.Vrm);
                expressions.Clear();
                foreach (var entry in instance.Vrm.Expression.Clips)
                    if (entry.Clip != null) expressions.Add(new ExpressionSnapshot {
                        Preset = entry.Preset, Name = entry.Clip.name, Json = JsonUtility.ToJson(entry.Clip) });
            }
            var animator = GetComponent<Animator>();
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                var description = animator.avatar.humanDescription;
                // The generated hierarchy has a new root name. AvatarBuilder requires that name in its skeleton description.
                avatarJson = JsonUtility.ToJson(new HumanoidSnapshot {
                    Human = description.human.Select(b => new HumanEntry { BoneName = b.boneName, HumanName = b.humanName }).ToArray(),
                    Skeleton = GetComponentsInChildren<Transform>(true).Select(t => new SkeletonEntry { Name = t.name, Position = t.localPosition, Rotation = t.localRotation, Scale = t.localScale }).ToArray()
                });
            }
        }

        public void RestoreIfNeeded()
        {
            var animator = GetComponent<Animator>();
            if (animator != null && animator.avatar == null && !string.IsNullOrEmpty(avatarJson))
            {
                var snapshot = JsonUtility.FromJson<HumanoidSnapshot>(avatarJson);
                var skeleton = snapshot.Skeleton.Select(b => new SkeletonBone { name = b.Name, position = b.Position, rotation = b.Rotation, scale = b.Scale }).ToArray();
                if (skeleton.Length > 0) skeleton[0].name = gameObject.name;
                animator.avatar = AvatarBuilder.BuildHumanAvatar(gameObject, new HumanDescription {
                    human = snapshot.Human.Select(b => new HumanBone { boneName = b.BoneName, humanName = b.HumanName, limit = new HumanLimit { useDefaultValues = true } }).ToArray(),
                    skeleton = skeleton, upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f, armStretch = .05f, legStretch = .05f
                });
            }
            var instance = GetComponent<Vrm10Instance>();
            if (instance == null || instance.Vrm != null || string.IsNullOrEmpty(vrmJson)) return;
            instance.Vrm = ScriptableObject.CreateInstance<VRM10Object>();
            JsonUtility.FromJsonOverwrite(vrmJson, instance.Vrm);
            instance.Vrm.Expression = new VRM10ObjectExpression();
            foreach (var entry in expressions)
            {
                var clip = ScriptableObject.CreateInstance<VRM10Expression>();
                JsonUtility.FromJsonOverwrite(entry.Json, clip);
                clip.name = entry.Name;
                instance.Vrm.Expression.AddClip(entry.Preset, clip);
            }
        }
        private void OnEnable() => RestoreIfNeeded();
    }
}

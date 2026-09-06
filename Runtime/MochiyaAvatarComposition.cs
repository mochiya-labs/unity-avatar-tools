using System;
using System.Collections.Generic;
using UnityEngine;
using UniVRM10;

namespace Mochiya.AvatarComposition
{
    public enum AssetKind { Avatar = 0, Attachment = 1 }
    public enum ActionKind { MorphSync, MorphOverride, NodeActive, MaterialSwap, ColliderLink }

    [Serializable]
    public sealed class AssetSelector
    {
        public bool Base;
        public Transform Node;
        public string[] BoneKeywords = Array.Empty<string>();
        public string[] MeshKeywords = Array.Empty<string>();
        public string[] NodeKeywords = Array.Empty<string>();
        public string[] BlendshapeKeywords = Array.Empty<string>();
        public string[] ParentKeywords = Array.Empty<string>();
        public int MorphIndex = -1;
        public string HumanBone;
    }

    [Serializable]
    public sealed class AssetCondition
    {
        public Transform Node;
        public bool Inverse;
        public string Control;
        public float Value = 1;
    }

    [Serializable]
    public sealed class AssetAction
    {
        public string Id;
        public ActionKind Kind;
        public int SourceOrder;
        public bool UseCondition;
        public AssetCondition Condition;
        public AssetSelector Target = new AssetSelector();
        public AssetSelector Driver = new AssetSelector();
        public AnimationCurve Curve = AnimationCurve.Linear(0, 0, 1, 1);
        public float Value;
        public bool Active;
        public int MaterialSlot;
        public Material Material;
    }

    [Serializable]
    public sealed class AssetJointMapping
    {
        public Transform Source;
        public AssetSelector Target = new AssetSelector { Base = true };
        public bool Attachment;
        public bool Snap;
    }

    [Serializable]
    public sealed class AssetControl
    {
        public string Id;
        public string Label;
        public float DefaultValue;
        public float Min;
        public float Max = 1;
    }

    [Serializable]
    public sealed class AssetNodeState
    {
        public Transform Node;
        public bool Active = true;
        public string[] Aliases = Array.Empty<string>();
    }

    /// <summary>Scene-owned authoring data. No VRChat, Modular Avatar or lilToon assembly is required.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Mochiya/Avatar Composition")]
    public sealed class MochiyaAvatarComposition : MonoBehaviour, ISerializationCallbackReceiver
    {
        public AssetKind Kind;
        public string[] ArmatureKeywords = Array.Empty<string>();
        public List<AssetJointMapping> Joints = new List<AssetJointMapping>();
        public List<AssetAction> Actions = new List<AssetAction>();
        public List<AssetControl> Controls = new List<AssetControl>();
        public List<AssetNodeState> Nodes = new List<AssetNodeState>();
        [HideInInspector] public string ConversionReport;

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize()
        {
            // Existing scenes/prefabs stored Outfit as 1 and Accessory as 2.
            // Both are now attachments; keep Avatar's serialized value unchanged.
            if ((int)Kind == 2) Kind = AssetKind.Attachment;
        }
    }
}

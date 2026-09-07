using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mochiya.AvatarComposition
{
    public enum AssetKind { Avatar = 0, Attachment = 1 }
    public enum ComponentKind { MergeArmature, BoneProxy, ShapeChanger, BlendshapeSync, ObjectToggle, MaterialSetter, MenuItem }
    public enum ShapeChangeType { Set, Delete }
    public enum PositionLockMode { Unidirectional, Bidirectional, NotLocked }
    public enum ProxyAttachmentMode { KeepWorldPose, AtRoot, KeepPosition, KeepRotation }
    public enum MenuControlType { Toggle, Button }
    public enum ComponentOrigin { ModularAvatar, ReferenceRig, ColliderAnchor, Authored }

    [Serializable]
    public sealed class AssetSelector
    {
        public bool Base;
        public Transform Node;
        public string[] Path;
        public bool BaseRoot;
        public string[] BoneKeywords = Array.Empty<string>();
        public string[] MeshKeywords = Array.Empty<string>();
        public string[] NodeKeywords = Array.Empty<string>();
        public string[] BlendshapeKeywords = Array.Empty<string>();
        public string[] ParentKeywords = Array.Empty<string>();
        public int MorphIndex = -1;
        public string HumanBone;
        public string[] HumanBonePath;
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
    public sealed class AssetEntry
    {
        public AssetSelector Target = new AssetSelector();
        public AssetSelector Driver = new AssetSelector();
        public AnimationCurve Curve = AnimationCurve.Linear(0, 0, 1, 1);
        public ShapeChangeType ChangeType;
        public float Value;
        public bool Active;
        public int MaterialSlot;
        public Material Material;
    }
    [Serializable]
    public sealed class AssetComponent
    {
        public string Id;
        public ComponentKind Kind;
        public Transform Source;
        public ComponentOrigin Origin = ComponentOrigin.Authored;
        public bool UseCondition;
        public AssetCondition Condition;
        public List<AssetEntry> Entries = new List<AssetEntry>();
        [Min(0)] public float Threshold = 0.01f;
        public AssetSelector Target = new AssetSelector { Base = true };
        public string Prefix = "", Suffix = "";
        public PositionLockMode LockMode = PositionLockMode.Unidirectional;
        public bool MangleNames = true;
        public ProxyAttachmentMode AttachmentMode;
        public bool MatchScale;
        public string Label, Parameter;
        public MenuControlType ControlType;
        public float Value = 1, DefaultValue;
        public bool Automatic = true;
    }
    [Serializable]
    public sealed class AssetNodeState
    {
        public Transform Node;
        public bool Active = true;
        public string[] Aliases = Array.Empty<string>();
    }
    /// <summary>Portable MA-style instructions. Resolved bone pairs belong to the consuming runtime.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Mochiya/Avatar Composition")]
    public sealed class MochiyaAvatarComposition : MonoBehaviour
    {
        public AssetKind Kind;
        public string[] ArmatureKeywords = Array.Empty<string>();
        public List<AssetComponent> Components = new List<AssetComponent>();
        public List<AssetNodeState> Nodes = new List<AssetNodeState>();
        [HideInInspector] public string ConversionReport;
    }
}

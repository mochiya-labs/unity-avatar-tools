using System;
using System.Collections.Generic;
using System.Linq;
using Mochiya.AvatarComposition;
using UniGLTF;
using UniJSON;
using UnityEngine;

namespace Mochiya.LilToon.Exporter.Editor
{
    internal static class MochiyaAvatarCompositionSerializer
    {
        public const string ExtensionName = "MOCHIYA_avatar_composition";
        public static IEnumerable<Material> ExtraMaterials(MochiyaAvatarComposition asset) => asset == null ? Enumerable.Empty<Material>() : asset.Actions.Where(a => a.Kind == ActionKind.MaterialSwap && a.Material != null).Select(a => a.Material).Distinct();

        // Only called on the private export copy. Inactive variants must remain in the file for portable toggles.
        public static void PrepareCopy(GameObject root)
        {
            root.GetComponent<MochiyaSceneResources>()?.RestoreIfNeeded();
            var instance = root.GetComponent<UniVRM10.Vrm10Instance>();
            if (instance != null) instance.UpdateType = UniVRM10.Vrm10Instance.UpdateTypes.None;
            var asset = root.GetComponent<MochiyaAvatarComposition>();
            if (asset == null) return;
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                if (node == root.transform) continue;
                var state = asset.Nodes.FirstOrDefault(x => x.Node == node);
                if (state == null) { state = new AssetNodeState { Node = node, Aliases = new[] { node.name } }; asset.Nodes.Add(state); }
                var renderer = node.GetComponent<Renderer>();
                state.Active = node.gameObject.activeSelf && (renderer == null || renderer.enabled);
                node.gameObject.SetActive(true);
                if (renderer != null) renderer.enabled = true;
            }
            root.SetActive(true);
        }
        public static void Attach(glTF gltf, MochiyaAvatarComposition asset, IReadOnlyDictionary<Transform, int> nodes, IList<Material> materials)
        {
            if (asset == null) return;
            int Node(Transform node)
            {
                if (node == null || !nodes.TryGetValue(node, out var index) || index < 0 || index >= gltf.nodes.Count)
                    throw new InvalidOperationException("A Mochiya local target did not survive export. Check root targets, mesh pruning and reference scope.");
                return index;
            }
            var f = new JsonFormatter();
            void Text(string key, string value) { f.Key(key); f.Value(value); }
            void Int(string key, int value) { f.Key(key); f.Value(value); }
            void Float(string key, float value) { if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Mochiya values must be finite."); f.Key(key); f.Value(value); }
            void Bool(string key, bool value) { f.Key(key); f.Value(value); }
            void Words(string key, IEnumerable<string> words)
            {
                var values = words?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
                if (values == null || values.Length == 0) return;
                f.Key(key); f.BeginList(); foreach (var word in values) f.Value(word); f.EndList();
            }
            void Selector(string key, AssetSelector selector)
            {
                f.Key(key); f.BeginMap(); Text("asset", selector.Base ? "base" : "self");
                if (!selector.Base)
                {
                    var index = Node(selector.Node); Int("node", index);
                    if (selector.MorphIndex >= 0)
                    {
                        var meshIndex = gltf.nodes[index].mesh;
                        var shape = selector.BlendshapeKeywords.FirstOrDefault();
                        var mesh = meshIndex < 0 ? null : gltf.meshes[meshIndex];
                        var readable = mesh == null ? null : new glTFMesh { extras = mesh.extras is glTFExtensionExport exported ? exported.Deserialize() : mesh.extras, primitives = mesh.primitives };
                        if (readable == null || !gltf_mesh_extras_targetNames.TryGet(readable, out var names))
                            throw new InvalidOperationException("A local morph target mesh was removed by the exporter.");
                        var exportedIndex = names.ToList().IndexOf(shape);
                        if (exportedIndex < 0) throw new InvalidOperationException($"Local morph '{shape}' was removed by the exporter. Exported names: [{string.Join(", ", names)}]. Preserve blendshapes in export settings.");
                        Int("morphIndex", exportedIndex);
                    }
                }
                Words("boneKeywords", selector.BoneKeywords); Words("meshKeywords", selector.MeshKeywords);
                Words("nodeKeywords", selector.NodeKeywords); Words("blendshapeKeywords", selector.BlendshapeKeywords);
                Words("parentKeywords", selector.ParentKeywords);
                if (!string.IsNullOrEmpty(selector.HumanBone)) Text("humanBone", selector.HumanBone);
                f.EndMap();
            }
            var kinds = new Dictionary<ActionKind, string> { [ActionKind.MorphSync] = "morph.sync", [ActionKind.MorphOverride] = "morph.override", [ActionKind.NodeActive] = "node.active", [ActionKind.MaterialSwap] = "material.swap", [ActionKind.ColliderLink] = "collider.link" };
            f.BeginMap(); Text("specVersion", "0.1"); Text("assetKind", asset.Kind.ToString().ToLowerInvariant());
            Words("requiredCapabilities", asset.Actions.Select(x => kinds[x.Kind]).Concat(asset.Joints.Count > 0 ? new[] { "rig.bind", "rig.attach" } : Array.Empty<string>()).Concat(asset.Controls.Count > 0 ? new[] { "control" } : Array.Empty<string>()));
            f.Key("matching"); f.BeginMap(); Text("mode", "keywordBestEffort"); Text("onUnresolved", "warnAndContinue"); Words("armatureKeywords", asset.ArmatureKeywords); f.EndMap();
            f.Key("rig"); f.BeginMap(); Text("role", asset.Kind == AssetKind.Avatar ? "avatar" : "attachmentReference");
            foreach (var attachments in new[] { false, true })
            {
                f.Key(attachments ? "attachmentRoots" : "jointMappings"); f.BeginList();
                foreach (var joint in asset.Joints.Where(x => x.Attachment == attachments))
                { f.BeginMap(); Int("sourceNode", Node(joint.Source)); Selector("target", joint.Target); if (attachments) Text("mode", joint.Snap ? "snap" : "preserveWorld"); f.EndMap(); }
                f.EndList();
            }
            f.EndMap();
            f.Key("nodes"); f.BeginList();
            foreach (var node in asset.Nodes.Where(x => x.Node != null && nodes.ContainsKey(x.Node) && nodes[x.Node] >= 0))
            { f.BeginMap(); Int("node", Node(node.Node)); Words("aliases", node.Aliases); Bool("active", node.Active); f.EndMap(); }
            f.EndList();
            f.Key("controls"); f.BeginList();
            foreach (var control in asset.Controls)
            { f.BeginMap(); Text("id", control.Id); Text("label", control.Label ?? control.Id); Float("defaultValue", control.DefaultValue); Float("min", control.Min); Float("max", control.Max); f.EndMap(); }
            f.EndList();
            f.Key("actions"); f.BeginList();
            foreach (var action in asset.Actions)
            {
                f.BeginMap(); Text("id", action.Id); Text("type", kinds[action.Kind]); Int("sourceOrder", action.SourceOrder);
                if (action.UseCondition && action.Condition != null)
                {
                    f.Key("condition"); f.BeginMap(); var control = !string.IsNullOrEmpty(action.Condition.Control);
                    Text("type", control ? "control" : "nodeActive"); Bool("inverse", action.Condition.Inverse);
                    if (control) { Text("control", action.Condition.Control); Float("value", action.Condition.Value); }
                    else { Text("asset", "self"); Int("node", Node(action.Condition.Node)); }
                    f.EndMap();
                }
                Selector(action.Kind == ActionKind.MorphSync ? "driven" : "target", action.Target);
                switch (action.Kind)
                {
                    case ActionKind.MorphSync:
                        Selector("driver", action.Driver);
                        f.Key("curve"); f.BeginMap(); Text("interpolation", "linear"); f.Key("points"); f.BeginList();
                        var curve = action.Curve ?? AnimationCurve.Linear(0, 0, 1, 1);
                        if (curve.length < 2) throw new InvalidOperationException("A sync curve requires two or more points.");
                        foreach (var key in curve.keys) { f.BeginList(); f.Value(key.time); f.Value(key.value); f.EndList(); }
                        f.EndList(); f.EndMap(); break;
                    case ActionKind.MorphOverride: Float("value", action.Value); break;
                    case ActionKind.NodeActive: Bool("value", action.Active); break;
                    case ActionKind.MaterialSwap:
                        var material = materials.IndexOf(action.Material); if (material < 0) throw new InvalidOperationException("An alternate material was not registered with the exporter.");
                        Int("slot", action.MaterialSlot); Int("material", material); break;
                    case ActionKind.ColliderLink: Selector("collider", action.Driver); break;
                }
                f.EndMap();
            }
            f.EndList(); f.EndMap();
            glTFExtensionExport.GetOrCreate(ref gltf.extensions).Add(ExtensionName, f.GetStore().Bytes);
            if (!gltf.extensionsUsed.Contains(ExtensionName)) gltf.extensionsUsed.Add(ExtensionName);
        }
    }

    internal sealed class MochiyaGltfExporter : gltfExporter
    {
        private readonly IMaterialExporter materialExporter;
        private readonly GltfExportSettings settings;
        public MochiyaGltfExporter(ExportingGltfData data, GltfExportSettings settings, IMaterialExporter materialExporter)
            : base(data, settings, progress: new EditorProgress(), animationExporter: new EditorAnimationExporter(), materialExporter: materialExporter, textureSerializer: new EditorTextureSerializer())
        { this.materialExporter = materialExporter; this.settings = settings; }
        public override void ExportExtensions(ITextureSerializer textureSerializer)
        {
            base.ExportExtensions(textureSerializer);
            var asset = Copy.GetComponent<MochiyaAvatarComposition>();
            foreach (var material in MochiyaAvatarCompositionSerializer.ExtraMaterials(asset))
            {
                if (Materials.Contains(material)) continue;
                Materials.Add(material); _gltf.materials.Add(materialExporter.ExportMaterial(material, TextureExporter, settings));
            }
            MochiyaAvatarCompositionSerializer.Attach(_gltf, asset, Nodes.Select((node, index) => (node, index)).ToDictionary(x => x.node, x => x.index), Materials);
        }
    }
}

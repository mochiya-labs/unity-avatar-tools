using System;
using System.Collections.Generic;
using System.Linq;
using Mochiya.AvatarComposition;
using UniGLTF;
using UniJSON;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor
{
    internal static class MochiyaAvatarCompositionSerializer
    {
        public const string ExtensionName = "MOCHIYA_avatar_composition";
        public static IEnumerable<Material> ExtraMaterials(MochiyaAvatarComposition asset) => asset == null ? Enumerable.Empty<Material>() : asset.Components.Where(c => c.Kind == ComponentKind.MaterialSetter).SelectMany(c => c.Entries).Where(a => a.Material != null).Select(a => a.Material).Distinct();

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
            var rootIndex = -1;
            int Node(Transform node)
            {
                // UniGLTF omits the scene container. Materialize an identity node only
                // when a component actually addresses it; existing indices stay stable.
                if (node == asset.transform && (!nodes.TryGetValue(node, out var root) || root < 0))
                {
                    if (rootIndex < 0)
                    {
                        rootIndex = gltf.nodes.Count;
                        var scene = gltf.scenes[gltf.scene];
                        gltf.nodes.Add(new glTFNode { name = asset.name, children = scene.nodes });
                        scene.nodes = new[] { rootIndex };
                    }
                    return rootIndex;
                }
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
                if (selector.Base && (selector.BaseRoot || selector.Path?.Length > 0)) { f.Key("path"); f.BeginList(); foreach (var segment in selector.Path ?? Array.Empty<string>()) f.Value(segment); f.EndList(); }
                if (!string.IsNullOrEmpty(selector.HumanBone)) Text("humanBone", selector.HumanBone);
                if (!string.IsNullOrEmpty(selector.HumanBone) && selector.HumanBonePath != null) { f.Key("humanBonePath"); f.BeginList(); foreach (var segment in selector.HumanBonePath) f.Value(segment); f.EndList(); }
                f.EndMap();
            }
            string Camel(object value) { var text = value.ToString(); return char.ToLowerInvariant(text[0]) + text.Substring(1); }
            f.BeginMap(); Text("specVersion", "0.1"); Text("assetKind", Camel(asset.Kind));
            Words("requiredCapabilities", asset.Components.Select(x => Camel(x.Kind)));
            f.Key("matching"); f.BeginMap(); Text("mode", "keywordBestEffort"); Text("onUnresolved", "warnAndContinue"); Words("armatureKeywords", asset.ArmatureKeywords); f.EndMap();
            f.Key("rig"); f.BeginMap(); Text("role", asset.Kind == AssetKind.Avatar ? "avatar" : "attachmentReference"); f.EndMap();
            f.Key("nodes"); f.BeginList();
            foreach (var node in asset.Nodes.Where(x => x.Node != null && nodes.ContainsKey(x.Node) && nodes[x.Node] >= 0))
            { f.BeginMap(); Int("node", Node(node.Node)); Words("aliases", node.Aliases); Bool("active", node.Active); f.EndMap(); }
            f.EndList();
            f.Key("components"); f.BeginList();
            foreach (var component in asset.Components)
            {
                f.BeginMap(); Text("id", component.Id); Text("type", Camel(component.Kind)); Int("sourceNode", Node(component.Source)); Text("origin", Camel(component.Origin));
                if (component.UseCondition && component.Condition != null)
                {
                    f.Key("condition"); f.BeginMap(); var control = !string.IsNullOrEmpty(component.Condition.Control);
                    Text("type", control ? "control" : "nodeActive"); Bool("inverse", component.Condition.Inverse);
                    if (control) { Text("control", component.Condition.Control); Float("value", component.Condition.Value); }
                    else { Text("asset", "self"); Int("node", Node(component.Condition.Node)); }
                    f.EndMap();
                }
                switch (component.Kind)
                {
                    case ComponentKind.MergeArmature:
                        Selector("target", component.Target); Text("prefix", component.Prefix ?? ""); Text("suffix", component.Suffix ?? ""); Text("lockMode", Camel(component.LockMode)); Bool("mangleNames", component.MangleNames); break;
                    case ComponentKind.BoneProxy:
                        Selector("target", component.Target); Text("attachmentMode", Camel(component.AttachmentMode)); Bool("matchScale", component.MatchScale); break;
                    case ComponentKind.MenuItem:
                        Text("label", component.Label ?? component.Id); Text("controlType", Camel(component.ControlType));
                        if (!string.IsNullOrEmpty(component.Parameter)) Text("parameter", component.Parameter);
                        Float("value", component.Value); Float("defaultValue", component.DefaultValue); Bool("automatic", component.Automatic); break;
                    default:
                        f.Key(component.Kind == ComponentKind.ShapeChanger ? "shapes" : component.Kind == ComponentKind.BlendshapeSync ? "bindings" : "objects"); f.BeginList();
                        foreach (var entry in component.Entries)
                        {
                            f.BeginMap(); Selector(component.Kind == ComponentKind.BlendshapeSync ? "driven" : "target", entry.Target);
                            switch (component.Kind)
                            {
                                case ComponentKind.BlendshapeSync:
                                    Selector("driver", entry.Driver);
                                    var curve = entry.Curve ?? AnimationCurve.Linear(0, 0, 1, 1);
                                    if (curve.length < 2) throw new InvalidOperationException("A sync curve requires two or more points.");
                                    f.Key("curve"); f.BeginMap(); Text("interpolation", "linear");
                                    f.Key("points"); f.BeginList(); foreach (var key in curve.keys) { f.BeginList(); f.Value(key.time); f.Value(key.value); f.EndList(); } f.EndList();
                                    f.EndMap(); break;
                                case ComponentKind.ShapeChanger: Text("changeType", Camel(entry.ChangeType)); Float("value", entry.Value); break;
                                case ComponentKind.ObjectToggle: Bool("value", entry.Active); break;
                                case ComponentKind.MaterialSetter:
                                    var material = materials.IndexOf(entry.Material); if (material < 0) throw new InvalidOperationException("An alternate material was not registered with the exporter.");
                                    Int("slot", entry.MaterialSlot); Int("material", material); break;
                            }
                            f.EndMap();
                        }
                        f.EndList(); break;
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

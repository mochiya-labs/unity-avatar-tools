[Getting started](../README.md) | [Documentation index](index.md)

# Compatibility and scripting reference

## Supported behavior and limits

**VRM** means `VRMC_vrm` unless another extension is named; **Mochiya** means `MOCHIYA_avatar_composition`.

| Output feature | Converted from | Main limitation |
| --- | --- | --- |
| VRM expressions | VRC visemes/eyelids; existing VRM expressions | VRC conversion covers five vowels and blink only. |
| VRM look-at | VRC eye rotations/View Position; existing VRM look-at | VRC eye ranges are approximated. |
| VRM first-person | Existing VRM settings or generated defaults | VRC-specific visibility is not translated. |
| Spring bones and colliders (`VRMC_springBone`) | VRC PhysBones/colliders; existing VRM physics | Approximate active-preset simulation and sphere/capsule collisions; no VRC interactions or angle limits. |
| Node constraints (`VRMC_node_constraint`) | Existing UniVRM constraints | No VRC constraint conversion. |
| Mochiya `mergeArmature` | MA Merge Armature; generated reference rig | Roots/settings are retained; web matching is unidirectional. Other lock modes warn and fall back. No Unity merge. |
| Mochiya `boneProxy` | MA Bone Proxy; external base-collider anchors | Keep World Pose or At Root in the web runtime; partial-pose/scale modes warn and keep world pose. |
| Mochiya `blendshapeSync` | MA Blendshape Sync | Grouped bindings with MA linear remap points; no synchronization chains or cycles. |
| Mochiya `shapeChanger` | MA Shape Changer | Preserves Set/Delete and distance threshold (`0.01` by default). The web runtime reversibly removes affected triangles. Exported morph frames or baked scale can change selection; export warns. Unavailable runtime geometry falls back to weight 0 with a warning. |
| Mochiya `objectToggle` | MA Object Toggle | Grouped visibility entries; no cyclic visibility rules. |
| Mochiya `materialSetter` | MA Material Setter | Grouped whole-material slot replacements. |
| Mochiya `menuItem` | MA Toggle/Button menu items | Automatic object activation and local parameter metadata; buttons are momentary. No Animator execution/networking/menu hierarchy. |
| lilToon materials (`MOCHIYA_materials_liltoon`) | All nine regular rendering modes; Normal/OnePass/TwoPass transparency and outlines | Extension 1.2 requires an updated runtime. Additional-light passes and Unity shadow parity remain deferred. 2D textures/UV0 only; dense animated fur has CPU cost in WebGL2. Lite, Multi, FurOnly and tessellation remain unsupported. |

Component records preserve source roots, settings, conditions and grouped entries; resolved bone pairs are computed by the web runtime. The `@mochiya/avatar-composition` specification defines the wire format and runtime rules. Each VRM retains its own spring/collider groups; cross-asset collider linking is outside this profile.

Other VRC/MA behavior, including Animator programs, Contacts and mesh processing, is omitted. Ordinary GLB omits VRM behavior. Texture export requires graphics-enabled Unity.

## Use from a script

Put this in an Editor script. `selected` is a scene GameObject, `path` ends in `.vrm` or `.glb`, and `profile` is an optional export profile.

```csharp
using Mochiya.AvatarTools.Editor;

var detected = MochiyaAvatarWorkflow.Detect(selected);
var report = MochiyaAvatarWorkflow.Validate(selected);
if (!report.CanConvert)
    throw new System.InvalidOperationException(string.Join("\n", report.Errors));

// Direct conversion, export and temporary-object cleanup:
var exportReport = MochiyaAvatarWorkflow.Export(selected, path, profile);

// Or keep an editable duplicate in the scene:
var converted = MochiyaAvatarWorkflow.ConvertToVrmGameObject(selected, profile);
MochiyaLilToonExporter.ExportWithProfile(converted.Root, path, profile);
```

Omit the profile argument or pass `null` for the bundled default. Reports contain errors, warnings and unsupported features. The explicit `ConvertAvatarInScene` and `ConvertAttachmentInScene` APIs enforce the same classification rules. Dispose a conversion result only to remove its duplicate. Use `MochiyaLilToonExporter.ExportGlb(prop, path)` for a general model without a humanoid.

For a Humanoid `AnimationClip`, use `MochiyaAnimationExporter.Export(clip, "motion.vrma")`. `MochiyaAnimationExporter.Create(clip)` returns the VRMA bytes without writing a file. Both accept an optional `Action<float>` progress callback; throwing `OperationCanceledException` from it cancels sampling and releases temporary resources. These APIs require Edit Mode and no reference avatar.

## Architecture and web playback

| Package | Responsibility |
| --- | --- |
| Modular Avatar | Authors attachment relationships and performs Unity-side merges when requested. |
| Mochiya Avatar Tools | Converts the selected asset, records supported MA intent, and adds Mochiya extensions during export. |
| UniVRM / UniGLTF | Provides standard VRM components, geometry/material conversion and VRM/GLB file writing. |
| `@pixiv/three-vrm` | Loads and updates standard VRM humanoids, expressions and spring bones in the browser. |
| [`three-liltoon`](https://github.com/mochiya-labs/three-liltoon) | Renders materials carrying `MOCHIYA_materials_liltoon`. |
| [`@mochiya/avatar-composition`](https://github.com/mochiya-labs/avatar-composition) | Reads `MOCHIYA_avatar_composition`, fits separate attachments by names, applies supported actions and restores the base on removal. |

The host enables rendering with `enableLilToon(renderer)` and registers both `AvatarCompositionLoaderPlugin` from `@mochiya/avatar-composition` and `enableLilToonVRM(new VRMLoaderPlugin(parser))` on a standard Three.js `GLTFLoader`. After loading completes, read `gltf.userData.avatar` to initialize the composition asset; do not access it inside another loader plugin’s hooks. Create `new AvatarComposition(base)` and call `composition.add(attachment)`; identities are generated automatically. Call `composition.update(delta)` once per frame. The [Avatar Composition example viewer](https://github.com/mochiya-labs/avatar-composition/tree/main/examples/viewer) demonstrates loading, authored-object controls and removal.

The composition extension uses component-based format. The selected export root starts active regardless of its Unity activation; child objects preserve their combined GameObject/renderer activation. The runtime treats a multi-material Unity object as one editable item. See the [Avatar Composition reference](https://github.com/mochiya-labs/avatar-composition/blob/main/specification/README.md) for activation and component behavior.

Both Mochiya extensions are optional additions to standard files. Ordinary viewers display standalone geometry and fallback materials without attachment actions. Matching uses bone, armature, mesh and blendshape names; missing names warn and skip only affected operations. It does not check base identity or reshape garments to fit different bodies.

Both converted avatars and attachments carry `MOCHIYA_avatar_composition`, with `assetKind: "avatar"` or `"attachment"`. Attachment exports use `rig.role: "attachmentReference"`. The composition extension stores MA-style component instructions.

## License

Mochiya Avatar Tools is [MIT-licensed](../LICENSE). UniVRM/UniGLTF (VRM Consortium) and lilToon (lilxyzw) are separate MIT-licensed projects whose source is not redistributed here. Models and textures retain their own licenses.

The animation exporter adapts [AnimationClipToVrmaSample](https://github.com/malaybaku/AnimationClipToVrmaSample), Copyright (c) 2023 Baku Dreameater, under the MIT License. Its copyright and complete license notice are retained in `Editor/MochiyaAnimationExporter.cs`. The reference skeleton is procedural; no source model or model-derived reference-pose asset is included.

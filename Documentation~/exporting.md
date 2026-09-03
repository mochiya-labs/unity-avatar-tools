# Exporting

## Editor window

Select the model or avatar root in the scene, then open **Mochiya > Export GLB or VRM with lilToon...**.

### GLB

Choose **Glb** for props, outfits, avatar parts, general models, or a model whose embedded Unity animation clips should be exported by UniGLTF. The window exposes UniGLTF's `GltfExportSettings`.

### VRM

Choose **Vrm** for a humanoid VRM 1.0 avatar. The root must have an `Animator` with a valid Humanoid avatar.

If the root is an imported or configured UniVRM avatar with a `Vrm10Instance`, the exporter uses its existing metadata. UniVRM also preserves its expressions, look-at, first-person, constraints, and spring bones. Otherwise, fill out the metadata shown in the window. VRM 1.0 requires a model name and at least one author.

The export operates on a temporary clone, so preparing the VRM humanoid data does not add components to the selected scene object.

The VRM panel mirrors UniVRM 0.131.2's implemented mesh export settings:

- **Morph Target Use Sparse** uses sparse glTF accessors for morph targets.
- **Freeze Mesh** bakes transform rotation and scale into the exported meshes.
- **Freeze Mesh Keep Rotation** preserves transform rotations while freezing the meshes.
- **Freeze Mesh Use Current Blend Shape Weight** uses the currently displayed blend shape weights as the frozen base shape.

The last two choices apply only when **Freeze Mesh** is enabled. For an attached `Vrm10Instance`, the exporter also uses UniVRM's geometry backup while freezing so spring-bone collider/joint geometry and the VRM look-at offset retain their world-space meaning.

## Validation

Errors block export. Warnings are informational.

The exporter blocks:

- specialized lilToon variants not supported by the web shader port;
- lilToon material textures that are not 2D glTF textures;
- VRM roots without a valid Humanoid avatar;
- missing required VRM metadata.

Non-lilToon materials are delegated unchanged to UniVRM. A model with no lilToon materials can still export, but it will not declare the Mochiya extension.

## Programmatic API

The Editor assembly exposes:

```csharp
using Mochiya.LilToon.Exporter.Editor;
using UniVRM10;
using UniGLTF;
using UnityEngine;

MochiyaLilToonExporter.ExportGlb(
    avatarRoot,
    outputPath,
    new GltfExportSettings());
```

For a VRM avatar with an attached `Vrm10Instance`:

```csharp
var vrmExportSettings = ScriptableObject.CreateInstance<VRM10ExportSettings>();
try
{
    vrmExportSettings.FreezeMesh = true;
    vrmExportSettings.FreezeMeshKeepRotation = false;
    vrmExportSettings.FreezeMeshUseCurrentBlendShapeWeight = true;

    MochiyaLilToonExporter.ExportVrm(
        avatarRoot,
        outputPath,
        meta: null,
        exportSettings: vrmExportSettings);
}
finally
{
    Object.DestroyImmediate(vrmExportSettings);
}
```

`vrmExportSettings` is UniVRM's own `VRM10ExportSettings` instance—the Mochiya package does not duplicate those options.

Passing `null` metadata makes the API use `avatarRoot`'s attached `Vrm10Instance.Vrm.Meta`. Pass a `VRM10ObjectMeta` explicitly for a plain Humanoid model.

The earlier `ExportVrm(root, path, meta, useSparseMorphTargets)` overload remains available for callers that do not need mesh freezing.

# Exporting

## Editor window

Select the model or avatar root in the scene, then open **Mochiya > Export GLB or VRM with lilToon...**.

### GLB

Choose **Glb** for props, outfits, avatar parts, general models, or a model whose embedded Unity animation clips should be exported by UniGLTF. The window exposes UniGLTF's `GltfExportSettings`.

### VRM

Choose **Vrm** for a humanoid VRM 1.0 avatar. The root must have an `Animator` with a valid Humanoid avatar.

If the root is an imported or configured UniVRM avatar with a `Vrm10Instance`, the exporter uses its existing metadata. UniVRM also preserves its expressions, look-at, first-person, constraints, and spring bones. Otherwise, fill out the metadata shown in the window. VRM 1.0 requires a model name and at least one author.

The export operates on a temporary clone, so preparing the VRM humanoid data does not add components to the selected scene object.

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
using UniGLTF;
using UnityEngine;

MochiyaLilToonExporter.ExportGlb(
    avatarRoot,
    outputPath,
    new GltfExportSettings());
```

For a VRM avatar with an attached `Vrm10Instance`:

```csharp
MochiyaLilToonExporter.ExportVrm(
    avatarRoot,
    outputPath,
    meta: null,
    useSparseMorphTargets: true);
```

Passing `null` metadata makes the API use `avatarRoot`'s attached `Vrm10Instance.Vrm.Meta`. Pass a `VRM10ObjectMeta` explicitly for a plain Humanoid model.

# Mochiya lilToon Exporter for Unity

An Editor-only Unity package that exports `.glb` models and VRM 1.0 `.vrm` avatars containing the `MOCHIYA_materials_liltoon` glTF material extension used by [`three-liltoon`](../three-liltoon/README.md).

The package is intentionally a companion to UniVRM, not a fork. UniGLTF/UniVRM continue to own meshes, skins, morph targets, animations, textures, VRM humanoid data, expressions, look-at, first-person settings, and spring bones. This package adds only:

- lilToon material validation;
- lilToon property and texture serialization;
- root `extensionsUsed` registration;
- a small export window and programmatic API.

## Requirements

- Unity 2022.3 LTS or newer
- lilToon 2.3.4
- UniVRM 0.131.2 packages `com.vrmc.gltf` and `com.vrmc.vrm`
- `three-liltoon` 0.1.0 or newer on the web

## Quick start

1. Install lilToon and UniVRM, then install this folder as a local Unity package.
2. Put the model or avatar in a scene and select its root.
3. Open **Mochiya > Export GLB or VRM with lilToon...**.
4. Choose GLB for a general model or VRM for a humanoid avatar.
5. For VRM, optionally enable **Freeze Mesh**, **Freeze Mesh Keep Rotation**, or **Freeze Mesh Use Current Blend Shape Weight**, matching UniVRM's VRM 1.0 exporter.
6. Resolve any blocking material or avatar validation errors and export.

VRM export reuses metadata and avatar behavior from an attached `Vrm10Instance` when present. A plain humanoid can also be exported after entering VRM 1.0 metadata in the window.
Mesh freezing uses UniVRM's own `BoneNormalizer` and VRM geometry backup, so look-at and spring-bone coordinates remain valid after transforms are baked.

See [package documentation](Documentation~/index.md) for installation, supported shaders, the extension schema, web loading, and the public API.

## Scope

The first release supports the regular lilToon shader's opaque, cutout, transparent, and outline variants. Specialized variants fail validation instead of silently degrading. See [compatibility](Documentation~/compatibility.md).

This is an unofficial Mochiya integration. `MOCHIYA_materials_liltoon` is not a Khronos, VRM Consortium, or lilToon standard.

# Mochiya lilToon Exporter for Unity

An Editor-only Unity package that exports `.glb` models and VRM 1.0 `.vrm` avatars containing the `MOCHIYA_materials_liltoon` glTF material extension used by [`three-liltoon`](https://github.com/zekailin00/three-liltoon).

The package is intentionally a companion to UniVRM, not a fork. UniGLTF/UniVRM continue to own meshes, skins, morph targets, animations, textures, VRM humanoid data, expressions, look-at, first-person settings, and spring bones. This package adds only:

- lilToon material validation;
- lilToon property and texture serialization;
- root `extensionsUsed` registration;
- a small export window and programmatic API.

## Requirements

- Unity 2022.3 LTS or newer
- lilToon 2.3.4 or newer
- UniVRM 0.131.2 or newer, including `com.vrmc.gltf` and `com.vrmc.vrm`
- `three-liltoon` 0.1.0 or newer on the web

## Installation

### 1. Install lilToon

Install `jp.lilxyzw.liltoon` 2.3.4 or newer through the official lilToon VPM repository or release package.

### 2. Install UniVRM

Install UniVRM 0.131.2 or newer. The following `Packages/manifest.json` example pins the minimum supported version:

```json
{
  "dependencies": {
    "com.vrmc.gltf": "https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.2",
    "com.vrmc.vrm": "https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.2"
  }
}
```

These entries belong in the consuming project's manifest. Unity supports Git dependencies for projects, but not Git dependencies between packages, so the exporter cannot install these Git sources automatically from its own `package.json`.

To use a newer UniVRM release, replace both `v0.131.2` revisions with the same newer release tag.

### 3. Install the Mochiya exporter from Git

In Unity, open **Window > Package Manager**, select **+ > Install package from git URL**, and enter:

```text
https://github.com/zekailin00/liltoon-unity-exporter.git
```

The same setup can be written directly in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.vrmc.gltf": "https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.2",
    "com.vrmc.vrm": "https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.2",
    "org.mochiya.liltoon-exporter": "https://github.com/zekailin00/liltoon-unity-exporter.git"
  }
}
```

This URL installs the current version from the repository's default branch. Unity records the resolved commit in the project's `Packages/packages-lock.json`.

To install from a local clone, choose **+ > Add package from disk...** and select the clone's `package.json`, or use:

```json
{
  "dependencies": {
    "org.mochiya.liltoon-exporter": "file:../../mochiya-liltoon-unity"
  }
}
```

Adjust the relative path from the Unity project's `Packages` directory.

## Quick start

1. Complete the installation steps above.
2. Put the model or avatar in a scene and select its root.
3. Open **Mochiya > Export GLB or VRM with lilToon...**.
4. Choose GLB for a general model or VRM for a humanoid avatar.
5. For VRM, optionally enable **Freeze Mesh**, **Freeze Mesh Keep Rotation**, or **Freeze Mesh Use Current Blend Shape Weight**, matching UniVRM's VRM 1.0 exporter.
6. Resolve any blocking material or avatar validation errors and export.

VRM export reuses metadata and avatar behavior from an attached `Vrm10Instance` when present. A plain humanoid can also be exported after entering VRM 1.0 metadata in the window.
Mesh freezing uses UniVRM's own `BoneNormalizer` and VRM geometry backup, so look-at and spring-bone coordinates remain valid after transforms are baked.

See [package documentation](Documentation~/index.md) for installation, supported shaders, the extension schema, web loading, and the public API.

## Scope

The current alpha supports the regular lilToon shader's opaque, cutout, transparent, and outline variants. Specialized variants fail validation instead of silently degrading. See [compatibility](Documentation~/compatibility.md).

This is an unofficial Mochiya integration. `MOCHIYA_materials_liltoon` is not a Khronos, VRM Consortium, or lilToon standard.

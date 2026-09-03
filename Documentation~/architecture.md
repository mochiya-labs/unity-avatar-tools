# Architecture

## Responsibility split

### UniGLTF and UniVRM

- scene hierarchy and coordinate conversion;
- meshes, skins, morph targets, and ordinary animations;
- image conversion and GLB packing;
- VRM 1.0 humanoid, meta, expressions, look-at, first-person, constraints, and spring bones;
- normal glTF material fallback.

### Mochiya Unity exporter

- identify supported lilToon materials;
- validate that the Three.js implementation can reproduce their shader variant;
- enumerate Unity shader properties and textures;
- register textures through UniVRM's `ITextureExporter`;
- reuse UniVRM's `VRM10ExportSettings` and its Editor inspector directly;
- optionally freeze VRM meshes through UniVRM's `BoneNormalizer` and `Vrm10GeometryBackup`;
- attach `MOCHIYA_materials_liltoon` to each material;
- add the extension name to root `extensionsUsed` only when used.

### three-liltoon

- register as a `GLTFLoader` plugin;
- detect the material extension;
- resolve glTF texture indices;
- construct `LilToonMaterial` and its render passes;
- leave ordinary glTF and VRM behavior to Three.js and `@pixiv/three-vrm`.

This boundary keeps the Unity package small and avoids maintaining a fork of UniVRM. It also lets UniVRM and `three-vrm` evolve independently from the Mochiya-specific material payload.

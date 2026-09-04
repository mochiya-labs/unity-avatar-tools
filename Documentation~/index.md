# Mochiya lilToon Exporter

This package creates web-ready GLB models and VRM 1.0 avatars whose lilToon materials can be reconstructed by `three-liltoon`.

## Documentation

- [Installation](installation.md)
- [Exporting models and avatars](exporting.md)
- [Architecture and ownership](architecture.md)
- [Material extension format](material-format.md)
- [Loading in Three.js](web-loading.md)
- [Compatibility and limitations](compatibility.md)

## Output contract

Each supported Unity lilToon material receives `extensions.MOCHIYA_materials_liltoon`. Its original shader property names and registered texture indices are stored in that payload. The file also keeps a normal glTF PBR fallback material, so software that does not understand the Mochiya extension can still show a basic color and main texture.

For a `.vrm` output, the same binary GLB container also includes the standard VRM 1.0 extensions emitted by UniVRM. The custom material extension does not replace `VRMC_vrm` or alter the file's VRM animation and avatar-behavior data.

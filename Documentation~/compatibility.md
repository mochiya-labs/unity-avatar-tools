# Compatibility

## Supported

The exporter and `three-liltoon` currently target the regular lilToon shader family:

- `lilToon`
- `Hidden/lilToonOutline`
- `Hidden/lilToonCutout`
- `Hidden/lilToonCutoutOutline`
- `Hidden/lilToonTransparent`
- `Hidden/lilToonTransparentOutline`

These cover opaque, alpha-cutout, normal transparent, and optional outline rendering.

## Blocked variants

The exporter rejects these families because exporting their properties without matching web render passes would produce false fidelity:

- lilToon Lite
- lilToon Multi
- one-pass and two-pass transparency
- refraction and refraction blur
- fur and fur-only
- gem
- tessellation
- overlay and outline-only
- fake shadow

Convert a material to the regular lilToon shader before export, or extend both `three-liltoon` and this validator/serializer in the same change.

## Other constraints

- Only 2D material textures can be represented by core glTF texture objects.
- Material UV channel selection is currently UV0.
- The extension reproduces shader properties, not Unity-specific scene lighting, probes, cameras, or post-processing.
- The package targets lilToon 2.3.4 and UniVRM 0.131.2.
- Viewers without `three-liltoon` see the embedded base-color/main-texture PBR fallback, not the lilToon look.

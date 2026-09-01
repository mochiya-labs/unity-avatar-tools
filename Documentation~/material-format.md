# Material extension format

Extension name: `MOCHIYA_materials_liltoon`

The extension is attached to each supported glTF material and listed once in the document's root `extensionsUsed` array.

```json
{
  "materials": [
    {
      "name": "Body",
      "pbrMetallicRoughness": {
        "baseColorFactor": [1, 1, 1, 1],
        "baseColorTexture": { "index": 0 }
      },
      "extensions": {
        "MOCHIYA_materials_liltoon": {
          "specVersion": "1.0",
          "lilToonVersion": "2.3.4",
          "shaderVariant": "Hidden/lilToonOutline",
          "renderMode": "opaque",
          "properties": {
            "_Color": [1, 1, 1, 1],
            "_UseShadow": 1,
            "_UseOutline": 1,
            "_OutlineWidth": 0.04,
            "_MainTex_ST": [1, 1, 0, 0]
          },
          "textures": {
            "_MainTex": { "index": 0, "texCoord": 0 },
            "_BumpMap": { "index": 1, "texCoord": 0 }
          }
        }
      }
    }
  ],
  "extensionsUsed": ["MOCHIYA_materials_liltoon"]
}
```

## Fields

- `specVersion`: Mochiya payload version. Current value: `1.0`.
- `lilToonVersion`: detected Unity lilToon package version, or `unknown` for legacy asset-folder installations.
- `shaderVariant`: exact Unity shader name used by the material.
- `renderMode`: `opaque`, `cutout`, or `transparent`.
- `properties`: all non-texture properties exposed by the Unity shader, using original lilToon names. Colors are stored as linear RGBA vectors.
- `textures`: assigned texture properties mapped to glTF texture indices. `texCoord` is currently `0`.

Texture scale and offset are emitted as the corresponding lilToon `_ST` vector after Unity-to-glTF vertical UV conversion. Textures with a Unity `[Normal]` attribute use UniVRM's normal-map conversion. Mask, bump, normal, noise, dither, parallax, UDIM, and AudioLink-named textures are exported as linear data; other textures use sRGB.

The extension is optional and is not placed in `extensionsRequired`. Unknown readers can use the PBR fallback.

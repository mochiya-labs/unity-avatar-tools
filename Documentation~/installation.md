[Getting started](../README.md) | [Documentation index](index.md)

# Installation reference

## Installation

Requires **Unity 2022.3 or newer** and **UniVRM/UniGLTF 0.131.2 or newer**. Add these entries to the existing `dependencies` object in your project's `Packages/manifest.json`:

```json
"com.vrmc.gltf": "https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.2",
"com.vrmc.vrm": "https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.2",
"org.mochiya.avatar-tools": "https://github.com/mochiya-labs/unity-avatar-tools.git"
```

The example pins a verified UniVRM version. For a downloaded checkout, use **Window → Package Manager → Add package from disk** and select its `package.json`.

Install **VRChat SDK Avatars**, **Modular Avatar** and **lilToon 2.3.4 or newer** when your source asset uses them. These integrations are optional, but missing scripts or shaders on your asset need to be repaired before conversion.


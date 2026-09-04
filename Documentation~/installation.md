# Installation

## 1. Install lilToon

Install `jp.lilxyzw.liltoon` 2.3.4 or newer through the official lilToon VPM repository or release package. lilToon is a peer requirement rather than a hard UPM dependency because its normal distribution is VPM-based.

## 2. Install UniVRM 0.131.2 or newer

Add both official UniVRM packages to the Unity project's `Packages/manifest.json`. This example pins the minimum supported version:

```json
{
  "dependencies": {
    "com.vrmc.gltf": "https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.2",
    "com.vrmc.vrm": "https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.2"
  }
}
```

Keep both entries even though VRM depends on UniGLTF. Direct Git references let Unity resolve the packages without requiring a scoped registry. To use a newer UniVRM release, replace both `v0.131.2` revisions with the same newer release tag.

## 3. Install the Mochiya exporter

Open Unity's Package Manager, choose **Install package from git URL**, and enter:

```text
https://github.com/zekailin00/liltoon-unity-exporter.git
```

You can also add the Git dependency directly to the Unity project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "org.mochiya.liltoon-exporter": "https://github.com/zekailin00/liltoon-unity-exporter.git"
  }
}
```

This installs the current version from the repository's default branch. Unity records the resolved commit in the project's `Packages/packages-lock.json`.

To install from a local clone, open Unity's Package Manager, choose **Add package from disk...**, and select the clone's `package.json`.

You can also add a local manifest entry:

```json
{
  "dependencies": {
    "org.mochiya.liltoon-exporter": "file:../../mochiya-liltoon-unity"
  }
}
```

Adjust the relative path from the Unity project's `Packages` directory.

## Compatibility

The exporter requires Unity 2022.3 or newer, lilToon 2.3.4 or newer, and UniVRM 0.131.2 or newer. The package manifest declares the minimum UniVRM version because Unity package manifests require a concrete dependency version; projects may resolve a newer compatible version.

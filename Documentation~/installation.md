# Installation

## 1. Install lilToon

Install `jp.lilxyzw.liltoon` 2.3.4 through the official lilToon VPM repository or release package. lilToon is a peer requirement rather than a hard UPM dependency because its normal distribution is VPM-based.

## 2. Install UniVRM 0.131.2

Add both official UniVRM packages to the Unity project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.vrmc.gltf": "https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.2",
    "com.vrmc.vrm": "https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.2"
  }
}
```

Keep both entries even though VRM depends on UniGLTF. Direct Git references let Unity resolve the packages without requiring a private registry.

## 3. Install the Mochiya exporter

For local development, open Unity's Package Manager, choose **Add package from disk...**, and select this folder's `package.json`.

You can also add a local manifest entry:

```json
{
  "dependencies": {
    "com.mochiya.liltoon-exporter": "file:../../mochiya-liltoon-unity"
  }
}
```

Adjust the relative path from the Unity project's `Packages` directory. When this package is moved to its own Git repository, use a Git UPM URL pinned to a release tag.

## Version policy

Version 0.1.0 is compiled against UniVRM 0.131.2 public APIs and requires Unity 2022.3. Upgrade UniVRM deliberately and run an Editor export smoke test before changing the package dependency versions.

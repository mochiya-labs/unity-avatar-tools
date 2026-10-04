[Getting started](../README.md) | [Documentation index](index.md)

# Installation reference

## Installation

Use **Unity 2022.3 or newer**, with **UniVRM/UniGLTF 0.131.2 or newer**. For an existing avatar project, keep the Unity version supported by that asset and its other packages. Save your scene and back up the project before installing or updating packages.

### Install through Unity's Package Manager

1. Open the Unity project containing your asset.
2. Install [Git](https://git-scm.com/downloads) if it is not already available, then restart Unity and Unity Hub. Unity uses Git to download these packages; you do not need to run Git commands.
3. Open **Window → Package Manager** (in Unity 6, **Window → Package Management → Package Manager**).
4. Click **+**, then **Add package from git URL…** (called **Install package from git URL…** in newer Editors). Paste the first URL below and click **Add/Install**. Wait for installation to finish, then repeat for the next URL, in order.

| Order | Package | URL to paste |
| --- | --- | --- |
| 1 | UniGLTF | `https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.2` |
| 2 | UniVRM 1.0 | `https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.2` |
| 3 | Mochiya Avatar Tools | `https://github.com/mochiya-labs/unity-avatar-tools.git` |

These URLs select a specific supported UniVRM version. If compatible UniGLTF and UniVRM 1.0 packages are already installed, keep them and skip their installation steps. Avoid mixing an older copy imported into `Assets` with another copy installed through Package Manager; follow [UniVRM's installation guidance](https://vrm.dev/en/univrm/install/univrm_install/) when replacing an existing installation.

Wait for Unity to finish importing and compiling. Installation is ready when **Mochiya → Upload to Mochiya** and **Mochiya → Avatar Tools** appear in the top menu and the Console has no red compilation errors. See [Unity's Git installation instructions](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-ui-giturl.html) if Package Manager cannot download a package.

Install **VRChat SDK Avatars**, **Modular Avatar (MA)** and **lilToon 2.3.4 or newer** when your source asset uses them, following the asset creator's setup instructions. They are optional integrations for Mochiya, but missing scripts or shaders on your asset must be repaired before conversion.

<details>
<summary>Alternative installation: manifest or downloaded package</summary>

Add these entries to the existing `dependencies` object in `Packages/manifest.json`. Preserve its other entries and valid JSON commas; do not replace the whole file.

```json
"com.vrmc.gltf": "https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.2",
"com.vrmc.vrm": "https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.2",
"org.mochiya.avatar-tools": "https://github.com/mochiya-labs/unity-avatar-tools.git"
```

For a downloaded Mochiya repository ZIP, extract it to a permanent folder outside your project's `Assets` folder. In Package Manager, choose **Add/Install package from disk…** and select its `package.json`. Install the UniVRM dependencies above as well. Keep the extracted folder in place while the project uses it.

</details>


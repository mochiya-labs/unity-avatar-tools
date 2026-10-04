[**English**](README.md) | [**日本語**](README.ja.md) | [**简体中文**](README.zh-CN.md) | [**한국어**](README.ko.md)

# Mochiya Avatar Tools

Share your Unity avatars, outfits and accessories on [Mochiya](https://www.mochiya.org), or export them as files for compatible apps. Use **Upload to Mochiya** to add your work to your personal Avatar Studio library or list it on the marketplace. Your original scene objects stay unchanged.

## Install

Use **Unity 2022.3 or newer** with [Git](https://git-scm.com/downloads) installed. Import the dependencies your model uses, such as VRChat SDK Avatars, Modular Avatar and lilToon (2.3.4 or newer).

Open **Window > Package Manager > + > Add package from git URL**. Add these URLs **one at a time, in this order**; wait for each installation to finish:

![Package Manager with Add package from git URL selected.](Documentation~/images/add-unity-package.webp)

1. UniGLTF:
   ```text
   https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.2
   ```
2. UniVRM:
   ```text
   https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.2
   ```
3. Mochiya Avatar Tools:
   ```text
   https://github.com/mochiya-labs/unity-avatar-tools.git
   ```

If UniVRM/UniGLTF 0.131.2 or newer is already installed, keep the compatible installation. When Unity finishes compiling, the **Mochiya** menu appears. [Other installation options](Documentation~/installation.md).

## Connect to upload to Mochiya

An **API key is required only for Upload to Mochiya**. To convert or export files on your computer, skip to [Mochiya Avatar Tools](#local-avatar-tools); you do not need an account or API key.

1. Sign in to [Mochiya](https://www.mochiya.org/profile), or create an account if needed, and open your profile.
2. Expand **API key** near the bottom of the profile and copy your key. This key lets the Unity tool upload to your Mochiya account.
3. In Unity, open **Mochiya > Upload to Mochiya**, choose your language, paste the key into **API key**, and click **Connect**. Connect successfully before uploading. Keep your key private.

![The API key section on your Mochiya profile, with the copy button visible.](Documentation~/images/api-key.png)

![Unity's Upload to Mochiya panel showing the language selector, masked API key field and Connect button.](Documentation~/images/connect-to-mochiya.webp)

## Upload directly to Mochiya

In **Edit Mode**, drag your model prefab from the **Project** panel into the scene's **Hierarchy**. Choose what you want to upload:

- **Whole avatar:** drag the avatar's root object. The upload includes the clothing and accessories inside it.
- **Attachment only (outfit, hair or accessory):** set it up on its intended avatar, following its creator's instructions. Keep it directly under the avatar root with its original Modular Avatar components intact, then drag only the attachment's root. Do not run MA Manual Bake first.

```text
Hierarchy
└── My avatar      ← drag this to upload the whole avatar
    ├── Body
    ├── Armature
    └── My outfit  ← drag this to upload only the outfit
```

For a separate attachment, the base avatar supplies the reference skeleton, but its body and other attachments are not included. You do not need to upload the base avatar first. Specify the intended avatar and version in your item's requirements; clothing is not automatically reshaped to fit other bodies.

1. In **Upload to Mochiya**, drag the model from the **Hierarchy** into **Avatar or attachment**. Check the title, select a category and cover image, and choose **Personal use** or **Listed on marketplace**. Add gallery images or buyer files under **Additional item details** if needed. Paid listings require seller setup on the website.
2. Resolve any errors, then click **Upload for personal use** or **Publish listing**. The model is exported automatically. When finished, click **View item**; existing items are edited on the website.

![Uploading an outfit separately from its base avatar with Upload to Mochiya.](Documentation~/images/upload-outfit.webp)

[File limits, sign-in and upload recovery](Documentation~/upload.md) · [Compatibility and limitations](Documentation~/reference.md#supported-behavior-and-limits)

<a id="local-avatar-tools"></a>

## Mochiya Avatar Tools

If you want a model file to keep, share or use in another app, open **Mochiya > Avatar Tools**. This panel saves your model as a VRM or GLB file for compatible apps. It can also create an editable VRM copy in your Unity scene if you want to inspect or adjust it before exporting.

For motion or poses, the same panel can export a Humanoid AnimationClip as **VRM Animation (.vrma)**. You can play this animation on compatible avatars in Mochiya Avatar Studio and other supporting apps. A single-frame clip stores a pose.

Conversion and export run on your computer and do not require a Mochiya account or API key. If you just want to upload a model to Mochiya, **Upload to Mochiya** handles the model export for you.

[Conversion and animation guide](Documentation~/avatar-tools.md) · [Full documentation](Documentation~/index.md) · [MIT license](LICENSE)

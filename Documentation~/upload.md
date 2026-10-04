[Getting started](../README.md) | [Documentation index](index.md)

# Upload reference

## Prepare your asset

Work in **Edit Mode**: Unity's Play button should be off. Drag the asset's prefab from the **Project** panel into the scene's **Hierarchy** panel, then select its top-level object in the Hierarchy.

- **Whole avatar:** select the avatar root. Clothing and accessories already inside that root are included in the uploaded model.
- **Separate outfit, hair or accessory:** configure it on its intended base avatar using the asset creator's instructions. Keep the attachment directly beneath the avatar root, with its original MA components intact. Select only the attachment root for upload. Do not run MA Manual Bake before this workflow.

For example, this outfit is a direct child of its base avatar:

```text
Hierarchy
└── My avatar
    ├── Body
    ├── Armature
    └── My outfit  ← select this to upload only the outfit
```

The base avatar supplies the attachment's reference rig; its body and sibling meshes are excluded from a separate attachment export. You do not need to upload the base first. Tell users which base avatar and version the attachment was made for: Mochiya does not automatically reshape clothing to fit every body.

Have a PNG or JPEG cover image ready. If you want buyers to receive a Unity package, source files or instructions, prepare those files separately—the upload does not automatically package your Unity project. Include only content you have permission to upload or distribute.

## Upload directly to Mochiya

You can upload straight from the original scene object; a separate conversion or manual model export is unnecessary. The upload panel supports English, Japanese, Simplified Chinese and Korean through **Language**. The labels below use English.

### Connect your account

1. Sign in to [Mochiya](https://www.mochiya.org), then open your [profile](https://www.mochiya.org/profile).
2. Expand **API key** below the profile tabs and copy the key.
3. In Unity, open **Mochiya → Upload to Mochiya**, paste it into **API key**, and click **Connect**.
4. Check **Connected as** to confirm the correct account.

### Complete the item and upload

1. Drag the prepared scene object from the Hierarchy into **Avatar or attachment**, or select it in the Hierarchy and click **Use selection**. Selecting a source fills the title with its object name; edit the title afterward.
2. Review the detection message and expand **Compatibility warnings**. Resolve errors before uploading. Warnings describe behavior that may be approximated or omitted.
3. Keep **Model format: VRM** for supported avatar expressions and spring physics. Choose **GLB** when you need general glTF geometry; ordinary GLB does not carry VRM behavior. The tool generates this model file for you.
4. Complete the fields below. For a first check, keep **Availability: Personal use**.
5. Click **Upload for personal use**, or **Publish listing** for a marketplace item. Keep Unity open while it prepares the model, uploads files and finishes the upload.
6. Wait for **Your item is ready.**, then click **View item**. Reaching the file-transfer stage alone does not mean the item has finished publishing.

| Field | What to enter |
| --- | --- |
| Title (required) | A clear item name, up to 80 characters. Selecting another source replaces it. |
| Category (required) | Avatar body, Outfit, Hair, Accessory or Other. This is the marketplace category, separate from automatic avatar/attachment detection. |
| Tags (optional) | Add one tag at a time with **Add**; up to 12 distinct tags, 40 characters each. |
| Availability | **Personal use** keeps it in your library for Avatar Studio. **Listed on marketplace** lets others acquire it for free or buy it. You can change this later on the website. |
| Item price (marketplace only) | USD `0` for free, or $0.50–$900,000 for paid items. Paid prices must cover the displayed fees. |
| Cover image (required) | Choose one PNG/JPEG showing the item. It is not captured automatically from Unity. |
| Export settings (optional) | Select a reusable export profile to customize model metadata and export options. See [export settings](avatar-tools.md#export-settings). |
| Additional item details (optional) | Expand this section for gallery images, additional downloadable files, specifications, requirements, credits and license terms. |

**Paid listings:** open your profile's **Seller information**, submit the seller application and bank destination, and wait for Mochiya approval before listing a paid item. Review the panel's **Payment processing fee**, **Mochiya fee** and **Estimated earnings** for the current deductions.

In **Additional item details**, describe supported avatars/versions and setup steps under **Requirements & setup**, and state what is included under **Specifications**. Add appropriate credits and license terms. These listing terms are separate from the metadata embedded in the VRM file; review both before distribution.

| File or text | Limit |
| --- | --- |
| Required cover and optional gallery | PNG/JPEG, 10 MiB per image; up to 12 gallery images |
| Generated model | One VRM/GLB, 500 MiB maximum |
| Additional downloadable files | Up to 20 files, 500 MiB each |
| All uploaded files combined | 2 GiB maximum, including images and the generated model |
| Specifications / Requirements & setup | 5,000 / 4,000 characters |
| Credits / License terms | 2,000 / 3,000 characters |

Additional downloadable files are only the files you explicitly select. The generated model is used for Mochiya's 3D preview and editor; it does not replace an installable Unity source package for buyers.

### Check and manage your item

Open **View item** and check the cover, details and 3D preview. Then open [Avatar Studio](https://www.mochiya.org/studio) with your uploaded item. For an attachment, test it with its intended base avatar. Check appearance, fit, available controls and motion: a successful upload and a Unity preview do not guarantee identical browser rendering.

Find your uploads in your profile's **Uploaded items** tab. Edit an existing item's details, files, price or availability on the website. The Unity panel creates new items; uploading again after a successful upload creates a separate item. To list an item you tested for personal use, edit that existing item on the website.

### Retry or cancel an upload

**Retry upload** keeps the current upload and skips files already received; an interrupted file starts again from the beginning. **Cancel** stops the current transfer without signing out. **Prepare a new upload**, or changing the form, starts fresh preparation. Unfinished sessions expire after 24 hours and are cleaned up by the service.

After an Editor reload and automatic connection check, retry to recover a completed upload or finalize files that already arrived. If some files are missing and local preparation state was lost, the panel asks you to prepare a new upload. If the final result is uncertain, check **Uploaded items** before starting another upload. Signing out clears local recovery information. Temporary exports are removed after completion or cleanup; source objects remain unchanged.

### Remembered sign-in

After a successful connection, your sign-in is remembered for this Unity project on this computer using your OS credential store. Reopening the panel, reloading scripts or restarting Unity checks the saved key before enabling uploads. **Disconnect** signs out and removes it. Failed connection checks (including a server outage) and API authentication/server/network failures clear the saved sign-in; ordinary item-validation or file-transfer errors keep it.

The key is not stored in project files or shared when you copy the project. Moving the project to another path requires connecting again. Windows uses Credential Manager; macOS uses Keychain; Linux requires `secret-tool` and an available Secret Service keyring. If access is denied, unlock your credential store and reconnect.

Keep the key private. If it is exposed, use **Regenerate** in your Mochiya profile, then connect again with the new key in each Unity project that uses it.

Raw MA/VRC setups are converted during upload; prepared VRM GameObjects can be exported directly. A change of server clears the remembered sign-in. A temporary model export is kept locally for retries and removed during cleanup.

See [troubleshooting](troubleshooting.md) for installation, connection and upload problems, and [supported behavior and limits](reference.md#supported-behavior-and-limits) for conversion differences.

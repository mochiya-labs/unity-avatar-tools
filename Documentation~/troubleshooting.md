[Getting started](../README.md) | [Documentation index](index.md)

## Troubleshooting

| Problem | What to do |
| --- | --- |
| Package Manager cannot find Git | Install Git, restart Unity and Unity Hub, and retry. On Windows, Git must be available on PATH; see [installation guidance](installation.md#install-through-unitys-package-manager). |
| No Mochiya menu, or red compilation errors | Wait for compilation, then open **Window → General → Console**. Resolve the first red error. Check that both required UniVRM packages are installed and that older duplicate copies are not present. |
| Missing scripts or pink materials | Restore the asset's required SDK, MA or shader packages using its creator's instructions before exporting. |
| “Place the prefab in a scene” or no meshes detected | Drag the prefab into the Hierarchy and select its model root, rather than the Project-panel prefab or an empty container. |
| Invalid humanoid or parent dependency | For an attachment, use its intended valid base avatar as the direct parent and retain the original MA setup. For a whole avatar, check its Humanoid rig and source setup. Conversion cannot repair an arbitrary broken rig. |
| Upload button is disabled | Connect and wait for **Connected as**. Confirm the account, network connection and access to your OS credential store. |
| Asked to update Avatar Tools | Update the package, allow Unity to compile, and reconnect. The installed upload form must be compatible with Mochiya's current service. |
| Paid listing rejected | Check seller approval in your profile and ensure the price covers the displayed fees. Use $0 only if you intend a free listing. |
| File or text limit error | Check the [file and text limits](upload.md#complete-the-item-and-upload), including the generated model and total size. Remove unneeded files or reduce image/model size in your source workflow, then prepare again. |
| Network error or interrupted upload | Restore the connection, reconnect if signed out, and use **Retry upload** when available. Check your profile before starting a new upload if completion was uncertain. |
| Too many uploads | Wait an hour before creating another upload. Use an existing retry when available. |
| Clothing, materials or physics look different in Mochiya | Test the intended base, review compatibility warnings and the [supported behavior and limits](reference.md#supported-behavior-and-limits). Arbitrary VRChat animation logic and exact Unity shader/physics parity are not supported. |

For Git installations, select **Mochiya Avatar Tools** in Package Manager and click **Update**. See [Unity's package update instructions](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-ui-update.html) for revision-specific installation alternatives. Back up first and test the updated project before replacing a working setup. For disk installations, replace the extracted package with the updated download while preserving its location, then let Unity reimport it.

If you still need help, [open an issue](https://github.com/mochiya-labs/unity-avatar-tools/issues) with your Unity/package versions, operating system, exact error text and steps to reproduce it. Remove API keys, personal details and private asset files from screenshots or logs; share a minimal reproduction only when you can redistribute it.


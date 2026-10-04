[Getting started](../README.md) | [Documentation index](index.md)

# Upload reference

## Upload directly to Mochiya

1. Open your [Mochiya profile](https://www.mochiya.org/profile), expand **API key** at the bottom, and copy your key.
2. Open **Mochiya → Upload to Mochiya**, paste the key and connect.
3. Select a scene avatar or attachment, complete the item details, and add a cover, optional gallery and additional buyer files.
4. Upload. The tool prepares and exports the model internally, then transfers the files.

After a successful connection, your sign-in is remembered for this Unity project on this computer using your OS credential store. Reopening the panel, reloading scripts or restarting Unity automatically checks the saved key before enabling uploads. **Disconnect** signs out and removes it. A failed connection check (including a server outage), an authentication/server/network failure from the Mochiya API, or a change of server clears the saved sign-in. Ordinary item-validation or file-transfer errors keep it. Regenerating your key on the website requires connecting again with the new key.

The key is not stored in your project files or shared when you copy the project. Moving the project to another path requires signing in again. Windows uses Credential Manager; macOS uses Keychain; Linux requires `secret-tool` and an available Secret Service keyring. If access is denied, unlock your credential store and reconnect. Keep your API key private.

Raw MA/VRC setups are converted through the existing export workflow; prepared VRM GameObjects can be exported directly. Choose VRM or GLB and optionally select the shared export profile. VRM preserves supported VRM behavior; ordinary GLB does not carry the VRM spring runtime. See [supported behavior and limits](reference.md#supported-behavior-and-limits). Prefab assets must first be placed in the Hierarchy.

Enter the same item details as the website: title, category, tags, Availability (Personal use or Listed on marketplace) and USD price, cover image, optional gallery, additional buyer files, specifications, requirements, credits and license text. Selecting an avatar or attachment fills the item title with its GameObject name (up to 80 characters), replacing the previous title. You can edit the title afterward. The required model file is produced internally. Source objects remain unchanged. A temporary export is kept for retries and removed afterward.

Images must be PNG/JPEG up to 10 MiB each. Models and additional files are each at most 500 MiB, with a total of 2 GiB. Up to 12 gallery images and 20 additional files are supported. Personal use is the default and keeps the item in your library for Avatar Studio. Listed on marketplace makes the item available for free acquisition or sale. You can change this anytime. Paid sales need the website's seller setup. Additional files are explicitly selected; the tool does not package your project or dependencies automatically.

Uploads show preparation, file transfer and finalization. Retry skips completed files and restarts an interrupted file. Cancel stops the current transfer without signing out; the server expires and cleans unfinished uploads. After an editor reload and automatic connection check, retry to recover a completed upload or finalize files that already arrived; if local preparation state was lost before all files arrived, start a fresh upload. Signing out clears local upload recovery. Existing item edits are available through the website. A Unity preview is not a guarantee of identical browser rendering.


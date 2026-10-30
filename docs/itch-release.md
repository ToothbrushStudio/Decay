# Decay — itch.io Release Plan

Two parts: shipping the build and keys on itch.io, then updating the
Decay page on toothbrush.studio (repo `ToothbrushStudio/ToothbrushStudioWebsite`).
Background: docs/release.md (build, licensing, signing).

## Before you start: known gaps

- **`--campaign` flag does not exist in KeyGen.** The website's
  `docs/decay-keys.md` tells you to run
  `dotnet run batch <pem> <count> --campaign web-puzzle` (and `--campaign itch`),
  but `tools/Decay.KeyGen/Program.cs` only takes `batch <pem> <count> [startNumber]`.
  Either add the flag, or keep the pools separate by number range (see step 3).
  Whichever you pick, fix the website doc so it matches.
- **Windows is the only target for launch.** The site's download block and
  release JSON are Windows-only; mobile/macOS stay "coming soon".
- **Code signing is required** (Smart App Control blocks unsigned exes). See
  release.md, "Packaging notes".

## Part 1 — Ship on itch.io

### 1. One-time setup

1. `cd tools/Decay.KeyGen && dotnet run gen`. Store `decay-publisher-private.pem`
   offline and backed up. Never commit it.
2. Paste the printed public key into `Decay.App/PublisherKey.cs`. Until this is
   done, builds are Free-only. (The file currently holds a key; confirm it is
   the one you will sell with.)
3. Create the itch.io project (Dashboard > Create new project):
   - Kind of project: **Downloadable**
   - Pricing: **Paid** (or "$0 or donate" if Free tier is the itch download and
     Pro is sold separately; see step 5)
   - Suggested URL: `toothbrushstudio.itch.io/decay`
   - Genre/tags: Tool, Art, Privacy, Encryption, Glitch
   - Short description (<=80 chars) and long description: reuse
     docs/launch-copy.md.
   - Screenshots/GIFs: docs/media/hero.png, reveal.png, presets.png. Add a
     screen recording of the un-rot reveal; it is the pitch.

### 2. Build and sign the Windows release

1. `dotnet test Decay.Core.Tests && dotnet test Decay.Renderer.Tests && dotnet test Decay.Licensing.Tests`
   (FrozenFormatTests must pass).
2. Bump `ApplicationDisplayVersion` (currently `0.1.0`) and `ApplicationVersion`
   in `Decay.App/Decay.App.csproj`.
3. `dotnet publish Decay.App -f net10.0-windows10.0.19041.0 -c Release`
4. Authenticode-sign the exe/MSIX (Azure Trusted Signing or similar).
5. Zip as `Decay-<version>-win-x64.zip`. This exact name is what the website's
   verify instructions assume.
6. Compute the hash and keep it for the website:
   `certutil -hashfile Decay-<version>-win-x64.zip SHA256`

### 3. Generate the Pro key pool

```bash
dotnet run batch decay-publisher-private.pem 500 > itch-keys.txt
```

- Track the last number issued. The next batch starts after it
  (`dotnet run batch <pem> 500 501`).
- The website's puzzle pool must not share keys with this one. Until
  `--campaign` exists, give each pool its own numeric range (e.g. itch from 1,
  web-puzzle from 10001) and record the ranges somewhere durable.
- Do not commit `itch-keys.txt`.

### 4. Upload and configure on itch.io

1. Install butler and log in: `butler login`.
2. Push the build:
   ```bash
   butler push Decay-<version>-win-x64.zip toothbrushstudio/decay:windows --userversion <version>
   ```
3. Mark the upload as Windows in the dashboard.
4. **Rot mode is now Pro-only**, so the Free/Pro split on the listing must
   say so. List Pro as: all five carriers, Rot mode, your own images as
   carriers, intensity dial, watermark-free exports.

### 5. Decide how Pro is sold

Pick one model before publishing; the page copy depends on it.

- **A. Free app, Pro key sold separately (recommended, matches the offline
  design).** The download is free; Pro is a paid "key" product/reward tier.
  Buyers get keys from the serial pool: Dashboard > project > Distribute /
  Edit > *Generate keys* or the "Upload keys" option, then upload
  `itch-keys.txt`. itch hands each purchaser the next key. Buyers paste it into
  the License tab.
- **B. Paid download that includes Pro.** Same build; the key is delivered with
  the purchase. Simpler page, but nobody can try Free first.

### 6. Publish and verify

1. Publish with visibility **Restricted** first. Buy it with a test account
   (or a 100% off coupon), download, paste the key, confirm Pro unlocks
   (Rot, all carriers, custom image, intensity, no watermark).
2. Confirm a Free install still decrypts Rot images created by Pro.
3. Set to **Public**.
4. Watch the key pool; top up before it runs out.

## Part 2 — Website changes (ToothbrushStudioWebsite)

Page: `Pages/Products/Decay.cshtml` (+ `Decay.cshtml.cs`). Follow the repo's
CLAUDE.md: no new projects, no styling hacks in site.css.

### Config only, no code or redeploy

Set these App Service settings (see the repo's `docs/decay-keys.md`, section 6):

```bash
az webapp config appsettings set --resource-group <rg> --name <app-name> \
  --settings Decay__ItchUrl=https://toothbrushstudio.itch.io/decay
```

This turns "Pro is coming to itch.io." into the "Get Decay Pro on itch.io"
button. Leave `Decay__KeyPageReady` unset/false until the real puzzle exists
(see "Not part of this release").

### Data file: `wwwroot/data/decay-release.json`

Currently all empty, so the page shows "The Windows build is on its way."
Fill it in for the release:

```json
{
  "version": "0.1.0",
  "windows": {
    "url": "https://toothbrushstudio.itch.io/decay",
    "sha256": "<hash from step 2.6>"
  },
  "notes": "First release."
}
```

- `url` can be the itch page or a direct download; the button reads "Download
  for Windows".
- If the zip is hosted on itch only, the SHA-256 line is still valid as long
  as the zip name matches `Decay-<version>-win-x64.zip`.

### Copy changes in `Decay.cshtml`

1. **Free/Pro table:** add a row after "Decrypt anything":
   `<tr><td>Rot mode (encrypt)</td><td>&mdash;</td><td>&#10003;</td></tr>`
   Matches the README table in the Decay repo (Rot is Pro-only as of PR #3).
2. **Description text:** the page only describes the glitch style. Add a short
   paragraph introducing Rot ("Rot keeps the image recognizable and stains it
   instead; Pro"), taken from README.md lines 38-42 of the Decay repo. Mention
   that decoding either style is free.
3. **Gallery:** add a Rot example image (`/images/decay/rot.png`, new asset)
   with alt text and caption; the current three images only show Glitch.
4. **"Get Pro" section:** once the itch decision (step 5) is made, replace the
   generic button text if needed, and list what Pro includes (five carriers,
   Rot, custom images, intensity dial, no watermark).
5. **Download section:** after `decay-release.json` is filled it renders
   automatically. Keep "macOS, iOS, and Android: coming soon."
6. **Meta description:** optionally mention Rot.
7. Remove the stale `TODO(user)` comment in the "Get Pro" `else` branch once
   `Decay__ItchUrl` is live.

### Docs

- Fix `docs/decay-keys.md` to match KeyGen (`--campaign` flag) or ship the flag.
- Privacy page already mentions itch.io; no change needed.

### Deploy

Merge to the branch the `main_toothbrushstudio.yml` workflow deploys from;
settings changes need no redeploy. Verify the page, the download link, the
hash text, and the Pro button after deploy.

## Not part of this release

- `Decay__KeyPageReady` stays off until the pool is imported, App Service
  secrets are set, and a real `IPuzzle` replaces `StubPuzzle`.
- macOS/iOS/Android builds, App Store export compliance (release.md).
- Choosing a source license for the repo.

## Launch checklist

- [ ] Keygen private key generated and backed up; public key in `PublisherKey.cs`
- [ ] Tests pass (incl. FrozenFormatTests); version bumped
- [ ] Windows build signed, zipped, hash recorded
- [ ] itch page created with copy, media, Pro description (incl. Rot)
- [ ] Key pool generated and uploaded; test purchase unlocks Pro
- [ ] `butler push` done; restricted test passed; page set public
- [ ] Website: `Decay__ItchUrl` set, `decay-release.json` filled, copy updated, deployed
- [ ] Launch posts from docs/launch-copy.md

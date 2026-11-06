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
   done, builds are Free-only. (Done: the key in the file is the one the
   launch keys are signed with.)
3. Payments (itch account settings, once): **Collected by itch.io, paid
   later**, so itch handles sales tax and VAT; a PayPal or Payoneer account
   for payouts; and the tax interview for the LLC. Payouts wait on the tax
   interview.
4. Create the itch.io project (Dashboard > Create new project):
   - Kind of project: **Downloadable**; classification: **Tools**
   - Pricing: **Paid**, with the Pro price as the minimum (see step 5)
   - URL: `toothbrushstudio.itch.io/decay`
   - Short description, tags and description: "itch.io page" in
     docs/launch-copy.md.
   - Cover image docs/media/hero.png (itch crops it to 630×500). Screenshots:
     hero.png, reveal.png, presets.png and the Encrypt and Decrypt tabs.
   - A recording of the un-rot reveal, as a YouTube link in "Gameplay video
     or trailer" (itch doesn't host video). It is the pitch.

### 2. Build and sign the Windows release

1. `dotnet test Decay.Core.Tests && dotnet test Decay.Renderer.Tests && dotnet test Decay.Licensing.Tests`
   (FrozenFormatTests and FrozenPipelineTests must pass).
2. Confirm `ApplicationDisplayVersion` (`1.0.0`; Windows packages it as `1.0.0.0`) and
   `ApplicationVersion` in `Decay.App/Decay.App.csproj`.
3. `dotnet publish Decay.App -f net10.0-windows10.0.19041.0 -c Release -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -o publish`
   Output: the `publish` folder in the repo root (from `-o publish`).
   (unpackaged and self-contained; zip the folder's contents). Run the exe there once
   to confirm it opens before signing.
4. Authenticode-sign the exe and our four DLLs (release.md, "Code signing").
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

1. Upload the zip with the **web uploader** (Edit project > Upload files).
   Don't use butler: it unpacks a pushed zip, and itch serves browser
   downloads of the build as a zip it makes itself, so the file people get
   would not match the SHA-256 on the website. The web uploader serves the
   file as uploaded; step 6 checks the hash to confirm.
2. Keep the file name `Decay-<version>-win-x64.zip`. The website's verify
   instructions use it.
3. Tick **Windows** and **This file is a demo and can be downloaded for
   free**. The demo box only appears once the project has a price.
4. **Add one paid file.** itch treats every file marked "demo" as free, so a
   page whose files are all demos shows no Buy button. Upload a small
   non-demo file (`Decay Pro - activation guide.txt`: where the key appears
   on the download page, how to activate in the GUI and CLI, support email)
   and leave "demo" unticked. Buyers get that file plus their key.
5. **Rot mode is Pro-only**, so the Free/Pro split on the listing must say
   so. List Pro as: all five carriers, Rot mode, your own images as
   carriers, intensity dial, watermark-free exports.

### 5. How Pro is sold (decided: one page)

The project is **Paid** at the Pro price, and the Windows zip is flagged as a
free "demo" file. So:

- anyone can download the app for free (the Free tier; decrypting is always
  free);
- each buyer gets one Pro key from the external key pool, shown on their
  download page, and pastes it into the License tab.

Why not the alternatives:

- **"$0 or donate":** itch gives external keys to every owner, and paying any
  amount makes someone an owner, so Pro would sell for cents. (Visitors who
  skip payment get no key.)
- **Paid download only:** someone sent a Decay image would have to pay to
  open it, which breaks "decrypting is free".
- **Two pages (free Decay, paid Decay Pro):** works, but itch won't sell a key
  without a downloadable file, so the zip goes on both pages and every
  release is uploaded twice.

Adding the keys:

1. Work on a copy of `itch-keys.txt` outside the repo. Remove any key used
   for testing. The License tab shows "Licensed to Decay Pro #NNNNN", and in
   a batch that started at 1, key #N is line N of the file.
2. Edit project > **Distribute** > **External keys**: add keys, type
   **Other**, one per line.
3. Check the count, then **View all** and compare one key with the file. Keys
   are about 160 characters (`DECAY1.<payload>.<signature>`), and itch
   doesn't document a length limit.
4. Record the range uploaded. When the pool is empty, buyers who claim are
   told to try again later, so top up early. Unclaimed keys can be removed
   from **View all**.

Refunds: in the "Collected by itch.io" mode, refunds go through itch.io
support. A refunded buyer's key keeps working, because keys are checked
offline. That is the cost of having no activation server.

### 6. Publish and verify

1. Set visibility to **Restricted** and also allow a password to view the
   page.
2. On a clean Windows 11 VM with Smart App Control on, open the page from a
   second itch account and buy it. A browser download carries the "from the
   internet" mark, so SmartScreen and Smart App Control treat it as they will
   for buyers. A file copied into a VM has no mark, so SmartScreen never
   checks it.
3. Hash the downloaded zip with `certutil`: it must match the hash from
   step 2.6.
4. Extract, run, paste the key from the download page into the License tab,
   and confirm Pro unlocks (Rot, all carriers, custom image, intensity, no
   watermark).
5. In a private window, signed out, download the free file and confirm it
   runs as Free and decrypts a Rot image made by Pro.
6. Set to **Public**.
7. Watch the key pool; top up before it runs out.

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
  "version": "1.0.0.0",
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
4. **"Get Pro" section:** the itch page is both the free download and the
   Pro purchase (step 5), so the download link and the Pro button both go to
   `toothbrushstudio.itch.io/decay`. List what Pro includes (five carriers,
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
- ~~Choosing a source license for the repo.~~ Decided: all rights reserved (`LICENSE`).

## Launch checklist

- [x] Keygen private key generated and backed up; public key in `PublisherKey.cs`
- [x] Tests pass (incl. the FrozenFormatTests and FrozenPipelineTests golden vectors); version bumped
- [ ] Windows build signed, zipped, hash recorded
- [ ] itch page created with copy, media, Pro description (incl. Rot)
- [ ] Key pool generated and added as external keys; test purchase unlocks Pro
- [ ] Signed zip uploaded (web uploader, Windows and demo ticked); restricted test passed; page set public
- [ ] Website: `Decay__ItchUrl` set, `decay-release.json` filled, copy updated, deployed
- [ ] Launch posts from docs/launch-copy.md

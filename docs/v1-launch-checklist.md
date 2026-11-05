# Decay 1.0 launch checklist (Windows via itch.io)

Work top to bottom. Tick each box as you finish it. Details for most steps
are in `docs/itch-release.md`, `docs/release.md` and `docs/repo-security.md`.

## Already done

- [x] License key pair generated, public key in `Decay.App/PublisherKey.cs`
- [x] Private key backed up offline
- [x] 500 Pro keys generated for itch
- [x] itch.io pages created
- [x] Code signing documents submitted
- [x] Version set to `1.0.0.0`, send guide and launch copy updated
      (on branch `claude/cloud-drive-send-guide`)

## 1. Merge the code changes

- [x] **Review and merge the send-guide branch into `main`** (PRs #11–#13).
- [ ] **Review and merge the pre-launch fixes** from
      `claude/sleepy-mccarthy-43i0mz`: crash and stale-result fixes, golden
      vectors for the frozen pipelines, repo hygiene. Check that the `test`
      workflow passes on the pull request.
  - Then on your build machine: `git checkout main && git pull`.

## 2. Build and test on Windows

- [ ] **Run the automated tests.**
  - In a terminal at the repo root:
    `dotnet test Decay.Core.Tests`, `dotnet test Decay.Renderer.Tests`,
    `dotnet test Decay.Licensing.Tests`.
  - All must pass. If `FrozenFormatTests` or `FrozenPipelineTests` fails,
    revert the change that broke it. Don't edit the test.
  - CI runs these on every push; this step confirms them on your machine.
- [ ] **Build the app and fix any errors.**
  - Run `dotnet workload install maui` once, then
    `dotnet build Decay.App -f net10.0-windows10.0.19041.0`.
- [ ] **Try the app by hand as a Free user.**
  - Encrypt a message with TV Static and save it.
  - Open the saved file on the **Decrypt** tab with the same passphrase and
    watch the un-rot reveal.
  - Try a wrong passphrase on the Decrypt tab. It should explain the failure.
  - Check that the watermark appears on Free exports and Pro features are
    locked.
- [ ] **Try it as a Pro user.**
  - Open the **License** tab and paste one of your 500 keys.
  - Confirm: all five carriers, Rot mode, your own image as a carrier, the
    intensity dial, and no watermark.
  - Make a Rot image as Pro, then open it on a **Free** install (or after
    removing the license). It must decrypt. Decrypting is always free.
  - Close and reopen the app: Pro must still be active.
- [ ] **Check the window and the error paths.**
  - The window opens fully on screen on a laptop at 125–150% scaling.
  - Opening a JPEG, a damaged file, or a PNG with no message explains the
    problem instead of crashing.
  - Edit the message after encrypting: the Save and Share buttons disappear
    until you encrypt again.
  - Share the PNG from the Encrypt tab (unpackaged builds use the Windows
    share sheet).
- [ ] **Do a real send.**
  - Upload a Decay image to Google Drive, OneDrive or iCloud Drive, share the
    link, and download it on a second device (or have a friend do it) using the
    **Download** button. Open it in Decay.
  - Fix the wording in the guide if any step doesn't match what you saw.

## 3. Sign and package

- [ ] **Wait for code-signing approval.** Check your signing service's
      dashboard or email. Don't ship unsigned. Windows Smart App Control will
      block it for many users.
- [ ] **Publish the release build.**
  - Delete any old `publish` folder first (publish adds to it without
    removing old files): `Remove-Item -Recurse -Force publish -ErrorAction SilentlyContinue`
  - `dotnet publish Decay.App -f net10.0-windows10.0.19041.0 -c Release -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -o publish`
  - The app is in the `publish` folder in the repo root (from `-o publish`).
    Zip the contents of that folder, not just the exe. It also holds the runtime files.
  - Check `publish` contains `coreclr.dll` and `hostfxr.dll` (the .NET
    runtime): `Test-Path publish\coreclr.dll` must print `True`. If not, see
    "Check the .NET runtime is bundled" in `docs/release.md`.
  - Run `Decay.App.exe` from that folder once to confirm it opens before signing.
- [ ] **Sign the exe and our DLLs** (`Decay.App.dll`, `Decay.Core.dll`,
      `Decay.Renderer.dll`, `Decay.Licensing.dll`) with your signing service,
      following its instructions (for Azure Trusted Signing, use its signing
      tool pointed at the published output). Smart App Control checks DLLs too.
- [ ] **Test the signed build on a clean machine** (or a fresh Windows user/VM)
      with Smart App Control on. It should install and run without a block.
- [ ] **Zip it and record the hash.**
  - Name it exactly `Decay-1.0.0.0-win-x64.zip`.
  - In a terminal: `certutil -hashfile Decay-1.0.0.0-win-x64.zip SHA256`
  - Copy the hash somewhere safe. The website needs it.

## 4. Publish on itch.io

- [ ] **Upload the key pool.**
  - In the itch dashboard, open the project and use the option to upload or
    generate keys, then upload your list of 500 keys. Don't commit that file to
    git and keep a copy outside the repo.
  - Note the last key number so the next batch can continue from it.
- [ ] **Upload the build with butler.**
  - Install butler and run `butler login`.
  - `butler push Decay-1.0.0.0-win-x64.zip toothbrushstudio/decay:windows --userversion 1.0.0.0`
  - In the dashboard, mark the upload as **Windows**.
- [ ] **Check the page copy.**
  - Free/Pro split must say Rot mode is Pro.
  - Pro = all five carriers, Rot, your own images, the intensity dial, no
    watermark.
  - Add your **contact email** in the description or support section, plus a
    short line on refunds and support.
- [ ] **Add media.** Hero, reveal and presets images are in `docs/media`. Record
      a screen capture of the un-rot reveal (Windows Game Bar: `Win` + `Alt` +
      `R`, or OBS) and upload it. It's the strongest selling point.
- [ ] **Test the buying flow while the page is Restricted.**
  - Set visibility to **Restricted** and buy it with a test account or a 100%
    coupon.
  - Download, paste the key you're handed into the License tab, and confirm Pro
    unlocks.
- [ ] **Set the page to Public.**

## 5. Update the website

- [ ] **Fill in `wwwroot/data/decay-release.json`** in the website repo
      (`ToothbrushStudio/ToothbrushStudioWebsite`) with the version
      (`1.0.0.0`), the itch URL, and the SHA-256 hash from step 3.
- [ ] **Set the itch link.** In Azure App Service settings, add
      `Decay__ItchUrl=https://toothbrushstudio.itch.io/decay`. No redeploy is
      needed.
- [ ] **Update the page copy** (`Pages/Products/Decay.cshtml`): add the Rot row
      to the Free/Pro table, a short Rot paragraph, a Rot example image, and
      the "Get Pro" list. Remove the stale `TODO(user)` comment.
- [ ] **Add the how-to-send text** from `docs/website-how-to-send.md`.
- [ ] **Deploy and check** the page, download link, hash text and Pro button.

## 6. Open the repo (when you're ready)

Follow `docs/repo-security.md`. The short version:

- [x] **Scan history for secrets** with `gitleaks detect --source . --log-opts="--all"`.
      Clean on 2026-11-05; two reviewed false positives are in `.gitleaksignore`.
      Run it again just before switching to public.
- [x] **Extend `.gitignore`** for `*.p12`, `*.snk`, `*.key`, `*.env`,
      `itch-keys.txt`, `*-keys.txt` and the publisher key (`*.pem`, `*.pfx`
      were already there). Still keep the key pool and private key outside the
      repo folder.
- [x] **Choose a license** and add a `LICENSE` file: all rights reserved.
- [x] **Add `SECURITY.md`.**
- [ ] **Turn on private vulnerability reporting** (Settings → Code security).
- [ ] **Protect `main`:** Settings → Rules → New branch ruleset. Require a pull
      request, one approval and code-owner review; block force pushes.
      Require the `test` workflow's two jobs to pass.
- [x] **Add `.github/CODEOWNERS`** with your GitHub handle.
- [x] **Add a CI workflow** (`.github/workflows/test.yml`, read-only, actions
      pinned to commit SHAs).
- [ ] **Require approval for fork pull request workflows** (Settings → Actions
      → General).
- [ ] **Turn on 2FA, secret scanning and push protection.**
- [ ] **Switch the repo to public** (Settings → General → Danger Zone → Change
      visibility).

## 7. Launch

- [ ] **Post the launch posts** from `docs/launch-copy.md` (X, Bluesky,
      Mastodon, Reddit). Attach the hero and reveal media. Make sure each one
      says the posted image is only a preview.
- [ ] **Be around for the first day** to answer questions. Most will be
      "my image won't open", so point them to the send guide.

## 8. After launch

- [ ] **Count Pro sales** in the itch dashboard. At **100 Pro sales**, start
      resilient mode (v2). Free downloads don't count.
- [ ] **Top up the key pool** before it runs out. Generate the next batch
      with `dotnet run batch <pem> 500 501` (start at one more than your last
      number), upload it, and note the new range.
- [ ] **Keep notes on support questions.** If people lose messages to typos or
      to chat apps, that shapes what v2 should do.

## Console edition (only if `decay.exe` ships with 1.0)

The changelog lists it for 1.0, but nothing above covers it. Either do these
or move it to a later release.

- [x] **Confirm the Windows CI job passes**: `decay selftest` on the real .NET
      Framework 4.8 (19 passed, license checks included) and the piped UTF-8
      round trips. Keep it green.
- [ ] **Try it by hand on Windows**: the hidden passphrase prompt, and
      non-ASCII output in a `cmd.exe` and a PowerShell window.
- [ ] **Sign `decay.exe`.**
- [ ] **Zip `Decay.Cli/bin/cli`** as `Decay-1.0.0-console.zip`, record its
      SHA-256, and push it to its own itch channel (for example
      `toothbrushstudio/decay:windows-console`).

## Later, not for 1.0

- macOS build (needs a Mac, Xcode and an Apple Developer account)
- Linux command-line tool or web decoder
- iOS and Android builds
- Resilient mode and the v3/v4 features

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
- [x] **Review and merge the pre-launch fixes** from
      `claude/sleepy-mccarthy-43i0mz`: crash and stale-result fixes, golden
      vectors for the frozen pipelines, repo hygiene. Check that the `test`
      workflow passes on the pull request.
  - Then on your build machine: `git checkout main && git pull`.

## 2. Build and test on Windows

- [x] **Run the automated tests.**
  - In a terminal at the repo root:
    `dotnet test Decay.Core.Tests`, `dotnet test Decay.Renderer.Tests`,
    `dotnet test Decay.Licensing.Tests`.
  - All must pass. If `FrozenFormatTests` or `FrozenPipelineTests` fails,
    revert the change that broke it. Don't edit the test.
  - CI runs these on every push; this step confirms them on your machine.
- [x] **Build the app and fix any errors.**
  - Run `dotnet workload install maui` once, then
    `dotnet build Decay.App -f net10.0-windows10.0.19041.0`.
- [x] **Try the app by hand as a Free user.**
  - Encrypt a message with TV Static and save it.
  - Open the saved file on the **Decrypt** tab with the same passphrase and
    watch the un-rot reveal.
  - Try a wrong passphrase on the Decrypt tab. It should explain the failure.
  - Check that the watermark appears on Free exports and Pro features are
    locked.
- [x] **Try it as a Pro user.**
  - Open the **License** tab and paste one of your 500 keys.
  - Confirm: all five carriers, Rot mode, your own image as a carrier, the
    intensity dial, and no watermark.
  - Make a Rot image as Pro, then open it on a **Free** install (or after
    removing the license). It must decrypt. Decrypting is always free.
  - Close and reopen the app: Pro must still be active.
- [x] **Check the window and the error paths.**
  - The window opens fully on screen on a laptop at 125–150% scaling.
  - Opening a JPEG, a damaged file, or a PNG with no message explains the
    problem instead of crashing.
  - Edit the message after encrypting: the Save and Share buttons disappear
    until you encrypt again.
  - Share the PNG from the Encrypt tab (unpackaged builds use the Windows
    share sheet).
- [x] **Do a real send.**
  - Upload a Decay image to Google Drive, OneDrive or iCloud Drive, share the
    link, and download it on a second device (or have a friend do it) using the
    **Download** button. Open it in Decay.
  - Fix the wording in the guide if any step doesn't match what you saw.

## 3. Sign and package

- [ ] **Wait for code-signing approval.** Check your signing service's
      dashboard or email. Don't ship unsigned. Windows Smart App Control will
      block it for many users.
- [x] **Publish the release build.**
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
- **Clean-machine test:** the unsigned build already ran in Windows Sandbox
  with no .NET installed. Test the signed build in step 4, with a real
  download from the Restricted itch page. A file copied into a VM has no
  "downloaded from the internet" mark, so SmartScreen never checks it
  (that's why Sandbox showed no "unknown publisher" warning).
- [ ] **Zip it and record the hash.**
  - Name it exactly `Decay-1.0.0.0-win-x64.zip`.
  - In a terminal: `certutil -hashfile Decay-1.0.0.0-win-x64.zip SHA256`
  - Copy the hash somewhere safe. The website needs it.

## 4. Publish on itch.io

One page sells Pro. The project is **Paid** at the Pro price, and the Windows
zip is a free download (itch's "demo" option), so anyone can get the Free app
and each buyer gets a Pro key. Don't use "$0 or donate": itch gives a key to
anyone who pays any amount. Details: `docs/itch-release.md`, step 5.

- [ ] **Set up payments** (account settings, once): **Collected by itch.io,
      paid later**, a PayPal or Payoneer account for payouts, and the tax
      interview for the LLC. Payouts wait on the tax interview.
- [ ] **Fill in the page** (Dashboard → Decay → Edit).
  - Kind **Downloadable**, classification **Tools**, release status
    **Released**, pricing **Paid** with the Pro price as the minimum.
  - Short description, tags and description: "itch.io page" in
    `docs/launch-copy.md`. It covers the Free/Pro split (Rot is Pro), the
    support email and refunds.
  - Leave visibility on **Draft**.
- [ ] **Add media.**
  - Cover image: `docs/media/hero.png` (itch crops it to 630×500).
  - Screenshots: hero, reveal and presets from `docs/media`, plus the Encrypt
    and Decrypt tabs.
  - Record the un-rot reveal (Windows Game Bar: `Win` + `Alt` + `R`, or OBS),
    put it on YouTube (unlisted works) and paste the link into **Gameplay
    video or trailer**. It's the strongest selling point.
- [ ] **Add the key pool.**
  - Work on a copy of the key file kept outside the repo. Never commit it.
  - Remove the key you tested Pro with and keep it as your own. The License
    tab shows its number (for example #00001). In a batch that started at 1,
    key #N is line N of the file.
  - Edit page → **Distribute** → **External keys**: add the keys with type
    **Other**, one per line. If itch won't take keys before the project has a
    file, do this right after the upload below.
  - Check the count (499), then **View all** and compare one key with the
    file. Keys are about 160 characters, so make sure none were cut short.
  - Record what's on itch (#00001–#00500 minus the test key). The next batch
    starts at 501.
- [x] **Add a paid file so the Buy button shows.** itch treats every file
      marked "demo" as free, so with only demo files the page has nothing to
      sell. Upload a small non-demo file, `Decay Pro - activation guide.txt`
      (how to find and enter the key, support email), and leave "demo"
      unticked. Done: the Buy and purchase buttons now appear.
- [ ] **Upload the signed zip with the web uploader** (Edit page → **Upload
      files**), not butler. butler unpacks a zip and itch then serves a zip it
      makes itself, so the SHA-256 on the website wouldn't match the download.
  - Keep the name `Decay-1.0.0.0-win-x64.zip`.
  - Tick **Windows** and **This file is a demo and can be downloaded for
    free**. The demo box appears once the project has a price.
- [ ] **Test the buying flow while the page is Restricted.**
  - Set visibility to **Restricted** and also allow a password to view the
    page.
  - On a clean Windows 11 VM with Smart App Control on, open the page from a
    second itch account and buy it (you pay yourself, less fees). A browser
    download carries the "from the internet" mark, so this is what a buyer
    sees.
  - Run `certutil -hashfile` on the downloaded zip: it must match the hash
    from step 3.
  - Extract it, run it, paste the key from the download page into the
    License tab, and confirm Pro unlocks.
  - In a private window, signed out, download the free file and confirm it
    runs as Free.
- [ ] **Set the page to Public.**

## 5. Update the website

The page work is prepared in two branches of `ToothbrushStudio/ToothbrushStudioWebsite`
(details: `docs/itch-release.md`, Part 2). Merging to `main` deploys, so merge
the launch branch only when the itch page is Public.

- [ ] **Merge `claude/decay-page-alignment`** (PR #57). Heading alignment, and
      the Download button is hidden until launch. Safe any time.
- [ ] **Fill in the hashes** in `wwwroot/data/decay-release.json`
      (`windows.sha256` and `console.sha256`) from step 3, on the launch
      branch.
- [ ] **Merge `claude/decay-page-launch`** (PR #58) once the signed zips are on
      itch and the page is Public. Merging it is the website launch: the
      Download button, Get Pro text and button, GitHub link, current send guide
      and itch URL (default in `appsettings.json`; `Decay__ItchUrl` overrides
      it) all go live together.
- [ ] **Deploy and check** the page, both downloads, the hash text and the Pro
      button.

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
- [x] **Turn on private vulnerability reporting** (Settings → Code security).
- [x] **Protect `main`:** Settings → Rules → New branch ruleset. Require a pull
      request and the `test` workflow's two jobs; block force pushes and
      deletions. Approvals are 0 because GitHub won't let a solo maintainer
      approve their own pull request. Rulesets are only enforced on public
      repos on the free plan; tested after going public: a direct push to
      `main` is refused and a pull request waits for the checks.
- [x] **Add `.github/CODEOWNERS`** with your GitHub handle.
- [x] **Add a CI workflow** (`.github/workflows/test.yml`, read-only, actions
      pinned to commit SHAs).
- [x] **Require approval for fork pull request workflows** (Settings → Actions
      → General).
- [x] **Turn on 2FA, secret scanning and push protection.**
- [x] **Switch the repo to public** (Settings → General → Danger Zone → Change
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
      number), add it on itch under **Distribute** → **External keys** (type
      **Other**), and note the new range.
- [ ] **Keep notes on support questions.** If people lose messages to typos or
      to chat apps, that shapes what v2 should do.

## Console edition (only if `decay.exe` ships with 1.0)

The changelog lists it for 1.0, but nothing above covers it. Either do these
or move it to a later release.

- [x] **Confirm the Windows CI job passes**: `decay selftest` on the real .NET
      Framework 4.8 (19 passed, license checks included) and the piped UTF-8
      round trips. Keep it green.
- [x] **Try it by hand on Windows**: the hidden passphrase prompt, and
      non-ASCII output in a `cmd.exe` and a PowerShell window.
- [ ] **Sign `decay.exe`** in the same signing run as the app. The four
      `System.*.dll` files beside it are already signed by Microsoft.
- [ ] **Zip `Decay.Cli/bin/cli`** as `Decay-1.0.0-console.zip` and add it to
      the same itch page with the web uploader. Tick **Windows** and the **demo**
      box: it must be a free download, because decrypting is free for everyone.

## Later, not for 1.0

- macOS build (needs a Mac, Xcode and an Apple Developer account)
- Linux command-line tool or web decoder
- iOS and Android builds
- Resilient mode and the v3/v4 features

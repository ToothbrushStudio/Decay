# Decay — Release Runbook

## Layout

| Project | Role | Ships |
|---|---|---|
| `Decay.Core` | Frozen v1 format, AES-256-GCM + PBKDF2, pixel codec, PNG codec | In every app |
| `Decay.Renderer` | Decay pipeline v1, carrier presets, watermark, tier policy | In every app |
| `Decay.Licensing` | Offline ECDSA license keys | In every app |
| `Decay.App` | .NET MAUI app: Android, iOS, Mac Catalyst, Windows | The product |
| `tools/Decay.KeyGen` | Publisher-side key pair + license signing | Never ships |

The engine (`Core` + `Renderer` + `Licensing`) has **zero external
dependencies** — image I/O included — and its Release assemblies total
**~54 KB**. The floppy budget lives.

## Build & test

```bash
dotnet test Decay.Core.Tests
dotnet test Decay.Renderer.Tests
dotnet test Decay.Licensing.Tests
```

88 tests; the FrozenFormatTests golden vector is the format freeze gate —
if it fails, the change breaks shipped images: revert the change, never
"fix" the test.

The app needs the MAUI workload once per machine: `dotnet workload install maui`.

```bash
dotnet build Decay.App -f net10.0-windows10.0.19041.0   # on Windows
dotnet build Decay.App -f net10.0-ios                    # on macOS (+Xcode)
dotnet build Decay.App -f net10.0-android
```

## Licensing operations (one-time setup)

1. `cd tools/Decay.KeyGen && dotnet run gen` — writes
   `decay-publisher-private.pem`. **Keep it offline and backed up; it must
   never enter the repository or a build.** Losing it stops new key sales
   but never invalidates a sold key.
2. Paste the printed public key into `Decay.App/PublisherKey.cs`.
3. Fulfillment, pick one:
   - **Serial pool (recommended, zero servers):**
     `dotnet run batch decay-publisher-private.pem 500 > keys.txt` emits
     500 anonymous keys ("Decay Pro #00001"…), one per line. Upload
     keys.txt as the license-key/serial pool on the payment platform
     (Gumroad, Lemon Squeezy, itch.io, Paddle all support this); each
     sale is handed the next key automatically. Track the last batch's
     end number and pass it as the start of the next
     (`dotnet run batch <pem> 500 501`).
   - **Manual, personalized:**
     `dotnet run sign decay-publisher-private.pem "Customer Name"` per
     sale — fine for low volume and gift keys.
   - **Webhook auto-signing (later, only if personalization at scale
     matters):** a small serverless function holding the private key
     signs on each sale. Note this puts the key on a server — weigh
     against the offline ethos.

Until step 2 is done, builds are Free-only by design and the License page
says so.

## Store distribution (Phase 5b, when mobile ships)

- **Apple / Google take-rate:** selling Pro *inside* the iOS/Android apps
  must use in-app purchase. `AppState` is the seam: a store tier source
  sits beside the license key check; nothing else in the app changes.
  Until then mobile builds are Free-tier + license paste (fine for
  TestFlight/internal).
- **Export compliance:** the app encrypts user messages with AES — answer
  **yes** to "does your app use encryption" on App Store Connect.
  `ITSAppUsesNonExemptEncryption` is intentionally NOT pre-set in the
  plists; classify honestly at first submission (standard-algorithm
  self-classification, annual report — routine, but a real step).
  Decide once, then set the plist key to stop the per-build question.

## Packaging notes

- **Windows:** `dotnet publish Decay.App -f net10.0-windows10.0.19041.0 -c Release -r win-x64 -p:SelfContained=true`
  (unpackaged and self-contained, the default; zip the publish folder). For an
  MSIX build, add `-p:WindowsPackageType=MSIX`. A packaged exe only runs once
  the MSIX is installed; launching the raw exe just spins and exits.
- **Android:** publish an `.aab` with your keystore
  (`-p:AndroidKeyStore=true` + signing properties).
- **iOS / Mac Catalyst:** archive from a Mac with an Apple Developer
  identity (`-p:ArchiveOnBuild=true`).
- **Code signing (Windows, required for launch):** unsigned builds are
  blocked outright by Smart App Control on customers' machines (there is
  no per-app exception) and flagged by SmartScreen. Sign the shipped exe
  or MSIX with an Authenticode certificate — Azure Trusted Signing is the
  cheap modern route — or distribute through the Microsoft Store, which
  signs for you. Local dev builds are unsigned by nature; dev machines
  run with Smart App Control off.
- PNG is the only export format everywhere. That is a product decision
  (lossless is survival), not a technical limitation — see
  docs/payload-format-v1.md.

## The promises that outrank everything in this file

1. Every shipped image decodes forever: format changes are additive, the
   compatibility ledgers in docs/ grow but never shrink.
2. Decryption never needs a license, an account, or a server.
3. The watermark never touches payload pixels (enforced in code, hard
   failure on violation).

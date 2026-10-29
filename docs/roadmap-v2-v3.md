# Decay — Roadmap sketch: v2 "Studio" and v3

*Status: planning sketch, not a spec. Nothing here changes format v1, which
stays frozen. Every item is additive and every future decoder keeps reading
every past version.*

## Ground rules (carried over from the README promises)

1. Decrypting is free, forever. That includes new containers: a free user can
   open a decoy image, extract an embedded file, and watch the reveal.
   Paid features gate **creating**, never **opening**.
2. Image + passphrase (+ keyfile, v3) is everything. No servers, no accounts.
3. Fragile by default. Anything that survives re-encoding is a separate,
   clearly labelled opt-in mode.
4. Zero-dependency engine. New code in `Decay.Core` / `Decay.Renderer` keeps
   the ~54 KB / no-NuGet property where possible.

## Tiers and pricing

| Tier | Price (target) | Contents |
|---|---|---|
| Free | $0 | Encrypt text with TV Static, watermarked. Decrypt everything. |
| Pro | ~$9.99 | Today's Pro (all presets, Rot, custom carrier, intensity, no watermark). |
| **Studio** | **$25** | Pro + everything marked **[Studio]** below. |

The license key already carries a tier field (`t` = `free` / `pro` in
`Decay.Licensing/LicenseKey.cs`). Add `studio` (Studio implies Pro). Existing
Pro keys keep working unchanged. Offer Pro-to-Studio as a discounted upgrade
key rather than a re-purchase.

---

# Version 2 — "Studio"

Headline: **one image, more than one secret, and more than text.**

## 2.1 Payload format v2 (the foundation, build first)

Container version byte `0x02`. v1 containers still decode through the existing
path. The decoder dispatches on the version byte.

### Layout sketch

```
Header (AAD-covered)
  magic "DCAY" | version 0x02 | pipeline id | pipeline param | flags
  slot count K (always the fixed table size, see padding)
Key slots, K x fixed size
  salt(16) | iterations(4) | nonce(12) | wrapped-entry-key(32) | tag(16)
  | entry index(1, inside the wrap)
Entry area
  entry 0 .. M-1: length(4) | nonce(12) | ciphertext | tag(16)
```

- Each passphrase derives a key-encryption key (PBKDF2, as v1). The decoder
  trial-unwraps every slot. The one that authenticates yields an **entry key**
  and an **entry index**. The entry is then decrypted with AES-256-GCM.
- **Decoy messages** are two entries, each reachable from its own slot:
  passphrase A opens the real message, passphrase B opens the decoy. In v3,
  multi-recipient is the same structure with several slots pointing at *one*
  entry, so the container work here already covers it.
- **Deniability needs padding.** Slot table is a fixed size (e.g. 4 slots);
  unused slots are random bytes, indistinguishable from real ones. Entries are
  padded with random bytes to a bucketed size so the entry sizes don't reveal
  which one is the decoy. The cost is capacity: a decoy image holds less real
  payload.
- **Cost of trial decryption:** K slots means up to K PBKDF2 runs per attempt
  (about 4 x 0.3 s at 600k iterations). Acceptable, but show progress.

### Plaintext envelope (inside each entry)

```
type(1)  0 = text, 1 = file
flags(1) bit0 = deflate-compressed
text:  UTF-8 bytes
file:  nameLen(2) | name(UTF-8) | mimeLen(1) | mime | data
```

- Compress before encrypting (a built-in deflate; BCL `DeflateStream` is fine
  in the app, and the engine can take a stream delegate if we want the engine
  itself to stay dependency-free).
- **Capacity**: 6 bits per pixel, so a 1920x1080 image carries about 1.5 MB.
  UI must show "fits / doesn't fit" live and offer to scale the carrier up.
  Custom carrier images give the most room.
- Filename handling on extract: strip any path separators, never trust the
  stored name for a path (zip-slip style bug). Treat extracted files as
  untrusted.

### Open question: pipelines vs decoys

Glitch (pipeline 1) is seeded from the public container hash, so the un-rot
animation works whichever passphrase opened the image. **Rot is not**: stains
are derived from the *real* message's characters and the passphrase-derived
mask. Options for Rot + decoy:
1. Disable Rot when decoy slots are used (simplest, recommended for 2.0).
2. Derive stains from slot 0 only; opening the decoy shows its text but skips
   the carrier recovery animation.
3. Make stains character-agnostic for v2 pipelines (changes the Rot concept).

Go with option 1 and document it. Revisit if people ask.

### Tests and freeze gate

- Golden-vector test for v2, like `FrozenFormatTests` for v1.
- Property tests: every slot opens exactly its own entry, wrong passphrases
  fail all slots, tampering any header or slot byte fails authentication.
- v1 golden vector must still pass untouched.
- Add a row to the compatibility ledger in the format doc.

## 2.2 Decoy messages **[Studio]**

- Encrypt tab: "Add decoy" reveals a second message + passphrase field.
- Decrypt tab: no change. It tries every slot; the user never learns whether
  a decoy exists. Do not show "slot 2 of 2" anywhere.
- Copy: explain honestly what it does and doesn't protect against (someone
  coerces a passphrase out of you; it is not magic against forensic analysis
  of the image statistics).

## 2.3 File payloads **[Studio]**

- Encrypt tab: "Attach file" (photo, PDF, voice note, zip) instead of, or
  alongside, text. Alongside means text + file as two entries behind one
  passphrase, or one entry with an envelope that holds both. Pick the
  single-envelope route to keep v2 simple: `type 2 = text + file`.
- Decrypt tab: "Save file" using the existing platform save dialog
  (`PlatformFileSaver`).
- Free users can **extract** files from any image.

## 2.4 New decay styles **[Studio]**

Each new style is a new **pipeline id**, specified in its own doc like
`render-pipeline-v2-rot.md`, and reuses format-level plumbing (pipeline id +
parameter byte). Unknown ids degrade to "no animation", so older builds still
decrypt.

| Id | Style | Idea | Invertible? |
|---|---|---|---|
| 0x03 | **Mold** | Rot's cousin: fuzzy fractal growth from a few seeds, hue by character | Approximate (blend), like Rot |
| 0x04 | **Burn** | Charred edges and ember glow spreading from the borders inward | Approximate |
| 0x05 | **Pixel-sort** | Rows/columns sorted by luminance in bands | Exact, stored as a seeded permutation of the band |
| 0x06 | **Dither bleed** | Ordered-dither color bleed along ciphertext-chosen axes | Exact if op is a per-channel XOR/add |
| 0x07 | **Datamosh smear** | Motion-vector style block smearing | Exact if block moves are permutations |
| 0x02+ | **Rot palettes** | Rot with alternative palettes: Frost, Ink, Rust, Neon | Same as Rot |

Rules for each: deterministic, integer-only, cross-platform bit-exact, seeded
from the public container hash, intensity mapped to a recognizable range (the
same lesson as the strength-slider fix in the changelog), and a golden test.
Aim to ship three new styles plus the Rot palettes in 2.0 and hold the rest.

## 2.5 Reveal export **[Studio]**

- The un-rot animation already exists in the app (`FramePlayer`). Add "Export
  reveal" on the Decrypt tab and a "Preview teaser" on the Encrypt tab.
- Output: animated GIF first (a small encoder fits the zero-dependency ethos),
  MP4 later via platform encoders. Cap resolution (e.g. 720 px) and length.
- Frames show the **message revealing**, so it must be opt-in with a
  warning: exporting a reveal publishes the plaintext. A teaser mode that
  plays the un-rot without showing text is the safer default for sharing.
- Free users get the watermark; this is also the main word-of-mouth surface.

## 2.6 Desktop workflow **[Studio]**

- Drag-and-drop onto the window; "Open with Decay" file association for `.png`.
- **Batch encrypt**: one message + one passphrase over a folder of carriers, or
  a CSV of (message, passphrase, carrier) rows. Run on a background task with
  progress and a per-item error list.
- `decay` CLI (new `tools/Decay.Cli`): `decay encrypt`, `decay decrypt`,
  `decay batch`. Reads the passphrase from an env var or prompt, never a
  command-line argument (it would end up in shell history and `ps`).
- Windows signing is already a launch requirement; extend that to the CLI.

## 2.7 Suggested build order for v2

1. Format v2 container + golden tests (nothing else is possible without it).
2. File payloads (smallest user-visible step on the new container).
3. Decoy slots + Encrypt/Decrypt UI.
4. Studio license tier and gating, with tests like `WatermarkAndTierTests`.
5. Pixel-sort, Dither bleed, one more style, Rot palettes.
6. Reveal export (GIF) and teaser mode.
7. Drag-and-drop, batch, CLI.
8. Store copy, screenshots, release notes (update `docs/launch-copy.md`).

---

# Version 3 — "Letters"

Headline: **keys you can hand someone, letters that feel like objects, and
messages that can survive.**

## 3.1 Multi-recipient letters **[Studio]**

Several passphrases each open the same entry. The container already supports
this from v2 (many slots, one entry). Work is UI: "Add recipient", a label
per recipient (labels live only in the sender's local archive, never in the
image), and a per-recipient generated passphrase.

## 3.2 Keyfiles and stronger unlock **[Studio]**

- Passphrase + keyfile: KEK = PBKDF2(passphrase || SHA-256(keyfile)). A
  header flag may tell the UI that a keyfile is needed, but it leaks that
  fact; make the hint optional.
- Diceware passphrase generator with entropy readout.
- Optional Argon2id in place of PBKDF2 via a new KDF id in the slot header.
  Argon2 isn't in the BCL, so this brings in the engine's first dependency
  or a hand-written implementation. Decide deliberately; it's the one item
  that threatens the zero-dependency property.
- The decoder must keep reading PBKDF2 slots forever.

## 3.3 Carrier packs and generator **[Studio]**

- Seeded procedural generators with parameters (palette, scale, density),
  saved as small JSON "recipes" the user can share. The recipe isn't needed to
  decrypt, since the carrier is just pixels.
- 10-15 additional presets across Erosion / Corruption / Necrosis.
- Custom-carrier tooling: crop, fit, "auto-tune intensity" so the result stays
  recognizable.

## 3.4 Resilient mode (opt-in) **[Studio]**

The biggest and riskiest item. An error-corrected embedding that survives
mild recompression, resizing, and ideally a screenshot.

- New payload version (the format doc already reserves this: "error-correction
  redundancy, if ever added, arrives as a new additive format version").
- Likely design: Reed-Solomon over a spread-spectrum or DCT-domain embedding
  rather than raw LSBs. This is a research project, not a feature. Budget a
  prototype and a robustness test harness (resize 50-100%, JPEG q60-95,
  screenshot captures from the real platforms) *before* promising anything.
- UI: a clearly separate toggle with copy that is honest about the trade-off
  ("this letter will survive a screenshot, and it will be less delicate").
  Capacity drops sharply, so expect text and small files only.
- Fragile stays the default, so the brand survives.

## 3.5 Stationery and letter formatting **[Studio]**

Typeset the message: a few fonts, signature (drawn or typed), wax-seal
stamps, paper textures. The formatted letter is the thing revealed on the
Decrypt tab. This is rendering above the envelope: the plaintext can carry a
small style block, so it needs an envelope type `3 = rich text` in the
plaintext spec.

## 3.6 Local archive **[Studio]**

A history of sent and received images with thumbnails and per-recipient
labels, stored only on the device (and encrypted at rest with a user-chosen
local passphrase or the OS keystore). Strictly no sync. Export/import of the
archive as an encrypted file keeps the "no servers" promise.

---

# More suggestions worth considering

Roughly ordered by value for effort.

**Safety and trust**
- **Verify before you send.** After creating an image, a one-tap "test
  decrypt" that re-opens the exported file to confirm it round-trips.
  Catches bad exports and share-sheet recompression early.
- **Share-path warnings.** Detect and warn when the target app is known to
  recompress (some messengers, social platforms) and suggest "send as file".
- **Signed letters.** Optional sender signature (Ed25519 or ECDSA, you already
  carry ECDSA) so a recipient can verify who made a letter. This needs a
  local identity key and a way to exchange public keys (QR code, in person).
- **Tamper evidence in the UI.** Distinguish "wrong passphrase" from "image was
  modified". GCM can't tell them apart (the spec says so), but you can detect
  that a downloaded image has lost its LSB pattern entirely (recompressed) and
  say that.

**Product feel**
- **Sound and haptics.** A low-volume decay crackle on encrypt, a reverse
  "sweep" on reveal. Cheap and memorable.
- **Time-locked reveal (cosmetic).** "Do not open before 24 Dec." Can't be
  enforced offline, so label it as etiquette, not security.
- **Reveal modes.** Slow cinematic un-rot vs. instant. Accessibility option
  for reduced motion.
- **Light/dark and font accessibility**, screen-reader support for the
  Decrypt result, high-contrast watermark.

**Distribution**
- **Gifting.** Sell Studio license keys as gift codes. Your offline signed
  keys and the `Decay.KeyGen` batch mode already make this nearly free.
- **Share extension / "Decay this image" quick action** on iOS, Android, and
  Windows Explorer context menu.
- **Print-friendly export** (PDF at print resolution). Pairs well with
  resilient mode if you ever attempt print-and-scan.
- **Landing page demo.** A web page that decrypts a sample image client-side
  (WASM). Doesn't send anything anywhere, and it's the best conversion tool
  you could build. It would need a WASM build of `Decay.Core`, which is
  feasible given it has no dependencies.

**Platform**
- **Linux build** (MAUI doesn't target it; the CLI and the engine do). A
  CLI-only Linux release is a cheap way to win over the privacy-minded crowd.
- **Library packaging.** Publish `Decay.Core` as a NuGet package and the
  format docs as an open spec. Cryptographers trust what they can read, and an
  open spec backs up the "every image decodes forever" promise.
- **Independent review.** Pay for a small crypto audit before shipping the v2
  container, since slots, wraps and padding are where mistakes hide.

## What I'd cut if scope gets tight

In v2: drop batch CSV, MP4 export, and two of the new styles. Keep decoys,
file payloads, GIF reveal, three styles. In v3: resilient mode is the only
item that can eat a year. Prototype it in isolation and decide on evidence.

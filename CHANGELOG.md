# Changelog

## 1.0.0.0 — launch (unreleased)

- **Pre-launch fixes**: the app no longer crashes on a damaged, unusual or
  huge image file, or when saving or sharing fails; each problem is
  explained. Editing anything after encrypting drops the finished image, so
  a letter that doesn't match the screen can't be saved. The Encrypt tab
  asks for the passphrase twice. The reveal starts in about 1.5 s instead of
  10 at 1024 px. The Decrypt tab says right away when an image holds no
  message, and no longer calls Pro custom-size images "resized". The window
  fits laptop screens, and the byte counter allows for the Free watermark.
  Console edition: piped passphrases and messages are read as UTF-8 (non-ASCII
  text used to be changed silently), damaged files get a clear error, and
  the checks run before the passphrase prompt. No format or crypto changes:
  golden vectors now pin the frozen Glitch and Rot pipelines, and the Rot
  spec is complete.

- **Console edition**: `decay.exe`, a command-line Decay for .NET Framework
  4.8 that runs on a stock Windows 10/11 machine with nothing installed and
  fits on a 1.44 MB floppy (about 400 KB). Encrypt and decrypt, Free and Pro
  tiers with offline license keys, and a `selftest` command. It reads and
  writes the same images as the app. No format or crypto changes.

- **Slimmer Encrypt tab**: Size, Intensity and the debug Pro override moved
  into a collapsed "Advanced" section; Carrier now has its own row; Play,
  Save and Share appear only after you encrypt; the send warning and the
  Help link are one button. The Help tab gained a "Test it before you send"
  section: decrypt your own saved image to confirm the passphrase and file
  work.

- **Help tab**: the send guide is now a tab after License (no back button to
  hunt for), with the app version and a website link. The Encrypt tab's
  button and the error dialogs jump to it.

- **Cloud-drive-first send guide**: the How to send page and error messages
  now lead with Google Drive / OneDrive / iCloud Drive links.

- **How to send guide and clearer errors**: a new "How to send it safely"
  page (linked from the Encrypt tab) lists the safe and destructive ways to
  send a Decay image. Decrypt failures now explain the likely cause — a
  re-compressed, resized or screenshotted image — and offer the guide. The
  open dialog accepts any image type so a JPEG/WebP copy gets an explanation
  instead of being hidden, and an image that isn't a Decay size is flagged.
  No format or crypto changes.

- **Strength slider stays recognizable**: Glitch's slider (1–255) now maps onto
  raw intensity 1–50 (it used to send raw 100+ by default, which obliterated
  the image). Rot gains a visible minimum. Decoding is unaffected.

- **Rot decay style (pipeline 2)**: an alternative to Glitch that keeps the
  image recognizable — each character stains the carrier with a green, brown
  or purple spore chosen by the character's value (masked with a
  passphrase-derived keystream so the colors leak nothing). Selectable on the
  Encrypt tab; Decrypt drains the stains and restores the carrier to within
  a couple of levels per channel.

The rewrite from prototype to product.

- **Frozen payload format v1**: AES-256-GCM + PBKDF2-HMAC-SHA256, versioned
  authenticated container embedded in pixel LSBs; decodes from image +
  passphrase alone. Golden-vector test guards the freeze.
- **Render pipeline v1**: 120 invertible, ciphertext-driven decay ops with
  an intensity dial (1–255, recorded in the authenticated header); exact
  reverse playback un-rots received images to the sender's carrier.
- **Five carrier presets** across the three decay stages — Erosion
  (TV Static, Scanlines), Corruption (Gradient Melt, Voronoi Shatter),
  Necrosis (Necrotic Blocks).
- **Free/Pro tiers**: payload-safe DECAY watermark on Free exports (hard
  failure on any overlap), preset/carrier/intensity gates, and offline
  ECDSA license keys — no activation server. Decryption is tierless,
  free, forever.
- **Built-in PNG codec** — the engine ends at ~54 KB with zero external
  dependencies.
- **Decay.App**: .NET MAUI (net10.0) for Windows, iOS, Android, and
  Mac Catalyst — encrypt/decrypt with animated rot and un-rot reveal,
  share-as-file flow, license activation page.
- Retired the WinForms alphabet-shift prototype.

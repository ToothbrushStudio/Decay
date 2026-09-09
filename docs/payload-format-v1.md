# Decay Payload Format — Version 1 (FROZEN)

*Status: frozen upon merge. Post-launch changes are additive only — new format
versions may be introduced, but every future decoder must read every past
version, forever. A letter encrypted at launch must still decode in 2036.*

---

## Overview

A Decay image carries an encrypted payload embedded in the pixel data of a
lossless PNG. Decoding requires only the image and the passphrase — no
accounts, no server, no original carrier file, no version handshake
(**the decode contract**).

Two layers:

1. **Payload container** — a byte sequence holding the encrypted message and
   everything needed to decrypt it (given the passphrase).
2. **Pixel encoding** — how those bytes are written into and read back from
   the image's pixels.

The visible glitch aesthetic is rendered *before* the payload is embedded and
is not consulted by the decoder. The decoder reads only the pixel encoding
described below.

## Survivability stance (decided)

**Pure fragility.** The payload does not survive re-encoding, resizing,
screenshotting, or any lossy transformation. Export is lossless PNG only.
Product UI and documentation must state clearly: *send the file, don't
post-and-screenshot.* Error-correction redundancy, if ever added, arrives as a
new additive format version.

---

## Layer 1: Payload container

All multi-byte integers are **big-endian, unsigned**.

| Offset | Size | Field | Value / meaning |
|---|---|---|---|
| 0 | 4 | Magic | `0x44 0x43 0x41 0x59` (ASCII `DCAY`) |
| 4 | 1 | Format version | `0x01` |
| 5 | 1 | Pipeline id | Decay render pipeline that produced the art; `0x00` = none, `0x01` = pipeline v1 (docs/render-pipeline-v1.md) |
| 6 | 1 | Pipeline parameter | Meaning defined by the pipeline. For pipeline v1: decay intensity in percent, 1–255, with `0x00` reserved to mean the default (100) |
| 7 | 16 | Salt | Random per message (PBKDF2 salt) |
| 23 | 4 | Iterations | PBKDF2 iteration count; valid range 1 – 10,000,000 |
| 27 | 12 | Nonce | Random per message (AES-GCM nonce) |
| 39 | 4 | Ciphertext length `N` | Byte length of the ciphertext |
| 43 | `N` | Ciphertext | AES-256-GCM ciphertext of the UTF-8 plaintext |
| 43+`N` | 16 | Auth tag | AES-GCM authentication tag |

Total container size: `59 + N` bytes.

The pipeline id and parameter let the decoder regenerate the exact decay-op
sequence for the reverse ("un-rot") animation. Both are authenticated like
the rest of the header but never gate decryption: an unknown pipeline id
means "no animation available", never a decode failure, and the parameter
byte is never validated by the format layer — its interpretation belongs
wholly to the pipeline it accompanies.

### Cryptography

- **Key derivation:** PBKDF2-HMAC-SHA256 over the UTF-8 passphrase with the
  container's salt and iteration count, producing a 32-byte key.
  Encoder default: 600,000 iterations (stored in the header, so future
  defaults can change without breaking decode).
- **Encryption:** AES-256-GCM. 12-byte nonce, 16-byte tag.
- **Associated data (AAD):** container bytes `[0, 43)` — the full fixed
  header. Any tampering with magic, version, pipeline id, pipeline parameter, salt,
  iterations, nonce, or the declared length invalidates the tag.
- **Plaintext:** the message as UTF-8 bytes. GCM is unpadded, so
  `N` = UTF-8 byte length of the message. Zero-length messages are valid.

### Decoder validation rules

1. Magic must equal `DCAY`, else: not a Decay image.
2. Version must be a version this decoder knows (`0x01`), else: newer-version
   error ("update Decay to read this image").
3. Iterations must be in `[1, 10,000,000]`, else: corrupt/hostile image
   (prevents a forged header from stalling the decoder with a
   multi-billion-iteration KDF).
4. `59 + N` must fit within the image's pixel capacity, else: corrupt image.
5. GCM tag verification failure means *wrong passphrase or modified image* —
   the two are cryptographically indistinguishable and must be reported as
   one condition. Never emit garbage plaintext.

---

## Layer 2: Pixel encoding

- Image pixels are treated in **row-major order** starting at the top-left
  pixel, in RGBA form. The alpha channel is never used or modified.
- Each pixel carries **6 payload bits** in the two least-significant bits of
  each color channel, in channel order **R, then G, then B**.
- The payload byte stream is consumed **MSB-first**. Within a channel's
  2-bit group, the earlier stream bit occupies the higher bit
  (bit 1), the later stream bit the lower (bit 0).
- The container occupies as many pixels as needed from the start of the
  sequence: `ceil((59 + N) * 8 / 6)` pixels. Trailing bits in the final
  pixel's last partially-used channel are zero; all later pixels are
  untouched by this layer.
- **Capacity:** `floor(W × H × 6 / 8)` bytes. Encoders must refuse messages
  whose container exceeds capacity.

### Watermark rule (encoder-side; not part of decoding)

The Free-tier watermark must only alter pixels **beyond the container's pixel
extent** (index ≥ `ceil((59 + N) * 8 / 6)`). The decoder never needs to know
whether or where a watermark exists. Encoders must verify non-overlap at
export time — a watermark that corrupts payloads is a launch-blocking bug.

### Rendering order

1. Render the carrier (procedural preset or custom upload).
2. Apply the visual decay/glitch transformation (deterministically driven by
   the ciphertext — the art is the message).
3. Embed the container into the LSBs as described above (imperceptible).
4. Apply watermark if Free tier, respecting the watermark rule.
5. Export as PNG (lossless) only.

---

## Compatibility ledger

| Version | Introduced | Status | Notes |
|---|---|---|---|
| 1 | v1.0 (launch) | Current | Initial frozen format |

Every future decoder must keep an entry here and a working read path for
every row in this table.

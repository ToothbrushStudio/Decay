# Resilient Mode — Plan for Version 2

*Status: planning document. Nothing here is implemented, and nothing here
changes frozen format v1. Numbers marked **(spike)** come from the
experiment in [`tools/resilient-spike`](../tools/resilient-spike/README.md);
numbers marked **(to measure)** are guesses that Phase 0 must replace with
real data.*

## 1. The goal, in one sentence

> Post a Decay image to a social app. Anyone who saves it from there can open
> it in Decay, type the passphrase, and read the letter.

Today that fails because every platform re-encodes uploads (usually to JPEG or
WebP), and the v1 pixel encoding hides data in the two lowest bits of each
colour channel, which re-encoding destroys by design. Resilient mode means
hiding the data somewhere re-encoding does **not** destroy, and adding enough
redundancy to repair what it does damage.

## 2. How hard is it? (the honest summary)

It splits into three levels of difficulty, and they should be built and judged
separately:

| Level | Survives | Difficulty | Verdict from the spike |
|---|---|---|---|
| **R1** | Re-compression only (JPEG/WebP, image stays the same size and shape) | Moderate | **Works.** Survives JPEG down to quality 40–50 and WebP at 75. |
| **R2** | R1 + the platform resizing, padding or cropping the image | Hard | Works *if the decoder can find the right scale and offset*. A 1-pixel shift alone destroys R1 (error rate 41%, spike). |
| **R3** | R2 + screenshots (non-integer scaling, UI around the image, colour shifts) | Research project | Not attempted. |

The key product insight is that **R1 can cover most real posting without
R2/R3** if the exported image is already the size and shape the platform
would keep. Then the platform has nothing to resize or crop, and only the
compression happens. See §6.

## 3. What platforms actually do to an image

Everything here is **(to measure)**. Phase 0 exists to turn this table into
facts, per platform, with real uploads. Expect these transformations:

- **Re-compress** to JPEG or WebP at some quality (often 70–90; some platforms
  go lower for big files).
- **Resize** so the long edge fits a cap. Caps vary a lot by platform, roughly
  from ~1000 to ~4000 px, and sometimes by account or surface (feed vs.
  full-screen vs. thumbnail).
- **Crop or pad** to allowed aspect ratios.
- **Strip metadata**, convert colour profiles to sRGB, flatten transparency.
- **Serve several variants.** What a viewer sees, what "save image" returns and
  what "download original" returns can all differ.
- **Not offer a save option at all** on some apps, in which case the receiver
  must screenshot (R3). Some mainstream apps behave this way. Check each
  platform before promising it.

Chat apps are a separate case: they usually recompress photos but treat
"send as file/document" as lossless. That already works with v1.

## 4. The technique

### 4.1 Where to hide the data

Hide bits in the **frequency domain** of the image's **brightness (luma)**
channel, in the same 8×8 block grid that JPEG itself uses:

1. Convert pixels to luma, split into 8×8 blocks, take the 2-D DCT of each.
2. In a handful of low/mid-frequency coefficients per block, force the value
   onto one of two interleaved grids depending on the bit (**quantization index
   modulation, QIM**): bit 0 snaps to multiples of Δ, bit 1 snaps to multiples
   of Δ offset by Δ/2.
3. Inverse-transform. Apply the luma change equally to R, G and B so colour
   is untouched.
4. To read: DCT again, and for each coefficient ask which grid it is nearest
   to. The distance gap is a *soft* confidence for the error-correction step.

Why this survives JPEG: JPEG's own loss is quantization of exactly these
coefficients. If Δ is comfortably larger than JPEG's quantization step for
that coefficient, rounding noise moves the value by less than Δ/4 and the bit
still reads correctly. Low and mid frequencies are used because JPEG keeps
them best.

### 4.2 Error correction

- **Reed–Solomon over GF(256)** with byte interleaving, plus **repetition
  with soft combining** of the raw sites. Both are small to write in C#
  (a few hundred lines) and need no dependencies, which keeps the engine's
  zero-dependency property.
- Stronger soft-decision codes (LDPC, convolutional + Viterbi) are a later
  optimisation if capacity matters more than simplicity.
- The AES-GCM tag stays the final judge. Whatever the ECC outputs, wrong bits
  fail authentication and the decoder says so. ECC can never make a wrong
  decode look right, and it never touches the key or the cipher.

### 4.3 The frame

```
[ sync / pilot area ][ header (heavy redundancy) ][ payload: v1 container, RS-coded, interleaved ]
```

- The **header** carries: encoding version, strength (Δ), RS rate, payload
  length. It is embedded with fixed, conservative settings (high repetition)
  so the decoder can always read it before it knows anything else.
- The **payload** is the unchanged v1 container (59 + N bytes), so the
  cryptography is exactly what it is today.
- Embedding positions are scrambled with a *public* fixed seed, so errors from
  localised damage spread across codewords instead of wiping one out.
- The decoder cannot use the passphrase to find positions, since it needs
  the data before it can derive any key. This is fine: positions only need to
  be unpredictable to the *platform*, not secret.

### 4.4 Spike results **(spike)**

1080×1080 image, 18,225 blocks, 8 coefficients per block = 145,800 raw bit
sites. Random bits, real libjpeg and libwebp (via Pillow), no resize. "Raw
BER" is the fraction of wrong bits before any error correction; "r=8" is a
rough proxy for combining 8 copies.

| Δ | PSNR after embed | JPEG q60 | JPEG q50 | JPEG q40 | WebP q75 | WebP q50 |
|---|---|---|---|---|---|---|
| 32 | ~36–38 dB | 0.0% | 0.0% | 0.1% | 8.5–14% | 23–33% |
| **48** | ~33–34 dB | 0.0% | 0.0% | 0.0% | **0.8–2.2%** | 7–11% |
| 64 | ~30–32 dB | 0.0% | 0.0% | 0.0% | 0.0–0.6% | 1.3–3.6% |

(Ranges are smooth vs. noisy carrier. The noisy carrier is the worst case.)

Other results:

- Three rounds of JPEG q75 in a row: 0.0% errors.
- Down to 1000 px and back to 1080 with perfect re-alignment: 0.0%. Down to
  540 and back: 0.2%. So **scaling is survivable once the scale is known**.
- Shifting the image by even 1 pixel: ~41% errors (coin flips). **Without
  grid alignment nothing works.**

What this tells us:

- A Δ around **48** is a good starting point: invisible damage budget big
  enough for JPEG down to ~q40 and WebP at ~q75, with a few percent raw
  errors that ECC handles easily.
- WebP is the harder channel than JPEG at the same nominal quality; test both.
- The cost of robustness is visible texture. PSNR 33–36 dB means a faint
  block-pattern in flat areas. On glitch art that is mostly hidden. On a
  smooth custom photo it can show. Strength should be a user control.

### 4.5 Capacity estimate **(spike-derived, rough)**

At 1080×1080 with 145,800 raw sites:

| Setting | Info bits | Usable plaintext |
|---|---|---|
| Repetition 4 + RS rate ¾ | ~27,000 | **≈ 3.3 KB** |
| Repetition 8 + RS rate ½ | ~9,100 | ≈ 1.1 KB |

1080×1350 gives about 25% more. So resilient letters are **text of up to a
few thousand characters, plus tiny extras**. They are not for photos or
files. This is a real product limit and the UI must show a live capacity meter.

## 5. Interaction with the existing visuals

This is the least obvious design problem and needs an early decision.

**Order of operations.** Render the decay art first, embed the resilient layer
second, export. The embedding changes pixels slightly, and the platform then
changes them more.

**The reveal.** Today the receiver gets a pixel-exact image and the app replays
the decay operations *backwards* to restore the original carrier. In a
recompressed image the pixels are no longer exact:

- **Rot (pipeline 2)** is a smooth blend of stains. Inverting it divides by
  (256 − alpha); small JPEG noise stays small. Expect a good-looking, slightly
  noisy un-rot. **Rot is the natural launch style for resilient mode.**
- **Glitch (pipeline 1)** is built from exact permutations and bit
  operations. Applying the inverse of an XOR or a block swap to JPEG-damaged
  pixels can *amplify* the damage (a flipped high bit is a huge error). It may
  produce garbage. Test this in Phase 0 before promising it. If it fails,
  Glitch in resilient mode shows the message without the carrier restoration,
  or uses only the inverse ops that degrade gracefully.
- **Fallback reveal.** If the carrier can't be restored, the decoder can still
  play a *forward* decay "in reverse" visually on the received image, and then
  show the letter. The message is exact; only the picture is approximate. The
  app should say so.

**Hybrid image (recommended).** An exported PNG can carry **both** layers: the
resilient layer, and then the exact v1 LSB container on top. The LSB writes
add ±1–3 per channel, far below Δ, so they don't disturb the robust layer.
- File intact (sent as a document): decoder finds the v1 magic, takes the
  exact path, and the reveal is perfect.
- File recompressed: v1 fails the magic check, the decoder falls back to the
  resilient path.
- Messages too long for the resilient capacity can still ship LSB-only, with a
  clear warning that they will not survive a feed.

## 6. Strategy for geometry: avoid the problem first, solve it second

Resizing and cropping are the real danger (§2). The cheapest defence is to
**never give the platform a reason to touch the geometry**:

- **Export "social-safe canvases":** 1080×1080 and 1080×1350 (and perhaps
  1080×566 for wide). These sit inside the limits of mainstream platforms
  **(to measure)**. If the platform keeps the size, the 8×8 grid stays aligned
  and only compression happens.
- Always export at exact multiples of 8 in both dimensions.
- Export upright (no EXIF rotation), opaque, sRGB.
- Keep the data off the outer few blocks, where platforms sometimes trim.

When a platform does change the geometry anyway (R2):

1. **Pilot pattern.** A known pattern embedded across the image that the
   decoder can look for. It detects scale and offset: try a set of candidate
   scales (the common platform caps), and for each find the 8×8 phase with the
   strongest pilot match.
2. **Resample to canonical.** Once found, resample the received image to the
   canonical grid and run the normal decoder. The spike shows this works
   (0–0.2% errors after a perfect round trip).
3. **Tiling.** Repeat the payload in tiles so a crop that removes a margin
   still leaves a decodable copy.

R3 (screenshots) adds a continuous scale, small rotations, and bars around the
image. It needs a proper registration step (for instance a log-polar pilot, or
detecting the block grid by autocorrelation). Treat it as a separate research
phase with its own go/no-go.

## 7. Where it fits in the format

Frozen format v1 is a payload container (Layer 1) plus a pixel encoding
(Layer 2). Resilient mode is a **new Layer-2 encoding**. The container, KDF,
cipher and AAD are untouched.

- Add an **encoding ledger** next to the existing compatibility ledger:
  `lsb-v1` (today) and `robust-v1` (this work).
- **Decode order:** try `lsb-v1` first (check the `DCAY` magic). If that fails,
  try `robust-v1` (look for sync, read the header, decode, then run the same
  container validation rules). If both fail, "not a Decay image, or too
  damaged".
- New spec: `docs/robust-encoding-v1.md`, frozen the way v1 is. Frozen means
  an image made in 2026 must still decode in 2036, so the DCT definition,
  coefficient choice, scrambling, frame layout and RS parameters are all
  part of the contract.
- **Determinism.** Extraction is statistical, so tiny float differences
  between platforms are harmless. But the *encoder* should still be
  reproducible for tests, so implement the DCT in fixed-point integer
  arithmetic, matching the project's "integer math only" ethos.
- The pipeline id/parameter bytes keep working as today. Resilient mode adds
  the frame header for its own parameters.

Compatibility: old apps see a resilient image as ordinary art (no v1 magic) and
say "not a Decay image". That's acceptable, but the error message should
suggest updating.

## 8. App and UX changes

**Encrypt tab**
- A mode switch: **Original** (fragile, exact, large capacity) vs **Feed**
  (resilient). Fragile stays the default, so the brand's "delicate letter"
  idea is intact; Feed is the "postable letter" mode.
- Live capacity meter (characters used / available), automatic canvas choice,
  and a strength control mapped onto Δ.
- A **"Test it"** button that runs the on-device channel simulation (JPEG q80,
  q60, WebP q75 …) against the image just made and reports pass/fail before
  the user posts.
- Honest copy: "This letter will survive being posted. It's less delicate, and
  text only."

**Decrypt tab**
- "Open image" accepts JPEG, WebP and PNG, not only PNG. The engine takes a
  raw pixel buffer (`PixelBuffer` already exists), and the *app layer* does the
  decoding with the platform image APIs. That means the engine does not gain
  a JPEG/WebP dependency.
- If no Decay data is found, give a useful hint: "Try saving the image at full
  size from the original post, or ask the sender to share the file."
- Decrypting stays free for everyone, including from feed-downloaded images.

**Per-platform guide.** A short in-app page listing, for each platform, how to
save the image at best quality (once Phase 0 has measured it) and which
platforms are unsupported (e.g. no way to save).

**Licensing.** Creating Feed-mode images is a Studio feature. Reading them is
always free.

**Watermark (Free tier).** The Free watermark must not overlap the resilient
layer's blocks. Reserve its blocks from embedding, the same non-overlap rule
the v1 spec has, with a hard failure on overlap.

## 9. Testing plan

1. **Channel simulator** in a separate, non-shipping test project, which may
   use a normal imaging library (ImageSharp or SkiaSharp) for JPEG/WebP. It
   sweeps JPEG quality 30–95, WebP 40–95, chroma subsampling on/off, 1–3
   re-encodes, and the scale/crop cases. Pass criterion: **≥ 99% decode success
   above the supported quality floor**, with a chart of the failure edge.
2. **Real-platform corpus.** Fixtures downloaded from real platforms (small
   files) checked into the tests. These are the ground truth; re-run
   periodically because platforms change their pipelines without notice.
3. **Golden vector** for `robust-v1`, like `FrozenFormatTests`.
4. **Hostile inputs.** Forged headers declaring huge lengths, random images
   that mimic sync, truncated files. Cap every length and iteration count,
   same as the v1 decoder rules.
5. **False positives.** Plain photos must not decode to anything. GCM already
   guarantees this cryptographically; the test makes sure the decoder also
   exits fast and doesn't burn time on PBKDF2 for garbage.
6. **Visual QA.** Side-by-side: carrier, Original-mode output and Feed-mode
   output across all presets and a few custom photos, at every strength.

## 10. Phased plan

Effort numbers are rough ranges for one developer and assume no surprises.

| Phase | Work | Exit criterion (go / no-go) |
|---|---|---|
| **0. Measure** (1–2 wk) | Upload test images to each target platform in each surface, download every available variant, record format, dimensions, quality (from JPEG quant tables). Port the spike to C# against the real pixel buffer. Test Glitch-inverse on degraded pixels. | At least the priority platforms leave the image in a decodable state at the chosen canvas, and we know which save paths work. If none do, stop here. |
| **1. R1 engine** (3–5 wk) | `Dct`, `ReedSolomon`, `RobustCodec` in `Decay.Core`; frame + header; decode order; fixed-point DCT; golden vectors; channel simulator. | ≥ 99% decode across the sweep at q ≥ 50 / WebP ≥ 75. |
| **2. App integration** (3–4 wk) | Feed mode on Encrypt, JPEG/WebP open on Decrypt, capacity meter, Test-it button, hybrid image, Rot + reveal behaviour, Studio gating, watermark exclusion. | Round trip through two real platforms by hand, on each OS. |
| **3. R2 geometry** (3–6 wk) | Pilot, scale/offset search, canonical resample, tiling. | Survives the platform resizes measured in Phase 0 without special canvases. |
| **4. R3 screenshots** (research, 4+ wk, optional) | Registration under arbitrary scale/crop; prototype in isolation. | A written go/no-go with measured success rate. Don't ship on hope. |
| **5. Harden** (2–3 wk) | Spec freeze, independent crypto/format review, docs, store copy, per-platform guide. | Review findings closed. |

A sensible release is **Phase 0 → 2** (R1 + hybrid + social-safe canvases),
then add Phase 3 in a point release. Phase 4 only ships if it clears its gate.

## 11. Work breakdown (files)

- `Decay.Core`: `Dct.cs` (fixed-point), `ReedSolomon.cs`, `RobustCodec.cs`,
  decode-order change in the decode entry point. New exceptions mirror the
  existing ones in `DecayExceptions.cs`.
- `Decay.Renderer`: a post-render embed step; watermark reservation in
  `Watermark.cs`; tier policy in `DecayProduct.cs` for the new mode.
- `Decay.Licensing`: Studio tier (the key already carries a tier field).
- `Decay.App`: `EncryptPage` (mode switch, meter, test), `DecryptPage` (open
  JPEG/WebP), platform image decoding.
- `Decay.*.Tests` plus a new non-shipping channel-simulator project.
- Docs: `robust-encoding-v1.md`, a ledger update to the format doc, README and
  launch copy.

## 12. Risks and open questions

**Risks**
- **Platforms change.** They may change their pipelines at any time. Mitigate
  with a conservative strength, the corpus tests, and a built-in "Test it".
- **Visible texture.** Strength vs. looks. Solve with perceptual weighting
  (embed more in busy regions, less in flat ones) in a later pass.
- **No save option on some apps** means R3 or no support there. Don't promise
  what the platform blocks.
- **Brand tension.** The README says fragility is the point. Resilient mode
  must stay opt-in and clearly named so it adds a new kind of letter rather
  than undoing the old one.
- **Scope.** R3 can absorb unlimited time. Hold the gate.
- **False comfort.** A post that survives today may not tomorrow. Say "tested
  with" in the UI, never "guaranteed".

**Open questions**
1. Which platforms are the launch priority? This decides the Phase 0 matrix
   and the canvas sizes.
2. Is Glitch worth supporting in Feed mode, or do we launch Rot and one or
   two new smooth styles first? (Depends on the Phase 0 inverse test.)
3. Do we want the hybrid image by default, or a separate export?
4. Is a few-KB text limit acceptable, or does the product want a "link to a
   file" scheme? (A link would need hosting, which breaks the no-servers
   promise, so the recommendation is no.)
5. Fixed-point DCT from day one, or float first and freeze later? Recommend
   fixed-point from day one: retrofitting a frozen spec is painful.
6. Should Feed mode cost Studio price, or be bundled in Pro to grow reach?
   The viral loop (people see posts, then download Decay) argues for making
   *reading* free (already decided) and *creating* paid.

## 13. Recommended next step

Do **Phase 0** and nothing else yet. It's about two weeks, it is cheap, and it
answers the questions that decide everything after it: what each platform
really does, whether Glitch survives, and which canvases are safe. Everything
in this document is a hypothesis until those measurements exist.

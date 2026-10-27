# Decay Render Pipeline — Version 2: Rot (pipeline id `0x02`)

*Status: experimental. The payload container is unchanged (format v1); only
the pipeline id/parameter bytes differ. Older builds read these images fine —
an unknown pipeline id means "no animation", never a decrypt failure.*

## Concept

Pipeline 1 scrambles the carrier. Rot keeps it recognizable and instead
**stains** it: each character of the message drops one spore of rot onto the
image, and the character's *value* sets the spore's color on a
sickly-green → brown → bruise-purple ramp. The message itself lives only in
the AES-GCM container; the stains are art, not a second data channel.

## Character values

Index map (the original prototype's ordering): `A=0, a=1, B=2, b=3 … Z=50,
z=51`, digits `52–61`, then space, ASCII punctuation, newline, tab
(`62–96`). Every other code point folds into `97 + cp % 53`, so indices span
`0–149`. Text is walked by Unicode scalar (surrogate pairs count once).

## Masking (why the colors don't leak the message)

Spore positions come from a public seed, so an unmasked color would read the
message straight off the image. Each index is therefore shifted:

`index' = (index + mask_i) mod 150`, with `mask_i` drawn from xoshiro256**
seeded by `HMAC-SHA256(PBKDF2 key, "decay-rot-v1-mask")` — the same
passphrase-derived key that protects the message. Without the passphrase the
masked values are uniformly random. This costs one extra PBKDF2 run on each
side.

## Plan (integer math only)

- Public seed: `SHA-256("decay-rot-v1" || container)`, xoshiro256**. First
  draw is the noise seed; then per character, in order: `x = Draw(10000)`,
  `y = Draw(10000)`, `radiusScale = 70 + Draw(60)`.
- Parameter byte = intensity percent (1–255, `0` ⇒ 100). Peak spore alpha =
  `clamp(intensity*90/100, 8, 180)` of 256; wash alpha =
  `clamp(intensity*22/100, 1, 60)`.
- Spores cover ~85% of the area in total: base radius from the character
  count, clamped to `[4, 35% of the short side]`.
- Each pixel takes the **single strongest** spore (soft quadratic falloff,
  value-noise ragged edges, per-pixel speckle), competing against a faint
  olive wash. One stain per pixel keeps the blend invertible.
- Blend: `p' = p + ((t − p)·a + 128 >> 8)`. Inverse:
  `p = floor(((p' << 8) − t·a + (256−a)/2) / (256−a))`, clamped.

## Recovery

The receiver opens the message, rebuilds the plan from plaintext + passphrase,
and inverts the blend. Result is the carrier to within ±2 per channel (±8 on
the first pixels, which also carry the payload's LSBs) — close, not
bit-exact like pipeline 1, because a blend toward a color is lossy at 8 bits.

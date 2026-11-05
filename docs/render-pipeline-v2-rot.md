# Decay Render Pipeline — Version 2: Rot (pipeline id `0x02`)

*Status: **FROZEN** (v1.0). The payload container is unchanged (format v1);
only the pipeline id/parameter bytes differ. The decoder rebuilds the stain
plan below bit-for-bit to restore the carrier, so any behavioral change
breaks the reveal of every Rot image already sent. Improvements ship as a new
pipeline id. Images declaring an unknown pipeline id still decrypt, with no
animation. The golden vectors in
`Decay.Renderer.Tests/FrozenPipelineTests.cs` guard this spec; the reference
implementation is `Decay.Renderer/RotDecay.cs`.*

## Concept

Pipeline 1 scrambles the carrier. Rot keeps it recognizable and instead
**stains** it: each character of the message drops one spore of rot onto the
image, and the character's *value* sets the spore's color on a
sickly-green → brown → bruise-purple ramp. The message itself lives only in
the AES-GCM container; the stains are art, not a second data channel.

## Conventions

- All arithmetic is integer. `/` on non-negative values is floor division.
  Where an operand can be negative, `/` **truncates toward zero** and `>>` is
  an **arithmetic** shift (floor division by a power of two). Each formula
  below says which applies.
- `xoshiro256**`, its seeding from 32 bytes (four little-endian u64s, with the
  all-zero fix) and `Draw(n) = Next() mod n` are exactly as in
  [render-pipeline-v1.md](render-pipeline-v1.md).
- `clamp(v, lo, hi)` limits `v` to `[lo, hi]`; `isqrt(v)` is the floor of the
  square root.

## Inputs

The sealed container `C` (all of its bytes, header through tag), the
plaintext `P` exactly as decrypted, and the passphrase. The parameter byte
is the intensity: `I = 100` if it is `0`, otherwise the byte (1–255).

## Character values

`P` is walked by Unicode scalar value: a UTF-16 surrogate pair counts once,
and an unpaired surrogate counts as U+FFFD (which is also what UTF-8
encoding of the plaintext produces). For a scalar value `v`:

| `v` | Index |
|---|---|
| `A`–`Z` | `(v − 'A')·2` |
| `a`–`z` | `(v − 'a')·2 + 1` |
| `0`–`9` | `52 + (v − '0')` |
| one of the 35 symbols below | `62 + position in the list` |
| anything else | `97 + (v mod 53)` |

The symbols, in order, written as a C# string literal (space first, then
`!` … `~`, then newline and tab):

```
" !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~\n\t"
```

Indices therefore span `0–149`. Carriage return (U+000D) is not in the list;
it folds to `97 + 13 = 110`.

## Masking (why the colors don't reveal the characters directly)

Spore positions come from a public seed, so an unmasked color would read the
message straight off the image. Each index is therefore shifted:

`index' = (index + mask.Draw(150)) mod 150`

where `mask` is xoshiro256\*\* seeded with
`HMAC-SHA256(K, ASCII("decay-rot-v1-mask"))` and `K` is the 32-byte PBKDF2
key that protects the message (`PBKDF2-HMAC-SHA256` of the UTF-8 passphrase
with the container's salt and iteration count). This costs one extra PBKDF2
run on each side.

*Security note:* xoshiro256\*\* is not a cryptographic generator. Treat the
stains as decoration: the message's confidentiality rests on AES-256-GCM
alone.

## Spores

`place` is xoshiro256\*\* seeded with
`SHA-256(ASCII("decay-rot-v1") || C)`. Its first output is the 64-bit
`noiseSeed`. Then, for each scalar value of `P` in order, `mask` draws once
(above) and `place` draws `X = Draw(10000)`, `Y = Draw(10000)`,
`S = 70 + Draw(60)`. Spore `s` is `(X, Y, S, index')`.

## Hash and value noise

64-bit unsigned arithmetic, modulo 2⁶⁴ (`x`, `y` are never negative here):

```
Hash(x, y, salt):
  z = noiseSeed + x·0x9E3779B97F4A7C15 + y·0xC2B2AE3D27D4EB4F + salt·0x165667B19E3779F9
  z = (z ^ (z >> 30)) · 0xBF58476D1CE4E5B9
  z = (z ^ (z >> 27)) · 0x94D049BB133111EB
  return z ^ (z >> 31)
```

Bilinear value noise, 0–255 (`>>` arithmetic):

```
ValueNoise(x, y, cell, salt):
  ix = x / cell;  iy = y / cell
  fx = (x mod cell)·256 / cell;  fy = (y mod cell)·256 / cell
  vAB = Hash(ix + A, iy + B, salt·16) & 0xFF    for A, B in {0, 1}
  top    = v00 + ((v10 − v00)·fx >> 8)
  bottom = v01 + ((v11 − v01)·fx >> 8)
  return top + ((bottom − top)·fy >> 8)
```

## The plan

The plan gives every pixel a stain color and a blend alpha. It depends only
on the spores, `noiseSeed`, `I` and the image size, never on pixel data, so
both sides compute the same plan.

**Grid.** Everything is computed on solid blocks so the rot reads as pixel
decay:

```
block     = max(2, min(W, H) / 80)
bw        = ceil(W / block);  bh = ceil(H / block);  blockCount = bw·bh
minBlocks = min(bw, bh)
cell      = max(2, minBlocks / 16)
peak      = 24 + (I − 1)·156 / 254        (24 … 180, of 256)
washPeak  = 4 + (I − 1)·52 / 254
```

**Spore coverage** (skipped when `P` is empty). Square spores jointly cover
about 150% of the grid:

```
baseRadius = isqrt((blockCount·150 / 100) / (4·N))       N = number of spores
baseRadius = clamp(baseRadius, 2, max(2, minBlocks·35 / 100))
```

Every block starts with `best = 0` and no owner. For each spore `s` in
order, with `cx = X·bw / 10000`, `cy = Y·bh / 10000` and
`radius = max(2, baseRadius·S / 100)`, visit every block `(x, y)` with
`|x − cx| ≤ radius`, `|y − cy| ≤ radius` inside the grid:

```
d = max(|x − cx|, |y − cy|)                 (Chebyshev: square rings)
if d >= radius: skip
w = 2·((radius − d)·256 / radius)
w = w + (ValueNoise(x, y, cell, 1) − 128) / 2      (/ truncates toward zero)
w = clamp(w, 0, 256)
w = (w + 32) / 64 · 64                       (four flat levels)
if w == 0: skip
if w > best[block]: best[block] = w; owner[block] = s
```

The comparison is strict, so on a tie the earlier spore keeps the block.

**Per pixel** `(x, y)`, with `bx = x / block`, `by = y / block`:

```
washAlpha = (washPeak·(160 + ValueNoise(bx, by, cell·3, 2)·96 / 255)) >> 8
alpha = washAlpha;  color = Wash
if the block has an owner s:
  speckle    = Hash(bx, by, 3) & 0xFF
  strength   = (peak·min(best, 256)) >> 8
  sporeAlpha = (strength·(205 + speckle·50 / 255)) >> 8
  if sporeAlpha > washAlpha:
    alpha = sporeAlpha;  color = Stain(StainPosition(index' of s, bx, by))
alpha = clamp(alpha, 0, 255)
```

**Color.** A block's position on the 0–510 ramp starts at the character's
value; smooth noise and per-block jitter carry neighboring blocks across the
ramp, so each stain mixes green, brown and purple:

```
StainPosition(index', bx, by):
  start  = index'·510 / 149
  drift  = (ValueNoise(bx, by, max(2, cell·3 / 4), 5) − 128)·3
  jitter = ((Hash(bx, by, 6) & 0xFF) − 128) / 4      (/ truncates toward zero)
  return (start + drift + jitter) mod 511            (non-negative result, 0–510)

Stain(s) = s ≤ 255 ? Lerp(Green, Brown, s) : Lerp(Brown, Purple, s − 255)
Lerp(a, b, t) = a + (b − a)·t / 255  per channel   (/ truncates toward zero)
```

| Color | R | G | B |
|---|---|---|---|
| Green | 86 | 130 | 44 |
| Brown | 112 | 78 | 40 |
| Purple | 88 | 40 | 112 |
| Wash | 104 | 98 | 48 |

## Blend

Each pixel is blended once toward its single stain color; alpha is never
touched. For each of R, G, B (`>>` arithmetic):

- Encode: `p' = p + (((t − p)·a + 128) >> 8)`
- Decode: `keep = 256 − a`,
  `p = floor(((p' << 8) − t·a + keep / 2) / keep)`, clamped to `[0, 255]`
  (floor division, also for negative numerators).

## Rendering order

1. Render the carrier.
2. Apply the Rot blend above.
3. Embed the container into the LSBs (payload-format-v1.md).
4. Apply the Free watermark if any (Rot is a Pro style, so normally none).

## Recovery

The receiver opens the message, rebuilds the plan from plaintext + passphrase,
and inverts the blend. Result is the carrier to within ±2 per channel (±8 on
the first pixels, which also carry the payload's LSBs) — close, not
bit-exact like pipeline 1, because a blend toward a color is lossy at 8 bits.

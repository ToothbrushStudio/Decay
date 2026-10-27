using System.Security.Cryptography;
using System.Text;
using Decay.Core;

namespace Decay.Renderer
{
    /// <summary>
    /// The "Rot" decay style (pipeline id 2): instead of scrambling pixels,
    /// every character of the message stains the carrier with a spore of rot
    /// whose color — sickly green, through brown, to bruise purple — is set
    /// by the character's value. The image stays recognizable; it just looks
    /// diseased. See docs/render-pipeline-v2-rot.md.
    ///
    /// Each pixel is blended once toward a single stain color, so the
    /// receiver can invert it to within a couple of levels. The message
    /// itself lives only in the AES container; this layer is aesthetic and
    /// leaks nothing, because each character's value is masked by a
    /// keystream only the passphrase holder can derive.
    /// </summary>
    public sealed class RotDecay
    {
        public const byte PipelineId = 2;
        public const int DefaultIntensity = 100;

        private const string SubkeyLabel = "decay-rot-v1-mask";
        private const int MinPeakAlpha = 24; // of 256
        private const int MaxPeakAlpha = 180; // of 256
        private const int MinSporeRadius = 4;

        private static readonly (int R, int G, int B) Green = (86, 130, 44);
        private static readonly (int R, int G, int B) Brown = (112, 78, 40);
        private static readonly (int R, int G, int B) Purple = (88, 40, 112);
        private static readonly (int R, int G, int B) Wash = (104, 98, 48);

        private readonly Spore[] _spores;
        private readonly ulong _noiseSeed;

        /// <summary>Decay intensity in percent (1–255); 100 is the default.</summary>
        public int Intensity { get; }

        private RotDecay(Spore[] spores, ulong noiseSeed, int intensity)
        {
            _spores = spores;
            _noiseSeed = noiseSeed;
            Intensity = intensity;
        }

        /// <summary>
        /// Builds the rot a container's pipeline id calls for, or null when
        /// the container is not a Rot image. Needs the plaintext and
        /// passphrase because the stains depend on both — the receiver has
        /// them once the message has been opened.
        /// </summary>
        public static RotDecay? FromContainer(
            byte[] container,
            string plainText,
            string passphrase
        )
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(plainText);

            if (DecayCipher.GetPipelineId(container) != PipelineId)
            {
                return null;
            }

            byte parameter = DecayCipher.GetPipelineParameter(container);
            int intensity = parameter == 0 ? DefaultIntensity : parameter;

            byte[] publicSeed = SHA256.HashData(
                [.. "decay-rot-v1"u8, .. container]
            );
            Xoshiro256 place = new Xoshiro256(publicSeed);
            Xoshiro256 mask = new Xoshiro256(
                DecayCipher.DeriveSubkey(container, passphrase, SubkeyLabel)
            );
            ulong noiseSeed = place.Next();

            List<Spore> spores = new List<Spore>();

            foreach (Rune rune in plainText.EnumerateRunes())
            {
                int index = (RotAlphabet.IndexOf(rune) + mask.Draw(RotAlphabet.Size))
                    % RotAlphabet.Size;

                spores.Add(new Spore(
                    place.Draw(10000),
                    place.Draw(10000),
                    70 + place.Draw(60),
                    index
                ));
            }

            return new RotDecay(spores.ToArray(), noiseSeed, intensity);
        }

        /// <summary>Rots a carrier: returns a new, fully decayed image.</summary>
        public PixelBuffer Apply(PixelBuffer carrier)
        {
            ArgumentNullException.ThrowIfNull(carrier);

            PixelBuffer image = carrier.Clone();
            Plan plan = BuildPlan(image.Width, image.Height);
            byte[] pixels = image.Pixels;

            for (int i = 0; i < plan.Alpha.Length; i++)
            {
                int alpha = plan.Alpha[i];
                int offset = i * PixelBuffer.BytesPerPixel;

                for (int c = 0; c < 3; c++)
                {
                    int p = pixels[offset + c];
                    int target = plan.Color[i * 3 + c];

                    pixels[offset + c] = (byte)(p + (((target - p) * alpha + 128) >> 8));
                }
            }

            return image;
        }

        /// <summary>
        /// Drains the rot from a received image, returning the carrier to
        /// within a few levels per channel (rounding in the blend, plus the
        /// invisible payload bits and any watermark).
        /// </summary>
        public PixelBuffer Restore(PixelBuffer rotted)
        {
            ArgumentNullException.ThrowIfNull(rotted);

            PixelBuffer image = rotted.Clone();
            Plan plan = BuildPlan(image.Width, image.Height);
            byte[] pixels = image.Pixels;

            for (int i = 0; i < plan.Alpha.Length; i++)
            {
                int alpha = plan.Alpha[i];
                int keep = 256 - alpha;
                int offset = i * PixelBuffer.BytesPerPixel;

                for (int c = 0; c < 3; c++)
                {
                    int target = plan.Color[i * 3 + c];
                    int numerator = (pixels[offset + c] << 8) - target * alpha + (keep >> 1);
                    int p = numerator >= 0
                        ? numerator / keep
                        : -((-numerator + keep - 1) / keep);

                    pixels[offset + c] = (byte)Math.Clamp(p, 0, 255);
                }
            }

            return image;
        }

        /// <summary>
        /// A frame of the cross-fade between two same-size images, for the
        /// rot/un-rot animation. step 0 is <paramref name="from"/>, step
        /// <paramref name="steps"/> is <paramref name="to"/>.
        /// </summary>
        public static PixelBuffer Interpolate(PixelBuffer from, PixelBuffer to, int step, int steps)
        {
            ArgumentNullException.ThrowIfNull(from);
            ArgumentNullException.ThrowIfNull(to);
            ArgumentOutOfRangeException.ThrowIfNegative(step);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(step, steps);

            if (from.Width != to.Width || from.Height != to.Height)
            {
                throw new ArgumentException("Images must be the same size.", nameof(to));
            }

            PixelBuffer frame = from.Clone();

            for (int i = 0; i < frame.Pixels.Length; i++)
            {
                if (i % PixelBuffer.BytesPerPixel == 3)
                {
                    continue;
                }

                int a = from.Pixels[i];
                int b = to.Pixels[i];

                frame.Pixels[i] = (byte)(a + (b - a) * step / steps);
            }

            return frame;
        }

        // ------------------------------------------------------------ plan

        private readonly record struct Spore(int X, int Y, int RadiusScale, int Index);

        private sealed class Plan
        {
            internal required byte[] Alpha { get; init; }
            internal required byte[] Color { get; init; } // RGB per pixel
        }

        /// <summary>
        /// The per-pixel stain (color + blend alpha) for an image size. Depends
        /// only on the message, key and size — never on pixel data — so the
        /// encoder and decoder compute the identical plan. Integer math only.
        /// </summary>
        private Plan BuildPlan(int width, int height)
        {
            int pixelCount = width * height;
            int minDimension = Math.Min(width, height);
            int cell = Math.Max(4, minDimension / 32);
            // Even intensity 1 must visibly rot; 255 stays mostly recognizable.
            int peak = MinPeakAlpha + (Intensity - 1) * (MaxPeakAlpha - MinPeakAlpha) / 254;
            int washPeak = 4 + (Intensity - 1) * 52 / 254;

            int[] best = new int[pixelCount];
            int[] owner = new int[pixelCount];

            Array.Fill(owner, -1);

            if (_spores.Length > 0)
            {
                // Spores jointly cover about 85% of the image, however many
                // characters there are.
                long areaTarget = (long)pixelCount * 85 / 100;
                int baseRadius = (int)Math.Sqrt(areaTarget * 100.0 / (_spores.Length * 314.0));

                baseRadius = Math.Clamp(baseRadius, MinSporeRadius, Math.Max(MinSporeRadius, minDimension * 35 / 100));

                for (int s = 0; s < _spores.Length; s++)
                {
                    Spore spore = _spores[s];
                    int cx = spore.X * width / 10000;
                    int cy = spore.Y * height / 10000;
                    int radius = Math.Max(MinSporeRadius, baseRadius * spore.RadiusScale / 100);
                    long r2 = (long)radius * radius;

                    for (int y = Math.Max(0, cy - radius); y <= Math.Min(height - 1, cy + radius); y++)
                    {
                        for (int x = Math.Max(0, cx - radius); x <= Math.Min(width - 1, cx + radius); x++)
                        {
                            long d2 = (long)(x - cx) * (x - cx) + (long)(y - cy) * (y - cy);

                            if (d2 >= r2)
                            {
                                continue;
                            }

                            int u = (int)((r2 - d2) * 256 / r2);
                            int w = u * u >> 8;

                            // Ragged, mottled — mold, not a vector circle.
                            w += (ValueNoise(x, y, cell, 1) - 128) * 3 / 4;
                            w = Math.Clamp(w, 0, 256);

                            int i = y * width + x;

                            if (w > best[i])
                            {
                                best[i] = w;
                                owner[i] = s;
                            }
                        }
                    }
                }
            }

            byte[] alpha = new byte[pixelCount];
            byte[] color = new byte[pixelCount * 3];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    int washAlpha = washPeak * (160 + ValueNoise(x, y, cell * 4, 2) * 96 / 255) >> 8;
                    int a = washAlpha;
                    (int R, int G, int B) target = Wash;

                    if (owner[i] >= 0)
                    {
                        int speckle = (int)(Hash(x, y, 3) & 0xFF);
                        int sporeAlpha = (peak * best[i] >> 8) * (205 + speckle * 50 / 255) >> 8;

                        if (sporeAlpha > washAlpha)
                        {
                            a = sporeAlpha;
                            target = Stain(_spores[owner[i]].Index);
                        }
                    }

                    alpha[i] = (byte)Math.Clamp(a, 0, 255);
                    color[i * 3] = (byte)target.R;
                    color[i * 3 + 1] = (byte)target.G;
                    color[i * 3 + 2] = (byte)target.B;
                }
            }

            return new Plan { Alpha = alpha, Color = color };
        }

        /// <summary>Character value → position on the green–brown–purple ramp.</summary>
        private static (int R, int G, int B) Stain(int index)
        {
            int s = index * 510 / (RotAlphabet.Size - 1); // 0..510

            return s <= 255 ? Lerp(Green, Brown, s) : Lerp(Brown, Purple, s - 255);
        }

        private static (int R, int G, int B) Lerp(
            (int R, int G, int B) a,
            (int R, int G, int B) b,
            int t
        )
        {
            return (
                a.R + (b.R - a.R) * t / 255,
                a.G + (b.G - a.G) * t / 255,
                a.B + (b.B - a.B) * t / 255
            );
        }

        private ulong Hash(int x, int y, int salt)
        {
            ulong z = _noiseSeed
                + (ulong)(uint)x * 0x9E3779B97F4A7C15UL
                + (ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL
                + (ulong)salt * 0x165667B19E3779F9UL;

            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;

            return z ^ (z >> 31);
        }

        /// <summary>Bilinear value noise in 0..255 over a lattice of the given cell size.</summary>
        private int ValueNoise(int x, int y, int cell, int salt)
        {
            int ix = x / cell;
            int iy = y / cell;
            int fx = (x % cell) * 256 / cell;
            int fy = (y % cell) * 256 / cell;

            int v00 = (int)(Hash(ix, iy, salt * 16) & 0xFF);
            int v10 = (int)(Hash(ix + 1, iy, salt * 16) & 0xFF);
            int v01 = (int)(Hash(ix, iy + 1, salt * 16) & 0xFF);
            int v11 = (int)(Hash(ix + 1, iy + 1, salt * 16) & 0xFF);

            int top = v00 + ((v10 - v00) * fx >> 8);
            int bottom = v01 + ((v11 - v01) * fx >> 8);

            return top + ((bottom - top) * fy >> 8);
        }
    }

    /// <summary>
    /// The character → index map behind Rot: A, a, B, b … Z, z, digits,
    /// space, symbols, newline, tab (97 entries, as in the original
    /// prototype's ordering), with every other character folded into the
    /// remaining slots so indices span 0–149.
    /// </summary>
    internal static class RotAlphabet
    {
        internal const int Size = 150;

        private const string Symbols = " !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~\n\t";
        private static readonly int KnownCount = 52 + 10 + Symbols.Length; // 97

        internal static int IndexOf(Rune rune)
        {
            int v = rune.Value;

            if (v >= 'A' && v <= 'Z')
            {
                return (v - 'A') * 2;
            }

            if (v >= 'a' && v <= 'z')
            {
                return (v - 'a') * 2 + 1;
            }

            if (v >= '0' && v <= '9')
            {
                return 52 + (v - '0');
            }

            int symbol = v < 128 ? Symbols.IndexOf((char)v) : -1;

            if (symbol >= 0)
            {
                return 62 + symbol;
            }

            return KnownCount + v % (Size - KnownCount);
        }
    }
}

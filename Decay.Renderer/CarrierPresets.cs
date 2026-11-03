using Decay.Core;

namespace Decay.Renderer
{
    /// <summary>The three canonical decay stages, in escalating violence.</summary>
    public enum DecayStage
    {
        /// <summary>Static and noise: TV static, scanlines.</summary>
        Erosion,

        /// <summary>Geometric and generative: gradients, voronoi.</summary>
        Corruption,

        /// <summary>Datamosh and glitch corruption.</summary>
        Necrosis,
    }

    /// <summary>A procedural carrier the user can pick instead of uploading.</summary>
    public sealed record CarrierPreset(
        string Id,
        string Name,
        DecayStage Stage,
        bool FreeTier,
        Func<int, int, int, PixelBuffer> Generate
    );

    /// <summary>
    /// Procedural carrier generators. Unlike the op pipeline these are NOT
    /// frozen: a carrier is generated once at encode time and travels as
    /// pixels, so decoders never regenerate it and the art can evolve
    /// freely. Determinism here is per-process only (same seed, same build,
    /// same image) — good enough for preview/regenerate UX.
    /// </summary>
    public static class CarrierPresets
    {
        public static readonly IReadOnlyList<CarrierPreset> All = new[]
        {
            new CarrierPreset("tv-static", "TV Static", DecayStage.Erosion, FreeTier: true, TvStatic),
            new CarrierPreset("scanlines", "Scanlines", DecayStage.Erosion, FreeTier: false, Scanlines),
            new CarrierPreset("gradient-melt", "Gradient Melt", DecayStage.Corruption, FreeTier: false, GradientMelt),
            new CarrierPreset("voronoi", "Voronoi Shatter", DecayStage.Corruption, FreeTier: false, Voronoi),
            new CarrierPreset("necrotic-blocks", "Necrotic Blocks", DecayStage.Necrosis, FreeTier: false, NecroticBlocks),
        };

        /// <summary>The Free-tier default: TV static (Erosion).</summary>
        public static CarrierPreset Default => All[0];

        public static PixelBuffer TvStatic(int width, int height, int seed)
        {
            PixelBuffer image = Opaque(width, height);
            Xoshiro256 random = new Xoshiro256((ulong)seed);
            byte[] pixels = image.Pixels;

            for (int y = 0; y < height; y++)
            {
                // Occasional darker horizontal interference bands.
                int band = (int)(random.Next() % 48);
                int dim = band < 3 ? 90 : 0;

                for (int x = 0; x < width; x++)
                {
                    ulong r = random.Next();
                    int gray = (int)(r & 0xFF);
                    int offset = (y * width + x) * PixelBuffer.BytesPerPixel;

                    // Mostly monochrome snow with a whisper of chroma.
                    pixels[offset] = ClampByte(gray + (int)((r >> 8) & 0x0F) - dim);
                    pixels[offset + 1] = ClampByte(gray + (int)((r >> 12) & 0x0F) - dim);
                    pixels[offset + 2] = ClampByte(gray + (int)((r >> 16) & 0x0F) - dim);
                }
            }

            return image;
        }

        public static PixelBuffer Scanlines(int width, int height, int seed)
        {
            PixelBuffer image = Opaque(width, height);
            Xoshiro256 random = new Xoshiro256((ulong)seed);
            byte[] pixels = image.Pixels;

            // Phosphor green-on-black with per-line brightness flutter.
            for (int y = 0; y < height; y++)
            {
                double phase = y % 4 / 4.0;
                double line = 0.25 + 0.75 * Math.Sin(phase * Math.PI);
                int flutter = (int)(random.Next() % 40);

                for (int x = 0; x < width; x++)
                {
                    ulong r = random.Next();
                    int glow = (int)(line * (150 + flutter)) + (int)(r & 0x1F);
                    int offset = (y * width + x) * PixelBuffer.BytesPerPixel;

                    pixels[offset] = ClampByte(glow / 5);
                    pixels[offset + 1] = ClampByte(glow);
                    pixels[offset + 2] = ClampByte(glow / 4);
                }
            }

            return image;
        }

        public static PixelBuffer GradientMelt(int width, int height, int seed)
        {
            PixelBuffer image = Opaque(width, height);
            Xoshiro256 random = new Xoshiro256((ulong)seed);
            byte[] pixels = image.Pixels;

            // Two corner hues drifting through a sine-warped diagonal.
            (int r1, int g1, int b1) = HueBytes(random);
            (int r2, int g2, int b2) = HueBytes(random);
            double warpAmp = 0.08 + random.Next() % 100 / 700.0;
            double warpFreq = 2 + random.Next() % 5;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double u = (double)x / width;
                    double v = (double)y / height;
                    double t = (u + v) / 2 + warpAmp * Math.Sin(warpFreq * Math.PI * v);

                    t = Num.Clamp(t, 0, 1);

                    int offset = (y * width + x) * PixelBuffer.BytesPerPixel;

                    pixels[offset] = ClampByte((int)(r1 + (r2 - r1) * t));
                    pixels[offset + 1] = ClampByte((int)(g1 + (g2 - g1) * t));
                    pixels[offset + 2] = ClampByte((int)(b1 + (b2 - b1) * t));
                }
            }

            return image;
        }

        public static PixelBuffer Voronoi(int width, int height, int seed)
        {
            PixelBuffer image = Opaque(width, height);
            Xoshiro256 random = new Xoshiro256((ulong)seed);
            byte[] pixels = image.Pixels;

            const int cellCount = 24;
            (int x, int y, int r, int g, int b)[] cells =
                new (int, int, int, int, int)[cellCount];

            for (int i = 0; i < cellCount; i++)
            {
                (int r, int g, int b) hue = HueBytes(random);

                cells[i] = (
                    (int)(random.Next() % (ulong)width),
                    (int)(random.Next() % (ulong)height),
                    hue.r,
                    hue.g,
                    hue.b
                );
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    long best = long.MaxValue, second = long.MaxValue;
                    int bestIndex = 0;

                    for (int i = 0; i < cellCount; i++)
                    {
                        long dx = x - cells[i].x;
                        long dy = y - cells[i].y;
                        long d = dx * dx + dy * dy;

                        if (d < best)
                        {
                            second = best;
                            best = d;
                            bestIndex = i;
                        }
                        else if (d < second)
                        {
                            second = d;
                        }
                    }

                    // Darken toward cell borders (where best ≈ second).
                    double edge = Math.Sqrt(second) - Math.Sqrt(best);
                    double shade = Num.Clamp(edge / 18.0, 0.25, 1.0);
                    int offset = (y * width + x) * PixelBuffer.BytesPerPixel;

                    pixels[offset] = ClampByte((int)(cells[bestIndex].r * shade));
                    pixels[offset + 1] = ClampByte((int)(cells[bestIndex].g * shade));
                    pixels[offset + 2] = ClampByte((int)(cells[bestIndex].b * shade));
                }
            }

            return image;
        }

        public static PixelBuffer NecroticBlocks(int width, int height, int seed)
        {
            PixelBuffer image = Opaque(width, height);
            Xoshiro256 random = new Xoshiro256((ulong)seed);
            byte[] pixels = image.Pixels;

            int block = Math.Max(8, Math.Min(width, height) / 24);

            for (int by = 0; by < height; by += block)
            {
                // Some block rows smear sideways: the same colors repeat
                // with a growing offset, like a broken keyframe.
                bool smear = random.Next() % 4 == 0;
                int smearShift = (int)(random.Next() % (ulong)Math.Max(1, block * 2));

                for (int bx = 0; bx < width; bx += block)
                {
                    ulong r = random.Next();
                    int baseR = (int)(r & 0x3F);
                    int baseG = (int)((r >> 6) & 0x7F);
                    int baseB = (int)((r >> 13) & 0x3F);

                    // Sickly palette: mostly murk, occasional hot glitch block.
                    if (r % 13 == 0)
                    {
                        baseR = 200 + (int)((r >> 20) & 0x37);
                        baseG = (int)((r >> 26) & 0x2F);
                        baseB = 130 + (int)((r >> 30) & 0x7F);
                    }

                    for (int y = by; y < Math.Min(by + block, height); y++)
                    {
                        int rowShift = smear ? (y - by) * smearShift / block : 0;

                        for (int x = bx; x < Math.Min(bx + block, width); x++)
                        {
                            int sx = (x + rowShift) % width;
                            int offset = (y * width + sx) * PixelBuffer.BytesPerPixel;

                            pixels[offset] = ClampByte(baseR + (y - by) * 2);
                            pixels[offset + 1] = ClampByte(baseG);
                            pixels[offset + 2] = ClampByte(baseB + (x - bx));
                        }
                    }
                }
            }

            return image;
        }

        private static PixelBuffer Opaque(int width, int height)
        {
            PixelBuffer image = new PixelBuffer(width, height);

            for (int i = 3; i < image.Pixels.Length; i += 4)
            {
                image.Pixels[i] = 255;
            }

            return image;
        }

        private static (int r, int g, int b) HueBytes(Xoshiro256 random)
        {
            // Saturated hue from a random angle; avoids muddy midtones.
            double angle = random.Next() % 3600 / 3600.0 * 2 * Math.PI;

            return (
                ClampByte((int)(128 + 127 * Math.Sin(angle))),
                ClampByte((int)(128 + 127 * Math.Sin(angle + 2.094))),
                ClampByte((int)(128 + 127 * Math.Sin(angle + 4.189)))
            );
        }

        private static byte ClampByte(int value)
        {
            return (byte)Num.Clamp(value, 0, 255);
        }
    }
}

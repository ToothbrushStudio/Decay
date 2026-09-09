using Decay.Core;

namespace Decay.Renderer
{
    /// <summary>
    /// One invertible decay operation. Parameters are the raw PRNG draws;
    /// they resolve to pixels with integer math at apply time. Every op is a
    /// bijection on the pixel data whose inverse cancels it exactly, and none
    /// performs arithmetic that could amplify the post-embed LSB deltas.
    /// FROZEN as part of pipeline v1 — see docs/render-pipeline-v1.md.
    /// </summary>
    internal readonly struct DecayOp
    {
        internal const int KindNoiseXor = 0;
        internal const int KindBandShift = 1;
        internal const int KindColumnShift = 2;
        internal const int KindBlockSwap = 3;
        internal const int KindChannelShift = 4;

        internal int Kind { get; init; }
        internal int Ramp { get; init; } // frame + 1, in [1, 40]

        // Raw draws; meaning depends on Kind.
        internal int A { get; init; }
        internal int B { get; init; }
        internal int C { get; init; }
        internal int D { get; init; }
        internal int E { get; init; }
        internal int F { get; init; }
        internal ulong SubSeed { get; init; }

        internal void Apply(PixelBuffer image)
        {
            Run(image, inverse: false);
        }

        internal void Invert(PixelBuffer image)
        {
            Run(image, inverse: true);
        }

        private void Run(PixelBuffer image, bool inverse)
        {
            switch (Kind)
            {
                case KindNoiseXor:
                    NoiseXor(image); // self-inverse
                    break;
                case KindBandShift:
                    BandShift(image, inverse);
                    break;
                case KindColumnShift:
                    ColumnShift(image, inverse);
                    break;
                case KindBlockSwap:
                    BlockSwap(image); // involution when disjoint; no-op otherwise
                    break;
                case KindChannelShift:
                    ChannelShift(image, inverse);
                    break;
            }
        }

        private static int Resolve(int fraction, int dimension)
        {
            return (int)((long)fraction * dimension / 10000);
        }

        private static int ResolveSize(int fraction, int dimension)
        {
            return Math.Max(1, Resolve(fraction, dimension));
        }

        private int ResolveShift(int fdx, int dimension)
        {
            return (int)((long)fdx * dimension * Ramp / 400000) % dimension;
        }

        private void NoiseXor(PixelBuffer image)
        {
            int x = Resolve(A, image.Width);
            int y = Resolve(B, image.Height);
            int w = Math.Min(ResolveSize(C, image.Width), image.Width - x);
            int h = Math.Min(ResolveSize(D, image.Height), image.Height - y);

            if (w <= 0 || h <= 0)
            {
                return;
            }

            int density = Math.Max(8, E * Ramp / 40);
            Xoshiro256 noise = new Xoshiro256(SubSeed);
            byte[] pixels = image.Pixels;

            for (int row = y; row < y + h; row++)
            {
                int offset = (row * image.Width + x) * PixelBuffer.BytesPerPixel;

                for (int col = 0; col < w; col++, offset += PixelBuffer.BytesPerPixel)
                {
                    ulong r = noise.Next();

                    if ((int)(r & 0xFF) < density)
                    {
                        pixels[offset] ^= (byte)(r >> 8);
                        pixels[offset + 1] ^= (byte)(r >> 16);
                        pixels[offset + 2] ^= (byte)(r >> 24);
                    }
                }
            }
        }

        private void BandShift(PixelBuffer image, bool inverse)
        {
            int y = Resolve(A, image.Height);
            int h = Math.Min(ResolveSize(B, image.Height), image.Height - y);
            int dx = ResolveShift(C, image.Width);

            if (h <= 0 || dx == 0)
            {
                return;
            }

            if (inverse)
            {
                dx = image.Width - dx;
            }

            byte[] rowCopy = new byte[image.Width * PixelBuffer.BytesPerPixel];
            byte[] pixels = image.Pixels;

            for (int row = y; row < y + h; row++)
            {
                int rowStart = row * image.Width * PixelBuffer.BytesPerPixel;
                int shiftBytes = dx * PixelBuffer.BytesPerPixel;

                Array.Copy(pixels, rowStart, rowCopy, 0, rowCopy.Length);
                Array.Copy(rowCopy, rowCopy.Length - shiftBytes, pixels, rowStart, shiftBytes);
                Array.Copy(rowCopy, 0, pixels, rowStart + shiftBytes, rowCopy.Length - shiftBytes);
            }
        }

        private void ColumnShift(PixelBuffer image, bool inverse)
        {
            int x = Resolve(A, image.Width);
            int w = Math.Min(ResolveSize(B, image.Width), image.Width - x);
            int dy = ResolveShift(C, image.Height);

            if (w <= 0 || dy == 0)
            {
                return;
            }

            if (inverse)
            {
                dy = image.Height - dy;
            }

            byte[] pixels = image.Pixels;
            byte[] columnCopy = new byte[image.Height * PixelBuffer.BytesPerPixel];
            int stride = image.Width * PixelBuffer.BytesPerPixel;

            for (int col = x; col < x + w; col++)
            {
                int colStart = col * PixelBuffer.BytesPerPixel;

                for (int row = 0; row < image.Height; row++)
                {
                    Array.Copy(
                        pixels,
                        colStart + row * stride,
                        columnCopy,
                        row * PixelBuffer.BytesPerPixel,
                        PixelBuffer.BytesPerPixel
                    );
                }

                for (int row = 0; row < image.Height; row++)
                {
                    Array.Copy(
                        columnCopy,
                        ((row + image.Height - dy) % image.Height) * PixelBuffer.BytesPerPixel,
                        pixels,
                        colStart + row * stride,
                        PixelBuffer.BytesPerPixel
                    );
                }
            }
        }

        private void BlockSwap(PixelBuffer image)
        {
            int w = ResolveSize(E, image.Width);
            int h = ResolveSize(F, image.Height);
            int x1 = Resolve(A, image.Width);
            int y1 = Resolve(B, image.Height);
            int x2 = Resolve(C, image.Width);
            int y2 = Resolve(D, image.Height);

            w = Math.Min(w, Math.Min(image.Width - x1, image.Width - x2));
            h = Math.Min(h, Math.Min(image.Height - y1, image.Height - y2));

            if (w <= 0 || h <= 0)
            {
                return;
            }

            bool intersect =
                x1 < x2 + w && x2 < x1 + w &&
                y1 < y2 + h && y2 < y1 + h;

            if (intersect)
            {
                return;
            }

            byte[] pixels = image.Pixels;
            byte[] rowTemp = new byte[w * PixelBuffer.BytesPerPixel];
            int stride = image.Width * PixelBuffer.BytesPerPixel;

            for (int row = 0; row < h; row++)
            {
                int off1 = (y1 + row) * stride + x1 * PixelBuffer.BytesPerPixel;
                int off2 = (y2 + row) * stride + x2 * PixelBuffer.BytesPerPixel;

                Array.Copy(pixels, off1, rowTemp, 0, rowTemp.Length);
                Array.Copy(pixels, off2, pixels, off1, rowTemp.Length);
                Array.Copy(rowTemp, 0, pixels, off2, rowTemp.Length);
            }
        }

        private void ChannelShift(PixelBuffer image, bool inverse)
        {
            int y = Resolve(A, image.Height);
            int h = Math.Min(ResolveSize(B, image.Height), image.Height - y);
            int channel = C;
            int dx = ResolveShift(D, image.Width);

            if (h <= 0 || dx == 0)
            {
                return;
            }

            if (inverse)
            {
                dx = image.Width - dx;
            }

            byte[] pixels = image.Pixels;
            byte[] channelCopy = new byte[image.Width];
            int stride = image.Width * PixelBuffer.BytesPerPixel;

            for (int row = y; row < y + h; row++)
            {
                int rowStart = row * stride + channel;

                for (int col = 0; col < image.Width; col++)
                {
                    channelCopy[col] = pixels[rowStart + col * PixelBuffer.BytesPerPixel];
                }

                for (int col = 0; col < image.Width; col++)
                {
                    pixels[rowStart + col * PixelBuffer.BytesPerPixel] =
                        channelCopy[(col + image.Width - dx) % image.Width];
                }
            }
        }
    }
}

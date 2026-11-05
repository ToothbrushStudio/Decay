using System.IO.Compression;
using Decay.Core;

namespace Decay.App
{
    /// <summary>
    /// Pictures for the 320-unit preview box. Anything larger than
    /// <see cref="MaxSide"/> is shrunk by a whole factor (box filter) and
    /// stored as an uncompressed PNG: several times faster than a full-size,
    /// fully compressed PNG, and the same picture on screen. Files people
    /// save are always encoded from the full-size image instead.
    /// </summary>
    internal static class PreviewImage
    {
        internal const int MaxSide = 1024;

        internal static byte[] Png(PixelBuffer image)
        {
            return DecayPng.Encode(Shrink(image), CompressionLevel.NoCompression);
        }

        internal static ImageSource Source(byte[] png)
        {
            return ImageSource.FromStream(() => new MemoryStream(png));
        }

        /// <summary>The image itself when it is small enough, otherwise a shrunk copy.</summary>
        internal static PixelBuffer Shrink(PixelBuffer image)
        {
            int factor = (Math.Max(image.Width, image.Height) + MaxSide - 1) / MaxSide;

            if (factor <= 1)
            {
                return image;
            }

            int width = Math.Max(1, image.Width / factor);
            int height = Math.Max(1, image.Height / factor);
            int area = factor * factor;
            PixelBuffer small = new PixelBuffer(width, height);
            byte[] source = image.Pixels;
            byte[] target = small.Pixels;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int channel = 0; channel < PixelBuffer.BytesPerPixel; channel++)
                    {
                        int sum = 0;

                        for (int dy = 0; dy < factor; dy++)
                        {
                            int row = ((y * factor + dy) * image.Width + x * factor) * PixelBuffer.BytesPerPixel;

                            for (int dx = 0; dx < factor; dx++)
                            {
                                sum += source[row + dx * PixelBuffer.BytesPerPixel + channel];
                            }
                        }

                        target[(y * width + x) * PixelBuffer.BytesPerPixel + channel] = (byte)(sum / area);
                    }
                }
            }

            return small;
        }
    }
}

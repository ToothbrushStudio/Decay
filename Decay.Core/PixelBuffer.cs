namespace Decay.Core
{
    /// <summary>
    /// A UI-framework-agnostic RGBA8888 pixel buffer, row-major from the
    /// top-left pixel. The WPF layer converts to/from BitmapSource at the
    /// edge; nothing in Decay.Core may depend on a UI framework.
    /// </summary>
    public sealed class PixelBuffer
    {
        public const int BytesPerPixel = 4;

        public int Width { get; }
        public int Height { get; }

        /// <summary>Raw RGBA bytes, length = Width * Height * 4.</summary>
        public byte[] Pixels { get; }

        public int PixelCount => Width * Height;

        public PixelBuffer(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            Width = width;
            Height = height;
            Pixels = new byte[width * height * BytesPerPixel];
        }

        public PixelBuffer(int width, int height, byte[] rgbaPixels)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            if (rgbaPixels is null) throw new ArgumentNullException(nameof(rgbaPixels));

            if (rgbaPixels.Length != width * height * BytesPerPixel)
            {
                throw new ArgumentException(
                    $"Pixel data length {rgbaPixels.Length} does not match " +
                    $"{width}x{height} RGBA ({width * height * BytesPerPixel} bytes).",
                    nameof(rgbaPixels)
                );
            }

            Width = width;
            Height = height;
            Pixels = rgbaPixels;
        }

        public PixelBuffer Clone()
        {
            return new PixelBuffer(Width, Height, (byte[])Pixels.Clone());
        }
    }
}

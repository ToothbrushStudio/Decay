using Decay.Core;

namespace Decay.Renderer
{
    /// <summary>
    /// The Free-tier "DECAY" mark, drawn on a dark plate in the bottom-right
    /// corner. Honors the format's watermark rule: it may only touch pixels
    /// at row-major index >= the payload's pixel extent, and Apply verifies
    /// that before writing a single pixel — a watermark that corrupts
    /// payloads is a launch-blocking bug, so overlap is a hard failure,
    /// never a silent skip.
    /// </summary>
    public static class Watermark
    {
        // "DECAY" in a 5x7 pixel font, one byte per row, low 5 bits used.
        private static readonly byte[][] Glyphs =
        {
            new byte[] { 0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110 }, // D
            new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111 }, // E
            new byte[] { 0b01111, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b01111 }, // C
            new byte[] { 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 }, // A
            new byte[] { 0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100 }, // Y
        };

        private const int LetterCount = 5;
        private const int GlyphWidth = 5;
        private const int GlyphHeight = 7;
        private const int GlyphSpacing = 1;
        private const int TextColumns =
            LetterCount * GlyphWidth + (LetterCount - 1) * GlyphSpacing; // 29

        /// <summary>
        /// Draws the mark. Call only after the payload is embedded, passing
        /// the pixel extent EmbedContainer returned.
        /// </summary>
        /// <exception cref="DecayCapacityException">
        /// The image has no watermark-safe room: the payload runs into the
        /// mark's region, or the image is too small to hold the mark at all.
        /// </exception>
        public static void Apply(PixelBuffer image, int payloadPixelExtent)
        {
            if (image is null) throw new ArgumentNullException(nameof(image));
            if (payloadPixelExtent < 0) throw new ArgumentOutOfRangeException(nameof(payloadPixelExtent));

            Plate plate = Layout(image.Width, image.Height);

            if (plate.X < 0 || plate.Y < 0)
            {
                throw new DecayCapacityException(
                    $"A {image.Width}x{image.Height} image is too small to carry "
                        + "the Decay watermark."
                );
            }

            // The watermark rule: the plate's smallest row-major pixel index
            // must lie beyond the payload. Verified BEFORE any pixel writes.
            int firstTouchedIndex = plate.Y * image.Width + plate.X;

            if (firstTouchedIndex < payloadPixelExtent)
            {
                throw new DecayCapacityException(
                    "The message is too long for a Free image of this size: the "
                        + "watermark needs the bottom-right corner. Shorten the "
                        + "message or choose a larger size."
                );
            }

            byte[] pixels = image.Pixels;

            for (int y = plate.Y; y < plate.Y + plate.Height; y++)
            {
                for (int x = plate.X; x < plate.X + plate.Width; x++)
                {
                    int offset = (y * image.Width + x) * PixelBuffer.BytesPerPixel;

                    // Darken the plate, retaining a whisper of the rot.
                    pixels[offset] = (byte)(pixels[offset] / 5);
                    pixels[offset + 1] = (byte)(pixels[offset + 1] / 5);
                    pixels[offset + 2] = (byte)(pixels[offset + 2] / 5);
                }
            }

            DrawText(image, plate.X + plate.Scale, plate.Y + plate.Scale, plate.Scale);
        }

        /// <summary>
        /// How many leading pixels, in row-major order, the mark never
        /// touches on an image of this size. A Free payload must fit within
        /// them. Zero if the image is too small to carry the mark at all.
        /// </summary>
        public static int SafePixelCount(int width, int height)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));

            Plate plate = Layout(width, height);

            return plate.X < 0 || plate.Y < 0 ? 0 : plate.Y * width + plate.X;
        }

        private readonly struct Plate
        {
            internal Plate(int x, int y, int width, int height, int scale)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Scale = scale;
            }

            internal int X { get; }
            internal int Y { get; }
            internal int Width { get; }
            internal int Height { get; }
            internal int Scale { get; } // also the padding around the text
        }

        private static Plate Layout(int imageWidth, int imageHeight)
        {
            int scale = Math.Max(1, Math.Min(imageWidth, imageHeight) / 160);
            int padding = scale;
            int margin = 2 * scale;
            int plateWidth = TextColumns * scale + 2 * padding;
            int plateHeight = GlyphHeight * scale + 2 * padding;

            return new Plate(
                imageWidth - plateWidth - margin,
                imageHeight - plateHeight - margin,
                plateWidth,
                plateHeight,
                scale
            );
        }

        /// <summary>
        /// Renders the DECAY mark standalone — same glyphs, same plate, no
        /// host image. The app's title banner uses this so the title at the
        /// top of the window and the watermark on Free exports are literally
        /// the same pixels.
        /// </summary>
        public static PixelBuffer RenderBadge(int scale = 3)
        {
            if (scale < 1) throw new ArgumentOutOfRangeException(nameof(scale));

            int padding = scale;
            int width = TextColumns * scale + 2 * padding;
            int height = GlyphHeight * scale + 2 * padding;
            PixelBuffer image = new PixelBuffer(width, height);

            for (int i = 0; i < image.Pixels.Length; i += PixelBuffer.BytesPerPixel)
            {
                image.Pixels[i] = 10;
                image.Pixels[i + 1] = 10;
                image.Pixels[i + 2] = 10;
                image.Pixels[i + 3] = 255;
            }

            DrawText(image, padding, padding, scale);

            return image;
        }

        private static void DrawText(PixelBuffer image, int textX, int textY, int scale)
        {
            byte[] pixels = image.Pixels;

            for (int letter = 0; letter < Glyphs.Length; letter++)
            {
                int letterX = textX + letter * (GlyphWidth + GlyphSpacing) * scale;

                for (int row = 0; row < GlyphHeight; row++)
                {
                    for (int col = 0; col < GlyphWidth; col++)
                    {
                        if ((Glyphs[letter][row] >> (GlyphWidth - 1 - col) & 1) == 0)
                        {
                            continue;
                        }

                        for (int dy = 0; dy < scale; dy++)
                        {
                            int y = textY + row * scale + dy;
                            int rowStart = (y * image.Width + letterX + col * scale)
                                * PixelBuffer.BytesPerPixel;

                            for (int dx = 0; dx < scale; dx++)
                            {
                                int offset = rowStart + dx * PixelBuffer.BytesPerPixel;

                                pixels[offset] = 235;
                                pixels[offset + 1] = 235;
                                pixels[offset + 2] = 235;
                            }
                        }
                    }
                }
            }
        }
    }
}

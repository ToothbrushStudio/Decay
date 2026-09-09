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
            ArgumentNullException.ThrowIfNull(image);
            ArgumentOutOfRangeException.ThrowIfNegative(payloadPixelExtent);

            int scale = Math.Max(1, Math.Min(image.Width, image.Height) / 160);
            int padding = scale;
            int margin = 2 * scale;
            int plateWidth = TextColumns * scale + 2 * padding;
            int plateHeight = GlyphHeight * scale + 2 * padding;
            int plateX = image.Width - plateWidth - margin;
            int plateY = image.Height - plateHeight - margin;

            if (plateX < 0 || plateY < 0)
            {
                throw new DecayCapacityException(
                    $"A {image.Width}x{image.Height} image is too small to carry "
                        + "the Decay watermark."
                );
            }

            // The watermark rule: the plate's smallest row-major pixel index
            // must lie beyond the payload. Verified BEFORE any pixel writes.
            int firstTouchedIndex = plateY * image.Width + plateX;

            if (firstTouchedIndex < payloadPixelExtent)
            {
                throw new DecayCapacityException(
                    "The message is too large to leave watermark-safe room in this "
                        + "image. Shorten the message, use a larger carrier, or "
                        + "export without a watermark."
                );
            }

            byte[] pixels = image.Pixels;

            for (int y = plateY; y < plateY + plateHeight; y++)
            {
                for (int x = plateX; x < plateX + plateWidth; x++)
                {
                    int offset = (y * image.Width + x) * PixelBuffer.BytesPerPixel;

                    // Darken the plate, retaining a whisper of the rot.
                    pixels[offset] = (byte)(pixels[offset] / 5);
                    pixels[offset + 1] = (byte)(pixels[offset + 1] / 5);
                    pixels[offset + 2] = (byte)(pixels[offset + 2] / 5);
                }
            }

            DrawText(image, plateX + padding, plateY + padding, scale);
        }

        /// <summary>
        /// Renders the DECAY mark standalone — same glyphs, same plate, no
        /// host image. The app's title banner uses this so the title at the
        /// top of the window and the watermark on Free exports are literally
        /// the same pixels.
        /// </summary>
        public static PixelBuffer RenderBadge(int scale = 3)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(scale, 1);

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

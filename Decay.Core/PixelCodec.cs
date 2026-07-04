namespace Decay.Core
{
    /// <summary>
    /// The Decay v1 pixel layer: payload bytes carried in the two
    /// least-significant bits of R, G, and B (6 bits per pixel), row-major
    /// from the top-left, byte stream MSB-first. FROZEN — see
    /// docs/payload-format-v1.md.
    /// </summary>
    internal static class PixelCodec
    {
        private const int BitsPerPixel = 6;

        /// <summary>Maximum payload bytes the image can carry.</summary>
        internal static int Capacity(PixelBuffer image)
        {
            return (int)Math.Min(int.MaxValue, (long)image.PixelCount * BitsPerPixel / 8);
        }

        /// <summary>
        /// Number of pixels a payload of the given size occupies — pixels at
        /// or beyond this index are free for watermarking.
        /// </summary>
        internal static int PixelExtent(int payloadLength)
        {
            return (int)(((long)payloadLength * 8 + BitsPerPixel - 1) / BitsPerPixel);
        }

        internal static void Embed(ReadOnlySpan<byte> payload, PixelBuffer image)
        {
            if (payload.Length > Capacity(image))
            {
                throw new DecayCapacityException(
                    $"The message needs {payload.Length} bytes but a " +
                    $"{image.Width}x{image.Height} carrier holds {Capacity(image)}."
                );
            }

            byte[] pixels = image.Pixels;
            int totalBits = payload.Length * 8;
            int bitIndex = 0;

            for (int pixel = 0; bitIndex < totalBits; pixel++)
            {
                int offset = pixel * PixelBuffer.BytesPerPixel;

                for (int channel = 0; channel < 3 && bitIndex < totalBits; channel++)
                {
                    int high = ReadBit(payload, bitIndex++);
                    int low = bitIndex < totalBits ? ReadBit(payload, bitIndex++) : 0;

                    pixels[offset + channel] =
                        (byte)((pixels[offset + channel] & ~0b11) | (high << 1) | low);
                }
            }
        }

        internal static byte[] Extract(PixelBuffer image, int payloadLength)
        {
            if (payloadLength > Capacity(image))
            {
                throw new DecayFormatException(
                    "The image's payload declares a message larger than the image can hold."
                );
            }

            byte[] pixels = image.Pixels;
            byte[] payload = new byte[payloadLength];
            int totalBits = payloadLength * 8;
            int bitIndex = 0;

            for (int pixel = 0; bitIndex < totalBits; pixel++)
            {
                int offset = pixel * PixelBuffer.BytesPerPixel;

                for (int channel = 0; channel < 3 && bitIndex < totalBits; channel++)
                {
                    int bits = pixels[offset + channel] & 0b11;

                    WriteBit(payload, bitIndex++, (bits >> 1) & 1);

                    if (bitIndex < totalBits)
                    {
                        WriteBit(payload, bitIndex++, bits & 1);
                    }
                }
            }

            return payload;
        }

        private static int ReadBit(ReadOnlySpan<byte> data, int bitIndex)
        {
            return (data[bitIndex / 8] >> (7 - bitIndex % 8)) & 1;
        }

        private static void WriteBit(byte[] data, int bitIndex, int bit)
        {
            data[bitIndex / 8] |= (byte)(bit << (7 - bitIndex % 8));
        }
    }
}

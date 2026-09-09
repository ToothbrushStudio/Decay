using System.Buffers.Binary;
using System.IO.Compression;

namespace Decay.Core
{
    /// <summary>
    /// A small, dependency-free PNG codec — the only image format Decay
    /// speaks (lossless is the survival requirement). Encoding always writes
    /// 8-bit RGBA (color type 6), non-interlaced. Decoding accepts the
    /// common still-image forms: 8-bit gray, gray+alpha, RGB, RGBA, and
    /// palette, all filter types, non-interlaced. Exotic PNGs (16-bit,
    /// interlaced, low bit depths) are rejected with a clear message rather
    /// than mis-decoded — a wrong pixel is a destroyed payload.
    /// </summary>
    public static class DecayPng
    {
        private static ReadOnlySpan<byte> Signature =>
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        // ---------------------------------------------------------- encode

        public static byte[] Encode(PixelBuffer image)
        {
            ArgumentNullException.ThrowIfNull(image);

            using MemoryStream output = new MemoryStream();

            output.Write(Signature);

            Span<byte> ihdr = stackalloc byte[13];

            BinaryPrimitives.WriteUInt32BigEndian(ihdr, (uint)image.Width);
            BinaryPrimitives.WriteUInt32BigEndian(ihdr.Slice(4), (uint)image.Height);
            ihdr[8] = 8; // bit depth
            ihdr[9] = 6; // color type: RGBA
            ihdr[10] = 0; // compression
            ihdr[11] = 0; // filter method
            ihdr[12] = 0; // no interlace
            WriteChunk(output, "IHDR"u8, ihdr);

            using MemoryStream idat = new MemoryStream();

            using (ZLibStream deflate = new ZLibStream(idat, CompressionLevel.Optimal, true))
            {
                int stride = image.Width * PixelBuffer.BytesPerPixel;

                for (int row = 0; row < image.Height; row++)
                {
                    deflate.WriteByte(0); // filter: None
                    deflate.Write(image.Pixels, row * stride, stride);
                }
            }

            WriteChunk(output, "IDAT"u8, idat.ToArray());
            WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);

            return output.ToArray();
        }

        public static void EncodeTo(PixelBuffer image, Stream destination)
        {
            ArgumentNullException.ThrowIfNull(destination);

            byte[] png = Encode(image);

            destination.Write(png, 0, png.Length);
        }

        // ---------------------------------------------------------- decode

        public static PixelBuffer Decode(byte[] pngBytes)
        {
            ArgumentNullException.ThrowIfNull(pngBytes);

            if (pngBytes.Length < 8 || !pngBytes.AsSpan(0, 8).SequenceEqual(Signature))
            {
                throw new InvalidDataException("Not a PNG file.");
            }

            int width = 0, height = 0, bitDepth = 0, colorType = 0;
            byte[]? palette = null;
            byte[]? paletteAlpha = null;
            using MemoryStream compressed = new MemoryStream();
            bool sawEnd = false;
            int offset = 8;

            while (!sawEnd)
            {
                if (offset + 8 > pngBytes.Length)
                {
                    throw new InvalidDataException("Truncated PNG.");
                }

                int length = (int)BinaryPrimitives.ReadUInt32BigEndian(pngBytes.AsSpan(offset));
                ReadOnlySpan<byte> type = pngBytes.AsSpan(offset + 4, 4);

                if (length < 0 || offset + 12 + length > pngBytes.Length)
                {
                    throw new InvalidDataException("Truncated PNG.");
                }

                ReadOnlySpan<byte> data = pngBytes.AsSpan(offset + 8, length);
                uint expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(
                    pngBytes.AsSpan(offset + 8 + length)
                );

                if (Crc32(pngBytes.AsSpan(offset + 4, 4 + length)) != expectedCrc)
                {
                    throw new InvalidDataException("PNG chunk checksum mismatch.");
                }

                if (type.SequenceEqual("IHDR"u8))
                {
                    width = (int)BinaryPrimitives.ReadUInt32BigEndian(data);
                    height = (int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4));
                    bitDepth = data[8];
                    colorType = data[9];

                    if (width <= 0 || height <= 0)
                    {
                        throw new InvalidDataException("Invalid PNG dimensions.");
                    }

                    if (data[12] != 0)
                    {
                        throw new InvalidDataException(
                            "Interlaced PNGs are not supported — re-save the image "
                                + "without interlacing."
                        );
                    }

                    if (bitDepth != 8)
                    {
                        throw new InvalidDataException(
                            $"{bitDepth}-bit PNGs are not supported — re-save the "
                                + "image as a standard 8-bit PNG."
                        );
                    }

                    if (colorType is not (0 or 2 or 3 or 4 or 6))
                    {
                        throw new InvalidDataException("Unsupported PNG color type.");
                    }
                }
                else if (type.SequenceEqual("PLTE"u8))
                {
                    palette = data.ToArray();
                }
                else if (type.SequenceEqual("tRNS"u8))
                {
                    paletteAlpha = data.ToArray();
                }
                else if (type.SequenceEqual("IDAT"u8))
                {
                    compressed.Write(data);
                }
                else if (type.SequenceEqual("IEND"u8))
                {
                    sawEnd = true;
                }

                offset += 12 + length;
            }

            if (width == 0)
            {
                throw new InvalidDataException("PNG has no header.");
            }

            int channels = colorType switch
            {
                0 => 1, // gray
                2 => 3, // rgb
                3 => 1, // palette index
                4 => 2, // gray + alpha
                _ => 4, // rgba
            };

            byte[] raw = Inflate(compressed, (width * channels + 1) * height);

            Unfilter(raw, width, height, channels);

            return ToRgba(raw, width, height, colorType, channels, palette, paletteAlpha);
        }

        public static PixelBuffer Decode(Stream source)
        {
            ArgumentNullException.ThrowIfNull(source);

            using MemoryStream buffer = new MemoryStream();

            source.CopyTo(buffer);

            return Decode(buffer.ToArray());
        }

        // ------------------------------------------------------- internals

        private static byte[] Inflate(MemoryStream compressed, int expectedLength)
        {
            compressed.Position = 0;

            byte[] raw = new byte[expectedLength];

            using ZLibStream inflate = new ZLibStream(compressed, CompressionMode.Decompress);

            int read = 0;

            while (read < expectedLength)
            {
                int n = inflate.Read(raw, read, expectedLength - read);

                if (n == 0)
                {
                    throw new InvalidDataException("PNG pixel data is truncated.");
                }

                read += n;
            }

            return raw;
        }

        /// <summary>Reverses PNG scanline filters in place (types 0–4).</summary>
        private static void Unfilter(byte[] raw, int width, int height, int channels)
        {
            int stride = width * channels;

            for (int row = 0; row < height; row++)
            {
                int lineStart = row * (stride + 1);
                byte filter = raw[lineStart];
                int current = lineStart + 1;
                int previous = current - (stride + 1);

                for (int i = 0; i < stride; i++)
                {
                    int a = i >= channels ? raw[current + i - channels] : 0; // left
                    int b = row > 0 ? raw[previous + i] : 0; // up
                    int c = row > 0 && i >= channels
                        ? raw[previous + i - channels]
                        : 0; // up-left

                    raw[current + i] = filter switch
                    {
                        0 => raw[current + i],
                        1 => (byte)(raw[current + i] + a),
                        2 => (byte)(raw[current + i] + b),
                        3 => (byte)(raw[current + i] + (a + b) / 2),
                        4 => (byte)(raw[current + i] + Paeth(a, b, c)),
                        _ => throw new InvalidDataException("Unknown PNG filter type."),
                    };
                }
            }
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);

            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        private static PixelBuffer ToRgba(
            byte[] raw,
            int width,
            int height,
            int colorType,
            int channels,
            byte[]? palette,
            byte[]? paletteAlpha
        )
        {
            if (colorType == 3 && palette is null)
            {
                throw new InvalidDataException("Palette PNG has no palette.");
            }

            PixelBuffer image = new PixelBuffer(width, height);
            byte[] pixels = image.Pixels;
            int stride = width * channels;

            for (int row = 0; row < height; row++)
            {
                int source = row * (stride + 1) + 1;
                int destination = row * width * PixelBuffer.BytesPerPixel;

                for (int x = 0; x < width; x++)
                {
                    int s = source + x * channels;
                    int d = destination + x * PixelBuffer.BytesPerPixel;

                    switch (colorType)
                    {
                        case 0: // gray
                            pixels[d] = pixels[d + 1] = pixels[d + 2] = raw[s];
                            pixels[d + 3] = 255;
                            break;
                        case 2: // rgb
                            pixels[d] = raw[s];
                            pixels[d + 1] = raw[s + 1];
                            pixels[d + 2] = raw[s + 2];
                            pixels[d + 3] = 255;
                            break;
                        case 3: // palette
                            int index = raw[s] * 3;

                            if (index + 2 >= palette!.Length)
                            {
                                throw new InvalidDataException("PNG palette index out of range.");
                            }

                            pixels[d] = palette[index];
                            pixels[d + 1] = palette[index + 1];
                            pixels[d + 2] = palette[index + 2];
                            pixels[d + 3] = paletteAlpha is not null && raw[s] < paletteAlpha.Length
                                ? paletteAlpha[raw[s]]
                                : (byte)255;
                            break;
                        case 4: // gray + alpha
                            pixels[d] = pixels[d + 1] = pixels[d + 2] = raw[s];
                            pixels[d + 3] = raw[s + 1];
                            break;
                        default: // rgba
                            pixels[d] = raw[s];
                            pixels[d + 1] = raw[s + 1];
                            pixels[d + 2] = raw[s + 2];
                            pixels[d + 3] = raw[s + 3];
                            break;
                    }
                }
            }

            return image;
        }

        private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
        {
            Span<byte> header = stackalloc byte[8];

            BinaryPrimitives.WriteUInt32BigEndian(header, (uint)data.Length);
            type.CopyTo(header.Slice(4));
            output.Write(header);
            output.Write(data);

            byte[] crcInput = new byte[4 + data.Length];

            type.CopyTo(crcInput);
            data.CopyTo(crcInput.AsSpan(4));

            Span<byte> crc = stackalloc byte[4];

            BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(crcInput));
            output.Write(crc);
        }

        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            uint[] table = new uint[256];

            for (uint n = 0; n < 256; n++)
            {
                uint c = n;

                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                }

                table[n] = c;
            }

            return table;
        }

        private static uint Crc32(ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFF;

            foreach (byte b in data)
            {
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFFFFFF;
        }
    }
}

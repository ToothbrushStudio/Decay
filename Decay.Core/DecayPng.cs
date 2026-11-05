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
    ///
    /// Every problem with the input file, including a damaged, hostile or
    /// oversized one, is reported as an InvalidDataException.
    /// </summary>
    public static class DecayPng
    {
        /// <summary>
        /// The largest image Decay reads or writes: 64 megapixels (256 MB as
        /// RGBA). Far above anything Decay makes (the app's largest carrier
        /// is 2048×2048), and low enough that a huge or hostile file can't
        /// exhaust memory or overflow the size arithmetic.
        /// </summary>
        public const int MaxPixelCount = 64 * 1024 * 1024;

        /// <summary>The longest side Decay reads or writes, in pixels.</summary>
        public const int MaxDimension = 32_768;

        /// <summary>The largest PNG file Decay reads, in bytes.</summary>
        public const int MaxFileBytes = 512 * 1024 * 1024;

        private static ReadOnlySpan<byte> Signature =>
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>True if Decay can write, and read back, an image of this size.</summary>
        public static bool IsWithinLimits(int width, int height)
        {
            return width >= 1
                && height >= 1
                && width <= MaxDimension
                && height <= MaxDimension
                && (long)width * height <= MaxPixelCount;
        }

        // ---------------------------------------------------------- encode

        /// <summary>Encodes with the smallest output. Use for files people keep.</summary>
        public static byte[] Encode(PixelBuffer image)
        {
            return Encode(image, CompressionLevel.Optimal);
        }

        /// <summary>
        /// Encodes with the given compression. The pixels are identical at
        /// every level; NoCompression is several times faster and suits
        /// throwaway frames such as animation previews.
        /// </summary>
        public static byte[] Encode(PixelBuffer image, CompressionLevel compression)
        {
            if (image is null) throw new ArgumentNullException(nameof(image));

            if (!IsWithinLimits(image.Width, image.Height))
            {
                throw new ArgumentException(
                    $"A {image.Width}x{image.Height} image is larger than Decay can read back.",
                    nameof(image)
                );
            }

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

            using (ZLibStream deflate = new ZLibStream(idat, compression, true))
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
            if (destination is null) throw new ArgumentNullException(nameof(destination));

            byte[] png = Encode(image);

            destination.Write(png, 0, png.Length);
        }

        // ---------------------------------------------------------- decode

        /// <exception cref="InvalidDataException">
        /// Not a PNG Decay can read: damaged, an unsupported kind, or larger
        /// than <see cref="MaxPixelCount"/>.
        /// </exception>
        public static PixelBuffer Decode(byte[] pngBytes)
        {
            if (pngBytes is null) throw new ArgumentNullException(nameof(pngBytes));

            Parsed png = Parse(pngBytes, headerOnly: false);

            int channels = png.ColorType switch
            {
                0 => 1, // gray
                2 => 3, // rgb
                3 => 1, // palette index
                4 => 2, // gray + alpha
                _ => 4, // rgba
            };

            byte[] raw;

            using (MemoryStream compressed = png.Compressed!)
            {
                raw = Inflate(compressed, (png.Width * channels + 1) * png.Height);
            }

            Unfilter(raw, png.Width, png.Height, channels);

            return ToRgba(raw, png.Width, png.Height, png.ColorType, channels, png.Palette, png.PaletteAlpha);
        }

        /// <summary>Reads at most <see cref="MaxFileBytes"/> from the stream and decodes it.</summary>
        /// <exception cref="InvalidDataException">Not a PNG Decay can read.</exception>
        public static PixelBuffer Decode(Stream source)
        {
            return Decode(ReadBytes(source));
        }

        /// <summary>
        /// Reads just the width and height from the PNG header, without
        /// decoding any pixels, so a caller can turn away an image that is too
        /// large before paying for it. The size is reported even when it is
        /// beyond what <see cref="Decode(byte[])"/> accepts.
        /// </summary>
        /// <exception cref="InvalidDataException">Not a PNG Decay can read.</exception>
        public static (int Width, int Height) ReadSize(byte[] pngBytes)
        {
            if (pngBytes is null) throw new ArgumentNullException(nameof(pngBytes));

            Parsed png = Parse(pngBytes, headerOnly: true);

            return (png.Width, png.Height);
        }

        /// <summary>
        /// Reads a whole file into memory, refusing anything larger than
        /// <see cref="MaxFileBytes"/> instead of reading it all.
        /// </summary>
        /// <exception cref="InvalidDataException">The file is too large.</exception>
        public static byte[] ReadBytes(Stream source)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));

            if (source.CanSeek && source.Length - source.Position > MaxFileBytes)
            {
                throw new InvalidDataException("The image file is too large to open.");
            }

            using MemoryStream buffer = new MemoryStream();
            byte[] chunk = new byte[81920];
            int n;

            while ((n = source.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + n > MaxFileBytes)
                {
                    throw new InvalidDataException("The image file is too large to open.");
                }

                buffer.Write(chunk, 0, n);
            }

            return buffer.ToArray();
        }

        // ------------------------------------------------------- internals

        private sealed class Parsed
        {
            internal int Width;
            internal int Height;
            internal int ColorType;
            internal byte[]? Palette;
            internal byte[]? PaletteAlpha;
            internal MemoryStream? Compressed;
        }

        /// <summary>
        /// Walks the chunks, checking every length against the file and every
        /// CRC. With headerOnly, stops at IHDR without enforcing the size
        /// limits; otherwise enforces them as soon as IHDR is read.
        /// </summary>
        private static Parsed Parse(byte[] png, bool headerOnly)
        {
            if (png.Length > MaxFileBytes)
            {
                throw new InvalidDataException("The image file is too large to open.");
            }

            if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature))
            {
                throw new InvalidDataException("Not a PNG file.");
            }

            Parsed parsed = new Parsed();
            MemoryStream compressed = new MemoryStream();
            bool sawHeader = false;
            bool sawEnd = false;
            int offset = 8;

            while (!sawEnd)
            {
                // Every chunk is at least 12 bytes: length, type and CRC.
                if (png.Length - offset < 12)
                {
                    throw new InvalidDataException("Truncated PNG.");
                }

                uint declared = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset));

                if (declared > (uint)(png.Length - offset - 12))
                {
                    throw new InvalidDataException("Truncated PNG.");
                }

                int length = (int)declared;
                ReadOnlySpan<byte> type = png.AsSpan(offset + 4, 4);
                ReadOnlySpan<byte> data = png.AsSpan(offset + 8, length);
                uint expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(
                    png.AsSpan(offset + 8 + length)
                );

                if (Crc32(png.AsSpan(offset + 4, 4 + length)) != expectedCrc)
                {
                    throw new InvalidDataException("PNG chunk checksum mismatch.");
                }

                if (type.SequenceEqual("IHDR"u8))
                {
                    ReadHeader(data, parsed);
                    sawHeader = true;

                    if (headerOnly)
                    {
                        return parsed;
                    }

                    if (!IsWithinLimits(parsed.Width, parsed.Height))
                    {
                        throw new InvalidDataException(
                            $"This image is {parsed.Width}×{parsed.Height}, larger than Decay "
                                + $"can open ({MaxPixelCount / (1024 * 1024)} megapixels at most)."
                        );
                    }
                }
                else if (type.SequenceEqual("PLTE"u8))
                {
                    parsed.Palette = data.ToArray();
                }
                else if (type.SequenceEqual("tRNS"u8))
                {
                    parsed.PaletteAlpha = data.ToArray();
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

            if (!sawHeader)
            {
                throw new InvalidDataException("PNG has no header.");
            }

            parsed.Compressed = compressed;

            return parsed;
        }

        private static void ReadHeader(ReadOnlySpan<byte> data, Parsed parsed)
        {
            if (data.Length != 13)
            {
                throw new InvalidDataException("Invalid PNG header.");
            }

            uint width = BinaryPrimitives.ReadUInt32BigEndian(data);
            uint height = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4));
            int bitDepth = data[8];
            int colorType = data[9];

            if (width == 0 || height == 0 || width > int.MaxValue || height > int.MaxValue)
            {
                throw new InvalidDataException("Invalid PNG dimensions.");
            }

            if (data[10] != 0 || data[11] != 0)
            {
                throw new InvalidDataException("Unsupported PNG compression or filter method.");
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

            parsed.Width = (int)width;
            parsed.Height = (int)height;
            parsed.ColorType = colorType;
        }

        private static byte[] Inflate(MemoryStream compressed, int expectedLength)
        {
            compressed.Position = 0;

            // Grow the buffer only as data actually inflates, so a tiny file
            // that merely claims a huge size can't force a huge allocation.
            byte[] raw = new byte[Math.Min(expectedLength, 1 << 20)];
            int read = 0;

            using ZLibStream inflate = new ZLibStream(compressed, CompressionMode.Decompress);

            while (read < expectedLength)
            {
                if (read == raw.Length)
                {
                    Array.Resize(ref raw, (int)Math.Min(expectedLength, (long)raw.Length * 2));
                }

                int n = inflate.Read(raw, read, raw.Length - read);

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

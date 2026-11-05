using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Decay.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Decay.Core.Tests
{
    /// <summary>
    /// Damaged, hostile and oversized files. The app and the console edition
    /// treat InvalidDataException as "this file can't be read", so every
    /// problem with the input must surface as exactly that, never as a crash
    /// or a huge allocation.
    /// </summary>
    public class PngHardeningTests
    {
        [Fact]
        public void ShortHeader_IsInvalidData()
        {
            byte[] png = Png(Chunk("IHDR", new byte[8]), Chunk("IEND"));

            Assert.Throws<InvalidDataException>(() => DecayPng.Decode(png));
        }

        [Theory]
        [InlineData(0x7FFFFFF0u)]
        [InlineData(0xFFFFFFFFu)]
        [InlineData(13u + 100u)]
        public void ChunkLengthPastEndOfFile_IsInvalidData(uint declaredLength)
        {
            byte[] png = Png(Header(4, 4));
            byte[] lying = new byte[png.Length + 12];

            png.CopyTo(lying, 0);
            BinaryPrimitives.WriteUInt32BigEndian(lying.AsSpan(png.Length), declaredLength);
            Encoding.ASCII.GetBytes("IDAT").CopyTo(lying, png.Length + 4);

            Assert.Throws<InvalidDataException>(() => DecayPng.Decode(lying));
        }

        [Fact]
        public void TruncatedAfterSignature_IsInvalidData()
        {
            byte[] png = DecayPng.Encode(DecayCipherTests.MakeCarrier(8, 8, seed: 2));

            Assert.Throws<InvalidDataException>(() => DecayPng.Decode(png[..13]));
            Assert.Throws<InvalidDataException>(() => DecayPng.Decode(png[..^5]));
        }

        [Fact]
        public void SizeBeyondTheLimit_IsRefusedBeforeAnyPixelWork()
        {
            byte[] png = Png(Header(30_000, 30_000), Chunk("IDAT", Deflate(new byte[100])), Chunk("IEND"));
            long before = GC.GetAllocatedBytesForCurrentThread();

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => DecayPng.Decode(png)
            );

            Assert.Contains("larger than Decay can open", exception.Message);
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1_000_000);
        }

        [Fact]
        public void TinyFileClaimingAHugeImage_DoesNotAllocateItsSize()
        {
            // 8192 x 8192 is within the limit (256 MB as RGBA), but the file
            // holds 100 bytes of pixel data. Memory must follow the data.
            byte[] png = Png(Header(8192, 8192), Chunk("IDAT", Deflate(new byte[100])), Chunk("IEND"));
            long before = GC.GetAllocatedBytesForCurrentThread();

            Assert.Throws<InvalidDataException>(() => DecayPng.Decode(png));
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 8_000_000);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(11)]
        public void UnknownCompressionOrFilterMethod_IsInvalidData(int field)
        {
            byte[] header = HeaderData(4, 4);

            header[field] = 1;

            Assert.Throws<InvalidDataException>(
                () => DecayPng.Decode(Png(Chunk("IHDR", header), Chunk("IDAT", Deflate(new byte[20])), Chunk("IEND")))
            );
        }

        [Fact]
        public void MissingPixelData_IsInvalidData()
        {
            Assert.Throws<InvalidDataException>(
                () => DecayPng.Decode(Png(Header(4, 4), Chunk("IEND")))
            );
        }

        [Fact]
        public void ReadSize_ReportsTheHeaderWithoutDecoding()
        {
            byte[] ours = DecayPng.Encode(DecayCipherTests.MakeCarrier(37, 21, seed: 5));
            byte[] huge = Png(Header(30_000, 20_000), Chunk("IEND"));

            Assert.Equal((37, 21), DecayPng.ReadSize(ours));
            Assert.Equal((30_000, 20_000), DecayPng.ReadSize(huge));
            Assert.Throws<InvalidDataException>(() => DecayPng.ReadSize(new byte[] { 1, 2, 3 }));
        }

        [Fact]
        public void ReadBytes_RefusesAnOversizedFileWithoutReadingIt()
        {
            using LongEmptyStream stream = new LongEmptyStream((long)DecayPng.MaxFileBytes + 1);

            Assert.Throws<InvalidDataException>(() => DecayPng.ReadBytes(stream));
            Assert.Equal(0, stream.Position);
        }

        [Fact]
        public void Encode_RefusesImagesDecayCannotReadBack()
        {
            Assert.Throws<ArgumentException>(
                () => DecayPng.Encode(new PixelBuffer(DecayPng.MaxDimension + 1, 1))
            );
            Assert.True(DecayPng.IsWithinLimits(DecayPng.MaxDimension, 1));
            Assert.True(DecayPng.IsWithinLimits(8192, 8192));
            Assert.False(DecayPng.IsWithinLimits(8192, 8193));
            Assert.False(DecayPng.IsWithinLimits(0, 10));
        }

        [Theory]
        [InlineData(CompressionLevel.Optimal)]
        [InlineData(CompressionLevel.Fastest)]
        [InlineData(CompressionLevel.NoCompression)]
        public void Encode_EveryCompressionLevel_KeepsThePixels(CompressionLevel level)
        {
            PixelBuffer original = DecayCipherTests.MakeCarrier(61, 33, seed: 8);

            Assert.Equal(original.Pixels, DecayPng.Decode(DecayPng.Encode(original, level)).Pixels);
        }

        [Fact]
        public void Fuzz_CorruptChunks_OnlyEverThrowInvalidData()
        {
            // Corrupt the contents (not just the bytes) of real PNGs and fix up
            // the CRCs, so the damage reaches the header, inflate, unfilter and
            // palette paths instead of stopping at the checksum.
            Random random = new Random(20261105);
            List<byte[]> seeds = new List<byte[]>
            {
                DecayPng.Encode(DecayCipherTests.MakeCarrier(9, 7, seed: 3)),
                ImageSharpPng(PngColorType.Palette),
                ImageSharpPng(PngColorType.GrayscaleWithAlpha),
                ImageSharpPng(PngColorType.Rgb),
            };
            int decoded = 0;
            int rejected = 0;

            for (int round = 0; round < 3_000; round++)
            {
                List<(string Type, byte[] Data)> chunks = Chunks(seeds[round % seeds.Count]);
                int victim = random.Next(chunks.Count);
                byte[] data = (byte[])chunks[victim].Data.Clone();

                if (data.Length > 0)
                {
                    for (int flips = 1 + random.Next(4); flips > 0; flips--)
                    {
                        data[random.Next(data.Length)] = (byte)random.Next(256);
                    }
                }

                if (random.Next(8) == 0)
                {
                    data = data[..random.Next(data.Length + 1)];
                }

                chunks[victim] = (chunks[victim].Type, data);

                byte[] png = Png(chunks.Select(c => Chunk(c.Type, c.Data)).ToArray());

                try
                {
                    DecayPng.Decode(png);
                    decoded++;
                }
                catch (InvalidDataException)
                {
                    rejected++;
                }
            }

            // Both outcomes must actually occur, or the fuzzer isn't reaching anything.
            Assert.True(decoded > 0 && rejected > 0, $"decoded {decoded}, rejected {rejected}");
        }

        // ------------------------------------------------------- helpers

        private static byte[] ImageSharpPng(PngColorType colorType)
        {
            using Image<Rgba32> image = new Image<Rgba32>(11, 6);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    image[x, y] = new Rgba32((byte)(x * 20), (byte)(y * 40), (byte)(x ^ y), (byte)(255 - x));
                }
            }

            using MemoryStream stream = new MemoryStream();

            image.Save(stream, new PngEncoder { ColorType = colorType, BitDepth = PngBitDepth.Bit8 });

            return stream.ToArray();
        }

        private static List<(string Type, byte[] Data)> Chunks(byte[] png)
        {
            List<(string, byte[])> chunks = new List<(string, byte[])>();

            for (int offset = 8; offset < png.Length;)
            {
                int length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset));

                chunks.Add((Encoding.ASCII.GetString(png, offset + 4, 4), png[(offset + 8)..(offset + 8 + length)]));
                offset += 12 + length;
            }

            return chunks;
        }

        private static byte[] HeaderData(int width, int height)
        {
            byte[] data = new byte[13];

            BinaryPrimitives.WriteUInt32BigEndian(data, (uint)width);
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), (uint)height);
            data[8] = 8; // bit depth
            data[9] = 6; // RGBA

            return data;
        }

        private static byte[] Header(int width, int height)
        {
            return Chunk("IHDR", HeaderData(width, height));
        }

        private static byte[] Deflate(byte[] raw)
        {
            using MemoryStream output = new MemoryStream();

            using (ZLibStream z = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
            {
                z.Write(raw);
            }

            return output.ToArray();
        }

        private static byte[] Chunk(string type, byte[]? data = null)
        {
            data ??= Array.Empty<byte>();

            byte[] chunk = new byte[12 + data.Length];
            byte[] typeBytes = Encoding.ASCII.GetBytes(type);

            BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)data.Length);
            typeBytes.CopyTo(chunk, 4);
            data.CopyTo(chunk, 8);
            BinaryPrimitives.WriteUInt32BigEndian(
                chunk.AsSpan(8 + data.Length),
                Crc32(chunk.AsSpan(4, 4 + data.Length))
            );

            return chunk;
        }

        private static byte[] Png(params byte[][] chunks)
        {
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

            return signature.Concat(chunks.SelectMany(c => c)).ToArray();
        }

        private static uint Crc32(ReadOnlySpan<byte> data)
        {
            uint crc = 0xFFFFFFFF;

            foreach (byte b in data)
            {
                crc ^= b;

                for (int k = 0; k < 8; k++)
                {
                    crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
                }
            }

            return crc ^ 0xFFFFFFFF;
        }

        /// <summary>A seekable stream that claims a length but holds only zeros.</summary>
        private sealed class LongEmptyStream : Stream
        {
            private readonly long _length;

            internal LongEmptyStream(long length)
            {
                _length = length;
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position { get; set; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int n = (int)Math.Min(count, _length - Position);

                Array.Clear(buffer, offset, n);
                Position += n;

                return n;
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}

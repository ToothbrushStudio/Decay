using Decay.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Decay.Core.Tests
{
    /// <summary>
    /// Validates the built-in PNG codec, cross-checked against ImageSharp
    /// (test-only dependency) so both directions of interop are proven:
    /// files we write open everywhere, and files phones/editors write open
    /// in Decay.
    /// </summary>
    public class DecayPngTests
    {
        [Theory]
        [InlineData(64, 64)]
        [InlineData(97, 61)]
        [InlineData(1, 1)]
        public void EncodeDecode_RoundTripsExactly(int width, int height)
        {
            PixelBuffer original = DecayCipherTests.MakeCarrier(width, height, seed: 9);

            PixelBuffer decoded = DecayPng.Decode(DecayPng.Encode(original));

            Assert.Equal(original.Width, decoded.Width);
            Assert.Equal(original.Height, decoded.Height);
            Assert.Equal(original.Pixels, decoded.Pixels);
        }

        [Fact]
        public void OurEncode_OpensInImageSharp_Identically()
        {
            PixelBuffer original = DecayCipherTests.MakeCarrier(80, 50, seed: 4);
            byte[] png = DecayPng.Encode(original);

            using Image<Rgba32> external = Image.Load<Rgba32>(png);
            byte[] externalPixels = new byte[original.Pixels.Length];

            external.CopyPixelDataTo(externalPixels);

            Assert.Equal(original.Pixels, externalPixels);
        }

        [Theory]
        [InlineData(PngColorType.Rgb)]
        [InlineData(PngColorType.RgbWithAlpha)]
        [InlineData(PngColorType.Grayscale)]
        [InlineData(PngColorType.GrayscaleWithAlpha)]
        [InlineData(PngColorType.Palette)]
        public void ImageSharpEncodes_DecodeIdentically(PngColorType colorType)
        {
            // ImageSharp's adaptive filtering exercises all five unfilter
            // paths; the color-type spread exercises every ToRgba branch.
            using Image<Rgba32> source = new Image<Rgba32>(90, 45);

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    byte v = (byte)(x * 3 + y * 7);

                    source[x, y] = colorType switch
                    {
                        PngColorType.Grayscale or PngColorType.GrayscaleWithAlpha =>
                            new Rgba32(v, v, v, 255),
                        PngColorType.Palette =>
                            new Rgba32((byte)(v % 4 * 60), (byte)(v % 3 * 80), 128, 255),
                        _ => new Rgba32(v, (byte)(255 - v), (byte)(x ^ y), 255),
                    };
                }
            }

            using MemoryStream stream = new MemoryStream();

            source.Save(stream, new PngEncoder { ColorType = colorType, BitDepth = PngBitDepth.Bit8 });

            PixelBuffer decoded = DecayPng.Decode(stream.ToArray());
            byte[] expected = new byte[source.Width * source.Height * 4];

            source.CopyPixelDataTo(expected);

            Assert.Equal(expected, decoded.Pixels);
        }

        [Fact]
        public void InterlacedPng_RejectedWithGuidance()
        {
            using Image<Rgba32> source = new Image<Rgba32>(32, 32);
            using MemoryStream stream = new MemoryStream();

            source.Save(stream, new PngEncoder { InterlaceMethod = PngInterlaceMode.Adam7 });

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => DecayPng.Decode(stream.ToArray())
            );

            Assert.Contains("Interlaced", exception.Message);
        }

        [Fact]
        public void SixteenBitPng_RejectedWithGuidance()
        {
            using Image<Rgba32> source = new Image<Rgba32>(16, 16);
            using MemoryStream stream = new MemoryStream();

            source.Save(stream, new PngEncoder { BitDepth = PngBitDepth.Bit16 });

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => DecayPng.Decode(stream.ToArray())
            );

            Assert.Contains("16-bit", exception.Message);
        }

        [Fact]
        public void NotAPng_Rejected()
        {
            Assert.Throws<InvalidDataException>(
                () => DecayPng.Decode(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })
            );
        }

        [Fact]
        public void CorruptedChunk_FailsChecksum()
        {
            byte[] png = DecayPng.Encode(DecayCipherTests.MakeCarrier(16, 16, seed: 1));

            png[^20] ^= 0xFF; // damage inside a chunk body

            Assert.Throws<InvalidDataException>(() => DecayPng.Decode(png));
        }

        [Fact]
        public void FullDecayRoundTrip_ThroughOwnCodec()
        {
            // The product path end to end with zero external code: encrypt,
            // write PNG, read PNG, decrypt.
            PixelBuffer carrier = DecayCipherTests.MakeCarrier(96, 96, seed: 33);

            DecayCipher.EncryptInto(
                "shipped through our own codec",
                "pw",
                carrier,
                iterations: 1_000
            );

            PixelBuffer reloaded = DecayPng.Decode(DecayPng.Encode(carrier));

            Assert.Equal(
                "shipped through our own codec",
                DecayCipher.Decrypt(reloaded, "pw")
            );
        }
    }
}

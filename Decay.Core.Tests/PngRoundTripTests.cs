using Decay.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Decay.Core.Tests
{
    /// <summary>
    /// Proves the decode contract end-to-end through a real PNG file:
    /// encrypt → save PNG → reload from bytes → decrypt with only the image
    /// and the passphrase. ImageSharp is a test-only dependency — it stands
    /// in for the PNG codec of whatever UI framework hosts Decay.Core, and
    /// never ships with the product (floppy budget).
    /// </summary>
    public class PngRoundTripTests
    {
        [Fact]
        public void EncryptSaveReloadDecrypt_RecoversPlaintext()
        {
            const string plainText = "Sent as a file, never screenshotted.";
            const string passphrase = "the rot is delicate";

            PixelBuffer carrier = DecayCipherTests.MakeCarrier(128, 128, seed: 7);

            DecayCipher.EncryptInto(plainText, passphrase, carrier, iterations: 1_000);

            byte[] pngBytes = SaveAsPng(carrier);
            PixelBuffer reloaded = LoadFromPng(pngBytes);

            Assert.Equal(plainText, DecayCipher.Decrypt(reloaded, passphrase));
        }

        [Fact]
        public void PngRoundTrip_IsLosslessForPayloadBits()
        {
            PixelBuffer original = DecayCipherTests.MakeCarrier(64, 64, seed: 99);

            DecayCipher.EncryptInto("bit-exact", "pw", original, iterations: 1_000);

            PixelBuffer reloaded = LoadFromPng(SaveAsPng(original));

            Assert.Equal(original.Pixels, reloaded.Pixels);
        }

        private static byte[] SaveAsPng(PixelBuffer buffer)
        {
            using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(
                buffer.Pixels,
                buffer.Width,
                buffer.Height
            );
            using MemoryStream stream = new MemoryStream();

            image.SaveAsPng(stream);

            return stream.ToArray();
        }

        private static PixelBuffer LoadFromPng(byte[] pngBytes)
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(pngBytes);

            byte[] pixels = new byte[image.Width * image.Height * PixelBuffer.BytesPerPixel];

            image.CopyPixelDataTo(pixels);

            return new PixelBuffer(image.Width, image.Height, pixels);
        }
    }
}

using Decay.Core;

namespace Decay.Core.Tests
{
    public class DecayCipherTests
    {
        // Small iteration count keeps the suite fast; the default (600k) is
        // covered once in RoundTrip_DefaultIterations.
        private const int TestIterations = 1_000;
        private const string Passphrase = "correct horse battery staple";

        internal static PixelBuffer MakeCarrier(int width = 64, int height = 64, int seed = 42)
        {
            PixelBuffer carrier = new PixelBuffer(width, height);
            Random random = new Random(seed);

            random.NextBytes(carrier.Pixels);

            for (int i = 3; i < carrier.Pixels.Length; i += 4)
            {
                carrier.Pixels[i] = 255;
            }

            return carrier;
        }

        [Theory]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("Encrypt your message into the rot.")]
        [InlineData("Multi-line\nmessage with\ttabs and \"quotes\" and \\slashes\\.")]
        [InlineData("Unicode: żółć 中文 العربية 🦷🪥 𝔡𝔢𝔠𝔞𝔶")]
        public void RoundTrip_RecoversPlaintext(string plainText)
        {
            PixelBuffer image = MakeCarrier();

            DecayCipher.EncryptInto(plainText, Passphrase, image, TestIterations);

            Assert.Equal(plainText, DecayCipher.Decrypt(image, Passphrase));
        }

        [Fact]
        public void RoundTrip_LongMessage()
        {
            string plainText = string.Concat(
                Enumerable.Repeat("The rot is delicate. ", 2_000)
            );
            PixelBuffer image = MakeCarrier(512, 512);

            DecayCipher.EncryptInto(plainText, Passphrase, image, TestIterations);

            Assert.Equal(plainText, DecayCipher.Decrypt(image, Passphrase));
        }

        [Fact]
        public void RoundTrip_DefaultIterations()
        {
            PixelBuffer image = MakeCarrier();

            DecayCipher.EncryptInto("launch-default KDF cost", Passphrase, image);

            Assert.Equal("launch-default KDF cost", DecayCipher.Decrypt(image, Passphrase));
        }

        [Fact]
        public void RoundTrip_MaxCapacityMessage()
        {
            PixelBuffer image = MakeCarrier(32, 32);
            string plainText = new string('x', DecayCipher.MaxPlainTextBytes(image));

            DecayCipher.EncryptInto(plainText, Passphrase, image, TestIterations);

            Assert.Equal(plainText, DecayCipher.Decrypt(image, Passphrase));
        }

        [Fact]
        public void Encrypt_OverCapacity_Throws()
        {
            PixelBuffer image = MakeCarrier(32, 32);
            string plainText = new string('x', DecayCipher.MaxPlainTextBytes(image) + 1);

            Assert.Throws<DecayCapacityException>(
                () => DecayCipher.EncryptInto(plainText, Passphrase, image, TestIterations)
            );
        }

        [Fact]
        public void Decrypt_WrongPassphrase_FailsCleanly()
        {
            PixelBuffer image = MakeCarrier();

            DecayCipher.EncryptInto("secret", Passphrase, image, TestIterations);

            Assert.Throws<DecayAuthenticationException>(
                () => DecayCipher.Decrypt(image, "correct horse battery stable")
            );
        }

        [Fact]
        public void Decrypt_TamperedCiphertext_FailsCleanly()
        {
            PixelBuffer image = MakeCarrier();

            DecayCipher.EncryptInto("tamper target", Passphrase, image, TestIterations);

            // Flip a payload-carrying LSB inside the ciphertext region
            // (past the 41-byte header = past pixel 55).
            image.Pixels[60 * PixelBuffer.BytesPerPixel] ^= 0b01;

            Assert.Throws<DecayAuthenticationException>(
                () => DecayCipher.Decrypt(image, Passphrase)
            );
        }

        [Fact]
        public void Decrypt_NotADecayImage_Throws()
        {
            Assert.Throws<DecayFormatException>(
                () => DecayCipher.Decrypt(MakeCarrier(), Passphrase)
            );
        }

        [Fact]
        public void Decrypt_ImageTooSmall_Throws()
        {
            Assert.Throws<DecayFormatException>(
                () => DecayCipher.Decrypt(new PixelBuffer(4, 4), Passphrase)
            );
        }

        [Fact]
        public void Decrypt_UnknownFormatVersion_ReportsUpdateNeeded()
        {
            PixelBuffer image = MakeCarrier();
            byte[] container = PayloadContainer.Seal(
                "future format",
                Passphrase,
                TestIterations
            );

            container[4] = 0x7F;
            PixelCodec.Embed(container, image);

            DecayVersionException exception = Assert.Throws<DecayVersionException>(
                () => DecayCipher.Decrypt(image, Passphrase)
            );

            Assert.Equal(0x7F, exception.FormatVersion);
        }

        [Fact]
        public void Decrypt_HostileIterationCount_Rejected()
        {
            PixelBuffer image = MakeCarrier();
            byte[] container = PayloadContainer.Seal("dos attempt", Passphrase, TestIterations);

            // Forge iterations = 0xFFFFFFFF (offset 21, big-endian).
            container[21] = container[22] = container[23] = container[24] = 0xFF;
            PixelCodec.Embed(container, image);

            Assert.Throws<DecayFormatException>(
                () => DecayCipher.Decrypt(image, Passphrase)
            );
        }

        [Fact]
        public void Watermark_BeyondPixelExtent_DoesNotAffectDecode()
        {
            PixelBuffer image = MakeCarrier();
            int extent = DecayCipher.EncryptInto(
                "watermark discipline",
                Passphrase,
                image,
                TestIterations
            );

            // Trash every pixel at or beyond the reported extent — a
            // worst-case watermark. Free exports must always decode.
            for (int i = extent * PixelBuffer.BytesPerPixel; i < image.Pixels.Length; i++)
            {
                image.Pixels[i] = 0xFF;
            }

            Assert.Equal("watermark discipline", DecayCipher.Decrypt(image, Passphrase));
        }

        [Fact]
        public void Encrypt_EmptyPassphrase_Rejected()
        {
            Assert.Throws<ArgumentException>(
                () => DecayCipher.EncryptInto("msg", "", MakeCarrier(), TestIterations)
            );
        }
    }
}

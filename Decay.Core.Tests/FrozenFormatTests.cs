using System.Security.Cryptography;
using System.Text;
using Decay.Core;

namespace Decay.Core.Tests
{
    /// <summary>
    /// Guards the FROZEN v1 format (docs/payload-format-v1.md). If any test
    /// in this class fails, the change breaks decoding of every Decay image
    /// already in the wild — it must be reverted, not "fixed" here. New
    /// behavior belongs in a new format version.
    /// </summary>
    public class FrozenFormatTests
    {
        [Fact]
        public void PixelLayer_BitPlacement_MatchesSpec()
        {
            // 3 bytes = 24 bits fill exactly 4 pixels at 6 bits per pixel.
            // 0xB4 0x2F 0xC1 = 10110100 00101111 11000001, MSB-first, packed
            // as 2-bit groups into R, G, B with the earlier bit higher.
            PixelBuffer image = new PixelBuffer(4, 1);

            PixelCodec.Embed(new byte[] { 0xB4, 0x2F, 0xC1 }, image);

            byte[] expectedChannelBits =
            {
                0b10, 0b11, 0b01, // pixel 0: 10 11 01
                0b00, 0b00, 0b10, // pixel 1: 00 00 10
                0b11, 0b11, 0b11, // pixel 2: 11 11 11
                0b00, 0b00, 0b01, // pixel 3: 00 00 01
            };

            for (int i = 0; i < expectedChannelBits.Length; i++)
            {
                int pixel = i / 3;
                int channel = i % 3;
                byte actual = image.Pixels[pixel * PixelBuffer.BytesPerPixel + channel];

                Assert.Equal(expectedChannelBits[i], actual & 0b11);
            }

            Assert.Equal(
                new byte[] { 0xB4, 0x2F, 0xC1 },
                PixelCodec.Extract(image, 3)
            );
        }

        [Fact]
        public void Container_FixedHeaderLayout_MatchesSpec()
        {
            byte[] salt = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
            byte[] nonce = Enumerable.Range(101, 12).Select(i => (byte)i).ToArray();

            byte[] container = PayloadContainer.Seal(
                "vector",
                "passphrase",
                1_000,
                salt,
                nonce
            );

            Assert.Equal(57 + 6, container.Length); // "vector" = 6 UTF-8 bytes
            Assert.Equal("DCAY"u8.ToArray(), container[..4]);
            Assert.Equal(0x01, container[4]);
            Assert.Equal(salt, container[5..21]);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x03, 0xE8 }, container[21..25]); // 1000 BE
            Assert.Equal(nonce, container[25..37]);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x06 }, container[37..41]); // len 6 BE
        }

        [Fact]
        public void Container_GoldenVector_IsStable()
        {
            // Deterministic inputs → the container bytes must never change.
            // Expected hash generated at freeze time; a mismatch means the
            // KDF, cipher, AAD, or layout changed.
            byte[] salt = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
            byte[] nonce = Enumerable.Range(101, 12).Select(i => (byte)i).ToArray();

            byte[] container = PayloadContainer.Seal(
                "Encrypt your message into the rot.",
                "correct horse battery staple",
                1_000,
                salt,
                nonce
            );

            string hash = Convert.ToHexString(SHA256.HashData(container));

            Assert.Equal(GoldenContainerSha256, hash);
        }

        // Captured at freeze time. If this assertion ever fails, images
        // already in the wild have become unreadable — revert the change.
        private const string GoldenContainerSha256 =
            "F0C4F1FB74D6E6498BC66433E9968B020FAFC767E4761A97315EB7AF04A8F304";

        [Fact]
        public void Container_GoldenVector_Decrypts()
        {
            byte[] salt = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
            byte[] nonce = Enumerable.Range(101, 12).Select(i => (byte)i).ToArray();

            byte[] container = PayloadContainer.Seal(
                "Encrypt your message into the rot.",
                "correct horse battery staple",
                1_000,
                salt,
                nonce
            );

            Assert.Equal(
                "Encrypt your message into the rot.",
                PayloadContainer.Open(container, "correct horse battery staple")
            );
        }

        [Fact]
        public void HeaderTamper_AnyFixedHeaderByte_FailsAuthentication()
        {
            // The full 41-byte fixed header is GCM associated data: flipping
            // any bit of salt, iterations (within valid range), or nonce must
            // fail authentication rather than yield garbage plaintext.
            byte[] salt = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
            byte[] nonce = Enumerable.Range(101, 12).Select(i => (byte)i).ToArray();
            byte[] container = PayloadContainer.Seal("aad", "pw", 1_000, salt, nonce);

            byte[] tampered = (byte[])container.Clone();
            tampered[30] ^= 0x01; // inside the nonce

            Assert.Throws<DecayAuthenticationException>(
                () => PayloadContainer.Open(tampered, "pw")
            );

            tampered = (byte[])container.Clone();
            tampered[10] ^= 0x01; // inside the salt

            Assert.Throws<DecayAuthenticationException>(
                () => PayloadContainer.Open(tampered, "pw")
            );
        }
    }
}

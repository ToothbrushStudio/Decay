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
                pipelineId: 0x01,
                1_000,
                salt,
                nonce
            );

            Assert.Equal(58 + 6, container.Length); // "vector" = 6 UTF-8 bytes
            Assert.Equal("DCAY"u8.ToArray(), container[..4]);
            Assert.Equal(0x01, container[4]); // format version
            Assert.Equal(0x01, container[5]); // pipeline id
            Assert.Equal(salt, container[6..22]);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x03, 0xE8 }, container[22..26]); // 1000 BE
            Assert.Equal(nonce, container[26..38]);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x06 }, container[38..42]); // len 6 BE
        }

        [Fact]
        public void Container_GoldenVector_IsStable()
        {
            // Deterministic inputs → the container bytes must never change.
            // Expected hash generated at freeze time; a mismatch means the
            // KDF, cipher, AAD, or layout changed.
            string hash = Convert.ToHexString(SHA256.HashData(GoldenContainer()));

            Assert.Equal(GoldenContainerSha256, hash);
        }

        // Captured at freeze time. If this assertion ever fails, images
        // already in the wild have become unreadable — revert the change.
        private const string GoldenContainerSha256 =
            "D4BBE88D9FB7028B635C0031E3489D112C04D03251EFCF86C88586AF81FF01F3";

        [Fact]
        public void Container_GoldenVector_Decrypts()
        {
            Assert.Equal(
                "Encrypt your message into the rot.",
                PayloadContainer.Open(GoldenContainer(), "correct horse battery staple")
            );
        }

        private static byte[] GoldenContainer()
        {
            byte[] salt = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
            byte[] nonce = Enumerable.Range(101, 12).Select(i => (byte)i).ToArray();

            return PayloadContainer.Seal(
                "Encrypt your message into the rot.",
                "correct horse battery staple",
                pipelineId: 0x01,
                1_000,
                salt,
                nonce
            );
        }

        [Fact]
        public void HeaderTamper_AnyFixedHeaderByte_FailsAuthentication()
        {
            // The full 42-byte fixed header is GCM associated data: flipping
            // any bit of pipeline id, salt, iterations (within valid range),
            // or nonce must fail authentication rather than yield garbage
            // plaintext or a silently different animation.
            byte[] salt = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
            byte[] nonce = Enumerable.Range(101, 12).Select(i => (byte)i).ToArray();
            byte[] container = PayloadContainer.Seal("aad", "pw", 0x01, 1_000, salt, nonce);

            byte[] tampered = (byte[])container.Clone();
            tampered[5] ^= 0x01; // pipeline id

            Assert.Throws<DecayAuthenticationException>(
                () => PayloadContainer.Open(tampered, "pw")
            );

            tampered = (byte[])container.Clone();
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

using Decay.Core;
using Decay.Renderer;

namespace Decay.Renderer.Tests
{
    public class WatermarkAndTierTests
    {
        private const int TestIterations = 1_000;
        private const string Passphrase = "decryption is free forever";

        private static CarrierPreset FreePreset => CarrierPresets.Default;
        private static CarrierPreset ProPreset =>
            CarrierPresets.All.First(p => !p.FreeTier);

        [Fact]
        public void FreeExport_IsWatermarked_AndStillDecrypts()
        {
            EncryptedArt art = EncryptFree("marked goods", 256, 256);

            // The mark's text pixels are exact (235,235,235) triples — a
            // signature vanishingly improbable in rot output. The glyphs
            // total ~77 such pixels at scale 1.
            Assert.True(CountMarkPixels(art.FinalImage) > 50);

            // ...and the payload is untouched.
            Assert.Equal(
                "marked goods",
                DecayArtist.Decrypt(art.FinalImage, Passphrase).PlainText
            );
        }

        [Fact]
        public void ProExport_HasNoWatermark()
        {
            EncryptedArt art = DecayProduct.EncryptWithPreset(
                "clean export",
                Passphrase,
                DecayTier.Pro,
                FreePreset,
                256,
                256,
                carrierSeed: 7,
                iterations: TestIterations
            );

            Assert.True(CountMarkPixels(art.FinalImage) < 10);
            Assert.Equal(
                "clean export",
                DecayArtist.Decrypt(art.FinalImage, Passphrase).PlainText
            );
        }

        [Fact]
        public void Watermark_OverlappingPayload_IsAHardFailure()
        {
            // 64x64 with a ~2600-byte message: fits the pixel capacity, but
            // the payload extends into the watermark's corner. Free must
            // refuse loudly — never corrupt, never silently skip the mark.
            string bigMessage = new string('x', 2_600);

            Assert.Throws<DecayCapacityException>(
                () => EncryptFree(bigMessage, 64, 64)
            );
        }

        [Fact]
        public void Watermark_SameOverlappingMessage_IsFineOnPro()
        {
            EncryptedArt art = DecayProduct.EncryptWithPreset(
                new string('x', 2_600),
                Passphrase,
                DecayTier.Pro,
                FreePreset,
                64,
                64,
                carrierSeed: 7,
                iterations: TestIterations
            );

            Assert.Equal(
                new string('x', 2_600),
                DecayArtist.Decrypt(art.FinalImage, Passphrase).PlainText
            );
        }

        [Fact]
        public void Watermark_ImageTooSmallForMark_Throws()
        {
            Assert.Throws<DecayCapacityException>(
                () => EncryptFree("tiny", 24, 24)
            );
        }

        [Fact]
        public void Watermark_NeverTouchesPixelsBeforeExtent()
        {
            // Apply to a sentinel-filled buffer and verify everything below
            // the declared extent is untouched.
            PixelBuffer image = new PixelBuffer(200, 200);

            Array.Fill(image.Pixels, (byte)0xAB);

            int extent = 5_000;

            Watermark.Apply(image, extent);

            for (int i = 0; i < extent * PixelBuffer.BytesPerPixel; i++)
            {
                Assert.Equal(0xAB, image.Pixels[i]);
            }
        }

        [Fact]
        public void Free_ProOnlyPreset_Rejected()
        {
            Assert.Throws<DecayTierException>(
                () => DecayProduct.EncryptWithPreset(
                    "no",
                    Passphrase,
                    DecayTier.Free,
                    ProPreset,
                    256,
                    256,
                    carrierSeed: 1,
                    iterations: TestIterations
                )
            );
        }

        [Fact]
        public void Free_CustomCarrier_Rejected()
        {
            Assert.Throws<DecayTierException>(
                () => DecayProduct.EncryptWithCustomCarrier(
                    "no",
                    Passphrase,
                    DecayTier.Free,
                    FreePreset.Generate(256, 256, 1),
                    iterations: TestIterations
                )
            );
        }

        [Fact]
        public void Free_AdjustedIntensity_Rejected()
        {
            Assert.Throws<DecayTierException>(
                () => DecayProduct.EncryptWithPreset(
                    "no",
                    Passphrase,
                    DecayTier.Free,
                    FreePreset,
                    256,
                    256,
                    carrierSeed: 1,
                    intensity: 42,
                    iterations: TestIterations
                )
            );
        }

        [Fact]
        public void Pro_EverythingGoes()
        {
            EncryptedArt art = DecayProduct.EncryptWithCustomCarrier(
                "full access",
                Passphrase,
                DecayTier.Pro,
                ProPreset.Generate(256, 256, 3),
                intensity: 42,
                iterations: TestIterations
            );

            DecryptedArt result = DecayArtist.Decrypt(art.FinalImage, Passphrase);

            Assert.Equal("full access", result.PlainText);
            Assert.Equal(42, result.Sequence!.Intensity);
        }

        [Fact]
        public void Decryption_IsTierless_FreeCanReadProImages()
        {
            // The promise: decryption is free, forever, for everyone. A Pro
            // image at exotic intensity opens through the same tier-free
            // path a Free user has.
            EncryptedArt art = DecayProduct.EncryptWithCustomCarrier(
                "from a pro",
                Passphrase,
                DecayTier.Pro,
                CarrierPresets.Voronoi(128, 128, 9),
                intensity: 200,
                iterations: TestIterations
            );

            Assert.Equal(
                "from a pro",
                DecayArtist.Decrypt(art.FinalImage, Passphrase).PlainText
            );
        }

        [Fact]
        public void RenderBadge_DrawsTheMarkStandalone()
        {
            PixelBuffer badge = Watermark.RenderBadge(scale: 2);

            // 29 text columns + padding on each side, at scale 2.
            Assert.Equal((29 + 2) * 2, badge.Width);
            Assert.Equal((7 + 2) * 2, badge.Height);

            int markPixels = CountMarkPixels(badge);

            // 77 glyph pixels at scale 2 = 308 exact-signature pixels.
            Assert.Equal(308, markPixels);
        }

        private static EncryptedArt EncryptFree(string plainText, int width, int height)
        {
            return DecayProduct.EncryptWithPreset(
                plainText,
                Passphrase,
                DecayTier.Free,
                FreePreset,
                width,
                height,
                carrierSeed: 7,
                iterations: TestIterations
            );
        }

        private static int CountMarkPixels(PixelBuffer image)
        {
            int count = 0;

            for (int i = 0; i < image.Pixels.Length; i += PixelBuffer.BytesPerPixel)
            {
                if (image.Pixels[i] == 235
                    && image.Pixels[i + 1] == 235
                    && image.Pixels[i + 2] == 235)
                {
                    count++;
                }
            }

            return count;
        }
    }
}

using Decay.Core;
using Decay.Renderer;

namespace Decay.Renderer.Tests
{
    public class DecayArtistTests
    {
        private const int TestIterations = 1_000;
        private const string Passphrase = "send the file, not a screenshot";

        [Fact]
        public void EncryptDecrypt_FullArtRoundTrip()
        {
            const string plainText = "The glitch is data. The art is the message.";
            PixelBuffer carrier = CarrierPresets.TvStatic(128, 128, seed: 21);

            EncryptedArt art = DecayArtist.Encrypt(
                plainText,
                Passphrase,
                carrier,
                TestIterations
            );
            DecryptedArt result = DecayArtist.Decrypt(art.FinalImage, Passphrase);

            Assert.Equal(plainText, result.PlainText);
            Assert.NotNull(result.Sequence);
        }

        [Fact]
        public void Decrypt_UnrotRecoversCarrier_WithinLsbTolerance()
        {
            // The receiver's un-rot walks back to the sender's carrier. The
            // only residue is the embedded payload's LSB deltas, relocated by
            // the inverse ops — every op preserves their magnitude, so no
            // byte may drift by more than the 2-LSB maximum of 3.
            PixelBuffer carrier = CarrierPresets.Voronoi(128, 128, seed: 4);
            EncryptedArt art = DecayArtist.Encrypt(
                "un-rot me",
                Passphrase,
                carrier,
                TestIterations
            );

            DecryptedArt result = DecayArtist.Decrypt(art.FinalImage, Passphrase);
            PixelBuffer recovered = result.Sequence!.Unrot(art.FinalImage);

            for (int i = 0; i < carrier.Pixels.Length; i++)
            {
                int delta = Math.Abs(carrier.Pixels[i] - recovered.Pixels[i]);

                Assert.True(
                    delta <= 3,
                    $"Byte {i} drifted by {delta} — an op is amplifying LSB deltas."
                );
            }
        }

        [Fact]
        public void Encrypt_WatermarkBeyondExtent_StillDecrypts()
        {
            PixelBuffer carrier = CarrierPresets.Scanlines(128, 128, seed: 9);
            EncryptedArt art = DecayArtist.Encrypt(
                "watermark discipline",
                Passphrase,
                carrier,
                TestIterations
            );

            for (
                int i = art.PayloadPixelExtent * PixelBuffer.BytesPerPixel;
                i < art.FinalImage.Pixels.Length;
                i++
            )
            {
                art.FinalImage.Pixels[i] = 0xFF;
            }

            Assert.Equal(
                "watermark discipline",
                DecayArtist.Decrypt(art.FinalImage, Passphrase).PlainText
            );
        }

        [Fact]
        public void Decrypt_PlainCoreImage_YieldsNoSequence()
        {
            // Images made through the simple DecayCipher path carry pipeline
            // id 0: they decrypt fine and simply have no animation.
            PixelBuffer image = CarrierPresets.TvStatic(64, 64, seed: 2);

            DecayCipher.EncryptInto("data only", Passphrase, image, TestIterations);

            DecryptedArt result = DecayArtist.Decrypt(image, Passphrase);

            Assert.Equal("data only", result.PlainText);
            Assert.Null(result.Sequence);
        }

        [Fact]
        public void Presets_AreDeterministic_AndDistinct()
        {
            foreach (CarrierPreset preset in CarrierPresets.All)
            {
                PixelBuffer a = preset.Generate(64, 64, 77);
                PixelBuffer b = preset.Generate(64, 64, 77);
                PixelBuffer c = preset.Generate(64, 64, 78);

                Assert.Equal(a.Pixels, b.Pixels);
                Assert.NotEqual(a.Pixels, c.Pixels);
            }
        }

        [Fact]
        public void Presets_FreeTier_IsTvStaticErosion()
        {
            CarrierPreset free = Assert.Single(
                CarrierPresets.All.Where(p => p.FreeTier)
            );

            Assert.Equal("tv-static", free.Id);
            Assert.Equal(DecayStage.Erosion, free.Stage);
            Assert.Same(free, CarrierPresets.Default);
        }

        [Fact]
        public void Presets_CoverAllThreeStages()
        {
            Assert.Equal(
                new[] { DecayStage.Erosion, DecayStage.Corruption, DecayStage.Necrosis },
                CarrierPresets.All.Select(p => p.Stage).Distinct().ToArray()
            );
        }
    }
}

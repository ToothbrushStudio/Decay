using Decay.Core;
using Decay.Renderer;

namespace Decay.Renderer.Tests
{
    public class IntensityTests
    {
        private const int TestIterations = 1_000;
        private const string Passphrase = "the rot is delicate";

        private static byte[] Seal(byte intensity)
        {
            return DecayCipher.SealContainer(
                "intensity vector",
                Passphrase,
                DecaySequence.PipelineV1,
                intensity,
                TestIterations
            );
        }

        [Fact]
        public void ParameterZero_MeansDefaultIntensity()
        {
            Assert.Equal(100, DecaySequence.FromContainer(Seal(0))!.Intensity);
        }

        [Fact]
        public void Parameter_IsCarriedThrough()
        {
            Assert.Equal(25, DecaySequence.FromContainer(Seal(25))!.Intensity);
            Assert.Equal(255, DecaySequence.FromContainer(Seal(255))!.Intensity);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(25)]
        [InlineData(255)]
        public void Unrot_IsExact_AtExtremeIntensities(byte intensity)
        {
            DecaySequence sequence = DecaySequence.FromContainer(Seal(intensity))!;
            PixelBuffer carrier = CarrierPresets.Voronoi(96, 96, seed: 6);

            PixelBuffer recovered = sequence.Unrot(sequence.RenderFinal(carrier));

            Assert.Equal(carrier.Pixels, recovered.Pixels);
        }

        [Fact]
        public void LowIntensity_LeavesMoreCarrierIntact()
        {
            PixelBuffer carrier = CarrierPresets.GradientMelt(128, 128, seed: 12);

            int Changed(byte intensity)
            {
                PixelBuffer final = DecaySequence
                    .FromContainer(Seal(intensity))!
                    .RenderFinal(carrier);

                return carrier.Pixels.Zip(final.Pixels, (a, b) => a == b ? 0 : 1).Sum();
            }

            int atWhisper = Changed(10);
            int atOverdrive = Changed(255);

            Assert.True(
                atWhisper < atOverdrive,
                $"intensity 10 changed {atWhisper} bytes, 255 changed {atOverdrive}"
            );
        }

        [Fact]
        public void Artist_IntensityRoundTrips_ThroughTheImage()
        {
            PixelBuffer carrier = CarrierPresets.TvStatic(96, 96, seed: 3);
            EncryptedArt art = DecayArtist.Encrypt(
                "dialed down",
                Passphrase,
                carrier,
                TestIterations,
                intensity: 40
            );

            DecryptedArt result = DecayArtist.Decrypt(art.FinalImage, Passphrase);

            Assert.Equal("dialed down", result.PlainText);
            Assert.Equal(40, result.Sequence!.Intensity);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(256)]
        [InlineData(-5)]
        public void Artist_OutOfRangeIntensity_Rejected(int intensity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DecayArtist.Encrypt(
                    "nope",
                    Passphrase,
                    CarrierPresets.TvStatic(64, 64, seed: 1),
                    TestIterations,
                    intensity
                )
            );
        }
    }
}

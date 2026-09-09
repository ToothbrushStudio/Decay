using Decay.Core;
using Decay.Renderer;

namespace Decay.Renderer.Tests
{
    public class DecaySequenceTests
    {
        private const int TestIterations = 1_000;
        private const string Passphrase = "the rot is delicate";

        private static byte[] SealPipelineV1(string plainText = "op stream fuel")
        {
            return DecayCipher.SealContainer(
                plainText,
                Passphrase,
                DecaySequence.PipelineV1,
                TestIterations
            );
        }

        [Fact]
        public void FromContainer_PipelineV1_BuildsSequence()
        {
            Assert.NotNull(DecaySequence.FromContainer(SealPipelineV1()));
        }

        [Fact]
        public void FromContainer_PipelineNone_ReturnsNull()
        {
            byte[] container = DecayCipher.SealContainer(
                "no art",
                Passphrase,
                DecayCipher.PipelineNone,
                TestIterations
            );

            Assert.Null(DecaySequence.FromContainer(container));
        }

        [Fact]
        public void FromContainer_UnknownFuturePipeline_ReturnsNull()
        {
            byte[] container = DecayCipher.SealContainer(
                "from the future",
                Passphrase,
                pipelineId: 0x63,
                TestIterations
            );

            Assert.Null(DecaySequence.FromContainer(container));
        }

        [Fact]
        public void RenderFinal_IsDeterministic()
        {
            byte[] container = SealPipelineV1();
            PixelBuffer carrier = CarrierPresets.TvStatic(96, 96, seed: 5);

            PixelBuffer a = DecaySequence.FromContainer(container)!.RenderFinal(carrier);
            PixelBuffer b = DecaySequence.FromContainer(container)!.RenderFinal(carrier);

            Assert.Equal(a.Pixels, b.Pixels);
        }

        [Fact]
        public void RenderFinal_ActuallyRots()
        {
            PixelBuffer carrier = CarrierPresets.TvStatic(96, 96, seed: 5);
            PixelBuffer final = DecaySequence
                .FromContainer(SealPipelineV1())!
                .RenderFinal(carrier);

            int changed = carrier.Pixels
                .Zip(final.Pixels, (x, y) => x == y ? 0 : 1)
                .Sum();

            // The art is the message: a rot that alters almost nothing is a
            // rendering bug, not a stylistic choice.
            Assert.True(
                changed > carrier.Pixels.Length / 10,
                $"Only {changed} of {carrier.Pixels.Length} bytes changed."
            );
        }

        [Fact]
        public void Unrot_InvertsRenderFinal_Exactly()
        {
            // Without the embed step the ops must cancel bit-for-bit.
            byte[] container = SealPipelineV1();
            DecaySequence sequence = DecaySequence.FromContainer(container)!;
            PixelBuffer carrier = CarrierPresets.Voronoi(128, 128, seed: 11);

            PixelBuffer recovered = sequence.Unrot(sequence.RenderFinal(carrier));

            Assert.Equal(carrier.Pixels, recovered.Pixels);
        }

        [Fact]
        public void Unrot_OddDimensions_InvertsExactly()
        {
            // Cyclic shifts and block clipping must stay bijective on
            // awkward sizes, not just powers of two.
            byte[] container = SealPipelineV1();
            DecaySequence sequence = DecaySequence.FromContainer(container)!;
            PixelBuffer carrier = CarrierPresets.NecroticBlocks(97, 61, seed: 3);

            PixelBuffer recovered = sequence.Unrot(sequence.RenderFinal(carrier));

            Assert.Equal(carrier.Pixels, recovered.Pixels);
        }

        [Fact]
        public void FrameStepping_MatchesWholeSequence()
        {
            byte[] container = SealPipelineV1();
            DecaySequence sequence = DecaySequence.FromContainer(container)!;
            PixelBuffer carrier = CarrierPresets.GradientMelt(80, 80, seed: 8);

            PixelBuffer stepped = carrier.Clone();

            for (int frame = 0; frame < DecaySequence.FrameCount; frame++)
            {
                sequence.ApplyFrame(stepped, frame);
            }

            Assert.Equal(sequence.RenderFinal(carrier).Pixels, stepped.Pixels);

            for (int frame = DecaySequence.FrameCount - 1; frame >= 0; frame--)
            {
                sequence.RevertFrame(stepped, frame);
            }

            Assert.Equal(carrier.Pixels, stepped.Pixels);
        }

        [Fact]
        public void DifferentContainers_ProduceDifferentRot()
        {
            PixelBuffer carrier = CarrierPresets.TvStatic(96, 96, seed: 5);

            PixelBuffer a = DecaySequence
                .FromContainer(SealPipelineV1("message one"))!
                .RenderFinal(carrier);
            PixelBuffer b = DecaySequence
                .FromContainer(SealPipelineV1("message two"))!
                .RenderFinal(carrier);

            Assert.NotEqual(a.Pixels, b.Pixels);
        }
    }
}

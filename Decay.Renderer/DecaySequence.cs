using System.Security.Cryptography;
using Decay.Core;

namespace Decay.Renderer
{
    /// <summary>
    /// The replayable decay of one message: 120 invertible ops in 40 frames,
    /// derived deterministically from the sealed container. The encoder plays
    /// it forward over the carrier; the decoder plays it backward over the
    /// received image, un-rotting it as the message is revealed. Op
    /// derivation is FROZEN as pipeline v1 — see docs/render-pipeline-v1.md.
    /// </summary>
    public sealed class DecaySequence
    {
        public const byte PipelineV1 = 1;
        public const int FrameCount = 40;
        private const int OpsPerFrame = 3;
        private const int TotalOps = FrameCount * OpsPerFrame;

        private readonly DecayOp[] _ops;

        /// <summary>The decay intensity this sequence was built with (percent).</summary>
        public int Intensity { get; }

        private DecaySequence(DecayOp[] ops, int intensity)
        {
            _ops = ops;
            Intensity = intensity;
        }

        /// <summary>
        /// Builds the sequence a container's pipeline id calls for, or null
        /// when the container carries no pipeline this build understands —
        /// which is never an error: the message still decrypts, the
        /// animation is simply unavailable.
        /// </summary>
        public static DecaySequence? FromContainer(byte[] container)
        {
            ArgumentNullException.ThrowIfNull(container);

            if (DecayCipher.GetPipelineId(container) != PipelineV1)
            {
                return null;
            }

            // Pipeline v1's parameter byte is the intensity percent;
            // 0 is reserved to mean the default, 100.
            byte parameter = DecayCipher.GetPipelineParameter(container);
            int intensity = parameter == 0 ? 100 : parameter;

            byte[] seed = SHA256.HashData(container);
            Xoshiro256 random = new Xoshiro256(seed);
            DecayOp[] ops = new DecayOp[TotalOps];

            for (int i = 0; i < TotalOps; i++)
            {
                ops[i] = GenerateOp(random, ramp: i / OpsPerFrame + 1, intensity);
            }

            return new DecaySequence(ops, intensity);
        }

        /// <summary>Applies one frame's ops in place (forward playback).</summary>
        public void ApplyFrame(PixelBuffer image, int frameIndex)
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frameIndex, FrameCount);

            for (int i = 0; i < OpsPerFrame; i++)
            {
                _ops[frameIndex * OpsPerFrame + i].Apply(image);
            }
        }

        /// <summary>Inverts one frame's ops in place (reverse playback).</summary>
        public void RevertFrame(PixelBuffer image, int frameIndex)
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frameIndex, FrameCount);

            for (int i = OpsPerFrame - 1; i >= 0; i--)
            {
                _ops[frameIndex * OpsPerFrame + i].Invert(image);
            }
        }

        /// <summary>The fully rotted final frame — the image to embed and export.</summary>
        public PixelBuffer RenderFinal(PixelBuffer carrier)
        {
            ArgumentNullException.ThrowIfNull(carrier);

            PixelBuffer image = carrier.Clone();

            for (int frame = 0; frame < FrameCount; frame++)
            {
                ApplyFrame(image, frame);
            }

            return image;
        }

        /// <summary>
        /// Walks a received image all the way back to the sender's carrier
        /// (plus the invisible payload residue, and any watermark).
        /// </summary>
        public PixelBuffer Unrot(PixelBuffer rotted)
        {
            ArgumentNullException.ThrowIfNull(rotted);

            PixelBuffer image = rotted.Clone();

            for (int frame = FrameCount - 1; frame >= 0; frame--)
            {
                RevertFrame(image, frame);
            }

            return image;
        }

        private static DecayOp GenerateOp(Xoshiro256 random, int ramp, int intensity)
        {
            int kind = random.Draw(5);

            return kind switch
            {
                DecayOp.KindNoiseXor => new DecayOp
                {
                    Kind = kind,
                    Ramp = ramp,
                    Intensity = intensity,
                    A = random.Draw(10000),
                    B = random.Draw(10000),
                    C = 1000 + random.Draw(4000),
                    D = 1000 + random.Draw(4000),
                    SubSeed = random.Next(),
                    E = 32 + random.Draw(96),
                },
                DecayOp.KindBandShift => new DecayOp
                {
                    Kind = kind,
                    Ramp = ramp,
                    Intensity = intensity,
                    A = random.Draw(10000),
                    B = 500 + random.Draw(2000),
                    C = 100 + random.Draw(2400),
                },
                DecayOp.KindColumnShift => new DecayOp
                {
                    Kind = kind,
                    Ramp = ramp,
                    Intensity = intensity,
                    A = random.Draw(10000),
                    B = 500 + random.Draw(2000),
                    C = 100 + random.Draw(2400),
                },
                DecayOp.KindBlockSwap => new DecayOp
                {
                    Kind = kind,
                    Ramp = ramp,
                    Intensity = intensity,
                    A = random.Draw(10000),
                    B = random.Draw(10000),
                    C = random.Draw(10000),
                    D = random.Draw(10000),
                    E = 500 + random.Draw(1500),
                    F = 500 + random.Draw(1500),
                },
                _ => new DecayOp
                {
                    Kind = DecayOp.KindChannelShift,
                    Ramp = ramp,
                    Intensity = intensity,
                    A = random.Draw(10000),
                    B = 500 + random.Draw(2000),
                    C = random.Draw(3),
                    D = 100 + random.Draw(2400),
                },
            };
        }
    }
}

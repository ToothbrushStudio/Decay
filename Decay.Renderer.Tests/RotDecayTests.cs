using Decay.Core;
using Decay.Renderer;

namespace Decay.Renderer.Tests
{
    public class RotDecayTests
    {
        private const int TestIterations = 1_000;
        private const string Passphrase = "rot from the inside out";
        private const string Message = "Meet me at the old mill at 9pm. Bring the key! ~Z";

        [Fact]
        public void EncryptRot_RoundTrips_MessageAndRot()
        {
            PixelBuffer carrier = CarrierPresets.GradientMelt(128, 128, seed: 7);

            EncryptedArt art = DecayArtist.EncryptRot(Message, Passphrase, carrier, TestIterations);
            DecryptedArt result = DecayArtist.Decrypt(art.FinalImage, Passphrase);

            Assert.Equal(Message, result.PlainText);
            Assert.NotNull(result.Rot);
            Assert.Null(result.Sequence);
            Assert.Null(art.Sequence);
            Assert.Equal(100, result.Rot!.Intensity);
        }

        [Fact]
        public void Restore_RecoversCarrier_WithinTolerance()
        {
            PixelBuffer carrier = CarrierPresets.Voronoi(160, 160, seed: 3);
            EncryptedArt art = DecayArtist.EncryptRot(Message, Passphrase, carrier, TestIterations);

            DecryptedArt result = DecayArtist.Decrypt(art.FinalImage, Passphrase);
            PixelBuffer restored = result.Rot!.Restore(art.FinalImage);

            for (int i = 0; i < carrier.Pixels.Length; i++)
            {
                int delta = Math.Abs(carrier.Pixels[i] - restored.Pixels[i]);
                int pixel = i / PixelBuffer.BytesPerPixel;

                // The payload's LSB bits (±3) are amplified by the inverse
                // blend; elsewhere only rounding remains.
                int limit = pixel < art.PayloadPixelExtent ? 8 : 2;

                Assert.True(delta <= limit, $"Byte {i} drifted by {delta} (limit {limit}).");
            }
        }

        [Fact]
        public void Rot_ChangesTheImage_ButKeepsItRecognizable()
        {
            PixelBuffer carrier = CarrierPresets.Voronoi(160, 160, seed: 3);
            EncryptedArt art = DecayArtist.EncryptRot(Message, Passphrase, carrier, TestIterations);

            long total = 0;
            int changed = 0;

            for (int i = 0; i < carrier.Pixels.Length; i += PixelBuffer.BytesPerPixel)
            {
                int d = 0;

                for (int c = 0; c < 3; c++)
                {
                    d += Math.Abs(carrier.Pixels[i + c] - art.FinalImage.Pixels[i + c]);
                }

                total += d;
                changed += d > 12 ? 1 : 0;
            }

            double meanPerChannel = total / (double)(carrier.PixelCount * 3);

            Assert.True(changed > carrier.PixelCount / 10, "The rot should be visible.");
            Assert.True(meanPerChannel < 60, $"Mean shift {meanPerChannel:F1} is too destructive.");
        }

        [Fact]
        public void WrongPassphrase_StillLocked()
        {
            EncryptedArt art = DecayArtist.EncryptRot(
                Message,
                Passphrase,
                CarrierPresets.TvStatic(96, 96, seed: 1),
                TestIterations
            );

            Assert.Throws<DecayAuthenticationException>(
                () => DecayArtist.Decrypt(art.FinalImage, "wrong")
            );
        }

        [Fact]
        public void SamePlaintext_DifferentContainer_DifferentRot()
        {
            PixelBuffer carrier = CarrierPresets.GradientMelt(96, 96, seed: 2);
            EncryptedArt a = DecayArtist.EncryptRot(Message, Passphrase, carrier, TestIterations);
            EncryptedArt b = DecayArtist.EncryptRot(Message, Passphrase, carrier, TestIterations);

            Assert.NotEqual(a.FinalImage.Pixels, b.FinalImage.Pixels);
        }

        [Fact]
        public void Rot_IsKeyedByPassphrase()
        {
            // Character values are masked by a passphrase-derived keystream,
            // so the same container and plaintext rot differently under a
            // different passphrase — the stains don't expose the characters.
            PixelBuffer carrier = CarrierPresets.GradientMelt(96, 96, seed: 2);
            byte[] container = DecayCipher.SealContainer(
                "aaaaaaaa",
                Passphrase,
                RotDecay.PipelineId,
                100,
                TestIterations
            );

            PixelBuffer withRight = RotDecay.FromContainer(container, "aaaaaaaa", Passphrase)!
                .Apply(carrier);
            PixelBuffer withWrong = RotDecay.FromContainer(container, "aaaaaaaa", "other pass")!
                .Apply(carrier);

            Assert.NotEqual(withRight.Pixels, withWrong.Pixels);
        }

        [Fact]
        public void EmptyAndLongMessages_Work()
        {
            PixelBuffer carrier = CarrierPresets.TvStatic(128, 128, seed: 5);
            string longMessage = string.Concat(Enumerable.Repeat("The rot sets in. ", 60));

            foreach (string message in new[] { "", longMessage, "héllo ☣ 🍄 wörld" })
            {
                EncryptedArt art = DecayArtist.EncryptRot(
                    message,
                    Passphrase,
                    carrier,
                    TestIterations
                );

                Assert.Equal(message, DecayArtist.Decrypt(art.FinalImage, Passphrase).PlainText);
            }
        }

        [Fact]
        public void Intensity_ScalesTheRot()
        {
            PixelBuffer carrier = CarrierPresets.Voronoi(128, 128, seed: 9);
            byte[] container = DecayCipher.SealContainer(
                Message,
                Passphrase,
                RotDecay.PipelineId,
                100,
                TestIterations
            );
            byte[] mild = (byte[])container.Clone();
            byte[] strong = (byte[])container.Clone();
            RotDecay gentle = RotDecay.FromContainer(Reparam(mild, 30), Message, Passphrase)!;
            RotDecay heavy = RotDecay.FromContainer(Reparam(strong, 200), Message, Passphrase)!;

            Assert.True(Distance(carrier, gentle.Apply(carrier)) < Distance(carrier, heavy.Apply(carrier)));
        }

        [Fact]
        public void Interpolate_Endpoints()
        {
            PixelBuffer a = CarrierPresets.TvStatic(32, 32, seed: 1);
            PixelBuffer b = CarrierPresets.TvStatic(32, 32, seed: 2);

            Assert.Equal(a.Pixels, RotDecay.Interpolate(a, b, 0, 10).Pixels);
            Assert.Equal(b.Pixels, RotDecay.Interpolate(a, b, 10, 10).Pixels);
        }

        [Fact]
        public void Alphabet_FollowsTheOriginalOrdering_AndStaysInRange()
        {
            Assert.Equal(0, RotAlphabet.IndexOf(new System.Text.Rune('A')));
            Assert.Equal(1, RotAlphabet.IndexOf(new System.Text.Rune('a')));
            Assert.Equal(2, RotAlphabet.IndexOf(new System.Text.Rune('B')));

            for (int cp = 0; cp < 0x3000; cp++)
            {
                if (!System.Text.Rune.IsValid(cp))
                {
                    continue;
                }

                int index = RotAlphabet.IndexOf(new System.Text.Rune(cp));

                Assert.InRange(index, 0, RotAlphabet.Size - 1);
            }
        }

        [Fact]
        public void Product_RotStyle_WatermarksFreeAndKeepsMessage()
        {
            EncryptedArt art = DecayProduct.EncryptWithPreset(
                "free rot",
                Passphrase,
                DecayTier.Free,
                CarrierPresets.Default,
                256,
                256,
                carrierSeed: 11,
                iterations: TestIterations,
                style: DecayStyle.Rot
            );

            DecryptedArt result = DecayArtist.Decrypt(art.FinalImage, Passphrase);

            Assert.Equal("free rot", result.PlainText);
            Assert.NotNull(result.Rot);
        }

        [Fact]
        public void Strength_KeepsGlitchWithinTheRecognizableRange()
        {
            Assert.Equal(1, DecayStrength.ToRaw(DecayStyle.Glitch, 1));
            Assert.Equal(DecayStrength.GlitchMaxRaw, DecayStrength.ToRaw(DecayStyle.Glitch, 255));

            int previous = 0;

            for (int s = 1; s <= 255; s++)
            {
                int raw = DecayStrength.ToRaw(DecayStyle.Glitch, s);

                Assert.InRange(raw, previous, DecayStrength.GlitchMaxRaw);
                previous = raw;
            }

            // The Free tier's locked default must not wreck the image.
            Assert.True(DecayStrength.ToRaw(DecayStyle.Glitch, TierPolicy.LockedIntensity) <= 25);
            Assert.Equal(200, DecayStrength.ToRaw(DecayStyle.Rot, 200));
        }

        [Fact]
        public void RotMinimum_IsStillVisible_AndMaximumRecognizable()
        {
            PixelBuffer carrier = CarrierPresets.Voronoi(128, 128, seed: 9);
            byte[] container = DecayCipher.SealContainer(
                Message, Passphrase, RotDecay.PipelineId, 100, TestIterations);
            long min = Distance(carrier, RotDecay.FromContainer(Reparam((byte[])container.Clone(), 1), Message, Passphrase)!.Apply(carrier));
            long max = Distance(carrier, RotDecay.FromContainer(Reparam((byte[])container.Clone(), 255), Message, Passphrase)!.Apply(carrier));

            Assert.True(min > carrier.Pixels.Length / 2, "Minimum rot should still be visible.");
            Assert.True(max < carrier.Pixels.Length * 70L, "Maximum rot should stay recognizable.");
        }

        // Rewrites the (authenticated) intensity byte; fine here because
        // only the plan is built, never opened.
        private static byte[] Reparam(byte[] container, byte intensity)
        {
            container[6] = intensity;

            return container;
        }

        private static long Distance(PixelBuffer a, PixelBuffer b)
        {
            long total = 0;

            for (int i = 0; i < a.Pixels.Length; i++)
            {
                total += Math.Abs(a.Pixels[i] - b.Pixels[i]);
            }

            return total;
        }
    }
}

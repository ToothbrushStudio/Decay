using Decay.Core;

namespace Decay.Renderer
{
    public enum DecayTier
    {
        Free,
        Pro,
    }

    /// <summary>How the carrier is decayed.</summary>
    public enum DecayStyle
    {
        /// <summary>Ciphertext-driven glitch: tears, swaps and static (pipeline 1).</summary>
        Glitch,

        /// <summary>Color rot: the image stays recognizable (pipeline 2).</summary>
        Rot,
    }

    /// <summary>
    /// Maps the user-facing strength slider (1–255, default 100) onto each
    /// style's raw pipeline intensity so that the whole slider stays within
    /// the recognizable range. Pipeline 1's op math is frozen and its raw
    /// intensity destroys an image well before 255 (raw 25 is mild, ~40 is
    /// the edge of recognizable, 65+ is gone), so Glitch compresses the
    /// slider into raw 1–<see cref="GlitchMaxRaw"/>. Rot is already tuned so
    /// that its full range is recognizable and passes through unchanged.
    /// The raw value is what is recorded in the container, so decoding is
    /// unaffected.
    /// </summary>
    public static class DecayStrength
    {
        public const int Min = 1;
        public const int Max = 255;
        public const int GlitchMaxRaw = 40;

        public static int ToRaw(DecayStyle style, int strength)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(strength, Min);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(strength, Max);

            return style == DecayStyle.Glitch
                ? 1 + (strength - 1) * (GlitchMaxRaw - 1) / (Max - 1)
                : strength;
        }
    }

    /// <summary>
    /// What each tier may do. Product policy, deliberately separate from the
    /// frozen format: nothing here affects whether an image decrypts —
    /// decryption is always free, forever, for everyone. The UI reads these
    /// same predicates to enable/disable controls.
    /// </summary>
    public static class TierPolicy
    {
        public const int LockedIntensity = 100;

        public static bool AppliesWatermark(DecayTier tier)
        {
            return tier == DecayTier.Free;
        }

        public static bool AllowsCustomCarrier(DecayTier tier)
        {
            return tier == DecayTier.Pro;
        }

        public static bool AllowsPreset(DecayTier tier, CarrierPreset preset)
        {
            ArgumentNullException.ThrowIfNull(preset);

            return tier == DecayTier.Pro || preset.FreeTier;
        }

        public static bool AllowsIntensity(DecayTier tier, int intensity)
        {
            return tier == DecayTier.Pro || intensity == LockedIntensity;
        }
    }

    /// <summary>A feature was requested that the user's tier does not include.</summary>
    public sealed class DecayTierException : Exception
    {
        public DecayTierException(string feature)
            : base($"{feature} requires Decay Pro.")
        {
        }
    }

    /// <summary>
    /// The tier-aware encrypt path the app calls: gates features, runs the
    /// artist, and applies the Free watermark. Decryption is NOT here — it
    /// has no tier and never will; use DecayArtist.Decrypt directly.
    /// </summary>
    public static class DecayProduct
    {
        /// <summary>Encrypts onto a procedurally generated preset carrier.</summary>
        public static EncryptedArt EncryptWithPreset(
            string plainText,
            string passphrase,
            DecayTier tier,
            CarrierPreset preset,
            int width,
            int height,
            int carrierSeed,
            int intensity = TierPolicy.LockedIntensity,
            int iterations = DecayCipher.DefaultIterations,
            DecayStyle style = DecayStyle.Glitch
        )
        {
            ArgumentNullException.ThrowIfNull(preset);

            if (!TierPolicy.AllowsPreset(tier, preset))
            {
                throw new DecayTierException($"The \"{preset.Name}\" carrier");
            }

            return Encrypt(
                plainText,
                passphrase,
                tier,
                preset.Generate(width, height, carrierSeed),
                intensity,
                iterations,
                style
            );
        }

        /// <summary>Encrypts onto a user-supplied carrier image (Pro).</summary>
        public static EncryptedArt EncryptWithCustomCarrier(
            string plainText,
            string passphrase,
            DecayTier tier,
            PixelBuffer carrier,
            int intensity = TierPolicy.LockedIntensity,
            int iterations = DecayCipher.DefaultIterations,
            DecayStyle style = DecayStyle.Glitch
        )
        {
            if (!TierPolicy.AllowsCustomCarrier(tier))
            {
                throw new DecayTierException("Uploading your own carrier image");
            }

            return Encrypt(plainText, passphrase, tier, carrier, intensity, iterations, style);
        }

        private static EncryptedArt Encrypt(
            string plainText,
            string passphrase,
            DecayTier tier,
            PixelBuffer carrier,
            int intensity,
            int iterations,
            DecayStyle style
        )
        {
            if (!TierPolicy.AllowsIntensity(tier, intensity))
            {
                throw new DecayTierException("Adjusting the decay intensity");
            }

            int raw = DecayStrength.ToRaw(style, intensity);
            EncryptedArt art = style == DecayStyle.Rot
                ? DecayArtist.EncryptRot(plainText, passphrase, carrier, iterations, raw)
                : DecayArtist.Encrypt(plainText, passphrase, carrier, iterations, raw);

            if (TierPolicy.AppliesWatermark(tier))
            {
                Watermark.Apply(art.FinalImage, art.PayloadPixelExtent);
            }

            return art;
        }
    }
}

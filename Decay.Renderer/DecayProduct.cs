using Decay.Core;

namespace Decay.Renderer
{
    public enum DecayTier
    {
        Free,
        Pro,
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
            int iterations = DecayCipher.DefaultIterations
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
                iterations
            );
        }

        /// <summary>Encrypts onto a user-supplied carrier image (Pro).</summary>
        public static EncryptedArt EncryptWithCustomCarrier(
            string plainText,
            string passphrase,
            DecayTier tier,
            PixelBuffer carrier,
            int intensity = TierPolicy.LockedIntensity,
            int iterations = DecayCipher.DefaultIterations
        )
        {
            if (!TierPolicy.AllowsCustomCarrier(tier))
            {
                throw new DecayTierException("Uploading your own carrier image");
            }

            return Encrypt(plainText, passphrase, tier, carrier, intensity, iterations);
        }

        private static EncryptedArt Encrypt(
            string plainText,
            string passphrase,
            DecayTier tier,
            PixelBuffer carrier,
            int intensity,
            int iterations
        )
        {
            if (!TierPolicy.AllowsIntensity(tier, intensity))
            {
                throw new DecayTierException("Adjusting the decay intensity");
            }

            EncryptedArt art = DecayArtist.Encrypt(
                plainText,
                passphrase,
                carrier,
                iterations,
                intensity
            );

            if (TierPolicy.AppliesWatermark(tier))
            {
                Watermark.Apply(art.FinalImage, art.PayloadPixelExtent);
            }

            return art;
        }
    }
}

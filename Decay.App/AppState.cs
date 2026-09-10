using Decay.Licensing;
using Decay.Renderer;

namespace Decay.App
{
    /// <summary>
    /// Session-level app state. The effective tier comes from an activated
    /// license key (validated offline against PublisherKey — no server, no
    /// phone-home). Store IAP for iOS/Android will layer in here later as a
    /// second tier source; every gate in the UI already reads TierPolicy,
    /// so that change stays local to this class.
    /// </summary>
    public static class AppState
    {
        private const string LicensePreferenceKey = "license";

        /// <summary>Debug-build override; see the dev toggle on EncryptPage.</summary>
        public static DecayTier DevTier { get; set; } = DecayTier.Free;

        public static DecayLicense? License { get; private set; }

        public static DecayTier Tier => License?.Tier ?? DevTier;

        /// <summary>Restores a previously activated license at startup.</summary>
        public static void Initialize()
        {
            License = LicenseKey.Validate(
                Preferences.Default.Get(LicensePreferenceKey, ""),
                PublisherKey.SubjectPublicKeyInfoBase64
            );
        }

        /// <summary>Validates and persists a pasted license key.</summary>
        public static bool TryActivate(string key)
        {
            DecayLicense? license = LicenseKey.Validate(
                key,
                PublisherKey.SubjectPublicKeyInfoBase64
            );

            if (license is null)
            {
                return false;
            }

            License = license;
            Preferences.Default.Set(LicensePreferenceKey, key);

            return true;
        }

        public static void Deactivate()
        {
            License = null;
            Preferences.Default.Remove(LicensePreferenceKey);
        }
    }
}

using System.Security.Cryptography;
using Decay.Licensing;
using Decay.Renderer;

namespace Decay.Licensing.Tests
{
    public class LicenseKeyTests
    {
        private static (ECDsa privateKey, string publicKeyBase64) MakeKeyPair()
        {
            ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            return (key, Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
        }

        private static DecayLicense ProLicense(string licensee = "Ada Lovelace")
        {
            return new DecayLicense(licensee, DecayTier.Pro, new DateOnly(2026, 9, 9));
        }

        [Fact]
        public void SignValidate_RoundTrips()
        {
            (ECDsa privateKey, string publicKey) = MakeKeyPair();

            using (privateKey)
            {
                string key = LicenseKey.Sign(ProLicense(), privateKey);
                DecayLicense? validated = LicenseKey.Validate(key, publicKey);

                Assert.NotNull(validated);
                Assert.Equal("Ada Lovelace", validated.Licensee);
                Assert.Equal(DecayTier.Pro, validated.Tier);
                Assert.Equal(new DateOnly(2026, 9, 9), validated.Issued);
            }
        }

        [Fact]
        public void EmailMangledKey_StillValidates()
        {
            (ECDsa privateKey, string publicKey) = MakeKeyPair();

            using (privateKey)
            {
                string key = LicenseKey.Sign(ProLicense(), privateKey);
                string mangled = string.Join(
                    "\n  ",
                    Enumerable.Range(0, key.Length / 20 + 1)
                        .Select(i => key.Substring(i * 20, Math.Min(20, key.Length - i * 20)))
                );

                Assert.NotNull(LicenseKey.Validate(mangled, publicKey));
            }
        }

        [Fact]
        public void UnicodeLicensee_RoundTrips()
        {
            (ECDsa privateKey, string publicKey) = MakeKeyPair();

            using (privateKey)
            {
                string key = LicenseKey.Sign(ProLicense("żółć \"quotes\" 中文 🦷"), privateKey);

                Assert.Equal(
                    "żółć \"quotes\" 中文 🦷",
                    LicenseKey.Validate(key, publicKey)!.Licensee
                );
            }
        }

        [Fact]
        public void TamperedPayload_Rejected()
        {
            (ECDsa privateKey, string publicKey) = MakeKeyPair();

            using (privateKey)
            {
                string key = LicenseKey.Sign(ProLicense(), privateKey);
                string[] parts = key.Split('.');

                // Re-point the license at a different licensee without re-signing.
                char[] payload = parts[1].ToCharArray();

                payload[5] = payload[5] == 'A' ? 'B' : 'A';

                Assert.Null(
                    LicenseKey.Validate($"{parts[0]}.{new string(payload)}.{parts[2]}", publicKey)
                );
            }
        }

        [Fact]
        public void KeyFromDifferentPublisher_Rejected()
        {
            (ECDsa privateKey, string _) = MakeKeyPair();
            (ECDsa otherPrivate, string otherPublic) = MakeKeyPair();

            using (privateKey)
            using (otherPrivate)
            {
                string key = LicenseKey.Sign(ProLicense(), privateKey);

                Assert.Null(LicenseKey.Validate(key, otherPublic));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("DECAY1")]
        [InlineData("DECAY1.notbase64!!!.alsono")]
        [InlineData("DECAY2.YQ.YQ")]
        [InlineData("total garbage pasted from an email")]
        public void MalformedKeys_ReturnNull(string? key)
        {
            (ECDsa privateKey, string publicKey) = MakeKeyPair();

            using (privateKey)
            {
                Assert.Null(LicenseKey.Validate(key, publicKey));
            }
        }

        [Fact]
        public void UnconfiguredBuild_EmptyPublisherKey_NeverValidates()
        {
            (ECDsa privateKey, string _) = MakeKeyPair();

            using (privateKey)
            {
                string key = LicenseKey.Sign(ProLicense(), privateKey);

                Assert.Null(LicenseKey.Validate(key, ""));
            }
        }
    }
}

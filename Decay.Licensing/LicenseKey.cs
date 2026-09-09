using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Decay.Renderer;

namespace Decay.Licensing
{
    /// <summary>A validated Decay license.</summary>
    public sealed record DecayLicense(string Licensee, DecayTier Tier, DateOnly Issued);

    /// <summary>
    /// Offline license keys, honoring the product's no-server ethos: a key
    /// is "DECAY1.&lt;payload&gt;.&lt;signature&gt;" where the payload is
    /// signed with the publisher's ECDSA P-256 private key and verified
    /// against the public key embedded in the app. No activation server, no
    /// phone-home, works forever. Losing the private key only stops NEW
    /// sales — existing keys keep validating, matching the promise that
    /// decryption (and everything already bought) never expires.
    ///
    /// The private key never ships and never enters this repository; the
    /// publisher generates it once with tools/Decay.KeyGen.
    /// </summary>
    public static class LicenseKey
    {
        private const string Prefix = "DECAY1";

        /// <summary>Signs a license. Publisher-side only (KeyGen tool).</summary>
        public static string Sign(DecayLicense license, ECDsa privateKey)
        {
            ArgumentNullException.ThrowIfNull(license);
            ArgumentNullException.ThrowIfNull(privateKey);

            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new Payload
            {
                n = license.Licensee,
                t = license.Tier == DecayTier.Pro ? "pro" : "free",
                d = license.Issued.ToString("yyyy-MM-dd"),
            });
            byte[] signature = privateKey.SignData(payload, HashAlgorithmName.SHA256);

            return $"{Prefix}.{Base64Url(payload)}.{Base64Url(signature)}";
        }

        /// <summary>
        /// Validates a pasted key against the publisher's public key
        /// (SubjectPublicKeyInfo, base64). Returns null for anything that is
        /// not a genuine, intact key — malformed, tampered, wrongly signed,
        /// or an empty publisher key (an unconfigured build). Whitespace and
        /// line breaks from email/chat mangling are tolerated.
        /// </summary>
        public static DecayLicense? Validate(string? key, string publisherPublicKeyBase64)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(publisherPublicKeyBase64))
            {
                return null;
            }

            string compact = string.Concat(key.Where(c => !char.IsWhiteSpace(c)));
            string[] parts = compact.Split('.');

            if (parts.Length != 3 || parts[0] != Prefix)
            {
                return null;
            }

            try
            {
                byte[] payload = FromBase64Url(parts[1]);
                byte[] signature = FromBase64Url(parts[2]);

                using ECDsa publicKey = ECDsa.Create();

                publicKey.ImportSubjectPublicKeyInfo(
                    Convert.FromBase64String(publisherPublicKeyBase64),
                    out _
                );

                if (!publicKey.VerifyData(payload, signature, HashAlgorithmName.SHA256))
                {
                    return null;
                }

                Payload? parsed = JsonSerializer.Deserialize<Payload>(payload);

                if (parsed?.n is null || parsed.d is null)
                {
                    return null;
                }

                return new DecayLicense(
                    parsed.n,
                    parsed.t == "pro" ? DecayTier.Pro : DecayTier.Free,
                    DateOnly.ParseExact(parsed.d, "yyyy-MM-dd")
                );
            }
            catch (FormatException)
            {
                return null;
            }
            catch (CryptographicException)
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private sealed class Payload
        {
            public string? n { get; set; } // licensee
            public string? t { get; set; } // tier
            public string? d { get; set; } // issued date
        }

        private static string Base64Url(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static byte[] FromBase64Url(string text)
        {
            string padded = text.Replace('-', '+').Replace('_', '/');

            return Convert.FromBase64String(
                padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=')
            );
        }
    }
}

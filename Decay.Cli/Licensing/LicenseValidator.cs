using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Decay.Renderer;

namespace Decay.Cli.Licensing
{
    /// <summary>A validated Decay license (console edition).</summary>
    internal sealed class ConsoleLicense
    {
        internal ConsoleLicense(string licensee, DecayTier tier, DateTime issued)
        {
            Licensee = licensee;
            Tier = tier;
            Issued = issued;
        }

        internal string Licensee { get; }

        internal DecayTier Tier { get; }

        internal DateTime Issued { get; }
    }

    /// <summary>
    /// Verify-only mirror of Decay.Licensing.LicenseKey.Validate for .NET
    /// Framework 4.8. Same key format ("DECAY1.&lt;payload&gt;.&lt;signature&gt;",
    /// ECDSA P-256 over SHA-256, offline) and the same accept/reject
    /// behavior; it only swaps the three APIs net48 lacks:
    /// ImportSubjectPublicKeyInfo (the key is read from its fixed 91-byte
    /// P-256 layout), System.Text.Json (MiniJson) and DateOnly (DateTime). Signing stays in tools/Decay.KeyGen on .NET 10.
    /// </summary>
    internal static class LicenseValidator
    {
        private const string Prefix = "DECAY1";

        // SubjectPublicKeyInfo for an uncompressed P-256 point: this 26-byte
        // header, then 0x04 || X (32 bytes) || Y (32 bytes).
        private static readonly byte[] P256SpkiHeader =
        {
            0x30, 0x59, 0x30, 0x13, 0x06, 0x07, 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x02, 0x01,
            0x06, 0x08, 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07, 0x03, 0x42, 0x00,
        };

        internal static ConsoleLicense? Validate(string? key, string publisherPublicKeyBase64)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(publisherPublicKeyBase64))
            {
                return null;
            }

            string compact = string.Concat(key!.Where(c => !char.IsWhiteSpace(c)));
            string[] parts = compact.Split('.');

            if (parts.Length != 3 || parts[0] != Prefix)
            {
                return null;
            }

            try
            {
                byte[] payload = FromBase64Url(parts[1]);
                byte[] signature = FromBase64Url(parts[2]);

                using (ECDsa publicKey = ImportP256PublicKey(Convert.FromBase64String(publisherPublicKeyBase64)))
                {
                    if (!publicKey.VerifyData(payload, signature, HashAlgorithmName.SHA256))
                    {
                        return null;
                    }
                }

                Dictionary<string, string>? fields = MiniJson.ParseStringFields(Encoding.UTF8.GetString(payload));

                string? licensee = Field(fields, "n");
                string? issued = Field(fields, "d");

                if (licensee is null || issued is null)
                {
                    return null;
                }

                return new ConsoleLicense(
                    licensee,
                    Field(fields, "t") == "pro" ? DecayTier.Pro : DecayTier.Free,
                    DateTime.ParseExact(issued, "yyyy-MM-dd", CultureInfo.InvariantCulture)
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
            catch (ArgumentException)
            {
                // Invalid UTF-8 in the payload.
                return null;
            }
        }

        private static string? Field(Dictionary<string, string>? fields, string name)
        {
            return fields is not null && fields.TryGetValue(name, out string? value) ? value : null;
        }

        private static ECDsa ImportP256PublicKey(byte[] spki)
        {
            if (spki.Length != 91
                || spki[26] != 0x04
                || !spki.Take(P256SpkiHeader.Length).SequenceEqual(P256SpkiHeader))
            {
                throw new CryptographicException("Not a P-256 public key.");
            }

            return ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = spki.Skip(27).Take(32).ToArray(),
                    Y = spki.Skip(59).Take(32).ToArray(),
                },
            });
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

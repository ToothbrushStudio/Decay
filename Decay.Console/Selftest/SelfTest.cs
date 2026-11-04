using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Decay.Cli.Licensing;
using Decay.Core;
using Decay.Renderer;

namespace Decay.Cli.Selftest
{
    /// <summary>
    /// "decay selftest": proves this build agrees with the .NET 10 engine.
    /// Known-answer tests for the crypto, a container and PNG written by the
    /// .NET 10 engine (Vectors.g.cs), license-key verification, and a full
    /// local encrypt/decrypt round trip. Exit code 0 only if everything passes.
    /// </summary>
    internal static class SelfTest
    {
        private static int _failures;

        internal static int Run()
        {
            _failures = 0;

            Check("AES-GCM matches NIST test case 1 (empty)", () => Nist(
                "00000000000000000000000000000000", "000000000000000000000000", "", "", "", "58e2fccefa7e3061367f1d57a4e7455a"));

            Check("AES-GCM matches NIST test case 2", () => Nist(
                "00000000000000000000000000000000", "000000000000000000000000", "",
                "00000000000000000000000000000000", "0388dace60b6a392f328c2b971b2fe78",
                "ab6e47d42cec13bdf53a67b21257bddf"));

            Check("AES-GCM matches NIST test case 4 (with AAD)", () => Nist(
                "feffe9928665731c6d6a8f9467308308", "cafebabefacedbaddecaf888",
                "feedfacedeadbeeffeedfacedeadbeefabaddad2",
                "d9313225f88406e5a55909c5aff5269a86a7a9531534f7da2e4c303d8a318a72"
                + "1c3c0c95956809532fcf0e2449a6b525b16aedf5aa0de657ba637b39",
                "42831ec2217774244b7221b784d0d49ce3aa212f2c02a4e035c17e2329aca12e"
                + "21d514b25466931c7d8f6a5aac84aa051ba30b396a0aac973d58e091",
                "5bc94fbc3221a5db94fae95ae7121a47"));

            Check("AES-GCM matches the .NET 10 engine (7 vectors)", () =>
            {
                foreach (string[] v in Vectors.AesGcm)
                {
                    if (!Nist(v[0], v[1], v[2], v[3], v[4], v[5]))
                    {
                        return false;
                    }
                }

                return true;
            });

            Check("AES-GCM rejects a flipped bit", () =>
            {
                string[] v = Vectors.AesGcm[5];
                byte[] cipher = FromHex(v[4]);

                cipher[0] ^= 1;

                try
                {
                    using (AesGcm gcm = new AesGcm(FromHex(v[0]), 16))
                    {
                        gcm.Decrypt(FromHex(v[1]), cipher, FromHex(v[5]), new byte[cipher.Length], FromHex(v[2]));
                    }

                    return false;
                }
                catch (AuthenticationTagMismatchException)
                {
                    return true;
                }
            });

            Check("PBKDF2-HMAC-SHA256 known answer", () =>
            {
                using (Rfc2898DeriveBytes kdf = new Rfc2898DeriveBytes(
                    Encoding.UTF8.GetBytes("passwordPASSWORDpassword"),
                    Encoding.UTF8.GetBytes("saltSALTsaltSALTsaltSALTsaltSALTsalt"),
                    4096,
                    HashAlgorithmName.SHA256))
                {
                    return ToHex(kdf.GetBytes(32)) == "348c89dbcbd32b2f32d814b8116e84cf2b17347ebc1800181c4e2a1fb8dd53e1";
                }
            });

            Check("opens a container sealed by the .NET 10 engine", () =>
                DecayCipher.OpenContainer(FromHex(Vectors.ContainerHex), Vectors.ContainerPassphrase)
                    == Vectors.ContainerPlainText);

            Check("rejects that container under a wrong passphrase", () =>
            {
                try
                {
                    DecayCipher.OpenContainer(FromHex(Vectors.ContainerHex), "wrong");

                    return false;
                }
                catch (DecayAuthenticationException)
                {
                    return true;
                }
            });

            Check("decodes a PNG written by the .NET 10 engine", () =>
            {
                PixelBuffer image = DecayPng.Decode(Convert.FromBase64String(Vectors.PngBase64));

                using (SHA256 sha = SHA256.Create())
                {
                    return image.Width == 16 && image.Height == 16
                        && ToHex(sha.ComputeHash(image.Pixels)) == Vectors.PngPixelsSha256;
                }
            });

            Check("PNG encode/decode round trip", () =>
            {
                PixelBuffer original = Gradient(40, 24);
                PixelBuffer back = DecayPng.Decode(DecayPng.Encode(original));

                return back.Width == 40 && back.Height == 24 && back.Pixels.SequenceEqual(original.Pixels);
            });

            Check("encrypt and decrypt a glitch image", () => RoundTrip(DecayStyle.Glitch));

            Check("encrypt and decrypt a rot image", () => RoundTrip(DecayStyle.Rot));

            Check("license: valid Pro key accepted", () =>
            {
                ConsoleLicense? license = LicenseValidator.Validate(Vectors.LicenseGood, Vectors.LicensePublicKey);

                return license is not null
                    && license.Tier == DecayTier.Pro
                    && license.Licensee == Vectors.LicenseGoodLicensee
                    && license.Issued == new DateTime(2026, 1, 2);
            });

            Check("license: valid Free key accepted", () =>
            {
                ConsoleLicense? license = LicenseValidator.Validate(Vectors.LicenseFree, Vectors.LicensePublicKey);

                return license is not null && license.Tier == DecayTier.Free && license.Licensee == "Free Person";
            });

            Check("license: key mangled by line breaks still accepted", () =>
            {
                string mangled = Vectors.LicenseGood.Insert(20, "\r\n  ").Insert(60, "\n");

                return LicenseValidator.Validate(mangled, Vectors.LicensePublicKey) is not null;
            });

            Check("license: tampered, truncated, wrong-key, garbage and empty are rejected", () =>
                LicenseValidator.Validate(Vectors.LicenseTampered, Vectors.LicensePublicKey) is null
                && LicenseValidator.Validate(Vectors.LicenseTruncated, Vectors.LicensePublicKey) is null
                && LicenseValidator.Validate(Vectors.LicenseWrongKey, Vectors.LicensePublicKey) is null
                && LicenseValidator.Validate("not a key", Vectors.LicensePublicKey) is null
                && LicenseValidator.Validate("", Vectors.LicensePublicKey) is null
                && LicenseValidator.Validate(null, Vectors.LicensePublicKey) is null
                && LicenseValidator.Validate(Vectors.LicenseGood, "") is null
                && LicenseValidator.Validate(Vectors.LicenseGood, "not base64!") is null);

            Check("license: a key does not validate under the shipped public key", () =>
                LicenseValidator.Validate(Vectors.LicenseGood, PublisherKeyBase64) is null);

            Console.WriteLine();
            Console.WriteLine(_failures == 0 ? "All self-tests passed." : $"{_failures} self-test(s) FAILED.");

            return _failures == 0 ? Exit.Ok : Exit.Failed;
        }

        private static string PublisherKeyBase64 => Decay.App.PublisherKey.SubjectPublicKeyInfoBase64;

        private static void Check(string name, Func<bool> test)
        {
            bool passed;
            string detail = "";

            try
            {
                passed = test();
            }
            catch (Exception exception) when (
                name.StartsWith("license", StringComparison.Ordinal)
                && (exception is NotImplementedException || exception is PlatformNotSupportedException))
            {
                // Mono (used to test on Linux) has no ECDSA at all; real Windows does.
                Console.WriteLine($"SKIP  {name} (ECDSA is not available on this platform)");

                return;
            }
            catch (Exception exception)
            {
                passed = false;
                detail = $" ({exception.GetType().Name}: {exception.Message})";
            }

            if (!passed)
            {
                _failures++;
            }

            Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  {name}{detail}");
        }

        // Encrypts then decrypts, checking ciphertext, tag and plaintext against the vector.
        private static bool Nist(string key, string nonce, string aad, string plain, string cipher, string tag)
        {
            byte[] k = FromHex(key);
            byte[] n = FromHex(nonce);
            byte[] a = FromHex(aad);
            byte[] p = FromHex(plain);
            byte[] c = new byte[p.Length];
            byte[] t = new byte[16];

            using (AesGcm gcm = new AesGcm(k, 16))
            {
                gcm.Encrypt(n, p, c, t, a);

                if (ToHex(c) != cipher || ToHex(t) != tag)
                {
                    return false;
                }

                byte[] back = new byte[p.Length];

                gcm.Decrypt(n, c, t, back, a);

                return back.SequenceEqual(p);
            }
        }

        private static bool RoundTrip(DecayStyle style)
        {
            const string message = "round trip ✓ ünïcode\nsecond line";
            PixelBuffer carrier = CarrierPresets.Voronoi(128, 128, 3);
            PixelBuffer original = carrier.Clone();

            // DecayArtist directly: the tier gate (Pro-only styles) is not what is under test.
            EncryptedArt art = style == DecayStyle.Rot
                ? DecayArtist.EncryptRot(message, "pw", carrier, 1000, 100)
                : DecayArtist.Encrypt(message, "pw", carrier, 1000, 25);

            byte[] png = DecayPng.Encode(art.FinalImage);
            PixelBuffer received = DecayPng.Decode(png);
            DecryptedArt result = DecayArtist.Decrypt(received, "pw");

            if (result.PlainText != message)
            {
                return false;
            }

            // The reveal must also hand back the sender's carrier. Embedding overwrites low
            // bits, so it is close, not exact; the .NET 10 engine measures the same
            // worst-case error on this input (3 levels glitch, 5 levels rot).
            PixelBuffer? restored = style == DecayStyle.Rot ? result.Rot?.Restore(received) : result.Sequence?.Unrot(received);

            if (restored is null)
            {
                return false;
            }

            int worst = 0;

            for (int i = 0; i < restored.Pixels.Length; i++)
            {
                worst = Math.Max(worst, Math.Abs(restored.Pixels[i] - original.Pixels[i]));
            }

            if (worst > (style == DecayStyle.Rot ? 5 : 3))
            {
                throw new InvalidOperationException($"restored carrier differs by up to {worst} levels");
            }

            return true;
        }

        private static PixelBuffer Gradient(int width, int height)
        {
            PixelBuffer image = new PixelBuffer(width, height);

            for (int i = 0; i < image.Pixels.Length; i++)
            {
                image.Pixels[i] = (byte)(i * 31 + i / 7);
            }

            return image;
        }

        private static byte[] FromHex(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder hex = new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes)
            {
                hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }
    }
}

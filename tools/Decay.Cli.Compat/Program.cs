using System.Security.Cryptography;
using System.Text;
using Decay.Cli.Licensing;
using Decay.Core;
using Decay.Licensing;
using Decay.Renderer;

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: vectors <out.cs> | make <png> <pass> <text> [glitch|rot] | check <png> <pass> <expected>");

    return 1;
}

switch (args[0])
{
    case "vectors":
        File.WriteAllText(args[1], Vectors());

        return 0;

    case "make":
    {
        PixelBuffer carrier = CarrierPresets.Voronoi(256, 256, 7);
        bool rot = args.Length > 4 && args[4] == "rot";
        EncryptedArt art = rot
            ? DecayArtist.EncryptRot(args[3], args[2], carrier, 1000, 100)
            : DecayArtist.Encrypt(args[3], args[2], carrier, 1000, 25);

        File.WriteAllBytes(args[1], DecayPng.Encode(art.FinalImage));
        Console.WriteLine($"wrote {args[1]} ({(rot ? "rot" : "glitch")})");

        return 0;
    }

    case "recompress":
    {
        // Decode a PNG and write it back with the .NET 10 encoder: same pixels, different zlib.
        byte[] original = File.ReadAllBytes(args[1]);
        PixelBuffer decoded = DecayPng.Decode(original);
        byte[] again = DecayPng.Encode(decoded);

        File.WriteAllBytes(args[2], again);
        Console.WriteLine($"{decoded.Width}x{decoded.Height}: as written {original.Length:N0} bytes, re-encoded by net10 {again.Length:N0} bytes, raw pixels {decoded.Pixels.Length:N0} bytes");

        return 0;
    }

    case "restoretest":
    {
        const string message = "round trip \u2713 \u00fcn\u00efcode\nsecond line";

        foreach (bool rot in new[] { false, true })
        {
            PixelBuffer carrier = CarrierPresets.Voronoi(128, 128, 3);
            PixelBuffer original = carrier.Clone();
            EncryptedArt art = rot
                ? DecayArtist.EncryptRot(message, "pw", carrier, 1000, 100)
                : DecayArtist.Encrypt(message, "pw", carrier, 1000, 25);
            PixelBuffer received = DecayPng.Decode(DecayPng.Encode(art.FinalImage));
            DecryptedArt result = DecayArtist.Decrypt(received, "pw");
            PixelBuffer restored = rot ? result.Rot!.Restore(received) : result.Sequence!.Unrot(received);
            int worst = 0;

            for (int i = 0; i < restored.Pixels.Length; i++)
            {
                worst = Math.Max(worst, Math.Abs(restored.Pixels[i] - original.Pixels[i]));
            }

            Console.WriteLine($"{(rot ? "rot" : "glitch")}: worst restore error {worst} levels");
        }

        return 0;
    }

    case "licensetest":
        return LicenseTest();

    case "storetest":
        return StoreTest();

    case "check":
    {
        PixelBuffer image = DecayPng.Decode(File.ReadAllBytes(args[1]));
        string text = DecayArtist.Decrypt(image, args[2]).PlainText;

        Console.WriteLine(text);

        if (text == args[3])
        {
            Console.WriteLine("MATCH");

            return 0;
        }

        Console.Error.WriteLine("MISMATCH");

        return 1;
    }

    default:
        Console.Error.WriteLine("unknown command");

        return 1;
}

static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

static string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

// A C# string literal that survives any file encoding: ASCII only, with escapes.
static string Literal(string s)
{
    StringBuilder text = new StringBuilder("\"");

    foreach (char c in s)
    {
        text.Append(c switch
        {
            '"' => "\\\"",
            '\\' => "\\\\",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _ when c < ' ' || c > '~' => $"\\u{(int)c:x4}",
            _ => c.ToString(),
        });
    }

    return text.Append('"').ToString();
}

// Integer-only test carrier; must match SelfTest.TestCarrier and FrozenPipelineTests.
static PixelBuffer TestCarrier(int width, int height)
{
    PixelBuffer image = new PixelBuffer(width, height);

    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            int i = (y * width + x) * PixelBuffer.BytesPerPixel;

            image.Pixels[i] = (byte)(x * 255 / Math.Max(1, width - 1));
            image.Pixels[i + 1] = (byte)(y * 255 / Math.Max(1, height - 1));
            image.Pixels[i + 2] = (byte)((x * 7 + y * 13) ^ (x * y));
            image.Pixels[i + 3] = 255;
        }
    }

    return image;
}

static void PipelineVector(StringBuilder sb, string name, string containerHex, string message, string passphrase, int width, int height)
{
    byte[] container = Convert.FromHexString(containerHex);
    PixelBuffer carrier = TestCarrier(width, height);
    bool rot = DecayCipher.GetPipelineId(container) == RotDecay.PipelineId;
    PixelBuffer image = rot
        ? RotDecay.FromContainer(container, message, passphrase)!.Apply(carrier)
        : DecaySequence.FromContainer(container)!.RenderFinal(carrier);
    string art = Hex(SHA256.HashData(image.Pixels));

    DecayCipher.EmbedContainer(container, image);

    DecryptedArt result = DecayArtist.Decrypt(image, passphrase);

    if (result.PlainText != message)
    {
        throw new InvalidOperationException($"{name}: the golden container did not decrypt.");
    }

    PixelBuffer reveal = rot ? result.Rot!.Restore(image) : result.Sequence!.Unrot(image);

    sb.AppendLine($"        internal const string {name}ContainerHex = {Q(Hex(container))};");
    sb.AppendLine($"        internal const string {name}PlainText = {Literal(message)};");
    sb.AppendLine($"        internal const string {name}Passphrase = {Literal(passphrase)};");
    sb.AppendLine($"        internal const int {name}Width = {width};");
    sb.AppendLine($"        internal const int {name}Height = {height};");
    sb.AppendLine($"        internal const string {name}CarrierSha256 = {Q(Hex(SHA256.HashData(carrier.Pixels)))};");
    sb.AppendLine($"        internal const string {name}ArtSha256 = {Q(art)};");
    sb.AppendLine($"        internal const string {name}RevealSha256 = {Q(Hex(SHA256.HashData(reveal.Pixels)))};");
}

static string Vectors()
{
    StringBuilder sb = new StringBuilder();

    sb.AppendLine("// <auto-generated>");
    sb.AppendLine("// Produced by the .NET 10 engine: dotnet run --project tools/Decay.Cli.Compat -- vectors <this file>");
    sb.AppendLine("// The self-test replays these on .NET Framework 4.8 to prove both editions agree.");
    sb.AppendLine("// </auto-generated>");
    sb.AppendLine("namespace Decay.Cli.Selftest");
    sb.AppendLine("{");
    sb.AppendLine("    internal static class Vectors");
    sb.AppendLine("    {");

    // A sealed container (AES-GCM + PBKDF2 + header) opened by passphrase.
    const string plain = "cross-check: meet at dawn \u2713 \u00fcn\u00efcode";
    byte[] container = DecayCipher.SealContainer(plain, "correct horse", 1, 0, 1000);

    sb.AppendLine($"        internal const string ContainerHex = {Q(Hex(container))};");
    sb.AppendLine("        internal const string ContainerPassphrase = \"correct horse\";");
    sb.AppendLine($"        internal const string ContainerPlainText = {Literal(plain)};");

    // AES-GCM known-answers from the real AesGcm, including awkward lengths.
    sb.AppendLine("        internal static readonly string[][] AesGcm =");
    sb.AppendLine("        {");

    Random random = new Random(1234);

    foreach (int length in new[] { 0, 1, 15, 16, 17, 100, 1000 })
    {
        byte[] key = new byte[32];
        byte[] nonce = new byte[12];
        byte[] aad = new byte[length % 7 == 0 ? 0 : 29];
        byte[] data = new byte[length];

        random.NextBytes(key);
        random.NextBytes(nonce);
        random.NextBytes(aad);
        random.NextBytes(data);

        byte[] cipher = new byte[length];
        byte[] tag = new byte[16];

        using (AesGcm gcm = new AesGcm(key, 16))
        {
            gcm.Encrypt(nonce, data, cipher, tag, aad);
        }

        sb.AppendLine($"            new[] {{ {Q(Hex(key))}, {Q(Hex(nonce))}, {Q(Hex(aad))}, {Q(Hex(data))}, {Q(Hex(cipher))}, {Q(Hex(tag))} }},");
    }

    sb.AppendLine("        };");

    // A PNG written by the .NET 10 encoder (different zlib) and the SHA-256 of its pixels.
    PixelBuffer image = new PixelBuffer(16, 16);

    for (int y = 0; y < 16; y++)
    {
        for (int x = 0; x < 16; x++)
        {
            int at = (y * 16 + x) * 4;

            image.Pixels[at] = (byte)(x * 16);
            image.Pixels[at + 1] = (byte)(y * 16);
            image.Pixels[at + 2] = (byte)((x ^ y) * 16);
            image.Pixels[at + 3] = 255;
        }
    }

    sb.AppendLine($"        internal const string PngBase64 = {Q(Convert.ToBase64String(DecayPng.Encode(image)))};");
    sb.AppendLine($"        internal const string PngPixelsSha256 = {Q(Hex(SHA256.HashData(image.Pixels)))};");

    // The frozen pipelines, rendered by this (.NET 10) engine from two of the
    // golden containers in Decay.Renderer.Tests/FrozenPipelineTests.cs. The
    // self-test renders them again on .NET Framework and compares.
    PipelineVector(
        sb,
        "Glitch",
        "444341590101140102030405060708090A0B0C0D0E0F10000003E865666768696A6B6C6D6E6F7000000019081734C156B8E22EE8E2540A0DDB0D302F922F0D7929AAD2BF468A457F6EEADA55D8DEC28410C916BF",
        "The glitch is the letter.",
        "frozen glitch",
        72,
        48
    );
    PipelineVector(
        sb,
        "Rot",
        "444341590102640102030405060708090A0B0C0D0E0F10000003E865666768696A6B6C6D6E6F700000002100742D4FC2C9BDAFA5C6E4E08F63EDC3C7FB05AFC56F79FB7A3C6EA154F3E7132EF872367B67C652511995E2EED714DC4B",
        "Meet me at 9pm!\n\tÉtude \U0001F9A0 ok\r~",
        "frozen rot",
        96,
        64
    );

    // License keys signed by a throwaway key pair; the validator takes the public key as a parameter.
    using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    using ECDsa stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    string publicKey = Convert.ToBase64String(signer.ExportSubjectPublicKeyInfo());
    string good = LicenseKey.Sign(new DecayLicense("Test Licensee \u00fc", DecayTier.Pro, new DateOnly(2026, 1, 2)), signer);
    string free = LicenseKey.Sign(new DecayLicense("Free Person", DecayTier.Free, new DateOnly(2026, 3, 4)), signer);
    string wrongKey = LicenseKey.Sign(new DecayLicense("Impostor", DecayTier.Pro, new DateOnly(2026, 1, 2)), stranger);
    string[] pieces = good.Split('.');
    char swapped = pieces[1][5] == 'A' ? 'B' : 'A';
    string tampered = pieces[0] + "." + pieces[1].Remove(5, 1).Insert(5, swapped.ToString()) + "." + pieces[2];

    sb.AppendLine($"        internal const string LicensePublicKey = {Q(publicKey)};");
    sb.AppendLine($"        internal const string LicenseGood = {Q(good)};");
    sb.AppendLine("        internal const string LicenseGoodLicensee = \"Test Licensee \\u00fc\";");
    sb.AppendLine($"        internal const string LicenseFree = {Q(free)};");
    sb.AppendLine($"        internal const string LicenseWrongKey = {Q(wrongKey)};");
    sb.AppendLine($"        internal const string LicenseTampered = {Q(tampered)};");
    sb.AppendLine($"        internal const string LicenseTruncated = {Q(good[..^10])};");
    sb.AppendLine("    }");
    sb.AppendLine("}");

    return sb.ToString();
}

static bool Same(string? key, string publicKey, out string note)
{
    DecayLicense? real = LicenseKey.Validate(key, publicKey);
    ConsoleLicense? mine = LicenseValidator.Validate(key, publicKey);

    note = $"real={(real is null ? "null" : real.Licensee)} mine={(mine is null ? "null" : mine.Licensee)}";

    if (real is null || mine is null)
    {
        return real is null && mine is null;
    }

    return real.Licensee == mine.Licensee
        && real.Tier == mine.Tier
        && real.Issued == DateOnly.FromDateTime(mine.Issued);
}

static int LicenseTest()
{
    using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    using ECDsa stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    using ECDsa p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);

    string pub = Convert.ToBase64String(signer.ExportSubjectPublicKeyInfo());
    int checks = 0;
    int differences = 0;
    int accepted = 0;

    void Compare(string label, string? key, string publicKey)
    {
        checks++;

        if (!Same(key, publicKey, out string note))
        {
            differences++;
            Console.WriteLine($"DIFFERENT: {label}: {note}");
        }
        else if (LicenseKey.Validate(key, publicKey) is not null)
        {
            accepted++;
        }
    }

    string[] names =
    {
        "Decay Pro #00001",
        "Test Licensee \u00fc",
        "Quote \" and back\\slash",
        "emoji \U0001F9A0 and \u2713 and \u00e9",
        "tab\tnew\nline",
        new string('x', 300),
        "<script>&'\"</script>",
    };

    foreach (string name in names)
    {
        foreach (DecayTier tier in new[] { DecayTier.Pro, DecayTier.Free })
        {
            string key = LicenseKey.Sign(new DecayLicense(name, tier, new DateOnly(2026, 11, 3)), signer);

            Compare($"valid {tier} '{name.Length}ch'", key, pub);
            Compare("wrong public key", key, Convert.ToBase64String(stranger.ExportSubjectPublicKeyInfo()));
            Compare("p384 public key", key, Convert.ToBase64String(p384.ExportSubjectPublicKeyInfo()));
            Compare("shipped public key", key, Decay.App.PublisherKey.SubjectPublicKeyInfoBase64);
            Compare("whitespace", key.Insert(10, " \r\n\t").Insert(40, "\n"), pub);
            Compare("leading/trailing space", "  " + key + "  \n", pub);
        }
    }

    // Every single-character mutation and every truncation of one key.
    string subject = LicenseKey.Sign(new DecayLicense("Mutation Subject", DecayTier.Pro, new DateOnly(2026, 1, 2)), signer);
    const string alphabet = "ABCabc019-_.";

    for (int i = 0; i < subject.Length; i++)
    {
        foreach (char replacement in alphabet)
        {
            if (replacement != subject[i])
            {
                Compare($"mutate {i}", subject.Remove(i, 1).Insert(i, replacement.ToString()), pub);
            }
        }

        Compare($"delete {i}", subject.Remove(i, 1), pub);
        Compare($"truncate {i}", subject[..i], pub);
    }

    foreach (string? junk in new string?[] { null, "", " ", "DECAY1", "DECAY1..", "DECAY1.a.b", "DECAY2.a.b", "a.b.c.d", "not a key" })
    {
        Compare($"junk '{junk}'", junk, pub);
    }

    foreach (string badKey in new[] { "", " ", "not base64!", "AAAA", Convert.ToBase64String(new byte[91]) })
    {
        Compare($"bad public key '{badKey}'", subject, badKey);
    }

    // Hand-built payloads: unsigned shapes the real parser also has to judge, signed by the real key.
    foreach (string json in new[]
    {
        "{\"n\":\"A\",\"t\":\"pro\",\"d\":\"2026-01-02\"}",
        "{\"n\":\"A\",\"d\":\"2026-01-02\"}",
        "{\"n\":\"A\",\"t\":\"pro\",\"d\":\"2026-01-02\",\"extra\":123,\"more\":null,\"flag\":true}",
        "{\"n\":null,\"d\":\"2026-01-02\"}",
        "{\"n\":\"A\",\"d\":\"2026-13-45\"}",
        "{\"n\":\"A\",\"d\":\"02/01/2026\"}",
        "{\"n\":\"A\",\"d\":\"2026-01-02\"} trailing",
        "{\"n\":\"A\",\"d\":\"2026-01-02\",}",
        "[]",
        "{}",
        "{\"n\":\"\\ud83e\\udda0\",\"d\":\"2026-01-02\"}",
        " { \"n\" : \"Spaced\" , \"d\" : \"2026-01-02\" } ",
    })
    {
        byte[] payload = Encoding.UTF8.GetBytes(json);
        byte[] sig = signer.SignData(payload, HashAlgorithmName.SHA256);
        string b(byte[] x) => Convert.ToBase64String(x).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Compare($"payload {json}", $"DECAY1.{b(payload)}.{b(sig)}", pub);
    }

    Console.WriteLine($"{checks} comparisons ({accepted} accepted by both, {checks - accepted} rejected by both), {differences} difference(s).");

    return differences == 0 ? 0 : 1;
}

static int StoreTest()
{
    string temp = Path.Combine(Path.GetTempPath(), "decay-store-" + Guid.NewGuid().ToString("N"));

    // Both key locations live in a throwaway folder, so this never touches a
    // real license file (on Windows, setting APPDATA would not redirect them).
    Directory.CreateDirectory(Path.Combine(temp, "portable"));
    LicenseStore.PortableFolderOverride = Path.Combine(temp, "portable");
    LicenseStore.UserFolderOverride = Path.Combine(temp, "user");
    Environment.SetEnvironmentVariable(LicenseStore.EnvironmentVariable, null);

    using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    string pub = Convert.ToBase64String(signer.ExportSubjectPublicKeyInfo());
    string Key(string name, DecayTier tier) =>
        LicenseKey.Sign(new DecayLicense(name, tier, new DateOnly(2026, 1, 2)), signer);

    string pro = Key("Pro Person", DecayTier.Pro);
    string free = Key("Free Person", DecayTier.Free);
    int failures = 0;

    void Expect(string label, bool ok)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {label}");

        if (!ok)
        {
            failures++;
        }
    }

    try
    {
        LicenseStore.Clear();

        LicenseStore.Result r = LicenseStore.Resolve(null, pub);
        Expect("nothing configured: Free, no license", r.License is null && r.Tier == DecayTier.Free);

        string saved = LicenseStore.Save(free, portable: false);
        r = LicenseStore.Resolve(null, pub);
        Expect("saved to the user folder is found", saved == LicenseStore.UserPath && r.Source == LicenseStore.UserPath && r.Tier == DecayTier.Free);

        LicenseStore.Save(pro, portable: true);
        r = LicenseStore.Resolve(null, pub);
        Expect("license.key beside the exe beats the user folder", r.Source == LicenseStore.PortablePath && r.Tier == DecayTier.Pro);

        Environment.SetEnvironmentVariable(LicenseStore.EnvironmentVariable, free);
        r = LicenseStore.Resolve(null, pub);
        Expect("DECAY_LICENSE beats the files", r.Source.StartsWith(LicenseStore.EnvironmentVariable) && r.Tier == DecayTier.Free);

        r = LicenseStore.Resolve(pro, pub);
        Expect("--license beats everything", r.Source == "--license" && r.Tier == DecayTier.Pro);

        r = LicenseStore.Resolve("garbage", pub);
        Expect("an invalid --license is reported and the next source is used",
            r.Rejected.Contains("--license") && r.Source.StartsWith(LicenseStore.EnvironmentVariable));

        Environment.SetEnvironmentVariable(LicenseStore.EnvironmentVariable, null);
        File.WriteAllText(LicenseStore.PortablePath, "not a key");
        r = LicenseStore.Resolve(null, pub);
        Expect("an invalid license.key is reported and the user folder is used",
            r.Rejected.Contains(LicenseStore.PortablePath) && r.Source == LicenseStore.UserPath);

        int removed = LicenseStore.Clear().Count;
        r = LicenseStore.Resolve(null, pub);
        Expect("clear removes both saved keys", removed == 2 && r.License is null);

        r = LicenseStore.Resolve(null, "");
        Expect("an empty publisher key never validates", r.License is null);
    }
    finally
    {
        LicenseStore.Clear();
        Directory.Delete(temp, recursive: true);
    }

    Console.WriteLine(failures == 0 ? "store tests passed" : $"{failures} store test(s) FAILED");

    return failures == 0 ? 0 : 1;
}

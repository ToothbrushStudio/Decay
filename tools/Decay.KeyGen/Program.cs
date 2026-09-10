using System.Security.Cryptography;
using Decay.Licensing;
using Decay.Renderer;

// Publisher-side tooling. The private key this produces must NEVER enter
// the repository or any build output — keep it offline, back it up. Losing
// it stops new license sales but never breaks a key already sold.

if (args.Length >= 1 && args[0] == "gen")
{
    string path = args.Length >= 2 ? args[1] : "decay-publisher-private.pem";

    if (File.Exists(path))
    {
        Console.Error.WriteLine($"Refusing to overwrite existing key file: {path}");

        return 1;
    }

    using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    File.WriteAllText(path, key.ExportECPrivateKeyPem());
    Console.WriteLine($"Private key written to {path} — keep it OFFLINE and backed up.");
    Console.WriteLine();
    Console.WriteLine("Embed this public key in Decay.App/PublisherKey.cs:");
    Console.WriteLine();
    Console.WriteLine(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));

    return 0;
}

if (args.Length >= 3 && args[0] == "batch")
{
    string pemPath = args[1];

    if (!int.TryParse(args[2], out int count) || count < 1 || count > 100_000)
    {
        Console.Error.WriteLine("Count must be between 1 and 100000.");

        return 1;
    }

    int start = args.Length >= 4 && int.TryParse(args[3], out int s) ? s : 1;

    using ECDsa batchKey = ECDsa.Create();

    batchKey.ImportFromPem(File.ReadAllText(pemPath));

    for (int i = 0; i < count; i++)
    {
        Console.WriteLine(LicenseKey.Sign(
            new DecayLicense(
                $"Decay Pro #{start + i:D5}",
                DecayTier.Pro,
                DateOnly.FromDateTime(DateTime.UtcNow)
            ),
            batchKey
        ));
    }

    return 0;
}

if (args.Length >= 3 && args[0] == "sign")
{
    string pemPath = args[1];
    string licensee = args[2];

    using ECDsa key = ECDsa.Create();

    key.ImportFromPem(File.ReadAllText(pemPath));

    string license = LicenseKey.Sign(
        new DecayLicense(licensee, DecayTier.Pro, DateOnly.FromDateTime(DateTime.UtcNow)),
        key
    );

    Console.WriteLine(license);

    return 0;
}

Console.WriteLine("Decay license tooling");
Console.WriteLine();
Console.WriteLine("  dotnet run gen [path]                 Generate the publisher key pair (once).");
Console.WriteLine("  dotnet run sign <private.pem> <name>  Sign a Pro license for a customer.");
Console.WriteLine("  dotnet run batch <private.pem> <count> [startNumber]");
Console.WriteLine("                                        Emit <count> anonymous Pro keys, one per");
Console.WriteLine("                                        line - upload as a serial pool to your");
Console.WriteLine("                                        payment platform for automatic delivery.");

return 1;

using System.Security.Cryptography;
using System.Text;
using Decay.Cli.Licensing;
using Decay.Core;
using Decay.Renderer;

namespace Decay.Cli
{
    /// <summary>The encrypt, decrypt and presets commands: the same pipeline the app's pages call.</summary>
    internal static class Commands
    {
        // The square sizes the app can produce; anything else was probably resized.
        private static readonly int[] DecaySizes = { 256, 384, 512, 768, 1024 };

        internal static int Encrypt(string[] argv)
        {
            Args args = Args.Parse(
                argv,
                new[] { "text", "in", "out", "preset", "size", "style", "strength", "carrier", "seed", "pass", "license" },
                new[] { "force" }
            );

            if (args.Positional.Count > 0)
            {
                throw new UsageException($"Unexpected argument \"{args.Positional[0]}\".");
            }

            string message = ReadMessage(args);
            string outPath = args.Get("out") ?? "letter.png";

            if (File.Exists(outPath) && !args.Has("force"))
            {
                throw new UsageException($"\"{outPath}\" already exists. Use --force to overwrite it.");
            }

            LicenseStore.Result license = ResolveLicense(args);
            DecayStyle style = ParseStyle(args.Get("style"));
            int strength = ParseInt(args.Get("strength"), "--strength", TierPolicy.LockedIntensity);

            if (strength < DecayStrength.Min || strength > DecayStrength.Max)
            {
                throw new UsageException($"--strength must be {DecayStrength.Min}-{DecayStrength.Max}.");
            }

            string passphrase = args.Get("pass") ?? Passphrase.ReadNew();

            Console.Error.WriteLine("Deriving key and rotting the carrier...");

            EncryptedArt art;
            string? carrierPath = args.Get("carrier");

            if (carrierPath is not null)
            {
                PixelBuffer carrier = DecayPng.Decode(File.ReadAllBytes(carrierPath));

                art = DecayProduct.EncryptWithCustomCarrier(
                    message,
                    passphrase,
                    license.Tier,
                    carrier,
                    strength,
                    style: style
                );
            }
            else
            {
                CarrierPreset preset = ParsePreset(args.Get("preset"));
                int size = ParseInt(args.Get("size"), "--size", 512);

                if (Array.IndexOf(DecaySizes, size) < 0)
                {
                    throw new UsageException("--size must be one of " + string.Join(", ", DecaySizes) + ".");
                }

                int seed = ParseInt(args.Get("seed"), "--seed", RandomSeed());

                art = DecayProduct.EncryptWithPreset(
                    message,
                    passphrase,
                    license.Tier,
                    preset,
                    size,
                    size,
                    seed,
                    strength,
                    style: style
                );
            }

            File.WriteAllBytes(outPath, DecayPng.Encode(art.FinalImage));

            Console.Error.WriteLine(
                $"Wrote {outPath} ({art.FinalImage.Width}x{art.FinalImage.Height}). "
                + "The image IS the message - send the file itself, never a screenshot."
            );

            if (license.Tier == DecayTier.Free)
            {
                Console.Error.WriteLine("Free tier: the image carries a watermark.");
            }

            return Exit.Ok;
        }

        internal static int Decrypt(string[] argv)
        {
            Args args = Args.Parse(argv, new[] { "carrier-out", "pass", "license" }, Array.Empty<string>());

            if (args.Positional.Count != 1)
            {
                throw new UsageException("decrypt needs exactly one image file.");
            }

            PixelBuffer received = DecayPng.Decode(File.ReadAllBytes(args.Positional[0]));

            if (Array.IndexOf(DecaySizes, received.Width) < 0 || received.Width != received.Height)
            {
                Console.Error.WriteLine(
                    $"Note: this image is {received.Width}x{received.Height}, which isn't a size Decay makes, "
                    + "so it has probably been resized or cropped. The message may be gone."
                );
            }

            string passphrase = args.Get("pass") ?? Passphrase.Read("Passphrase: ");

            Console.Error.WriteLine("Deriving key...");

            DecryptedArt result = DecayArtist.Decrypt(received, passphrase);

            Console.Out.WriteLine(result.PlainText);

            string? carrierOut = args.Get("carrier-out");

            if (carrierOut is not null)
            {
                PixelBuffer? recovered = result.Rot is RotDecay rot
                    ? rot.Restore(received)
                    : result.Sequence?.Unrot(received);

                if (recovered is null)
                {
                    Console.Error.WriteLine("No separate carrier is recoverable from this image.");
                }
                else
                {
                    File.WriteAllBytes(carrierOut, DecayPng.Encode(recovered));
                    Console.Error.WriteLine($"Recovered carrier written to {carrierOut}.");
                }
            }

            return Exit.Ok;
        }

        internal static int Presets()
        {
            LicenseStore.Result license = LicenseStore.Resolve(null);

            Console.WriteLine("Carrier presets (use --preset <id> or <number>):");

            for (int i = 0; i < CarrierPresets.All.Count; i++)
            {
                CarrierPreset preset = CarrierPresets.All[i];
                string access = TierPolicy.AllowsPreset(license.Tier, preset) ? "" : "  (Pro)";

                Console.WriteLine($"  {i + 1}. {preset.Id,-16} {preset.Name}, {preset.Stage}{access}");
            }

            return Exit.Ok;
        }

        // ------------------------------------------------------- helpers

        private static LicenseStore.Result ResolveLicense(Args args)
        {
            LicenseStore.Result license = LicenseStore.Resolve(args.Get("license"));

            foreach (string rejected in license.Rejected)
            {
                Console.Error.WriteLine($"Warning: the license key in {rejected} is not valid; ignoring it.");
            }

            return license;
        }

        private static string ReadMessage(Args args)
        {
            string? text = args.Get("text");
            string? file = args.Get("in");

            if ((text is null) == (file is null))
            {
                throw new UsageException("Give the message with exactly one of --text or --in.");
            }

            if (text is not null)
            {
                return text;
            }

            if (file == "-")
            {
                return Console.In.ReadToEnd();
            }

            return File.ReadAllText(file!, Encoding.UTF8);
        }

        private static DecayStyle ParseStyle(string? value)
        {
            switch ((value ?? "glitch").ToLowerInvariant())
            {
                case "glitch":
                    return DecayStyle.Glitch;
                case "rot":
                    return DecayStyle.Rot;
                default:
                    throw new UsageException("--style must be glitch or rot.");
            }
        }

        private static CarrierPreset ParsePreset(string? value)
        {
            if (value is null)
            {
                return CarrierPresets.Default;
            }

            if (int.TryParse(value, out int number) && number >= 1 && number <= CarrierPresets.All.Count)
            {
                return CarrierPresets.All[number - 1];
            }

            CarrierPreset? byId = CarrierPresets.All.FirstOrDefault(
                p => string.Equals(p.Id, value, StringComparison.OrdinalIgnoreCase)
            );

            return byId ?? throw new UsageException($"Unknown preset \"{value}\". Run \"decay presets\".");
        }

        private static int ParseInt(string? value, string name, int fallback)
        {
            if (value is null)
            {
                return fallback;
            }

            if (!int.TryParse(value, out int parsed))
            {
                throw new UsageException($"{name} must be a whole number.");
            }

            return parsed;
        }

        private static int RandomSeed()
        {
            byte[] bytes = new byte[4];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return BitConverter.ToInt32(bytes, 0) & int.MaxValue;
        }
    }

    /// <summary>Reads a passphrase without echoing it.</summary>
    internal static class Passphrase
    {
        internal static string ReadNew()
        {
            string first = Read("Passphrase: ");

            if (Console.IsInputRedirected)
            {
                return first;
            }

            if (Read("Repeat passphrase: ") != first)
            {
                throw new UsageException("The passphrases do not match.");
            }

            return first;
        }

        internal static string Read(string prompt)
        {
            if (Console.IsInputRedirected)
            {
                return Console.In.ReadLine() ?? "";
            }

            Console.Error.Write(prompt);

            StringBuilder typed = new StringBuilder();

            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
                    break;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (typed.Length > 0)
                    {
                        typed.Length--;
                    }
                }
                else if (key.KeyChar != '\0')
                {
                    typed.Append(key.KeyChar);
                }
            }

            Console.Error.WriteLine();

            return typed.ToString();
        }
    }
}

using Decay.Cli.Licensing;
using Decay.App;
using Decay.Renderer;

namespace Decay.Cli
{
    /// <summary>activate, license and deactivate: offline license keys, no server.</summary>
    internal static class LicenseCommands
    {
        internal static int Activate(string[] argv)
        {
            Args args = Args.Parse(argv, Array.Empty<string>(), new[] { "portable" });

            if (args.Positional.Count == 0)
            {
                throw new UsageException("activate needs the license key.");
            }

            // Keys are often pasted with line breaks; accept them split across arguments.
            string key = string.Concat(args.Positional);
            ConsoleLicense? license = LicenseValidator.Validate(key, PublisherKey.SubjectPublicKeyInfoBase64);

            if (license is null)
            {
                Console.Error.WriteLine("decay: that is not a valid Decay license key.");

                return Exit.BadImageOrPassphrase;
            }

            string path = LicenseStore.Save(key, args.Has("portable"));

            Console.WriteLine($"Activated: {license.Licensee} ({license.Tier}).");
            Console.WriteLine($"Saved to {path}");

            return Exit.Ok;
        }

        internal static int Show(string[] argv)
        {
            Args args = Args.Parse(argv, new[] { "license" }, Array.Empty<string>());
            LicenseStore.Result result = LicenseStore.Resolve(args.Get("license"));

            foreach (string rejected in result.Rejected)
            {
                Console.Error.WriteLine($"Warning: the license key in {rejected} is not valid.");
            }

            if (result.License is null)
            {
                Console.WriteLine("No license: Free tier (watermarked, preset TV static, glitch style only).");
                Console.WriteLine("Decrypting is always free.");

                return Exit.Ok;
            }

            Console.WriteLine($"Licensee: {result.License.Licensee}");
            Console.WriteLine($"Tier:     {result.License.Tier}");
            Console.WriteLine($"Issued:   {result.License.Issued:yyyy-MM-dd}");
            Console.WriteLine($"Source:   {result.Source}");

            return Exit.Ok;
        }

        internal static int Deactivate()
        {
            var removed = LicenseStore.Clear();

            Console.WriteLine(
                removed.Count == 0
                    ? "No saved license key found."
                    : "Removed: " + string.Join(", ", removed)
            );

            return Exit.Ok;
        }
    }
}

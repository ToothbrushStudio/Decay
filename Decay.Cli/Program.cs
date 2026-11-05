using System.Text;
using Decay.Core;
using Decay.Renderer;

namespace Decay.Cli
{
    /// <summary>Process exit codes.</summary>
    internal static class Exit
    {
        internal const int Ok = 0;
        internal const int Usage = 1;
        internal const int BadImageOrPassphrase = 2;
        internal const int NeedsPro = 3;
        internal const int Failed = 4;
    }

    internal static class Program
    {
        internal const string Version = "1.0.0";

        private static int Main(string[] argv)
        {
            try
            {
                Console.OutputEncoding = new UTF8Encoding(false);
            }
            catch (IOException)
            {
                // No console attached (redirected); keep the default.
            }

            if (argv.Length == 0 || argv[0] is "help" or "--help" or "-h" or "/?")
            {
                Console.Error.WriteLine(HelpText);

                return argv.Length == 0 ? Exit.Usage : Exit.Ok;
            }

            string command = argv[0].ToLowerInvariant();
            string[] rest = argv.Skip(1).ToArray();

            try
            {
                switch (command)
                {
                    case "encrypt":
                        return Commands.Encrypt(rest);
                    case "decrypt":
                        return Commands.Decrypt(rest);
                    case "presets":
                        return Commands.Presets();
                    case "activate":
                        return LicenseCommands.Activate(rest);
                    case "license":
                        return LicenseCommands.Show(rest);
                    case "deactivate":
                        return LicenseCommands.Deactivate();
                    case "selftest":
                        return Selftest.SelfTest.Run();
                    case "version":
                    case "--version":
                        Console.WriteLine($"decay {Version} (console, .NET Framework 4.8)");

                        return Exit.Ok;
                    default:
                        throw new UsageException($"Unknown command \"{argv[0]}\".");
                }
            }
            catch (UsageException exception)
            {
                Console.Error.WriteLine("decay: " + exception.Message);
                Console.Error.WriteLine("Run \"decay help\" for usage.");

                return Exit.Usage;
            }
            catch (ArgumentException exception)
            {
                // Bad values that got past the option checks (for example an out-of-range number).
                Console.Error.WriteLine("decay: " + exception.Message);

                return Exit.Usage;
            }
            catch (DecayTierException exception)
            {
                Console.Error.WriteLine("decay: " + exception.Message);
                Console.Error.WriteLine("Activate a license with: decay activate <key>");

                return Exit.NeedsPro;
            }
            catch (DecayAuthenticationException exception)
            {
                Console.Error.WriteLine("decay: " + exception.Message);

                return Exit.BadImageOrPassphrase;
            }
            catch (DecayFormatException exception)
            {
                Console.Error.WriteLine("decay: " + exception.Message);

                return Exit.BadImageOrPassphrase;
            }
            catch (InvalidDataException exception)
            {
                Console.Error.WriteLine("decay: " + exception.Message);

                return Exit.BadImageOrPassphrase;
            }
            catch (DecayCapacityException exception)
            {
                Console.Error.WriteLine("decay: " + exception.Message);

                return Exit.Failed;
            }
            catch (IOException exception)
            {
                Console.Error.WriteLine("decay: " + exception.Message);

                return Exit.Failed;
            }
            catch (UnauthorizedAccessException exception)
            {
                Console.Error.WriteLine("decay: " + exception.Message);

                return Exit.Failed;
            }
            catch (Exception exception)
            {
                // Anything unexpected still gets a message and an exit code,
                // not a stack trace or a Windows crash dialog.
                Console.Error.WriteLine($"decay: unexpected error ({exception.GetType().Name}): {exception.Message}");

                return Exit.Failed;
            }
        }

        private const string HelpText = @"DECAY " + Version + @" - encrypt your message into the rot (console edition)

Usage:
  decay encrypt (--text <message> | --in <file|->) [options]
  decay decrypt <image.png> [options]
  decay presets
  decay activate <license-key> [--portable]
  decay license
  decay deactivate
  decay selftest
  decay version

encrypt options:
  --out <file.png>     Where to write the image (default letter.png)
  --preset <id|n>      Carrier preset, by id or number (see ""decay presets"")
  --size <n>           256, 384, 512 (default), 768 or 1024
  --style glitch|rot   Decay style (rot needs Pro)
  --strength <1-255>   Decay strength, default 100 (Pro)
  --carrier <file.png> Use your own PNG as the carrier (Pro)
  --seed <n>           Repeat a carrier
  --force              Overwrite the output file

decrypt options:
  --carrier-out <file.png>  Also save the recovered original carrier

common options:
  --pass <passphrase>  Prefer the hidden prompt: command lines are visible
  --license <key>      Use this license key for this run

Piped input (--in - and a piped passphrase) must be UTF-8 text.

The image IS the message. Send the PNG file itself: a screenshot, resize or
re-compression destroys it. Decryption is free for everyone, always.

Licenses are checked offline, in this order: --license, the DECAY_LICENSE
environment variable, license.key next to decay.exe, then
%APPDATA%\Decay\license.key.

Exit codes: 0 ok, 1 usage, 2 wrong passphrase or damaged image,
            3 feature needs Pro, 4 other failure.";
    }
}

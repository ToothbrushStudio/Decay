using System.Text;
using Decay.App;
using Decay.Renderer;

namespace Decay.Cli.Licensing
{
    /// <summary>
    /// Finds and saves the license key. Offline only: nothing is sent
    /// anywhere. Lookup order: --license, the DECAY_LICENSE environment
    /// variable, license.key next to decay.exe (so a key can travel on the
    /// floppy), then %APPDATA%\Decay\license.key.
    /// </summary>
    internal static class LicenseStore
    {
        internal const string FileName = "license.key";
        internal const string EnvironmentVariable = "DECAY_LICENSE";

        internal sealed class Result
        {
            internal ConsoleLicense? License { get; set; }

            internal string Source { get; set; } = "";

            /// <summary>Sources that held a key which did not validate.</summary>
            internal List<string> Rejected { get; } = new List<string>();

            internal DecayTier Tier => License?.Tier ?? DecayTier.Free;
        }

        internal static string PortablePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);

        internal static string UserPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Decay",
                FileName
            );

        internal static Result Resolve(string? flagKey, string publisherKey = PublisherKey.SubjectPublicKeyInfoBase64)
        {
            Result result = new Result();

            foreach (KeyValuePair<string, string?> candidate in Candidates(flagKey))
            {
                if (string.IsNullOrWhiteSpace(candidate.Value))
                {
                    continue;
                }

                ConsoleLicense? license = LicenseValidator.Validate(candidate.Value, publisherKey);

                if (license is null)
                {
                    result.Rejected.Add(candidate.Key);

                    continue;
                }

                result.License = license;
                result.Source = candidate.Key;

                return result;
            }

            return result;
        }

        internal static string Save(string key, bool portable)
        {
            string path = portable ? PortablePath : UserPath;
            string? directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, key.Trim() + Environment.NewLine, new UTF8Encoding(false));

            return path;
        }

        /// <summary>Deletes saved keys; returns the paths removed.</summary>
        internal static List<string> Clear()
        {
            List<string> removed = new List<string>();

            foreach (string path in new[] { PortablePath, UserPath })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    removed.Add(path);
                }
            }

            return removed;
        }

        private static IEnumerable<KeyValuePair<string, string?>> Candidates(string? flagKey)
        {
            yield return new KeyValuePair<string, string?>("--license", flagKey);
            yield return new KeyValuePair<string, string?>(
                EnvironmentVariable + " environment variable",
                Environment.GetEnvironmentVariable(EnvironmentVariable)
            );
            yield return new KeyValuePair<string, string?>(PortablePath, ReadFile(PortablePath));
            yield return new KeyValuePair<string, string?>(UserPath, ReadFile(UserPath));
        }

        private static string? ReadFile(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}

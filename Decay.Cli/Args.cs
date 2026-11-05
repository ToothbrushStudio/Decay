namespace Decay.Cli
{
    /// <summary>Tiny argument parser: positionals, "--name value" options and "--flag" switches.</summary>
    internal sealed class Args
    {
        private readonly Dictionary<string, string> _options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal List<string> Positional { get; } = new List<string>();

        /// <exception cref="UsageException">An unknown option, or a value option with no value.</exception>
        internal static Args Parse(
            IEnumerable<string> argv,
            IEnumerable<string> valueOptions,
            IEnumerable<string> flags
        )
        {
            HashSet<string> valued = new HashSet<string>(valueOptions, StringComparer.OrdinalIgnoreCase);
            HashSet<string> switches = new HashSet<string>(flags, StringComparer.OrdinalIgnoreCase);
            Args args = new Args();
            string[] items = argv.ToArray();

            for (int i = 0; i < items.Length; i++)
            {
                string item = items[i];

                if (item == "-" || !item.StartsWith("--", StringComparison.Ordinal))
                {
                    args.Positional.Add(item);

                    continue;
                }

                string name = item.Substring(2);
                string? inline = null;
                int equals = name.IndexOf('=');

                if (equals >= 0)
                {
                    inline = name.Substring(equals + 1);
                    name = name.Substring(0, equals);
                }

                if (switches.Contains(name) && inline is null)
                {
                    args._flags.Add(name);
                }
                else if (valued.Contains(name))
                {
                    if (inline is null)
                    {
                        if (i + 1 >= items.Length)
                        {
                            throw new UsageException($"--{name} needs a value.");
                        }

                        inline = items[++i];
                    }

                    args._options[name] = inline;
                }
                else
                {
                    throw new UsageException($"Unknown option --{name}.");
                }
            }

            return args;
        }

        internal string? Get(string name)
        {
            return _options.TryGetValue(name, out string? value) ? value : null;
        }

        internal bool Has(string name)
        {
            return _flags.Contains(name);
        }
    }

    /// <summary>The command line was wrong; the message says how.</summary>
    internal sealed class UsageException : Exception
    {
        internal UsageException(string message)
            : base(message)
        {
        }
    }
}

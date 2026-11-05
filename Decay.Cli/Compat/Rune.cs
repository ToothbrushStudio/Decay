namespace System.Text
{
    /// <summary>
    /// Minimal stand-in for System.Text.Rune (a Unicode scalar value), which
    /// .NET Framework lacks. Only the Value property is provided.
    /// </summary>
    internal readonly struct Rune
    {
        internal Rune(int value)
        {
            Value = value;
        }

        internal int Value { get; }
    }

    internal static class StringRuneExtensions
    {
        /// <summary>
        /// Enumerates the string's Unicode scalar values. An unpaired
        /// surrogate yields U+FFFD, as System.Text.Rune enumeration does.
        /// </summary>
        internal static IEnumerable<Rune> EnumerateRunes(this string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    yield return new Rune(char.ConvertToUtf32(c, text[i + 1]));
                    i++;
                }
                else if (char.IsSurrogate(c))
                {
                    yield return new Rune(0xFFFD);
                }
                else
                {
                    yield return new Rune(c);
                }
            }
        }
    }
}

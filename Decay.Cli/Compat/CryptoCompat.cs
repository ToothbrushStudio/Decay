namespace System.Security.Cryptography
{
    /// <summary>Stand-in for .NET Core's CryptographicOperations (only what the engine uses).</summary>
    internal static class CryptographicOperations
    {
        internal static void ZeroMemory(Span<byte> buffer)
        {
            buffer.Clear();
        }

        internal static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            int difference = 0;

            for (int i = 0; i < left.Length; i++)
            {
                difference |= left[i] ^ right[i];
            }

            return difference == 0;
        }
    }

    /// <summary>The authentication tag did not match: wrong key, or damaged data.</summary>
    internal sealed class AuthenticationTagMismatchException : CryptographicException
    {
        internal AuthenticationTagMismatchException()
            : base("The computed authentication tag did not match the input authentication tag.")
        {
        }
    }
}

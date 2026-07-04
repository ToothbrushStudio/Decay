using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Decay.Core
{
    /// <summary>
    /// Serializes and opens the Decay v1 payload container. The byte layout
    /// is FROZEN — see docs/payload-format-v1.md. Changes are additive only:
    /// new versions may be introduced, but this reader must work forever.
    /// </summary>
    internal static class PayloadContainer
    {
        internal const byte FormatVersion1 = 0x01;
        internal const int MagicSize = 4;
        internal const int SaltSize = 16;
        internal const int NonceSize = 12;
        internal const int TagSize = 16;
        internal const int KeySize = 32;
        internal const int HeaderSize = MagicSize + 1 + SaltSize + 4 + NonceSize + 4; // 41
        internal const int MinContainerSize = HeaderSize + TagSize; // 57
        internal const int MaxIterations = 10_000_000;

        private static ReadOnlySpan<byte> Magic => "DCAY"u8;

        internal static byte[] Seal(string plainText, string passphrase, int iterations)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);

            return Seal(plainText, passphrase, iterations, salt, nonce);
        }

        // Deterministic overload used by tests and format vectors.
        internal static byte[] Seal(
            string plainText,
            string passphrase,
            int iterations,
            byte[] salt,
            byte[] nonce
        )
        {
            if (iterations < 1 || iterations > MaxIterations)
            {
                throw new ArgumentOutOfRangeException(nameof(iterations));
            }

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] container = new byte[MinContainerSize + plainBytes.Length];
            Span<byte> header = container.AsSpan(0, HeaderSize);

            Magic.CopyTo(header);
            header[MagicSize] = FormatVersion1;
            salt.CopyTo(header.Slice(5, SaltSize));
            BinaryPrimitives.WriteUInt32BigEndian(header.Slice(21, 4), (uint)iterations);
            nonce.CopyTo(header.Slice(25, NonceSize));
            BinaryPrimitives.WriteUInt32BigEndian(header.Slice(37, 4), (uint)plainBytes.Length);

            byte[] key = DeriveKey(passphrase, salt, iterations);

            try
            {
                using AesGcm aes = new AesGcm(key, TagSize);

                aes.Encrypt(
                    nonce,
                    plainBytes,
                    container.AsSpan(HeaderSize, plainBytes.Length),
                    container.AsSpan(HeaderSize + plainBytes.Length, TagSize),
                    header
                );
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }

            return container;
        }

        internal static string Open(ReadOnlySpan<byte> container, string passphrase)
        {
            ContainerInfo info = ReadHeader(container);

            if (container.Length != MinContainerSize + info.CiphertextLength)
            {
                throw new DecayFormatException(
                    "The image's payload is truncated or corrupted."
                );
            }

            ReadOnlySpan<byte> header = container.Slice(0, HeaderSize);
            ReadOnlySpan<byte> ciphertext = container.Slice(HeaderSize, info.CiphertextLength);
            ReadOnlySpan<byte> tag = container.Slice(HeaderSize + info.CiphertextLength, TagSize);

            byte[] key = DeriveKey(passphrase, header.Slice(5, SaltSize), info.Iterations);
            byte[] plainBytes = new byte[info.CiphertextLength];

            try
            {
                using AesGcm aes = new AesGcm(key, TagSize);

                aes.Decrypt(info.Nonce, ciphertext, tag, plainBytes, header);
            }
            catch (AuthenticationTagMismatchException)
            {
                throw new DecayAuthenticationException();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }

            return Encoding.UTF8.GetString(plainBytes);
        }

        /// <summary>
        /// Validates the fixed header and returns its fields. Used both by
        /// Open and by the pixel layer to learn how many bytes to extract.
        /// </summary>
        internal static ContainerInfo ReadHeader(ReadOnlySpan<byte> headerBytes)
        {
            if (headerBytes.Length < HeaderSize)
            {
                throw new DecayFormatException("The image's payload is truncated or corrupted.");
            }

            if (!headerBytes.Slice(0, MagicSize).SequenceEqual(Magic))
            {
                throw new DecayFormatException(
                    "This does not appear to be a Decay image."
                );
            }

            byte version = headerBytes[MagicSize];

            if (version != FormatVersion1)
            {
                throw new DecayVersionException(version);
            }

            uint iterations = BinaryPrimitives.ReadUInt32BigEndian(headerBytes.Slice(21, 4));

            if (iterations < 1 || iterations > MaxIterations)
            {
                throw new DecayFormatException(
                    "The image's payload declares an invalid key-derivation cost."
                );
            }

            uint ciphertextLength = BinaryPrimitives.ReadUInt32BigEndian(headerBytes.Slice(37, 4));

            if (ciphertextLength > int.MaxValue - MinContainerSize)
            {
                throw new DecayFormatException(
                    "The image's payload declares an invalid message length."
                );
            }

            return new ContainerInfo(
                (int)iterations,
                (int)ciphertextLength,
                headerBytes.Slice(25, NonceSize).ToArray()
            );
        }

        private static byte[] DeriveKey(string passphrase, ReadOnlySpan<byte> salt, int iterations)
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                passphrase,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                KeySize
            );
        }

        internal readonly record struct ContainerInfo(
            int Iterations,
            int CiphertextLength,
            byte[] Nonce
        )
        {
            public int TotalSize => MinContainerSize + CiphertextLength;
        }
    }
}

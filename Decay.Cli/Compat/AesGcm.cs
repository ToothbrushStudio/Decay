namespace System.Security.Cryptography
{
    /// <summary>
    /// AES-GCM for .NET Framework 4.8, which has no AesGcm type. Matches the
    /// surface of .NET Core's AesGcm that the engine calls, and the standard
    /// GCM construction (NIST SP 800-38D) with a 96-bit nonce, so containers
    /// sealed here and by .NET 10's AesGcm are interchangeable. The block
    /// cipher is the platform's AES; only counter mode and GHASH are done
    /// here, with no secret-dependent branches or table lookups.
    /// </summary>
    internal sealed class AesGcm : IDisposable
    {
        private const int BlockSize = 16;
        private const int NonceSize = 12;

        private readonly Aes _aes;
        private readonly ICryptoTransform _encryptor;
        private readonly ulong _hashKeyHigh;
        private readonly ulong _hashKeyLow;
        private bool _disposed;

        internal AesGcm(byte[] key, int tagSizeInBytes)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));

            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
            {
                throw new ArgumentException("The key must be 128, 192 or 256 bits.", nameof(key));
            }

            if (tagSizeInBytes < 12 || tagSizeInBytes > BlockSize)
            {
                throw new ArgumentOutOfRangeException(nameof(tagSizeInBytes));
            }

            TagSize = tagSizeInBytes;

            _aes = Aes.Create();
            _aes.Mode = CipherMode.ECB;
            _aes.Padding = PaddingMode.None;
            _aes.Key = key;
            _encryptor = _aes.CreateEncryptor();

            byte[] zero = new byte[BlockSize];
            byte[] hashKey = EncryptBlocks(zero);

            _hashKeyHigh = ReadUInt64(hashKey, 0);
            _hashKeyLow = ReadUInt64(hashKey, 8);
        }

        private int TagSize { get; }

        internal void Encrypt(
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> plaintext,
            Span<byte> ciphertext,
            Span<byte> tag,
            ReadOnlySpan<byte> associatedData = default
        )
        {
            Check(nonce, plaintext.Length, ciphertext.Length, tag.Length);

            byte[] data = plaintext.ToArray();

            ApplyKeystream(nonce, data);
            data.CopyTo(ciphertext);

            byte[] computed = ComputeTag(nonce, associatedData, data);

            computed.AsSpan(0, TagSize).CopyTo(tag);
        }

        internal void Decrypt(
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> ciphertext,
            ReadOnlySpan<byte> tag,
            Span<byte> plaintext,
            ReadOnlySpan<byte> associatedData = default
        )
        {
            Check(nonce, ciphertext.Length, plaintext.Length, tag.Length);

            byte[] data = ciphertext.ToArray();
            byte[] computed = ComputeTag(nonce, associatedData, data);

            if (!CryptographicOperations.FixedTimeEquals(computed.AsSpan(0, TagSize), tag))
            {
                throw new AuthenticationTagMismatchException();
            }

            ApplyKeystream(nonce, data);
            data.CopyTo(plaintext);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _encryptor.Dispose();
            _aes.Dispose();
        }

        private void Check(ReadOnlySpan<byte> nonce, int inputLength, int outputLength, int tagLength)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AesGcm));

            if (nonce.Length != NonceSize)
            {
                throw new ArgumentException("The nonce must be 12 bytes.", nameof(nonce));
            }

            if (inputLength != outputLength)
            {
                throw new ArgumentException("The input and output buffers must be the same length.");
            }

            if (tagLength != TagSize)
            {
                throw new ArgumentException("The tag buffer length must match the tag size.");
            }
        }

        // CTR keystream starting at counter value 2 (J0 + 1); J0 is nonce || 1.
        private void ApplyKeystream(ReadOnlySpan<byte> nonce, byte[] data)
        {
            if (data.Length == 0)
            {
                return;
            }

            int blocks = (data.Length + BlockSize - 1) / BlockSize;
            byte[] counters = new byte[blocks * BlockSize];

            for (int i = 0; i < blocks; i++)
            {
                nonce.CopyTo(counters.AsSpan(i * BlockSize, NonceSize));

                uint counter = (uint)i + 2;
                int at = i * BlockSize + NonceSize;

                counters[at] = (byte)(counter >> 24);
                counters[at + 1] = (byte)(counter >> 16);
                counters[at + 2] = (byte)(counter >> 8);
                counters[at + 3] = (byte)counter;
            }

            byte[] stream = EncryptBlocks(counters);

            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= stream[i];
            }
        }

        private byte[] ComputeTag(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> associatedData, byte[] ciphertext)
        {
            ulong yh = 0;
            ulong yl = 0;

            Absorb(associatedData, ref yh, ref yl);
            Absorb(ciphertext, ref yh, ref yl);

            // Length block: bit lengths of A and C, 64 bits each.
            yh ^= (ulong)associatedData.Length * 8;
            yl ^= (ulong)ciphertext.Length * 8;
            Multiply(ref yh, ref yl, _hashKeyHigh, _hashKeyLow);

            byte[] j0 = new byte[BlockSize];

            nonce.CopyTo(j0);
            j0[BlockSize - 1] = 1;

            byte[] mask = EncryptBlocks(j0);

            WriteUInt64(yh, mask, 0, xor: true);
            WriteUInt64(yl, mask, 8, xor: true);

            return mask;
        }

        private void Absorb(ReadOnlySpan<byte> data, ref ulong yh, ref ulong yl)
        {
            byte[] block = new byte[BlockSize];

            for (int offset = 0; offset < data.Length; offset += BlockSize)
            {
                int take = Math.Min(BlockSize, data.Length - offset);

                Array.Clear(block, 0, BlockSize);
                data.Slice(offset, take).CopyTo(block);

                yh ^= ReadUInt64(block, 0);
                yl ^= ReadUInt64(block, 8);
                Multiply(ref yh, ref yl, _hashKeyHigh, _hashKeyLow);
            }
        }

        // x = x * h in GF(2^128) with GCM's bit order, branch-free.
        private static void Multiply(ref ulong xh, ref ulong xl, ulong hh, ulong hl)
        {
            ulong zh = 0;
            ulong zl = 0;
            ulong vh = hh;
            ulong vl = hl;

            for (int i = 0; i < 128; i++)
            {
                ulong bit = i < 64 ? (xh >> (63 - i)) & 1 : (xl >> (127 - i)) & 1;
                ulong take = 0UL - bit;

                zh ^= vh & take;
                zl ^= vl & take;

                ulong carry = 0UL - (vl & 1);

                vl = (vl >> 1) | (vh << 63);
                vh = (vh >> 1) ^ (0xE100000000000000UL & carry);
            }

            xh = zh;
            xl = zl;
        }

        private byte[] EncryptBlocks(byte[] blocks)
        {
            byte[] output = new byte[blocks.Length];

            _encryptor.TransformBlock(blocks, 0, blocks.Length, output, 0);

            return output;
        }

        private static ulong ReadUInt64(byte[] buffer, int offset)
        {
            ulong value = 0;

            for (int i = 0; i < 8; i++)
            {
                value = (value << 8) | buffer[offset + i];
            }

            return value;
        }

        private static void WriteUInt64(ulong value, byte[] buffer, int offset, bool xor)
        {
            for (int i = 7; i >= 0; i--)
            {
                byte b = (byte)value;

                buffer[offset + i] = xor ? (byte)(buffer[offset + i] ^ b) : b;
                value >>= 8;
            }
        }
    }
}

namespace Decay.Renderer
{
    /// <summary>
    /// xoshiro256** — the pipeline's random stream. Implemented here rather
    /// than using System.Random because the op sequence must reproduce
    /// bit-for-bit on every platform and runtime version, forever (the
    /// decoder replays it to un-rot). FROZEN as part of pipeline v1 — see
    /// docs/render-pipeline-v1.md.
    /// </summary>
    internal sealed class Xoshiro256
    {
        private ulong _s0, _s1, _s2, _s3;

        /// <summary>Seeds from a 32-byte hash read as four little-endian u64s.</summary>
        internal Xoshiro256(ReadOnlySpan<byte> seed32)
        {
            // BinaryPrimitives, not BitConverter: the read must be
            // little-endian regardless of the machine's endianness.
            _s0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(seed32.Slice(0, 8));
            _s1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(seed32.Slice(8, 8));
            _s2 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(seed32.Slice(16, 8));
            _s3 = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(seed32.Slice(24, 8));

            if ((_s0 | _s1 | _s2 | _s3) == 0)
            {
                _s0 = 0x9E3779B97F4A7C15UL;
            }
        }

        /// <summary>Seeds from a single u64 via four rounds of splitmix64.</summary>
        internal Xoshiro256(ulong seed)
        {
            _s0 = SplitMix64(ref seed);
            _s1 = SplitMix64(ref seed);
            _s2 = SplitMix64(ref seed);
            _s3 = SplitMix64(ref seed);
        }

        internal ulong Next()
        {
            ulong result = ulong.RotateLeft(_s1 * 5, 7) * 9;
            ulong t = _s1 << 17;

            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = ulong.RotateLeft(_s3, 45);

            return result;
        }

        internal int Draw(int bound)
        {
            return (int)(Next() % (ulong)bound);
        }

        private static ulong SplitMix64(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;

            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;

            return z ^ (z >> 31);
        }
    }
}

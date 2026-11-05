namespace System.IO.Compression
{
    /// <summary>
    /// ZLibStream for .NET Framework 4.8, which has only DeflateStream. A
    /// zlib stream is a 2-byte header, a raw deflate body and a big-endian
    /// Adler-32 of the uncompressed data. Output differs byte-for-byte from
    /// .NET 10's compressor but any zlib reader (including .NET 10's) decodes
    /// it to the same bytes.
    /// </summary>
    internal sealed class ZLibStream : Stream
    {
        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private readonly bool _compress;
        private readonly DeflateStream _deflate;
        private uint _a = 1;
        private uint _b;
        private bool _disposed;

        internal ZLibStream(Stream stream, CompressionLevel level, bool leaveOpen)
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));

            _stream = stream;
            _leaveOpen = leaveOpen;
            _compress = true;

            // CMF = deflate with a 32K window; FLG chosen so the pair is a multiple of 31.
            _stream.WriteByte(0x78);
            _stream.WriteByte(level == CompressionLevel.Fastest ? (byte)0x01 : (byte)0xDA);
            _deflate = new DeflateStream(stream, level, leaveOpen: true);
        }

        internal ZLibStream(Stream stream, CompressionMode mode)
            : this(stream, mode, leaveOpen: false)
        {
        }

        internal ZLibStream(Stream stream, CompressionMode mode, bool leaveOpen)
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));

            _stream = stream;
            _leaveOpen = leaveOpen;
            _compress = mode == CompressionMode.Compress;

            if (_compress)
            {
                _stream.WriteByte(0x78);
                _stream.WriteByte(0xDA);
                _deflate = new DeflateStream(stream, CompressionLevel.Optimal, leaveOpen: true);

                return;
            }

            int cmf = stream.ReadByte();
            int flg = stream.ReadByte();

            if (cmf < 0 || flg < 0 || (cmf & 0x0F) != 8 || ((cmf << 8) | flg) % 31 != 0 || (flg & 0x20) != 0)
            {
                throw new InvalidDataException("The data is not a valid zlib stream.");
            }

            _deflate = new DeflateStream(stream, CompressionMode.Decompress, leaveOpen: true);
        }

        public override bool CanRead => !_compress;

        public override bool CanSeek => false;

        public override bool CanWrite => _compress;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_compress) throw new NotSupportedException();

            return _deflate.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!_compress) throw new NotSupportedException();

            for (int i = offset; i < offset + count; i++)
            {
                _a = (_a + buffer[i]) % 65521;
                _b = (_b + _a) % 65521;
            }

            _deflate.Write(buffer, offset, count);
        }

        public override void Flush()
        {
            _deflate.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _deflate.Dispose();

                if (_compress)
                {
                    uint adler = (_b << 16) | _a;

                    _stream.WriteByte((byte)(adler >> 24));
                    _stream.WriteByte((byte)(adler >> 16));
                    _stream.WriteByte((byte)(adler >> 8));
                    _stream.WriteByte((byte)adler);
                }

                if (!_leaveOpen)
                {
                    _stream.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}

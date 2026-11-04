namespace System.IO
{
    /// <summary>
    /// Stream.Write(ReadOnlySpan&lt;byte&gt;) does not exist on .NET Framework;
    /// this extension lets the engine's PNG writer compile unchanged.
    /// </summary>
    internal static class StreamSpanExtensions
    {
        internal static void Write(this Stream stream, ReadOnlySpan<byte> buffer)
        {
            byte[] copy = buffer.ToArray();

            stream.Write(copy, 0, copy.Length);
        }
    }
}

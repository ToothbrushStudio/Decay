namespace Decay.Core
{
    /// <summary>
    /// The image is not a Decay image, is corrupted, or declares values
    /// outside the format's validity rules.
    /// </summary>
    public class DecayFormatException : Exception
    {
        public DecayFormatException(string message) : base(message) { }
    }

    /// <summary>
    /// The image was written by a newer payload format version than this
    /// decoder understands. The message is intact — the user needs a newer
    /// copy of Decay, not a different passphrase.
    /// </summary>
    public sealed class DecayVersionException : DecayFormatException
    {
        public byte FormatVersion { get; }

        public DecayVersionException(byte formatVersion)
            : base(
                $"This image uses Decay payload format version {formatVersion}, " +
                "which this copy of Decay is too old to read. Update Decay to decrypt it."
            )
        {
            FormatVersion = formatVersion;
        }
    }

    /// <summary>
    /// The message does not fit in the carrier image.
    /// </summary>
    public sealed class DecayCapacityException : Exception
    {
        public DecayCapacityException(string message) : base(message) { }
    }

    /// <summary>
    /// Authentication failed: wrong passphrase or a modified image. AES-GCM
    /// cannot distinguish the two, so they are reported as one condition.
    /// </summary>
    public sealed class DecayAuthenticationException : Exception
    {
        public DecayAuthenticationException()
            : base("Wrong passphrase, or the image has been modified since it was created.")
        {
        }
    }
}

namespace Decay.Core
{
    /// <summary>
    /// The public face of Decay's encryption core, honoring the decode
    /// contract: image + passphrase is everything. No accounts, no server,
    /// no original carrier file, no version handshake.
    /// </summary>
    public static class DecayCipher
    {
        public const int DefaultIterations = 600_000;

        /// <summary>
        /// Encrypts the message and embeds it into the carrier's pixels in
        /// place. The carrier should already be fully rendered (glitch
        /// applied); the watermark, if any, must be applied afterwards and
        /// only to pixels at index >= the returned pixel extent.
        /// </summary>
        /// <returns>
        /// The number of pixels occupied by the payload — the watermark-safe
        /// boundary.
        /// </returns>
        public static int EncryptInto(
            string plainText,
            string passphrase,
            PixelBuffer carrier,
            int iterations = DefaultIterations
        )
        {
            ArgumentNullException.ThrowIfNull(plainText);
            ArgumentException.ThrowIfNullOrEmpty(passphrase);
            ArgumentNullException.ThrowIfNull(carrier);

            byte[] container = PayloadContainer.Seal(plainText, passphrase, iterations);

            PixelCodec.Embed(container, carrier);

            return PixelCodec.PixelExtent(container.Length);
        }

        /// <summary>
        /// Extracts and decrypts the message from a Decay image.
        /// </summary>
        /// <exception cref="DecayFormatException">Not a Decay image, or corrupted.</exception>
        /// <exception cref="DecayVersionException">Written by a newer format version.</exception>
        /// <exception cref="DecayAuthenticationException">Wrong passphrase or modified image.</exception>
        public static string Decrypt(PixelBuffer image, string passphrase)
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentException.ThrowIfNullOrEmpty(passphrase);

            if (PixelCodec.Capacity(image) < PayloadContainer.MinContainerSize)
            {
                throw new DecayFormatException(
                    "The image is too small to be a Decay image."
                );
            }

            byte[] header = PixelCodec.Extract(image, PayloadContainer.HeaderSize);
            PayloadContainer.ContainerInfo info = PayloadContainer.ReadHeader(header);
            byte[] container = PixelCodec.Extract(image, info.TotalSize);

            return PayloadContainer.Open(container, passphrase);
        }

        /// <summary>
        /// The largest message, in UTF-8 bytes, the carrier can hold. Useful
        /// for live UI feedback while the user types.
        /// </summary>
        public static int MaxPlainTextBytes(PixelBuffer carrier)
        {
            ArgumentNullException.ThrowIfNull(carrier);

            return Math.Max(0, PixelCodec.Capacity(carrier) - PayloadContainer.MinContainerSize);
        }
    }
}

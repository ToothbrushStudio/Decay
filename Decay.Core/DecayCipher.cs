namespace Decay.Core
{
    /// <summary>
    /// The public face of Decay's encryption core, honoring the decode
    /// contract: image + passphrase is everything. No accounts, no server,
    /// no original carrier file, no version handshake.
    ///
    /// Two levels of API:
    /// - EncryptInto / Decrypt: the simple path, no renderer involved.
    /// - SealContainer / EmbedContainer / ExtractContainer / OpenContainer:
    ///   the container-level path used by Decay.Renderer, which needs the
    ///   container bytes to drive the decay art before embedding and to
    ///   rebuild the un-rot sequence after extraction.
    /// </summary>
    public static class DecayCipher
    {
        public const int DefaultIterations = 600_000;

        /// <summary>No decay pipeline: the image carries data but no replayable art.</summary>
        public const byte PipelineNone = 0;

        /// <summary>
        /// Encrypts the message into a sealed container. The container is
        /// public data (its ciphertext is opaque without the passphrase) and
        /// is what drives the deterministic decay art.
        /// </summary>
        public static byte[] SealContainer(
            string plainText,
            string passphrase,
            byte pipelineId,
            byte pipelineParameter = 0,
            int iterations = DefaultIterations
        )
        {
            ArgumentNullException.ThrowIfNull(plainText);
            ArgumentException.ThrowIfNullOrEmpty(passphrase);

            return PayloadContainer.Seal(
                plainText,
                passphrase,
                pipelineId,
                pipelineParameter,
                iterations
            );
        }

        /// <summary>
        /// Embeds a sealed container into the image's pixels in place. Call
        /// after all visual rendering; only the watermark may follow, and
        /// only on pixels at index >= the returned pixel extent.
        /// </summary>
        /// <returns>
        /// The number of pixels occupied by the payload — the watermark-safe
        /// boundary.
        /// </returns>
        public static int EmbedContainer(byte[] container, PixelBuffer image)
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(image);

            PixelCodec.Embed(container, image);

            return PixelCodec.PixelExtent(container.Length);
        }

        /// <summary>
        /// Reads the sealed container back out of a Decay image. Requires no
        /// passphrase — the container is public data.
        /// </summary>
        /// <exception cref="DecayFormatException">Not a Decay image, or corrupted.</exception>
        /// <exception cref="DecayVersionException">Written by a newer format version.</exception>
        public static byte[] ExtractContainer(PixelBuffer image)
        {
            ArgumentNullException.ThrowIfNull(image);

            if (PixelCodec.Capacity(image) < PayloadContainer.MinContainerSize)
            {
                throw new DecayFormatException(
                    "The image is too small to be a Decay image."
                );
            }

            byte[] header = PixelCodec.Extract(image, PayloadContainer.HeaderSize);
            PayloadContainer.ContainerInfo info = PayloadContainer.ReadHeader(header);

            return PixelCodec.Extract(image, info.TotalSize);
        }

        /// <summary>
        /// Decrypts a sealed container extracted from an image.
        /// </summary>
        /// <exception cref="DecayAuthenticationException">Wrong passphrase or modified image.</exception>
        public static string OpenContainer(byte[] container, string passphrase)
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentException.ThrowIfNullOrEmpty(passphrase);

            return PayloadContainer.Open(container, passphrase);
        }

        /// <summary>
        /// The decay pipeline id a container declares. Unknown ids must be
        /// treated as "no animation available", never as a decrypt failure.
        /// </summary>
        public static byte GetPipelineId(byte[] container)
        {
            ArgumentNullException.ThrowIfNull(container);

            return PayloadContainer.ReadHeader(container).PipelineId;
        }

        /// <summary>
        /// The pipeline-defined parameter byte. For pipeline 1 this is the
        /// decay intensity in percent (0 means the default, 100).
        /// </summary>
        public static byte GetPipelineParameter(byte[] container)
        {
            ArgumentNullException.ThrowIfNull(container);

            return PayloadContainer.ReadHeader(container).PipelineParameter;
        }

        /// <summary>
        /// Simple path: encrypts the message and embeds it into the carrier's
        /// pixels in place, with no decay pipeline recorded.
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
            ArgumentNullException.ThrowIfNull(carrier);

            byte[] container = SealContainer(
                plainText,
                passphrase,
                PipelineNone,
                iterations: iterations
            );

            return EmbedContainer(container, carrier);
        }

        /// <summary>
        /// Simple path: extracts and decrypts the message from a Decay image.
        /// </summary>
        /// <exception cref="DecayFormatException">Not a Decay image, or corrupted.</exception>
        /// <exception cref="DecayVersionException">Written by a newer format version.</exception>
        /// <exception cref="DecayAuthenticationException">Wrong passphrase or modified image.</exception>
        public static string Decrypt(PixelBuffer image, string passphrase)
        {
            ArgumentException.ThrowIfNullOrEmpty(passphrase);

            return OpenContainer(ExtractContainer(image), passphrase);
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

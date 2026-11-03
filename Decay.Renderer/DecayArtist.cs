using Decay.Core;

namespace Decay.Renderer
{
    /// <summary>
    /// The result of encrypting a message into art: the exported image, the
    /// sequence that produced it (for the forward preview animation), and
    /// the watermark-safe pixel boundary. Exactly one of Sequence (Glitch
    /// style) and Rot (Rot style) is set.
    /// </summary>
    public sealed record EncryptedArt(
        PixelBuffer FinalImage,
        DecaySequence? Sequence,
        int PayloadPixelExtent,
        RotDecay? Rot = null
    );

    /// <summary>
    /// The result of decrypting a Decay image: the message, and — when the
    /// image was made with a pipeline this build knows — the sequence to
    /// play backwards as the reveal animation. A null Sequence is normal for
    /// data-only images and images from newer pipelines. Rot images carry
    /// their stain plan in Rot instead of a Sequence.
    /// </summary>
    public sealed record DecryptedArt(
        string PlainText,
        DecaySequence? Sequence,
        RotDecay? Rot = null
    );

    /// <summary>
    /// Orchestrates Decay.Core and the render pipeline into the two moves
    /// the product actually makes: message → rotted image, and rotted image
    /// + passphrase → message + reverse animation.
    /// </summary>
    public static class DecayArtist
    {
        /// <summary>
        /// Encrypts the message and rots the carrier with the resulting
        /// ciphertext-driven decay. Rendering order per the format spec:
        /// glitch, then embed; any watermark must come after, and only on
        /// pixels at index >= PayloadPixelExtent.
        /// </summary>
        /// <param name="intensity">
        /// Decay intensity in percent, 1–255. 100 is the standard ramp; lower
        /// leaves more of the carrier legible, higher approaches total
        /// obliteration. Recorded in the container (authenticated), so the
        /// receiver's un-rot replays it exactly.
        /// </param>
        public static EncryptedArt Encrypt(
            string plainText,
            string passphrase,
            PixelBuffer carrier,
            int iterations = DecayCipher.DefaultIterations,
            int intensity = 100
        )
        {
            if (carrier is null) throw new ArgumentNullException(nameof(carrier));
            if (intensity < 1) throw new ArgumentOutOfRangeException(nameof(intensity));
            if (intensity > 255) throw new ArgumentOutOfRangeException(nameof(intensity));

            byte[] container = DecayCipher.SealContainer(
                plainText,
                passphrase,
                DecaySequence.PipelineV1,
                (byte)intensity,
                iterations
            );
            DecaySequence sequence = DecaySequence.FromContainer(container)!;
            PixelBuffer final = sequence.RenderFinal(carrier);
            int extent = DecayCipher.EmbedContainer(container, final);

            return new EncryptedArt(final, sequence, extent);
        }

        /// <summary>
        /// Encrypts the message in the Rot style: the carrier keeps its look
        /// and is stained green, brown and purple by the characters of the
        /// message. Same container, same embed and watermark rules as Encrypt.
        /// </summary>
        /// <param name="intensity">
        /// Stain strength in percent, 1–255; 100 is the default. Recorded in
        /// the container, so the receiver restores with exactly the same plan.
        /// </param>
        public static EncryptedArt EncryptRot(
            string plainText,
            string passphrase,
            PixelBuffer carrier,
            int iterations = DecayCipher.DefaultIterations,
            int intensity = RotDecay.DefaultIntensity
        )
        {
            if (carrier is null) throw new ArgumentNullException(nameof(carrier));
            if (intensity < 1) throw new ArgumentOutOfRangeException(nameof(intensity));
            if (intensity > 255) throw new ArgumentOutOfRangeException(nameof(intensity));

            byte[] container = DecayCipher.SealContainer(
                plainText,
                passphrase,
                RotDecay.PipelineId,
                (byte)intensity,
                iterations
            );
            RotDecay rot = RotDecay.FromContainer(container, plainText, passphrase)!;
            PixelBuffer final = rot.Apply(carrier);
            int extent = DecayCipher.EmbedContainer(container, final);

            return new EncryptedArt(final, null, extent, rot);
        }

        /// <summary>
        /// Decrypts a Decay image and, when possible, rebuilds the decay
        /// sequence so the UI can un-rot the image as the message reveals
        /// (walk RevertFrame from frame 39 down to 0, or call Unrot for the
        /// end state directly).
        /// </summary>
        /// <exception cref="DecayFormatException">Not a Decay image, or corrupted.</exception>
        /// <exception cref="DecayVersionException">Written by a newer format version.</exception>
        /// <exception cref="DecayAuthenticationException">Wrong passphrase or modified image.</exception>
        public static DecryptedArt Decrypt(PixelBuffer image, string passphrase)
        {
            byte[] container = DecayCipher.ExtractContainer(image);
            string plainText = DecayCipher.OpenContainer(container, passphrase);

            return new DecryptedArt(
                plainText,
                DecaySequence.FromContainer(container),
                RotDecay.FromContainer(container, plainText, passphrase)
            );
        }
    }
}

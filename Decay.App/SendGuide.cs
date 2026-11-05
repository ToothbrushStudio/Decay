namespace Decay.App
{
    /// <summary>
    /// Words and checks that help people send and open fragile Decay images.
    /// Kept in one place so the guide page, the error alerts and the status
    /// lines all say the same thing.
    /// </summary>
    public static class SendGuide
    {
        /// <summary>
        /// Absolute Shell route of the Help tab, which holds the send guide
        /// (see AppShell).
        /// </summary>
        public const string Route = "//help";

        /// <summary>Support contact shown on the Help tab. Empty hides the section.</summary>
        public const string SupportEmail = "support@toothbrush.studio";

        public const string WebsiteUrl = "https://toothbrush.studio";

        /// <summary>The square sizes the Encrypt tab can produce.</summary>
        public static readonly int[] DecaySizes = { 256, 384, 512, 768, 1024 };

        private const string ResendAdvice =
            "Ask the sender to send the image again as a file — a cloud-drive link "
            + "(Google Drive, OneDrive, iCloud Drive), an email attachment, AirDrop, "
            + "or \"File\" / \"Document\" in a chat app — not as a photo.";

        /// <summary>
        /// What the Decrypt tab's open dialog offers: every common image type,
        /// not only PNG, so a copy that was converted along the way gets an
        /// explanation instead of being invisible in the dialog.
        /// </summary>
        public static readonly FilePickerFileType OpenableImages = new FilePickerFileType(
            new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = new[]
                {
                    ".png", ".jpg", ".jpeg", ".webp", ".heic", ".heif", ".avif", ".gif", ".bmp", ".tif", ".tiff",
                },
                [DevicePlatform.Android] = new[] { "image/*" },
                [DevicePlatform.iOS] = new[] { "public.image" },
                [DevicePlatform.MacCatalyst] = new[] { "public.image" },
            }
        );

        public static bool IsDecaySize(int width, int height) =>
            width == height && Array.IndexOf(DecaySizes, width) >= 0;

        /// <summary>
        /// The file picked is not a PNG Decay can read. <paramref name="detail"/>
        /// is the decoder's reason, shown for files that claim to be PNGs.
        /// </summary>
        public static string NotReadableMessage(string fileName, string? detail = null)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();

            string? kind = extension switch
            {
                ".jpg" or ".jpeg" => "a JPEG",
                ".webp" => "a WebP image",
                ".heic" or ".heif" => "a HEIC photo",
                ".avif" => "an AVIF image",
                ".gif" => "a GIF",
                ".bmp" => "a BMP image",
                ".tif" or ".tiff" => "a TIFF image",
                _ => null,
            };

            if (kind is null)
            {
                string reason = string.IsNullOrWhiteSpace(detail) ? "" : detail + "\n\n";

                return "Decay can't read this file.\n\n" + reason + "It may be damaged, or it "
                    + "may have been converted along the way.\n\n" + ResendAdvice;
            }

            return $"This file is {kind}. Decay images are PNGs, and a copy that has been "
                + "converted or re-compressed — by a chat app, a social site, or a "
                + "\"save as\" — no longer holds the message.\n\n" + ResendAdvice;
        }

        /// <summary>The PNG decoded but contains no readable Decay data.</summary>
        public static string NoMessageFound(int width, int height)
        {
            string resized = IsDecaySize(width, height)
                ? ""
                : $" It is {width}×{height}, which isn't a size Decay makes, so it was "
                    + "probably resized.";

            return "No Decay message was found in this image." + resized
                + "\n\nThe usual cause is that it passed through an app or site that "
                + "re-compressed, resized, or screenshotted it. That destroys the message "
                + "for good.\n\n" + ResendAdvice;
        }

        /// <summary>
        /// Authentication failed. The cipher can't tell a wrong passphrase
        /// from a changed image, so the message covers both.
        /// </summary>
        public static string CouldNotUnlock() =>
            "That passphrase didn't open the image.\n\n"
            + "First, check the spelling, capital letters, and spaces.\n\n"
            + "If you're sure it's right, the image itself may have been changed — "
            + "a chat app, social site, or screenshot can alter it without any visible "
            + "difference. " + ResendAdvice;
    }
}

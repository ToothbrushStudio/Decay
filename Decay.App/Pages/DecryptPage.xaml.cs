using Decay.Core;
using Decay.Renderer;

namespace Decay.App.Pages
{
    public partial class DecryptPage : ContentPage
    {
        private PixelBuffer? _received;
        private string _message = "";
        private byte[]? _recoveredImagePng;

        public DecryptPage()
        {
            InitializeComponent();
        }

        private async void OnOpenImage(object? sender, EventArgs e)
        {
            FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Open a Decay image",

                // Not just PNG: if someone picks a JPEG saved from a chat or
                // a site, we can explain what happened instead of hiding it.
                FileTypes = FilePickerFileType.Images,
            });

            if (file is null)
            {
                return;
            }

            try
            {
                await using Stream stream = await file.OpenReadAsync();

                _received = await Task.Run(() => DecayPng.Decode(stream));

                byte[] png = await Task.Run(() => DecayPng.Encode(_received));

                Preview.Source = ImageSource.FromStream(() => new MemoryStream(png));
                DecryptButton.IsEnabled = true;
                CopyButton.IsEnabled = false;
                SaveImageButton.IsEnabled = false;
                _recoveredImagePng = null;
                MessageLabel.Text = "";
                string hint = SendGuide.LoadedHint(_received.Width, _received.Height);

                StatusLabel.Text = $"Loaded {_received.Width}×{_received.Height}. "
                    + (hint.Length > 0
                        ? hint
                        : "If this file was screenshotted or re-compressed along the way, "
                            + "the message inside it is already gone.");
            }
            catch (InvalidDataException)
            {
                await ShowProblemAsync("Can't read this file", SendGuide.NotReadableMessage(file.FileName));
            }
        }

        /// <summary>
        /// Explains a failure and offers the "How to send" tips, which cover
        /// both what the sender should do and what to check on this end.
        /// </summary>
        private async Task ShowProblemAsync(string title, string message)
        {
            bool wantsTips = await DisplayAlertAsync(title, message, "Show tips", "OK");

            if (wantsTips)
            {
                await Shell.Current.GoToAsync(SendGuide.Route);
            }
        }

        private void OnTogglePassphrase(object? sender, EventArgs e)
        {
            PassphraseEntry.IsPassword = !PassphraseEntry.IsPassword;
            PassphraseToggle.Text = PassphraseEntry.IsPassword ? "Show" : "Hide";
        }

        private async void OnDecrypt(object? sender, EventArgs e)
        {
            if (_received is null)
            {
                return;
            }

            string passphrase = PassphraseEntry.Text ?? "";

            if (passphrase.Length == 0)
            {
                await DisplayAlertAsync("Passphrase", "A passphrase is required.", "OK");

                return;
            }

            PixelBuffer received = _received;

            DecryptButton.IsEnabled = false;
            StatusLabel.Text = "Deriving key…";

            try
            {
                DecryptedArt result = await Task.Run(
                    () => DecayArtist.Decrypt(received, passphrase)
                );

                _message = result.PlainText;
                CopyButton.IsEnabled = true;

                if (result.Rot is RotDecay rot)
                {
                    // Rot style: drain the stains. The restored image is the
                    // carrier to within a couple of levels per channel.
                    StatusLabel.Text = "Draining the rot…";

                    PixelBuffer restored = await Task.Run(() => rot.Restore(received));
                    FramePlayer fade = await FramePlayer.RenderCrossfadeAsync(received, restored);

                    _recoveredImagePng = fade.LastFramePng;
                    SaveImageButton.IsEnabled = true;

                    fade.Play(
                        Preview,
                        Dispatcher,
                        TimeSpan.FromMilliseconds(70),
                        onFrame: (index, total) =>
                            MessageLabel.Text = RevealSlice(_message, index, total),
                        onDone: () =>
                        {
                            MessageLabel.Text = _message;
                            StatusLabel.Text = "The rot drained from the image.";
                        }
                    );

                    return;
                }

                if (result.Sequence is null)
                {
                    // Data-only image, or a newer pipeline: no animation, and
                    // no separate "recovered" image beneath the rot — the
                    // received file is all there is.
                    MessageLabel.Text = _message;
                    StatusLabel.Text = "Decrypted.";
                    _recoveredImagePng = await Task.Run(() => DecayPng.Encode(received));
                    SaveImageButton.IsEnabled = true;

                    return;
                }

                StatusLabel.Text = "Rendering the reveal…";

                FramePlayer player = await FramePlayer.RenderReverseAsync(
                    result.Sequence,
                    received
                );

                // The recovered image is fully rendered as soon as the
                // sequence is built — saving needn't wait for playback.
                _recoveredImagePng = player.LastFramePng;
                SaveImageButton.IsEnabled = true;

                StatusLabel.Text = "Un-rotting…";
                player.Play(
                    Preview,
                    Dispatcher,
                    TimeSpan.FromMilliseconds(70),
                    onFrame: (index, total) =>
                        MessageLabel.Text = RevealSlice(_message, index, total),
                    onDone: () =>
                    {
                        MessageLabel.Text = _message;
                        StatusLabel.Text = "The image un-rotted to its origin.";
                    }
                );
            }
            catch (DecayAuthenticationException)
            {
                await ShowProblemAsync("Couldn't unlock it", SendGuide.CouldNotUnlock());
            }
            catch (DecayVersionException exception)
            {
                await DisplayAlertAsync("Update needed", exception.Message, "OK");
            }
            catch (DecayFormatException)
            {
                await ShowProblemAsync(
                    "No message found",
                    SendGuide.NoMessageFound(received.Width, received.Height)
                );
            }
            finally
            {
                DecryptButton.IsEnabled = true;
            }
        }

        private static string RevealSlice(string message, int index, int total)
        {
            if (total <= 0)
            {
                return message;
            }

            int length = (int)((long)message.Length * index / total);

            // Don't cut a surrogate pair in half mid-reveal.
            if (length > 0 && length < message.Length && char.IsHighSurrogate(message[length - 1]))
            {
                length--;
            }

            return message[..length];
        }

        private async void OnCopy(object? sender, EventArgs e)
        {
            await Clipboard.Default.SetTextAsync(_message);

            StatusLabel.Text = "Copied.";
        }

        private async void OnSaveImage(object? sender, EventArgs e)
        {
            if (_recoveredImagePng is null)
            {
                return;
            }

            try
            {
                string? path = await PlatformFileSaver.SaveAsync(
                    $"decay-recovered-{DateTime.Now:yyyyMMdd-HHmmss}.png",
                    _recoveredImagePng
                );

                if (path is not null)
                {
                    StatusLabel.Text = $"Saved to {path}.";
                }
            }
            catch (PlatformNotSupportedException exception)
            {
                await DisplayAlertAsync("Not available here", exception.Message, "OK");
            }
        }
    }
}

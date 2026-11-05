using Decay.Core;
using Decay.Renderer;

namespace Decay.App.Pages
{
    public partial class DecryptPage : ContentPage
    {
        private PixelBuffer? _received;
        private byte[]? _receivedPreview;
        private string _message = "";
        private PixelBuffer? _recovered;
        private FramePlayer? _player;

        // Bumped whenever another image is opened, so work that finishes late
        // for an older image can tell it is no longer wanted.
        private int _imageVersion;

        public DecryptPage()
        {
            InitializeComponent();
        }

        private async void OnOpenImage(object? sender, EventArgs e)
        {
            FileResult? file;

            try
            {
                file = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Open a Decay image",

                    // Not just PNG: if someone picks a JPEG saved from a chat or
                    // a site, we can explain what happened instead of hiding it.
                    FileTypes = SendGuide.OpenableImages,
                });
            }
            catch (Exception exception)
            {
                await DisplayAlertAsync("Can't open the file dialog", exception.Message, "OK");

                return;
            }

            if (file is null)
            {
                return;
            }

            // A new image: stop any reveal still playing and forget the old result.
            int version = ++_imageVersion;

            StopPlayback();
            ResetResult();
            _received = null;
            _receivedPreview = null;
            Preview.Source = null;
            DecryptButton.IsEnabled = false;
            StatusLabel.Text = "Opening…";

            PixelBuffer received;
            byte[] preview;
            DecayFormatException? noMessage;

            try
            {
                await using Stream stream = await file.OpenReadAsync();

                received = await Task.Run(() => DecayPng.Decode(stream));
                preview = await Task.Run(() => PreviewImage.Png(received));

                // The container header is public, so check for a message now
                // rather than after the passphrase has been typed.
                noMessage = await Task.Run(() => FindMessageProblem(received));
            }
            catch (InvalidDataException exception)
            {
                if (version == _imageVersion)
                {
                    StatusLabel.Text = "";
                    await ShowProblemAsync(
                        "Can't read this file",
                        SendGuide.NotReadableMessage(file.FileName, exception.Message)
                    );
                }

                return;
            }
            catch (Exception exception)
            {
                // The file couldn't be read at all: locked, offline in a cloud
                // folder, permissions, and so on.
                if (version == _imageVersion)
                {
                    StatusLabel.Text = "";
                    await DisplayAlertAsync("Can't open this file", exception.Message, "OK");
                }

                return;
            }

            if (version != _imageVersion)
            {
                return; // another image was opened in the meantime
            }

            _receivedPreview = preview;
            Preview.Source = PreviewImage.Source(preview);

            if (noMessage is DecayVersionException newer)
            {
                StatusLabel.Text = $"Loaded {received.Width}×{received.Height}, made by a newer Decay.";
                await DisplayAlertAsync("Update needed", newer.Message, "OK");

                return;
            }

            if (noMessage is not null)
            {
                StatusLabel.Text = $"Loaded {received.Width}×{received.Height}. No Decay message found.";
                await ShowProblemAsync(
                    "No message found",
                    SendGuide.NoMessageFound(received.Width, received.Height)
                );

                return;
            }

            _received = received;
            DecryptButton.IsEnabled = true;
            StatusLabel.Text = $"Loaded {received.Width}×{received.Height}. "
                + "Enter the passphrase and press Decrypt.";
        }

        /// <summary>Null when the image holds a Decay message; otherwise why it doesn't.</summary>
        private static DecayFormatException? FindMessageProblem(PixelBuffer image)
        {
            try
            {
                DecayCipher.ExtractContainer(image);

                return null;
            }
            catch (DecayFormatException exception)
            {
                return exception;
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
            int version = _imageVersion;

            StopPlayback();
            ResetResult();

            if (_receivedPreview is not null)
            {
                Preview.Source = PreviewImage.Source(_receivedPreview);
            }

            DecryptButton.IsEnabled = false;
            StatusLabel.Text = "Deriving key…";

            try
            {
                DecryptedArt result = await Task.Run(
                    () => DecayArtist.Decrypt(received, passphrase)
                );

                if (version != _imageVersion)
                {
                    return;
                }

                string message = result.PlainText;
                bool rot = result.Rot is not null;
                FramePlayer? player = null;

                if (result.Rot is RotDecay rotDecay)
                {
                    // Rot style: drain the stains. The restored image is the
                    // carrier to within a couple of levels per channel.
                    StatusLabel.Text = "Draining the rot…";

                    PixelBuffer restored = await Task.Run(() => rotDecay.Restore(received));

                    player = await FramePlayer.RenderCrossfadeAsync(received, restored);
                }
                else if (result.Sequence is DecaySequence sequence)
                {
                    StatusLabel.Text = "Rendering the reveal…";
                    player = await FramePlayer.RenderReverseAsync(sequence, received);
                }

                if (version != _imageVersion)
                {
                    return;
                }

                _message = message;
                CopyButton.IsEnabled = true;

                if (player is null)
                {
                    // Data-only image, or a newer pipeline: no animation, and
                    // no separate "recovered" image beneath the rot — the
                    // received file is all there is.
                    _recovered = received;
                    SaveImageButton.IsEnabled = true;
                    MessageLabel.Text = message;
                    StatusLabel.Text = "Decrypted.";

                    return;
                }

                // The recovered image is complete before playback starts, so
                // saving needn't wait for the animation.
                _recovered = player.FinalImage;
                SaveImageButton.IsEnabled = true;
                _player = player;
                StatusLabel.Text = rot ? "Draining the rot…" : "Un-rotting…";

                player.Play(
                    Preview,
                    Dispatcher,
                    TimeSpan.FromMilliseconds(70),
                    onFrame: (index, total) => MessageLabel.Text = RevealSlice(message, index, total),
                    onDone: () =>
                    {
                        MessageLabel.Text = message;
                        StatusLabel.Text = rot
                            ? "The rot drained from the image."
                            : "The image un-rotted to its origin.";
                    }
                );
            }
            catch (DecayAuthenticationException)
            {
                StatusLabel.Text = "";
                await ShowProblemAsync("Couldn't unlock it", SendGuide.CouldNotUnlock());
            }
            catch (DecayVersionException exception)
            {
                StatusLabel.Text = "";
                await DisplayAlertAsync("Update needed", exception.Message, "OK");
            }
            catch (DecayFormatException)
            {
                StatusLabel.Text = "";
                await ShowProblemAsync(
                    "No message found",
                    SendGuide.NoMessageFound(received.Width, received.Height)
                );
            }
            catch (Exception exception)
            {
                StatusLabel.Text = "";
                await DisplayAlertAsync("Something went wrong", exception.Message, "OK");
            }
            finally
            {
                if (version == _imageVersion)
                {
                    DecryptButton.IsEnabled = true;
                }
            }
        }

        private void StopPlayback()
        {
            _player?.Stop();
            _player = null;
        }

        private void ResetResult()
        {
            _message = "";
            _recovered = null;
            MessageLabel.Text = "";
            CopyButton.IsEnabled = false;
            SaveImageButton.IsEnabled = false;
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
            try
            {
                await Clipboard.Default.SetTextAsync(_message);
                StatusLabel.Text = "Copied.";
            }
            catch (Exception exception)
            {
                await DisplayAlertAsync("Couldn't copy", exception.Message, "OK");
            }
        }

        private async void OnSaveImage(object? sender, EventArgs e)
        {
            if (_recovered is null)
            {
                return;
            }

            PixelBuffer recovered = _recovered;

            try
            {
                byte[] png = await Task.Run(() => DecayPng.Encode(recovered));
                string? path = await PlatformFileSaver.SaveAsync(
                    $"decay-recovered-{DateTime.Now:yyyyMMdd-HHmmss}.png",
                    png
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
            catch (Exception exception)
            {
                await DisplayAlertAsync("Couldn't save the image", exception.Message, "OK");
            }
        }
    }
}

using Decay.Core;
using Decay.Renderer;

namespace Decay.App.Pages
{
    public partial class DecryptPage : ContentPage
    {
        private PixelBuffer? _received;
        private string _message = "";

        public DecryptPage()
        {
            InitializeComponent();
        }

        private async void OnOpenImage(object? sender, EventArgs e)
        {
            FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Open a Decay image",
                FileTypes = FilePickerFileType.Png,
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
                MessageLabel.Text = "";
                StatusLabel.Text = $"Loaded {_received.Width}×{_received.Height}. "
                    + "If this file was screenshotted or re-compressed along the way, "
                    + "the message inside it is already gone.";
            }
            catch (InvalidDataException exception)
            {
                await DisplayAlert("Can't read image", exception.Message, "OK");
            }
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
                await DisplayAlert("Passphrase", "A passphrase is required.", "OK");

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

                if (result.Sequence is null)
                {
                    // Data-only image, or a newer pipeline: no animation,
                    // just the message.
                    MessageLabel.Text = _message;
                    StatusLabel.Text = "Decrypted.";

                    return;
                }

                StatusLabel.Text = "Rendering the reveal…";

                FramePlayer player = await FramePlayer.RenderReverseAsync(
                    result.Sequence,
                    received
                );

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
                        StatusLabel.Text = "The image un-rotted to its origin. "
                            + "The message was the rot itself.";
                    }
                );
            }
            catch (DecayAuthenticationException exception)
            {
                await DisplayAlert("Locked", exception.Message, "OK");
            }
            catch (DecayVersionException exception)
            {
                await DisplayAlert("Update needed", exception.Message, "OK");
            }
            catch (DecayFormatException exception)
            {
                await DisplayAlert("Not a Decay image", exception.Message, "OK");
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
    }
}

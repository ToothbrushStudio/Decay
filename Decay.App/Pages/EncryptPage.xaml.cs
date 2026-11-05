using System.Text;
using Decay.Core;
using Decay.Renderer;

namespace Decay.App.Pages
{
    public partial class EncryptPage : ContentPage
    {
        private static readonly int[] Sizes = SendGuide.DecaySizes;

        private const int MaxCustomCarrierDimension = 2048;

        // The button beside the carrier dropdown: a new random carrier for a
        // preset, or a different image file for Custom Image.
        private const string NewCarrierText = "New carrier";
        private const string ChooseImageText = "Choose image… (PNG)";

        private int _carrierSeed = Random.Shared.Next();
        private PixelBuffer? _customCarrier;
        private byte[]? _carrierPreview;
        private byte[]? _customCarrierPreview;
        private EncryptedArt? _art;
        private FramePlayer? _player;

        // Bumped whenever anything that shapes the image changes, so a result
        // that finishes after such a change is recognized as stale.
        private int _inputsVersion;

        // The latest carrier regeneration; older ones that finish late are dropped.
        private int _carrierRequest;

        public EncryptPage()
        {
            InitializeComponent();

            StylePicker.ItemsSource = new List<string>
            {
                "Glitch — scramble the pixels",
                "Rot (Pro) — stain the colors (keeps the image recognizable)",
            };
            StylePicker.SelectedIndex = 0;
            PresetPicker.ItemsSource = CarrierPresets.All
                .Select(p => p.FreeTier ? p.Name : $"{p.Name} (Pro)")
                .Append("Custom Image (Pro)")
                .ToList();
            PresetPicker.SelectedIndex = 0;
            SizePicker.ItemsSource = Sizes.Select(s => $"{s} × {s}").ToList();
            SizePicker.SelectedIndex = 2;

            UpdateTierUi();
            RegenerateCarrier();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // A license may have been activated on the License tab.
#if DEBUG
            DevTierRow.IsVisible = AppState.License is null;
#else
            DevTierRow.IsVisible = false;
#endif
            EnforceTierSelections();
            UpdateTierUi();
        }

        private void SetResultButtons(bool ready)
        {
            ResultRow.IsVisible = ready;
            PlayButton.IsEnabled = ready;
            SaveImageButton.IsEnabled = ready;
            ShareButton.IsEnabled = ready;
        }

        private void OnToggleAdvanced(object? sender, EventArgs e)
        {
            AdvancedSection.IsVisible = !AdvancedSection.IsVisible;
            AdvancedToggle.Text = AdvancedSection.IsVisible ? "Advanced ▾" : "Advanced ▸";
        }

        private bool IsCustomSelected =>
            PresetPicker.SelectedIndex == CarrierPresets.All.Count;

        private CarrierPreset SelectedPreset =>
            CarrierPresets.All[
                Math.Clamp(PresetPicker.SelectedIndex, 0, CarrierPresets.All.Count - 1)
            ];

        private static int DefaultPresetIndex =>
            CarrierPresets.All.ToList().IndexOf(CarrierPresets.Default);

        private DecayStyle SelectedStyle =>
            StylePicker.SelectedIndex == 1 ? DecayStyle.Rot : DecayStyle.Glitch;

        private int SelectedSize => Sizes[Math.Max(0, SizePicker.SelectedIndex)];

        private void OnProToggled(object? sender, ToggledEventArgs e)
        {
            AppState.DevTier = e.Value ? DecayTier.Pro : DecayTier.Free;

            EnforceTierSelections();
            UpdateTierUi();
        }

        private void EnforceTierSelections()
        {
            if (AppState.Tier == DecayTier.Free)
            {
                IntensitySlider.Value = TierPolicy.LockedIntensity;

                if (StylePicker.SelectedIndex != 0)
                {
                    StylePicker.SelectedIndex = 0;
                }

                if (IsCustomSelected || !SelectedPreset.FreeTier)
                {
                    // Triggers OnPresetChanged, which regenerates the
                    // carrier and relabels the button beside the dropdown.
                    PresetPicker.SelectedIndex = DefaultPresetIndex;
                }

                _customCarrier = null;
                _customCarrierPreview = null;
            }
        }

        private void UpdateTierUi()
        {
            IntensitySlider.IsEnabled = AppState.Tier == DecayTier.Pro;

            // Free images keep the watermark's corner clear, so they hold less.
            UpdateCapacityLabel();
        }

        /// <summary>
        /// Something that shapes the image changed (message, passphrase,
        /// style, carrier, size or intensity): the finished image no longer
        /// matches the screen, so drop it rather than let a stale letter be
        /// saved or shared.
        /// </summary>
        private void InvalidateResult()
        {
            _inputsVersion++;
            StopPlayback();

            if (_art is null)
            {
                return;
            }

            _art = null;
            SetResultButtons(false);
            ShowCarrierPreview();
            StatusLabel.Text = "Changed since you encrypted. Press Encrypt to make a new image.";
        }

        private void StopPlayback()
        {
            _player?.Stop();
            _player = null;
        }

        private void ShowCarrierPreview()
        {
            byte[]? png = IsCustomSelected ? _customCarrierPreview : _carrierPreview;

            Preview.Source = png is null ? null : PreviewImage.Source(png);
        }

        private async void OnPresetChanged(object? sender, EventArgs e)
        {
            InvalidateResult();

            if (IsCustomSelected)
            {
                if (!TierPolicy.AllowsCustomCarrier(AppState.Tier))
                {
                    await DisplayAlertAsync(
                        "Decay Pro",
                        "Using your own image as the carrier requires Decay Pro.",
                        "OK"
                    );
                    PresetPicker.SelectedIndex = DefaultPresetIndex;

                    return;
                }

                NewCarrierButton.Text = ChooseImageText;
                SizePicker.IsEnabled = false;
                ShowCarrierPreview();
                UpdateCapacityLabel();

                // First selection opens the dialog directly; after that the
                // button beside the dropdown picks a different image.
                if (_customCarrier is null)
                {
                    await PickCustomCarrierAsync();
                }

                return;
            }

            NewCarrierButton.Text = NewCarrierText;
            SizePicker.IsEnabled = true;
            _customCarrier = null;
            _customCarrierPreview = null;

            if (!TierPolicy.AllowsPreset(AppState.Tier, SelectedPreset))
            {
                await DisplayAlertAsync(
                    "Decay Pro",
                    $"The \"{SelectedPreset.Name}\" carrier requires Decay Pro.",
                    "OK"
                );
                PresetPicker.SelectedIndex = DefaultPresetIndex;

                return;
            }

            RegenerateCarrier();
        }

        private async void OnStyleChanged(object? sender, EventArgs e)
        {
            InvalidateResult();

            if (!TierPolicy.AllowsStyle(AppState.Tier, SelectedStyle))
            {
                await DisplayAlertAsync(
                    "Decay Pro",
                    "Rot mode requires Decay Pro.",
                    "OK"
                );
                StylePicker.SelectedIndex = 0;
            }
        }

        private void OnSizeChanged(object? sender, EventArgs e)
        {
            RegenerateCarrier();
            UpdateCapacityLabel();
        }

        private async void OnNewCarrier(object? sender, EventArgs e)
        {
            if (IsCustomSelected)
            {
                await PickCustomCarrierAsync();

                return;
            }

            _carrierSeed = Random.Shared.Next();
            RegenerateCarrier();
        }

        private void OnTogglePassphrase(object? sender, EventArgs e)
        {
            bool hide = !PassphraseEntry.IsPassword;

            PassphraseEntry.IsPassword = hide;
            RepeatPassphraseEntry.IsPassword = hide;
            PassphraseToggle.Text = hide ? "Show" : "Hide";
        }

        private void OnPassphraseChanged(object? sender, TextChangedEventArgs e)
        {
            InvalidateResult();
        }

        private async Task PickCustomCarrierAsync()
        {
            FileResult? file;

            try
            {
                file = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Choose a carrier image (PNG)",
                    FileTypes = FilePickerFileType.Png,
                });
            }
            catch (Exception exception)
            {
                await DisplayAlertAsync("Can't open the file dialog", exception.Message, "OK");

                return;
            }

            if (file is null)
            {
                if (_customCarrier is null)
                {
                    StatusLabel.Text = "Choose an image to use as the carrier.";
                }

                return;
            }

            try
            {
                byte[] bytes;

                await using (Stream stream = await file.OpenReadAsync())
                {
                    bytes = await Task.Run(() => DecayPng.ReadBytes(stream));
                }

                // Turn away an oversized image before paying to decode it.
                (int width, int height) = DecayPng.ReadSize(bytes);

                if (width > MaxCustomCarrierDimension || height > MaxCustomCarrierDimension)
                {
                    await DisplayAlertAsync(
                        "Too large",
                        $"Carriers are limited to {MaxCustomCarrierDimension}×"
                            + $"{MaxCustomCarrierDimension} for now — this one is "
                            + $"{width}×{height}. Resize it and re-save as PNG.",
                        "OK"
                    );

                    return;
                }

                PixelBuffer carrier = await Task.Run(() => DecayPng.Decode(bytes));
                byte[] preview = await Task.Run(() => PreviewImage.Png(carrier));

                if (!IsCustomSelected)
                {
                    return; // the user moved on to a preset while this loaded
                }

                InvalidateResult();
                _customCarrier = carrier;
                _customCarrierPreview = preview;
                ShowCarrierPreview();
                StatusLabel.Text = $"Custom carrier: {file.FileName}, "
                    + $"{carrier.Width}×{carrier.Height}.";
                UpdateCapacityLabel();
            }
            catch (InvalidDataException exception)
            {
                await DisplayAlertAsync("Can't read image", exception.Message, "OK");
            }
            catch (Exception exception)
            {
                await DisplayAlertAsync("Can't open this file", exception.Message, "OK");
            }
        }

        private void OnIntensityChanged(object? sender, ValueChangedEventArgs e)
        {
            IntensityValueLabel.Text = ((int)e.NewValue).ToString();
            InvalidateResult();
        }

        private void OnMessageChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateCapacityLabel();
            InvalidateResult();
        }

        private async void RegenerateCarrier()
        {
            if (IsCustomSelected)
            {
                return;
            }

            CarrierPreset preset = SelectedPreset;
            int size = SelectedSize;
            int seed = _carrierSeed;
            int request = ++_carrierRequest;

            InvalidateResult();

            try
            {
                byte[] preview = await Task.Run(
                    () => PreviewImage.Png(preset.Generate(size, size, seed))
                );

                if (request != _carrierRequest || IsCustomSelected)
                {
                    return; // a newer carrier was asked for in the meantime
                }

                _carrierPreview = preview;
                ShowCarrierPreview();
                StatusLabel.Text = $"Carrier: {preset.Name}, {size}×{size}.";
                UpdateCapacityLabel();
            }
            catch (Exception exception)
            {
                await DisplayAlertAsync("Couldn't make the carrier", exception.Message, "OK");
            }
        }

        private void UpdateCapacityLabel()
        {
            int width;
            int height;

            if (IsCustomSelected)
            {
                if (_customCarrier is null)
                {
                    CapacityLabel.Text = "";

                    return;
                }

                width = _customCarrier.Width;
                height = _customCarrier.Height;
            }
            else
            {
                width = height = SelectedSize;
            }

            int used = Encoding.UTF8.GetByteCount(MessageEditor.Text ?? "");
            int max = DecayProduct.MaxMessageBytes(AppState.Tier, width, height);

            CapacityLabel.Text = $"{used:N0} / {max:N0} bytes";
            CapacityLabel.TextColor = used > max
                ? (Color)Application.Current!.Resources["Warn"]
                : (Color)Application.Current!.Resources["InkDim"];
        }

        private async void OnEncrypt(object? sender, EventArgs e)
        {
            string message = MessageEditor.Text ?? "";
            string passphrase = PassphraseEntry.Text ?? "";

            if (passphrase.Length == 0)
            {
                await DisplayAlertAsync("Passphrase", "A passphrase is required.", "OK");

                return;
            }

            if ((RepeatPassphraseEntry.Text ?? "") != passphrase)
            {
                await DisplayAlertAsync(
                    "Passphrases don't match",
                    "Type the same passphrase in both boxes. A typo here would lock the "
                        + "message away for good, even from you.",
                    "OK"
                );

                return;
            }

            if (IsCustomSelected && _customCarrier is null)
            {
                await DisplayAlertAsync(
                    "No carrier image",
                    "Choose an image to use as the carrier first.",
                    "OK"
                );

                return;
            }

            CarrierPreset preset = SelectedPreset;
            int size = SelectedSize;
            int seed = _carrierSeed;
            int intensity = (int)IntensitySlider.Value;
            DecayStyle style = SelectedStyle;
            DecayTier tier = AppState.Tier;
            PixelBuffer? customCarrier = IsCustomSelected ? _customCarrier : null;

            // Drop any earlier image first, so a failure here can't leave it
            // on screen looking like the result of this attempt.
            InvalidateResult();

            int version = _inputsVersion;

            EncryptButton.IsEnabled = false;
            StatusLabel.Text = "Deriving key and rotting the carrier…";

            try
            {
                EncryptedArt art = customCarrier is not null
                    ? await Task.Run(() => DecayProduct.EncryptWithCustomCarrier(
                        message,
                        passphrase,
                        tier,
                        customCarrier,
                        intensity,
                        style: style
                    ))
                    : await Task.Run(() => DecayProduct.EncryptWithPreset(
                        message,
                        passphrase,
                        tier,
                        preset,
                        size,
                        size,
                        seed,
                        intensity,
                        style: style
                    ));

                byte[] preview = await Task.Run(() => PreviewImage.Png(art.FinalImage));

                if (version != _inputsVersion)
                {
                    // Something changed while this ran, so the image doesn't
                    // match the screen any more.
                    StatusLabel.Text = "Changed while encrypting. Press Encrypt again.";

                    return;
                }

                _art = art;
                Preview.Source = PreviewImage.Source(preview);
                SetResultButtons(true);
                StatusLabel.Text = "Encrypted. The image now IS the message — save or share the file itself.";
            }
            catch (DecayTierException exception)
            {
                StatusLabel.Text = "";
                await DisplayAlertAsync("Decay Pro", exception.Message, "OK");
            }
            catch (DecayCapacityException exception)
            {
                StatusLabel.Text = "";
                await DisplayAlertAsync("Too large", exception.Message, "OK");
            }
            catch (ArgumentException exception)
            {
                StatusLabel.Text = "";
                await DisplayAlertAsync("Invalid input", exception.Message, "OK");
            }
            catch (Exception exception)
            {
                StatusLabel.Text = "";
                await DisplayAlertAsync("Something went wrong", exception.Message, "OK");
            }
            finally
            {
                EncryptButton.IsEnabled = true;
            }
        }

        private async void OnPlayDecay(object? sender, EventArgs e)
        {
            if (_art is null)
            {
                return;
            }

            EncryptedArt art = _art;
            int version = _inputsVersion;

            StopPlayback();
            PlayButton.IsEnabled = false;
            StatusLabel.Text = "Rendering decay frames…";

            try
            {
                FramePlayer player;

                if (art.Rot is RotDecay rot)
                {
                    // Rot style: fade from the clean carrier into the stained image.
                    PixelBuffer clean = await Task.Run(() => rot.Restore(art.FinalImage));

                    player = await FramePlayer.RenderCrossfadeAsync(clean, art.FinalImage);
                }
                else
                {
                    // Walk the exported image back to its start state, then play
                    // the rot forward — the exact bijection replay the receiver
                    // gets in reverse.
                    DecaySequence sequence = art.Sequence!;
                    PixelBuffer start = await Task.Run(() => sequence.Unrot(art.FinalImage));

                    player = await FramePlayer.RenderForwardAsync(sequence, start);
                }

                if (version != _inputsVersion || !ReferenceEquals(_art, art))
                {
                    return; // the result was replaced or dropped meanwhile
                }

                _player = player;
                StatusLabel.Text = "The rot, frame by frame.";
                player.Play(
                    Preview,
                    Dispatcher,
                    TimeSpan.FromMilliseconds(70),
                    onDone: () =>
                    {
                        PlayButton.IsEnabled = true;
                        StatusLabel.Text = "Final frame — this is the exported image.";
                    }
                );
            }
            catch (Exception exception)
            {
                PlayButton.IsEnabled = _art is not null;
                await DisplayAlertAsync("Couldn't play the decay", exception.Message, "OK");
            }
        }

        private async void OnSaveImage(object? sender, EventArgs e)
        {
            if (_art is null)
            {
                return;
            }

            EncryptedArt art = _art;

            try
            {
                byte[] png = await Task.Run(() => DecayPng.Encode(art.FinalImage));
                string? path = await PlatformFileSaver.SaveAsync(
                    $"decay-{DateTime.Now:yyyyMMdd-HHmmss}.png",
                    png
                );

                if (path is not null)
                {
                    StatusLabel.Text = $"Saved to {path}. "
                        + "Send that file itself — never a screenshot.";
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

        private async void OnHowToSend(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(SendGuide.Route);
        }

        private async void OnShare(object? sender, EventArgs e)
        {
            if (_art is null)
            {
                return;
            }

            EncryptedArt art = _art;

            try
            {
                byte[] png = await Task.Run(() => DecayPng.Encode(art.FinalImage));
                string path = Path.Combine(
                    FileSystem.CacheDirectory,
                    $"decay-{DateTime.Now:yyyyMMdd-HHmmss}.png"
                );

                await File.WriteAllBytesAsync(path, png);
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Send the file itself — never a screenshot",
                    File = new ShareFile(path),
                });
            }
            catch (Exception exception)
            {
                await DisplayAlertAsync("Couldn't share the image", exception.Message, "OK");
            }
        }
    }
}

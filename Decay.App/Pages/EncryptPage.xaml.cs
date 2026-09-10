using System.Text;
using Decay.Core;
using Decay.Renderer;

namespace Decay.App.Pages
{
    public partial class EncryptPage : ContentPage
    {
        private static readonly int[] Sizes = { 256, 384, 512, 768, 1024 };

        private const int MaxCustomCarrierDimension = 2048;
        private const int CustomCarrierDefaultIntensity = 50;

        private int _carrierSeed = Random.Shared.Next();
        private PixelBuffer? _carrier;
        private PixelBuffer? _customCarrier;
        private EncryptedArt? _art;

        private PixelBuffer? ActiveCarrier => _customCarrier ?? _carrier;

        public EncryptPage()
        {
            InitializeComponent();

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

        private bool IsCustomSelected =>
            PresetPicker.SelectedIndex == CarrierPresets.All.Count;

        private CarrierPreset SelectedPreset =>
            CarrierPresets.All[
                Math.Clamp(PresetPicker.SelectedIndex, 0, CarrierPresets.All.Count - 1)
            ];

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
                _customCarrier = null;

                if (IsCustomSelected || !SelectedPreset.FreeTier)
                {
                    // Triggers OnPresetChanged, which regenerates and hides
                    // the choose-image button.
                    PresetPicker.SelectedIndex = CarrierPresets.All
                        .ToList()
                        .IndexOf(CarrierPresets.Default);
                }
            }
        }

        private void UpdateTierUi()
        {
            IntensitySlider.IsEnabled = AppState.Tier == DecayTier.Pro;
        }

        private async void OnPresetChanged(object? sender, EventArgs e)
        {
            if (IsCustomSelected)
            {
                if (!TierPolicy.AllowsCustomCarrier(AppState.Tier))
                {
                    await DisplayAlertAsync(
                        "Decay Pro",
                        "Using your own image as the carrier requires Decay Pro.",
                        "OK"
                    );
                    PresetPicker.SelectedIndex = CarrierPresets.All
                        .ToList()
                        .IndexOf(CarrierPresets.Default);

                    return;
                }

                ChooseImageButton.IsVisible = true;
                SizePicker.IsEnabled = false;

                // First selection opens the dialog directly; the button
                // stays as the retry/change-image affordance.
                if (_customCarrier is null)
                {
                    // A personal photo should still look like itself. Gentler
                    // than the preset default (100), which is fine for
                    // procedural noise but drowns a recognizable image.
                    IntensitySlider.Value = CustomCarrierDefaultIntensity;

                    await PickCustomCarrierAsync();
                }

                return;
            }

            ChooseImageButton.IsVisible = false;
            SizePicker.IsEnabled = true;
            _customCarrier = null;

            if (!TierPolicy.AllowsPreset(AppState.Tier, SelectedPreset))
            {
                await DisplayAlertAsync(
                    "Decay Pro",
                    $"The \"{SelectedPreset.Name}\" carrier requires Decay Pro.",
                    "OK"
                );
                PresetPicker.SelectedIndex = CarrierPresets.All
                    .ToList()
                    .IndexOf(CarrierPresets.Default);

                return;
            }

            RegenerateCarrier();
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
            PassphraseEntry.IsPassword = !PassphraseEntry.IsPassword;
            PassphraseToggle.Text = PassphraseEntry.IsPassword ? "Show" : "Hide";
        }

        private async void OnChooseImage(object? sender, EventArgs e)
        {
            await PickCustomCarrierAsync();
        }

        private async Task PickCustomCarrierAsync()
        {
            FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose a carrier image (PNG)",
                FileTypes = FilePickerFileType.Png,
            });

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
                await using Stream stream = await file.OpenReadAsync();

                PixelBuffer carrier = await Task.Run(() => DecayPng.Decode(stream));

                if (carrier.Width > MaxCustomCarrierDimension
                    || carrier.Height > MaxCustomCarrierDimension)
                {
                    await DisplayAlertAsync(
                        "Too large",
                        $"Carriers are limited to {MaxCustomCarrierDimension}×"
                            + $"{MaxCustomCarrierDimension} for now — this one is "
                            + $"{carrier.Width}×{carrier.Height}. Resize it and re-save "
                            + "as PNG.",
                        "OK"
                    );

                    return;
                }

                _customCarrier = carrier;
                _art = null;
                PlayButton.IsEnabled = false;
                ShareButton.IsEnabled = false;

                byte[] png = await Task.Run(() => DecayPng.Encode(carrier));

                Preview.Source = ImageSource.FromStream(() => new MemoryStream(png));
                StatusLabel.Text = $"Custom carrier: {file.FileName}, "
                    + $"{carrier.Width}×{carrier.Height}.";
                UpdateCapacityLabel();
            }
            catch (InvalidDataException exception)
            {
                await DisplayAlertAsync("Can't read image", exception.Message, "OK");
            }
        }

        private void OnIntensityChanged(object? sender, ValueChangedEventArgs e)
        {
            IntensityValueLabel.Text = ((int)e.NewValue).ToString();
        }

        private void OnMessageChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateCapacityLabel();
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

            _art = null;
            _customCarrier = null;
            PlayButton.IsEnabled = false;
            ShareButton.IsEnabled = false;

            _carrier = await Task.Run(() => preset.Generate(size, size, seed));

            byte[] png = await Task.Run(() => DecayPng.Encode(_carrier));

            Preview.Source = ImageSource.FromStream(() => new MemoryStream(png));
            StatusLabel.Text = $"Carrier: {preset.Name}, {size}×{size}.";
            UpdateCapacityLabel();
        }

        private void UpdateCapacityLabel()
        {
            if (ActiveCarrier is null)
            {
                return;
            }

            int used = Encoding.UTF8.GetByteCount(MessageEditor.Text ?? "");
            int max = DecayCipher.MaxPlainTextBytes(ActiveCarrier);

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

            CarrierPreset preset = SelectedPreset;
            int size = SelectedSize;
            int seed = _carrierSeed;
            int intensity = (int)IntensitySlider.Value;

            EncryptButton.IsEnabled = false;
            StatusLabel.Text = "Deriving key and rotting the carrier…";

            try
            {
                if (IsCustomSelected && _customCarrier is null)
                {
                    await DisplayAlertAsync(
                        "No carrier image",
                        "Choose an image to use as the carrier first.",
                        "OK"
                    );

                    return;
                }

                PixelBuffer? customCarrier = IsCustomSelected ? _customCarrier : null;

                _art = customCarrier is not null
                    ? await Task.Run(() => DecayProduct.EncryptWithCustomCarrier(
                        message,
                        passphrase,
                        AppState.Tier,
                        customCarrier,
                        intensity
                    ))
                    : await Task.Run(() => DecayProduct.EncryptWithPreset(
                        message,
                        passphrase,
                        AppState.Tier,
                        preset,
                        size,
                        size,
                        seed,
                        intensity
                    ));

                byte[] png = await Task.Run(() => DecayPng.Encode(_art.FinalImage));

                Preview.Source = ImageSource.FromStream(() => new MemoryStream(png));
                PlayButton.IsEnabled = true;
                ShareButton.IsEnabled = true;
                StatusLabel.Text = "Encrypted. The image now IS the message — share the file itself.";
            }
            catch (DecayTierException exception)
            {
                await DisplayAlertAsync("Decay Pro", exception.Message, "OK");
            }
            catch (DecayCapacityException exception)
            {
                await DisplayAlertAsync("Too large", exception.Message, "OK");
            }
            catch (ArgumentException exception)
            {
                await DisplayAlertAsync("Invalid input", exception.Message, "OK");
            }
            finally
            {
                EncryptButton.IsEnabled = true;

                if (_art is null)
                {
                    StatusLabel.Text = "";
                }
            }
        }

        private async void OnPlayDecay(object? sender, EventArgs e)
        {
            if (_art is null)
            {
                return;
            }

            PlayButton.IsEnabled = false;
            StatusLabel.Text = "Rendering decay frames…";

            // Walk the exported image back to its start state, then play the
            // rot forward — the exact bijection replay the receiver gets in
            // reverse.
            EncryptedArt art = _art;
            PixelBuffer start = await Task.Run(() => art.Sequence.Unrot(art.FinalImage));
            FramePlayer player = await FramePlayer.RenderForwardAsync(art.Sequence, start);

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

        private async void OnShare(object? sender, EventArgs e)
        {
            if (_art is null)
            {
                return;
            }

            byte[] png = await Task.Run(() => DecayPng.Encode(_art.FinalImage));
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
    }
}

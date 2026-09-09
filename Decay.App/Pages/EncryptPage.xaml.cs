using System.Text;
using Decay.Core;
using Decay.Renderer;

namespace Decay.App.Pages
{
    public partial class EncryptPage : ContentPage
    {
        private static readonly int[] Sizes = { 256, 384, 512, 768, 1024 };

        private int _carrierSeed = Random.Shared.Next();
        private PixelBuffer? _carrier;
        private EncryptedArt? _art;

        public EncryptPage()
        {
            InitializeComponent();

            PresetPicker.ItemsSource = CarrierPresets.All
                .Select(p => p.FreeTier ? p.Name : $"{p.Name} (Pro)")
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

        private CarrierPreset SelectedPreset =>
            CarrierPresets.All[Math.Max(0, PresetPicker.SelectedIndex)];

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

                if (!SelectedPreset.FreeTier)
                {
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

        private void OnNewCarrier(object? sender, EventArgs e)
        {
            _carrierSeed = Random.Shared.Next();
            RegenerateCarrier();
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
            CarrierPreset preset = SelectedPreset;
            int size = SelectedSize;
            int seed = _carrierSeed;

            _art = null;
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
            if (_carrier is null)
            {
                return;
            }

            int used = Encoding.UTF8.GetByteCount(MessageEditor.Text ?? "");
            int max = DecayCipher.MaxPlainTextBytes(_carrier);

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
                _art = await Task.Run(() => DecayProduct.EncryptWithPreset(
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

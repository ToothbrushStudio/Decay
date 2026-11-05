using Decay.Licensing;
using Decay.Renderer;

namespace Decay.App.Pages
{
    public partial class LicensePage : ContentPage
    {
        public LicensePage()
        {
            InitializeComponent();
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (AppState.License is { } license)
            {
                StatusLabel.Text =
                    $"Licensed to {license.Licensee} ({license.Tier}), issued {license.Issued:yyyy-MM-dd}.";
                DeactivateButton.IsEnabled = true;
            }
            else
            {
                StatusLabel.Text = PublisherKey.SubjectPublicKeyInfoBase64.Length == 0
                    ? "This build has no publisher key configured — license keys cannot "
                        + "activate. (Run tools/Decay.KeyGen and fill PublisherKey.cs.)"
                    : "Free tier. Paste a license key to unlock Pro.";
                DeactivateButton.IsEnabled = false;
            }
        }

        private async void OnActivate(object? sender, EventArgs e)
        {
            if (AppState.TryActivate(KeyEditor.Text ?? ""))
            {
                DecayLicense license = AppState.License!;

                KeyEditor.Text = "";
                RefreshStatus();
                await DisplayAlertAsync(
                    "Activated",
                    license.Tier == DecayTier.Pro
                        ? $"Welcome to Decay Pro, {license.Licensee}."
                        : $"License activated for {license.Licensee} ({license.Tier} tier).",
                    "OK"
                );
            }
            else
            {
                await DisplayAlertAsync(
                    "Invalid key",
                    "That key didn't validate. Check for missing characters — or that "
                        + "this is the right key for this app.",
                    "OK"
                );
            }
        }

        private void OnDeactivate(object? sender, EventArgs e)
        {
            AppState.Deactivate();
            RefreshStatus();
        }
    }
}

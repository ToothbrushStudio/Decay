namespace Decay.App.Pages
{
    public partial class HowToSendPage : ContentPage
    {
        public HowToSendPage()
        {
            InitializeComponent();

            VersionLabel.Text = $"Decay {AppInfo.Current.VersionString}";

            if (SendGuide.SupportEmail.Length > 0)
            {
                SupportLabel.Text = $"Questions or problems? Email {SendGuide.SupportEmail}.";
                SupportSection.IsVisible = true;
            }
        }

        private async void OnWebsite(object? sender, EventArgs e)
        {
            await Launcher.Default.OpenAsync(SendGuide.WebsiteUrl);
        }
    }
}

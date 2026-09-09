namespace Decay.App
{
    public partial class App : Application
    {
        public App()
        {
            AppState.Initialize();
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            Window window = new Window(new AppShell());

            // Desktop: open tall enough that the Encrypt page needs no
            // scrolling. Mobile platforms ignore window sizing.
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacCatalyst())
            {
                window.Width = 640;
                window.Height = 1040;
                window.MinimumWidth = 480;
                window.MinimumHeight = 720;
            }

            return window;
        }
    }
}

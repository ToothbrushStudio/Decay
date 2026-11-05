namespace Decay.App
{
    public partial class App : Application
    {
        // Tall enough that the Encrypt tab needs no scrolling.
        private const double PreferredWidth = 640;
        private const double PreferredHeight = 1040;

        // When the screen size is unknown: fits a 1366×768 laptop.
        private const double FallbackHeight = 700;

        public App()
        {
            AppState.Initialize();
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            Window window = new Window(new AppShell());

            // Desktop only; mobile platforms ignore window sizing. Never open
            // taller than the screen: a 1080p laptop at 150% scaling has only
            // about 690 units of height above the taskbar. The pages scroll,
            // so a shorter window loses nothing.
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacCatalyst())
            {
                DesktopWorkArea.Area? area = DesktopWorkArea.Get();
                double height = area is { } screen
                    ? Math.Floor(Math.Min(PreferredHeight, screen.Height * 0.92))
                    : FallbackHeight;
                double width = area is { } wide
                    ? Math.Floor(Math.Min(PreferredWidth, wide.Width * 0.92))
                    : PreferredWidth;

                window.Width = width;
                window.Height = height;
                window.MinimumWidth = Math.Min(480, width);
                window.MinimumHeight = Math.Min(560, height);

                if (area is { } work)
                {
                    window.X = Math.Floor(work.Left + (work.Width - width) / 2);
                    window.Y = Math.Floor(work.Top + (work.Height - height) / 2);
                }
            }

            return window;
        }
    }
}

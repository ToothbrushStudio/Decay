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
            return new Window(new AppShell());
        }
    }
}

using Microsoft.Maui.Hosting;

namespace Decay.App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            MauiAppBuilder builder = MauiApp.CreateBuilder();

            builder.UseMauiApp<App>();

            return builder.Build();
        }
    }
}

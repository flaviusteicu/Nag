using Avalonia;
using System;
using Velopack;

namespace Nag
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Velopack MUST run before anything else.
            // Handles install/uninstall/update hooks and exits if needed.
            VelopackApp.Build().Run();

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}

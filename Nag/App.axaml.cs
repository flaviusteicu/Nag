using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Nag.Services;
using Nag.Interfaces;
using Nag.TrayIcon;
using Nag.Views;
using Nag.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace Nag
{
    public partial class App : Application
    {
        private Mutex? _mutex;
        public IServiceProvider ServiceProvider { get; private set; } = null!;
        private TrayIconManager? _trayIconManager;
        private SettingsWindow? _settingsWindow;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);
                ServiceProvider = serviceCollection.BuildServiceProvider();

                // Single Instance Mutex
                _mutex = new Mutex(true, Core.Constants.MutexName, out bool createdNew);
                if (!createdNew)
                {
                    Console.WriteLine("Nag is already running.");
                    desktop.Shutdown();
                    return;
                }

                _desktop = desktop;
                desktop.Exit += OnExit;

                // First-run detection: Categories/ not existing means fresh install
                var categoriesDir = Path.Combine(AppContext.BaseDirectory, "Categories");
                bool isFirstRun = !Directory.Exists(categoriesDir);

                if (isFirstRun)
                {
                    var welcomeWindow = new WelcomeWindow();
                    welcomeWindow.Closed += (_, _) => CompleteInitialization(welcomeWindow.DidImport);
                    welcomeWindow.Show();
                }
                else
                {
                    CompleteInitialization(didImport: false);
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private IClassicDesktopStyleApplicationLifetime? _desktop;

        private void CompleteInitialization(bool didImport)
        {
            if (_desktop == null) return;

            var settingsService = ServiceProvider.GetRequiredService<ISettingsService>();
            var messageService = ServiceProvider.GetRequiredService<IMessageService>();
            var categoryService = ServiceProvider.GetRequiredService<ICategoryService>();

            if (!didImport)
            {
                // Either not first run (idempotent) or user skipped import
                categoryService.EnsureCategoriesScaffold();
            }

            categoryService.SyncCategories();

            var scheduler = ServiceProvider.GetRequiredService<INotificationScheduler>();
            var positioningService = ServiceProvider.GetRequiredService<IPositioningService>();

            // Surface corrupted configuration files to the user
            if (settingsService.LoadCorrupted || messageService.LoadCorrupted)
            {
                var warningMsg = "A configuration file was corrupted and has been reset to defaults. Check nag.log for details.";
                var popup = new NotificationPopup("system", "Warning", warningMsg, 10, positioningService);
                popup.Show();
            }

            _trayIconManager = new TrayIconManager();
            _trayIconManager.Initialize(this, settingsService, messageService, categoryService, scheduler, positioningService, () => ShowSettingsWindow(scheduler), () => _desktop.Shutdown());

            scheduler.Start();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IMessageService, MessageService>();
            services.AddSingleton<ICategoryService, CategoryService>();
            services.AddSingleton<INotificationScheduler, NotificationScheduler>();
            services.AddSingleton<IPositioningService, Nag.Helpers.PositioningService>();

            services.AddTransient<SettingsViewModel>();
            services.AddTransient<WeightSettingsViewModel>();

            services.AddTransient<SettingsWindow>();
            services.AddTransient<WeightSettingsWindow>();
        }

        private void ShowSettingsWindow(INotificationScheduler scheduler)
        {
            if (_settingsWindow == null || !_settingsWindow.IsVisible)
            {
                _settingsWindow = ServiceProvider.GetRequiredService<SettingsWindow>();
                _settingsWindow.OnSettingsSaved += (s, ev) => scheduler.ForceRecalculate();
                _settingsWindow.Show();
                ForceActiveFocus(_settingsWindow);
            }
            else
            {
                if (_settingsWindow.WindowState == Avalonia.Controls.WindowState.Minimized)
                    _settingsWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                ForceActiveFocus(_settingsWindow);
            }
        }

        private void ForceActiveFocus(Avalonia.Controls.Window window)
        {
            var isTopmostStr = window.Topmost;
            window.Topmost = true;
            window.Topmost = isTopmostStr;
            window.Activate();
        }

        private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            ServiceProvider.GetService<INotificationScheduler>()?.Stop();
            _trayIconManager?.Dispose();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
    }
}

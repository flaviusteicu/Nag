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

            if (!didImport)
            {
                // Either not first run (idempotent) or user skipped import
                settingsService.EnsureCategoriesScaffold();
            }

            settingsService.SyncCategories();

            var scheduler = ServiceProvider.GetRequiredService<INotificationScheduler>();
            var positioningService = ServiceProvider.GetRequiredService<IPositioningService>();

            _trayIconManager = new TrayIconManager();
            InitializeTrayIcon(_desktop, settingsService, scheduler, positioningService);

            scheduler.Start();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<INotificationScheduler, NotificationScheduler>();
            services.AddSingleton<IPositioningService, Nag.Helpers.PositioningService>();
            
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<WeightSettingsViewModel>();
            
            services.AddTransient<SettingsWindow>();
            services.AddTransient<WeightSettingsWindow>();
        }

        private void InitializeTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, ISettingsService settingsService, INotificationScheduler scheduler, IPositioningService positioningService)
        {
            if (_trayIconManager == null) return;
            
            bool isPaused = settingsService.Settings.IsPaused;
            bool isStartup = StartupService.IsStartupEnabled();

            _trayIconManager.Initialize(this, settingsService, scheduler, positioningService, () => ShowSettingsWindow(scheduler), () => desktop.Shutdown());
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

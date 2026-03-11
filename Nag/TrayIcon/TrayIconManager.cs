using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Nag.Models;
using Nag.Services;
using Nag.Interfaces;
using System;
using System.Collections.Generic;
using Nag.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Nag.TrayIcon
{
    public class TrayIconManager : IDisposable
    {
        private Avalonia.Controls.TrayIcon _trayIcon;
        private NativeMenu _contextMenu;

        private ISettingsService _settingsService = null!;
        private IMessageService _messageService = null!;
        private ICategoryService _categoryService = null!;
        private INotificationScheduler _scheduler = null!;
        private Action _showSettingsCallback = null!;
        private Action _exitCallback = null!;

        public TrayIconManager()
        {
            _trayIcon = new Avalonia.Controls.TrayIcon();
            _contextMenu = new NativeMenu();
            _trayIcon.Menu = _contextMenu;
            _trayIcon.IsVisible = true;
            _trayIcon.Clicked += (s, e) => _showSettingsCallback?.Invoke();
        }

        public void Initialize(Application app, ISettingsService settingsService, IMessageService messageService, ICategoryService categoryService, INotificationScheduler scheduler, IPositioningService positioningService, Action showSettingsCallback, Action exitCallback)
        {
            _settingsService = settingsService;
            _messageService = messageService;
            _categoryService = categoryService;
            _scheduler = scheduler;
            _showSettingsCallback = showSettingsCallback;
            _exitCallback = exitCallback;

            try
            {
                _trayIcon.Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://Nag/app_icon.ico")));
            }
            catch { }

            // Wire up the scheduler's trigger event to actually show notification popups
            scheduler.OnTriggerNotification += (s, e) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var msg = _messageService.GetRandomMessage();
                    if (msg != null)
                    {
                        var pos = ((App)Application.Current!).ServiceProvider.GetRequiredService<IPositioningService>();
                        var popup = new NotificationPopup(msg.Value.CategoryId, msg.Value.CategoryName, msg.Value.Message, _settingsService.Settings.NotificationDurationSeconds, pos);
                        popup.Show();
                    }
                });
            };

            RebuildMenu();
        }

        public void RebuildMenu()
        {
            _contextMenu.Items.Clear();
            bool isPaused = _settingsService.Settings.IsPaused;

            _trayIcon.ToolTipText = "Nag" + (isPaused ? " (Paused)" : "");

            var fireNow = new NativeMenuItem("Fire Now");
            fireNow.Click += (s, e) => _scheduler.ForceRecalculate();
            fireNow.Click += (s, e) => {
                var pos = ((App)Application.Current!).ServiceProvider.GetRequiredService<IPositioningService>();
                var msg = _messageService.GetRandomMessage();
                if (msg != null)
                {
                    var popup = new NotificationPopup(msg.Value.CategoryId, msg.Value.CategoryName, msg.Value.Message, _settingsService.Settings.NotificationDurationSeconds, pos);
                    popup.Show();
                }
            };
            _contextMenu.Items.Add(fireNow);

            var pauseResume = new NativeMenuItem(isPaused ? "Resume" : "Pause");
            pauseResume.Click += (s, e) => TogglePause();
            _contextMenu.Items.Add(pauseResume);

            _contextMenu.Items.Add(new NativeMenuItemSeparator());

            var categoriesItem = new NativeMenuItem("Categories");
            var categoriesMenu = new NativeMenu();
            categoriesItem.Menu = categoriesMenu;
            _contextMenu.Items.Add(categoriesItem);

            foreach (var category in _messageService.Messages.Categories)
            {
                var catItem = new NativeMenuItem(category.Name);
                catItem.ToggleType = NativeMenuItemToggleType.CheckBox;
                catItem.IsChecked = category.Enabled;
                catItem.Click += (s, e) => ToggleCategory(category);
                categoriesMenu.Items.Add(catItem);
            }

            _contextMenu.Items.Add(new NativeMenuItemSeparator());

            var settings = new NativeMenuItem("Settings");
            settings.Click += (s, e) => _showSettingsCallback();
            _contextMenu.Items.Add(settings);

            var reload = new NativeMenuItem("Reload Messages");
            reload.Click += (s, e) =>
            {
                var result = _categoryService.SyncCategories();
                _messageService.LoadMessages();
                RebuildMenu();

                // Show feedback as a notification popup
                var pos = ((App)Application.Current!).ServiceProvider.GetRequiredService<IPositioningService>();
                var popup = new NotificationPopup("system", "Categories Synced", result, 8, pos);
                popup.Show();
            };
            _contextMenu.Items.Add(reload);

            _contextMenu.Items.Add(new NativeMenuItemSeparator());

            var exit = new NativeMenuItem("Exit");
            exit.Click += (s, e) => _exitCallback();
            _contextMenu.Items.Add(exit);
        }

        private void TogglePause()
        {
            _settingsService.Settings.IsPaused = !_settingsService.Settings.IsPaused;
            _settingsService.SaveSettings();

            if (_settingsService.Settings.IsPaused)
                _scheduler.Stop();
            else
            {
                _scheduler.ForceRecalculate();
                _scheduler.Start();
            }
            RebuildMenu();
        }

        private void ToggleCategory(MessageCategory category)
        {
            category.Enabled = !category.Enabled;
            _messageService.SaveMessages();
            RebuildMenu();
        }

        public void Dispose()
        {
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
        }
    }
}

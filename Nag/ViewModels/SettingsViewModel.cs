using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Nag.Core;
using Nag.Models;
using Nag.Services;
using Nag.Interfaces;

namespace Nag.ViewModels
{
    /// <summary>
    /// Serves as the primary DataContext for the SettingsWindow. 
    /// Manages the translation of `AppSettings` into bindable UI properties, handles frequency math, 
    /// and invokes commands for saving or launching the preview window without relying on UI Code-Behind.
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly INotificationScheduler _scheduler;
        private readonly IServiceProvider _serviceProvider;
        private readonly bool _initialStartupState;

        public Action? RequestClose { get; set; }
        public Action? RequestPreview { get; set; }
        public Action? RequestOpenWeights { get; set; }

        public AppSettings Settings => _settingsService.Settings;

        // Custom Frequency Validation Logic Bindings
        public bool IsFreqLight
        {
            get => Settings.Frequency == "Light";
            set { if (value) { Settings.Frequency = "Light"; OnPropertyChanged(nameof(IsFreqCustom)); } }
        }
        
        public bool IsFreqModerate
        {
            get => Settings.Frequency == "Moderate";
            set { if (value) { Settings.Frequency = "Moderate"; OnPropertyChanged(nameof(IsFreqCustom)); } }
        }
        
        public bool IsFreqIntensive
        {
            get => Settings.Frequency == "Intensive";
            set { if (value) { Settings.Frequency = "Intensive"; OnPropertyChanged(nameof(IsFreqCustom)); } }
        }

        public bool IsFreqCustom
        {
            get => !IsFreqLight && !IsFreqModerate && !IsFreqIntensive;
            set { if (value && !IsFreqCustom) { Settings.Frequency = $"{CustomFreqMin}-{CustomFreqMax}"; OnPropertyChanged(); } }
        }

        public List<int> CustomFreqOptions { get; } = Enumerable.Range(1, 99).ToList();

        private int _customFreqMin = 1;
        public int CustomFreqMin
        {
            get => _customFreqMin;
            set
            {
                if (SetProperty(ref _customFreqMin, value))
                {
                    if (_customFreqMax < _customFreqMin) CustomFreqMax = _customFreqMin;
                    if (IsFreqCustom) Settings.Frequency = $"{_customFreqMin}-{_customFreqMax}";
                }
            }
        }

        private int _customFreqMax = 2;
        public int CustomFreqMax
        {
            get => _customFreqMax;
            set
            {
                if (SetProperty(ref _customFreqMax, value))
                {
                    if (_customFreqMin > _customFreqMax) CustomFreqMin = _customFreqMax;
                    if (IsFreqCustom) Settings.Frequency = $"{_customFreqMin}-{_customFreqMax}";
                }
            }
        }

        /// <summary> Gets or sets the 24-hour format string defining when the day begins. </summary>
        public string ActiveHoursStart
        {
            get => Settings.ActiveHoursStart;
            set { Settings.ActiveHoursStart = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActiveHoursStartTooltip)); }
        }

        public TimeSpan ActiveHoursStartTimeSpan
        {
            get => TimeSpan.TryParse(Settings.ActiveHoursStart, out var ts) ? ts : new TimeSpan(8, 0, 0);
            set { ActiveHoursStart = value.ToString(@"hh\:mm"); OnPropertyChanged(); }
        }

        public string ActiveHoursStartTooltip 
        {
            get 
            {
                if (TimeSpan.TryParse(Settings.ActiveHoursStart, out var ts))
                    return $"Start time ( {DateTime.Today.Add(ts):h:mm tt} )";
                return "Start time";
            }
        }

        /// <summary> Gets or sets the 24-hour format string defining when the day ends. </summary>
        public string ActiveHoursEnd
        {
            get => Settings.ActiveHoursEnd;
            set { Settings.ActiveHoursEnd = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActiveHoursEndTooltip)); }
        }

        public TimeSpan ActiveHoursEndTimeSpan
        {
            get => TimeSpan.TryParse(Settings.ActiveHoursEnd, out var ts) ? ts : new TimeSpan(22, 0, 0);
            set { ActiveHoursEnd = value.ToString(@"hh\:mm"); OnPropertyChanged(); }
        }

        public string ActiveHoursEndTooltip 
        {
            get 
            {
                if (TimeSpan.TryParse(Settings.ActiveHoursEnd, out var ts))
                    return $"End time ( {DateTime.Today.Add(ts):h:mm tt} )";
                return "End time";
            }
        }

        /// <summary> Gets or sets how long the popup slider remains visible before retreating automatically. </summary>
        public int NotificationDurationSeconds
        {
            get => Settings.NotificationDurationSeconds;
            set { Settings.NotificationDurationSeconds = value; OnPropertyChanged(); }
        }

        /// <summary> Controls whether the application registers itself to run on Windows user login. </summary>
        private bool _startWithWindows;
        public bool StartWithWindows
        {
            get => _startWithWindows;
            set => SetProperty(ref _startWithWindows, value);
        }

        /// <summary> Returns true only on Windows, where auto-start via Registry is supported. </summary>
        public bool IsStartupSupported => System.OperatingSystem.IsWindows();

        /// <summary> Reads the app version from the VERSION file, or returns "dev" if unavailable. </summary>
        public string VersionText
        {
            get
            {
                var path = Path.Combine(AppContext.BaseDirectory, "VERSION");
                if (File.Exists(path))
                {
                    var ver = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(ver)) return $"v{ver}";
                }
                return "dev";
            }
        }

        /// <summary> Command to commit state changes to disk and rebuild the notification schedule dynamically. </summary>
        public ICommand SaveCommand { get; }
        
        /// <summary> Command to invoke a dummy test notification sliding onto the screen. </summary>
        public ICommand PreviewCommand { get; }
        
        /// <summary> Command to launch the sub-window for dynamically weighting category appearances. </summary>
        public ICommand AdjustWeightsCommand { get; }

        /// <summary> Command to import a custom pack from a folder. </summary>
        public ICommand ImportPackCommand { get; }
        
        /// <summary> Command to launch the Windows Explorer to the physical root directory of the application. </summary>
        public ICommand OpenFolderCommand { get; }

        public Action? RequestImportPack { get; set; }

        public event EventHandler? OnSettingsSaved;

        public SettingsViewModel(ISettingsService settingsService, INotificationScheduler scheduler, IServiceProvider serviceProvider)
        {
            _settingsService = settingsService;
            _scheduler = scheduler;
            _serviceProvider = serviceProvider;

            if (IsFreqCustom)
            {
                var parts = Settings.Frequency.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0], out int min) && int.TryParse(parts[1], out int max))
                {
                    _customFreqMin = min;
                    _customFreqMax = max;
                }
            }

            _initialStartupState = StartupService.IsStartupEnabled();
            _startWithWindows = _initialStartupState;

            SaveCommand = new RelayCommand(_ => SaveSettings());
            PreviewCommand = new RelayCommand(_ => RequestPreview?.Invoke());
            AdjustWeightsCommand = new RelayCommand(_ => RequestOpenWeights?.Invoke());
            ImportPackCommand = new RelayCommand(_ => RequestImportPack?.Invoke());
            OpenFolderCommand = new RelayCommand(_ => OpenFolder());
        }

        private void SaveSettings()
        {
            if (_startWithWindows != _initialStartupState)
            {
                StartupService.SetStartup(_startWithWindows);
            }

            _settingsService.SaveSettings();
            _scheduler.ForceRecalculate();
            
            OnSettingsSaved?.Invoke(this, EventArgs.Empty);
            RequestClose?.Invoke();
        }

        private void OpenFolder()
        {
            try
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = AppContext.BaseDirectory,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch { }
        }
    }
}

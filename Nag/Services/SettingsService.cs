using System;
using System.IO;
using System.Text.Json;
using Nag.Core;
using Nag.Models;
using Nag.Interfaces;

namespace Nag.Services
{
    /// <summary>
    /// Manages the reading and writing of settings.json.
    /// Gracefully handles missing or corrupted files by falling back to defaults
    /// and logging the error to nag.log so users know what happened.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly string _settingsPath = Path.Combine(AppContext.BaseDirectory, Constants.SettingsFileName);

        public AppSettings Settings { get; private set; } = new();
        public bool LoadCorrupted { get; private set; }

        public SettingsService()
        {
            LoadSettings();
        }

        public void LoadSettings()
        {
            if (File.Exists(_settingsPath))
            {
                try
                {
                    var json = File.ReadAllText(_settingsPath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    Settings = JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
                }
                catch (Exception ex)
                {
                    NagLogger.Error("LoadSettings", ex);
                    LoadCorrupted = true;
                    Settings = new AppSettings();
                }
            }
            else
            {
                SaveSettings();
            }
        }

        public void SaveSettings()
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
    }
}

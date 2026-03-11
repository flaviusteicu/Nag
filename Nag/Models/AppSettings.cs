using System;

namespace Nag.Models
{
    /// <summary>
    /// Represents the user's localized physical preferences mapped to `settings.json`.
    /// </summary>
    public class AppSettings
    {
        public string Frequency { get; set; } = "Moderate"; // Light, Moderate, Intensive
        public string ActiveHoursStart { get; set; } = "08:00";
        public string ActiveHoursEnd { get; set; } = "22:00";
        public bool StartWithWindows { get; set; } = false;
        public int NotificationDurationSeconds { get; set; } = 15;
        public bool IsPaused { get; set; } = false;
    }
}

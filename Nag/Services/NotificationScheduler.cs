using System;
using System.Collections.Generic;
using System.Linq;
using Nag.Models;
using Nag.Interfaces;

namespace Nag.Services
{
    /// <summary>
    /// The heart of the Nag disruption engine.
    /// Uses Stratified Randomization to generate unpredictable yet evenly-distributed 
    /// notification timings throughout the user's defined active hours.
    /// </summary>
    public class NotificationScheduler : INotificationScheduler
    {
        private readonly ISettingsService _settingsService;
        // The background ticker that evaluates if a scheduled time has physically arrived
        private System.Timers.Timer? _timer;
        private List<DateTime> _scheduledTimes = new();
        
        public event EventHandler? OnTriggerNotification;
        public event EventHandler<DateTime>? OnNextScheduledTimeCalculated;

        public NotificationScheduler(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _timer = new System.Timers.Timer(60000); // Check every minute
            _timer.Elapsed += (s, e) => CheckSchedule();
        }

        public void Start()
        {
            CalculateSchedule();
            _timer?.Start();
        }

        public void Stop()
        {
            _timer?.Stop();
        }

        public void ForceRecalculate()
        {
            CalculateSchedule();
        }

        /// <summary>
        /// Core Stratification Algorithm.
        /// Slices the active day into equal blocks based on total requested notifications, 
        /// then rolls an RNG dice inside each block to pick a completely unpredictable trigger minute.
        /// Automatically recalculates maximum possible iterations to prevent mathematical impossible intervals.
        /// </summary>
        private void CalculateSchedule()
        {
            _scheduledTimes.Clear();
            var settings = _settingsService.Settings;
            
            if (settings.IsPaused)
            {
                OnNextScheduledTimeCalculated?.Invoke(this, DateTime.MaxValue);
                return;
            }

            if (!TimeSpan.TryParse(settings.ActiveHoursStart, out var start) || 
                !TimeSpan.TryParse(settings.ActiveHoursEnd, out var end))
            {
                start = TimeSpan.FromHours(8);
                end = TimeSpan.FromHours(22);
            }

            var now = DateTime.Now;
            var today = now.Date;
            
            var activeStart = today.Add(start);
            var activeEnd = today.Add(end);
            
            if (activeEnd <= activeStart)
                activeEnd = activeEnd.AddDays(1);

            if (now >= activeEnd)
            {
                activeStart = activeStart.AddDays(1);
                activeEnd = activeEnd.AddDays(1);
            }
            
            var generationStart = now > activeStart ? now : activeStart;
            var windowMinutes = (activeEnd - generationStart).TotalMinutes;
            
            if (windowMinutes <= 0) 
            {
                OnNextScheduledTimeCalculated?.Invoke(this, DateTime.MaxValue);
                return;
            }

            int count;
            var parts = settings.Frequency.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out int minCount) && int.TryParse(parts[1], out int maxCount))
            {
                count = new Random().Next(minCount, maxCount + 1);
            }
            else if (int.TryParse(settings.Frequency, out int exactCount))
            {
                count = exactCount;
            }
            else
            {
                count = settings.Frequency switch
                {
                    "Light" => new Random().Next(1, 3), 
                    "Intensive" => new Random().Next(5, 11), 
                    _ => new Random().Next(3, 6) 
                };
            }

            // If the user launches the app late, we do NOT scale down the requested count anymore.
            // We force all requested notifications into the remaining `windowMinutes`.
            int scaledCount = count;
            
            // In case of extreme custom settings where even 1 minute gaps aren't enough,
            // cap the absolute maximum to 1 per minute.
            int maxPossibleAbsolute = (int)Math.Floor(windowMinutes);
            scaledCount = Math.Min(scaledCount, maxPossibleAbsolute);
            
            if (scaledCount <= 0)
            {
                OnNextScheduledTimeCalculated?.Invoke(this, DateTime.MaxValue);
                return;
            }

            var intervalMinutes = windowMinutes / scaledCount;
            var random = new Random();
            var lastTime = generationStart;

            // Determine the dynamic minimum gap.
            // By default, we aim for a traditional 60-minute gap to prevent bursts.
            // However, if the user requests too many notifications for the active hour window, 
            // we dynamically compress the gap to fit them all evenly.
            int targetGap = 60;
            var theoreticalMaxAtTargetGap = windowMinutes / targetGap;
            if (scaledCount > theoreticalMaxAtTargetGap)
            {
                // Compress the gap dynamically. e.g., 17 notifications across 8 hours = ~28 minute minimum gaps.
                targetGap = (int)Math.Floor(windowMinutes / (scaledCount + 1));
                if (targetGap < 1) targetGap = 1; // Absolute minimum 1 minute gap
            }

            for (int i = 0; i < scaledCount; i++)
            {
                // Define the start and end of this specific "strata" chunk of the day
                var windowMin = generationStart.AddMinutes(i * intervalMinutes);
                var windowMax = generationStart.AddMinutes((i + 1) * intervalMinutes);
                
                // Enforce the dynamic minimum gap condition from the PREVIOUS block's randomly generated time
                if (i > 0)
                {
                    windowMin = new DateTime(Math.Max(windowMin.Ticks, lastTime.AddMinutes(targetGap).Ticks));
                }
                
                if (windowMin >= windowMax)
                {
                     var t = windowMin;
                     if (t < activeEnd)
                     {
                         _scheduledTimes.Add(t);
                         lastTime = t;
                     }
                     continue;
                }

                // Draw an RNG offset inside the valid bounds of this strata chunk
                int span = (int)(windowMax - windowMin).TotalMinutes;
                var randOffset = random.Next(0, span);
                var triggerTime = windowMin.AddMinutes(randOffset);
                
                _scheduledTimes.Add(triggerTime);
                lastTime = triggerTime;
            }

            // Ensure our generated times are strictly chronological 
            _scheduledTimes.Sort();
            
            var next = _scheduledTimes.FirstOrDefault();
            if (next != default)
            {
                 OnNextScheduledTimeCalculated?.Invoke(this, next);
            }
            else
            {
                OnNextScheduledTimeCalculated?.Invoke(this, DateTime.MaxValue);
            }
        }

        private void CheckSchedule()
        {
            if (_settingsService.Settings.IsPaused) return;

            var now = DateTime.Now;
            
            if (!_scheduledTimes.Any() || _scheduledTimes.Last() < now.AddHours(-1))
            {
                 CalculateSchedule();
            }

            var toTrigger = _scheduledTimes.Where(t => t <= now).ToList();
            if (toTrigger.Any())
            {
                foreach (var t in toTrigger)
                {
                    _scheduledTimes.Remove(t);
                }
                
                OnTriggerNotification?.Invoke(this, EventArgs.Empty);

                var next = _scheduledTimes.FirstOrDefault();
                if (next != default)
                {
                    OnNextScheduledTimeCalculated?.Invoke(this, next);
                }
                else
                {
                    OnNextScheduledTimeCalculated?.Invoke(this, DateTime.MaxValue);
                }
            }
        }
    }
}

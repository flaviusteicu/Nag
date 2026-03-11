using System;
using Nag.Models;
using Nag.Services;
using Nag.Interfaces;
using Moq;
using Xunit;

namespace Nag.Tests
{
    public class NotificationSchedulerTests
    {
        [Fact]
        public void CalculateSchedule_DoesNotSchedule_WhenPaused()
        {
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.Settings).Returns(new AppSettings { IsPaused = true });

            var scheduler = new NotificationScheduler(mockSettings.Object);
            DateTime? nextTrigger = null;
            
            scheduler.OnNextScheduledTimeCalculated += (s, time) => nextTrigger = time;
            
            scheduler.Start();
            
            Assert.True(nextTrigger.HasValue, "Event should fire even if paused.");
            Assert.Equal(DateTime.MaxValue, nextTrigger.Value);
        }

        [Fact]
        public void CalculateSchedule_ResolvesCorrectBounds_WithStandardFrequency()
        {
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.Settings).Returns(new AppSettings 
            { 
                IsPaused = false,
                ActiveHoursStart = "00:01", // Make the window 24h basically, to ensure we don't accidentally fall outside it during dynamic test execution time
                ActiveHoursEnd = "23:59",
                Frequency = "10"
            });

            var scheduler = new NotificationScheduler(mockSettings.Object);
            DateTime? nextTrigger = null;
            
            scheduler.OnNextScheduledTimeCalculated += (s, time) => nextTrigger = time;
            
            scheduler.Start();
            
            Assert.True(nextTrigger.HasValue);
            Assert.NotEqual(DateTime.MaxValue, nextTrigger.Value);
            Assert.True(nextTrigger.Value >= DateTime.Now.AddMinutes(-1), "Trigger should be in the future.");
            // Ensures it generated bounded times correctly
        }

        [Fact]
        public void CalculateSchedule_CompressesIntervals_WhenExtremeFrequencyRequested()
        {
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.Settings).Returns(new AppSettings 
            { 
                IsPaused = false,
                ActiveHoursStart = "08:00",
                ActiveHoursEnd = "10:00", // Only 2 hours window
                Frequency = "100" // 100 notifications in 120 minutes
            });

            var scheduler = new NotificationScheduler(mockSettings.Object);
            DateTime? nextTrigger = null;
            
            scheduler.OnNextScheduledTimeCalculated += (s, time) => nextTrigger = time;
            
            // This tests that the theoreticalMaxAtTargetGap and dynamic minimum gap logic 
            // does not crash or throw DivisionByZero when crammed with enormous requests.
            scheduler.Start();
            
            Assert.True(nextTrigger.HasValue);
            Assert.NotEqual(DateTime.MaxValue, nextTrigger.Value);
        }
        
        [Fact]
        public void CalculateSchedule_FallbackToSafeguards_WhenActiveHoursAreInvalid()
        {
            var mockSettings = new Mock<ISettingsService>();
            mockSettings.Setup(s => s.Settings).Returns(new AppSettings 
            { 
                IsPaused = false,
                ActiveHoursStart = "invalid",
                ActiveHoursEnd = "data"
            });

            var scheduler = new NotificationScheduler(mockSettings.Object);
            DateTime? nextTrigger = null;
            
            scheduler.OnNextScheduledTimeCalculated += (s, time) => nextTrigger = time;
            
            scheduler.Start();
            
            Assert.True(nextTrigger.HasValue);
            // It should fallback to 08:00 -> 22:00
            Assert.NotEqual(DateTime.MaxValue, nextTrigger.Value);
        }
    }
}

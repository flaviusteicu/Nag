using System;
using Nag.Models;
using Nag.ViewModels;
using Nag.Interfaces;
using Moq;
using Xunit;

namespace Nag.Tests
{
    public class SettingsViewModelTests
    {
        [Fact]
        public void FrequencyProperties_ParseStandardConfigurationsCorrectly()
        {
            var mockSettings = new Mock<ISettingsService>();
            var mockScheduler = new Mock<INotificationScheduler>();
            var mockProvider = new Mock<IServiceProvider>();

            var appSettings = new AppSettings { Frequency = "Light" };
            mockSettings.Setup(s => s.Settings).Returns(appSettings);

            var vm = new SettingsViewModel(mockSettings.Object, mockScheduler.Object, mockProvider.Object);

            Assert.True(vm.IsFreqLight);
            Assert.False(vm.IsFreqModerate);
            Assert.False(vm.IsFreqCustom);
            
            // Changing the UI property should immediately reflect inside the underlying Settings Model
            vm.IsFreqIntensive = true;
            
            Assert.True(vm.IsFreqIntensive);
            Assert.False(vm.IsFreqLight);
            Assert.Equal("Intensive", appSettings.Frequency);
        }

        [Fact]
        public void FrequencyProperties_ParseCustomRangesCorrectly()
        {
            var mockSettings = new Mock<ISettingsService>();
            var mockScheduler = new Mock<INotificationScheduler>();
            var mockProvider = new Mock<IServiceProvider>();

            // Simulate the model loading previously saved custom range data
            var appSettings = new AppSettings { Frequency = "7-12" };
            mockSettings.Setup(s => s.Settings).Returns(appSettings);

            var vm = new SettingsViewModel(mockSettings.Object, mockScheduler.Object, mockProvider.Object);

            Assert.True(vm.IsFreqCustom);
            Assert.Equal(7, vm.CustomFreqMin);
            Assert.Equal(12, vm.CustomFreqMax);
            
            // Adjusting UI Dropdowns automatically recalculates the boundary restrictions
            vm.CustomFreqMin = 15;
            
            // The max should be auto-shoved forwards so it isn't lower than Min
            Assert.Equal(15, vm.CustomFreqMax);
            Assert.Equal("15-15", appSettings.Frequency);
        }
    }
}

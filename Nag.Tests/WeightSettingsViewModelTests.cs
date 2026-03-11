using System;
using System.Collections.Generic;
using System.Linq;
using Nag.Models;
using Nag.ViewModels;
using Nag.Interfaces;
using Moq;
using Xunit;

namespace Nag.Tests
{
    public class WeightSettingsViewModelTests
    {
        [Fact]
        public void Initialization_LoadsEnabledCategories()
        {
            var mockMessages = new Mock<IMessageService>();

            var store = new MessageStore();
            store.Categories.Add(new MessageCategory { Id = "c1", Name = "Cat 1", Enabled = true, Weight = 15 });
            store.Categories.Add(new MessageCategory { Id = "c2", Name = "Disabled Cat", Enabled = false, Weight = 10 });

            mockMessages.Setup(s => s.Messages).Returns(store);

            var vm = new WeightSettingsViewModel(mockMessages.Object);

            // It should only load categories that are physically enabled for display
            Assert.Single(vm.CategoryViewModels);
            Assert.Equal("Cat 1", vm.CategoryViewModels.First().Category.Name);
            Assert.Equal(15, vm.CategoryViewModels.First().Weight);
        }

        [Fact]
        public void SaveCommand_CommitsWeightsAndInvokesEvents()
        {
            var mockMessages = new Mock<IMessageService>();

            var store = new MessageStore();
            store.Categories.Add(new MessageCategory { Id = "c1", Name = "Cat 1", Enabled = true, Weight = 1 });

            // Using Setup properly without tracking internals since we just need to verify the call
            mockMessages.Setup(s => s.Messages).Returns(store);
            mockMessages.Setup(s => s.SaveMessages()).Verifiable();

            var vm = new WeightSettingsViewModel(mockMessages.Object);

            // Emulate user dragging a slider
            vm.CategoryViewModels.First().Weight = 99;

            bool eventFired = false;
            vm.OnWeightsSaved += (s, e) => eventFired = true;

            bool closeFired = false;
            vm.RequestClose = () => closeFired = true;

            // Execute the Save UX Command
            vm.SaveCommand.Execute(null);

            // Assert Model mutation
            Assert.Equal(99, store.Categories.First().Weight);

            // Assert Service logic triggered
            mockMessages.Verify(s => s.SaveMessages(), Times.Once);

            // Assert correct UI cascading
            Assert.True(eventFired);
            Assert.True(closeFired);
        }
    }
}

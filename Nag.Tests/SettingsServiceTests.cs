using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nag.Models;
using Nag.Services;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Nag.Tests
{
    public class SettingsServiceTests : IDisposable
    {
        private readonly string _settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        private readonly string _messagesPath = Path.Combine(AppContext.BaseDirectory, "messages.json");

        public SettingsServiceTests()
        {
            // Clean up state before each test
            if (File.Exists(_settingsPath)) File.Delete(_settingsPath);
            if (File.Exists(_messagesPath)) File.Delete(_messagesPath);
        }

        public void Dispose()
        {
            // Clean up state after each test
            if (File.Exists(_settingsPath)) File.Delete(_settingsPath);
            if (File.Exists(_messagesPath)) File.Delete(_messagesPath);
        }

        [Fact]
        public void Constructor_CreatesDefaultSettingsFile_WhenItDoesNotExist()
        {
            var service = new SettingsService();

            Assert.True(File.Exists(_settingsPath), "settings.json should be created by the constructor.");
            Assert.NotNull(service.Settings);
        }

        [Fact]
        public void Constructor_CreatesDefaultMessagesFile_WhenItDoesNotExist()
        {
            var service = new MessageService();

            Assert.True(File.Exists(_messagesPath), "messages.json should be created by the constructor.");
            Assert.NotNull(service.Messages);
        }

        [Fact]
        public void GetRandomMessage_ReturnsSystemFallback_WhenNoCategoriesEnabled()
        {
            var service = new MessageService();
            // Default configuration has zero categories

            var msg = service.GetRandomMessage();

            Assert.NotNull(msg);
            Assert.Equal("system", msg.Value.CategoryId);
            Assert.Equal("System", msg.Value.CategoryName);
            Assert.Contains("No messages loaded", msg.Value.Message);
        }

        [Fact]
        public void GetRandomMessage_RespectsCategoryWeights_Statistically()
        {
            var service = new MessageService();

            service.Messages.Categories.Add(new MessageCategory
            {
                Id = "light",
                Name = "Light Weight",
                Enabled = true,
                Weight = 10,
                Messages = new List<string> { "L1", "L2", "L3" }
            });

            service.Messages.Categories.Add(new MessageCategory
            {
                Id = "heavy",
                Name = "Heavy Weight",
                Enabled = true,
                Weight = 90,
                Messages = new List<string> { "H1", "H2", "H3", "H4", "H5", "H6", "H7", "H8", "H9", "H10" }
            });

            int lightSelected = 0;
            int heavySelected = 0;

            // Draw 1000 tickets
            for (int i = 0; i < 1000; i++)
            {
                var result = service.GetRandomMessage();
                if (result?.CategoryId == "light") lightSelected++;
                else if (result?.CategoryId == "heavy") heavySelected++;
            }

            // Statistically, "heavy" should be picked far more frequently (~90% of the time)
            Assert.True(heavySelected > 800, $"Heavy should vastly outweigh Light, but got {heavySelected} vs {lightSelected}");
            Assert.True(lightSelected > 40 && lightSelected < 180, $"Light should occasionally trigger, got {lightSelected}");
        }

        [Fact]
        public void GetRandomMessage_AvoidsConsecutiveDuplicates_WhenMultipleMessagesExist()
        {
            var service = new MessageService();

            service.Messages.Categories.Add(new MessageCategory
            {
                Id = "multi",
                Name = "Multi",
                Enabled = true,
                Weight = 1,
                Messages = new List<string> { "Message A", "Message B" }
            });

            string? lastMessage = null;
            int consecutiveDuplicates = 0;

            for (int i = 0; i < 100; i++)
            {
                var result = service.GetRandomMessage();
                if (result?.Message == lastMessage)
                {
                    consecutiveDuplicates++;
                }
                lastMessage = result?.Message;
            }

            // The anti-repetition constraint should mathematically prevent identical sequential messages
            Assert.Equal(0, consecutiveDuplicates);
        }

        [Fact]
        public void LoadSettings_SetsLoadCorrupted_WhenJsonIsInvalid()
        {
            File.WriteAllText(_settingsPath, "NOT VALID JSON {{{");

            var service = new SettingsService();

            Assert.True(service.LoadCorrupted, "LoadCorrupted should be true when settings.json is corrupted.");
            Assert.NotNull(service.Settings); // Should still have default settings
        }

        [Fact]
        public void LoadMessages_SetsLoadCorrupted_WhenJsonIsInvalid()
        {
            File.WriteAllText(_messagesPath, "NOT VALID JSON {{{");

            var service = new MessageService();

            Assert.True(service.LoadCorrupted, "LoadCorrupted should be true when messages.json is corrupted.");
            Assert.NotNull(service.Messages); // Should still have default messages
        }
    }
}

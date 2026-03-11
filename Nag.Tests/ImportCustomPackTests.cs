using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nag.Models;
using Nag.Services;
using Xunit;

namespace Nag.Tests
{
    public class ImportCustomPackTests : IDisposable
    {
        private readonly string _categoriesDir;
        private readonly string _imagesDir;
        private readonly string _settingsPath;
        private readonly string _messagesPath;
        private readonly string _tempExternalDir;

        public ImportCustomPackTests()
        {
            _categoriesDir = Path.Combine(AppContext.BaseDirectory, "Categories");
            _imagesDir = Path.Combine(AppContext.BaseDirectory, "Images");
            _settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
            _messagesPath = Path.Combine(AppContext.BaseDirectory, "messages.json");
            _tempExternalDir = Path.Combine(Path.GetTempPath(), $"NagTest_{Guid.NewGuid():N}");

            Cleanup();
        }

        public void Dispose()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (Directory.Exists(_categoriesDir)) Directory.Delete(_categoriesDir, true);
            if (Directory.Exists(_imagesDir)) Directory.Delete(_imagesDir, true);
            if (Directory.Exists(_tempExternalDir)) Directory.Delete(_tempExternalDir, true);
            if (File.Exists(_settingsPath)) File.Delete(_settingsPath);
            if (File.Exists(_messagesPath)) File.Delete(_messagesPath);
        }

        [Fact]
        public void EnsureCategoriesScaffold_CreatesExampleFolder()
        {
            var service = new SettingsService();
            service.EnsureCategoriesScaffold();

            var exampleDir = Path.Combine(_categoriesDir, "Example");
            Assert.True(Directory.Exists(exampleDir), "Example folder should be created.");
            Assert.True(File.Exists(Path.Combine(exampleDir, "messages.txt")), "messages.txt should be created.");
        }

        [Fact]
        public void EnsureCategoriesScaffold_DoesNotOverwrite_WhenAlreadyExists()
        {
            var service = new SettingsService();
            service.EnsureCategoriesScaffold();

            // Modify the messages.txt
            var msgPath = Path.Combine(_categoriesDir, "Example", "messages.txt");
            File.WriteAllText(msgPath, "Custom content");

            // Call again — should not overwrite
            service.EnsureCategoriesScaffold();
            Assert.Equal("Custom content", File.ReadAllText(msgPath));
        }

        [Fact]
        public void SyncCategories_CreatesNewCategory_FromValidFolder()
        {
            var service = new SettingsService();

            // Create a valid category folder
            var catDir = Path.Combine(_categoriesDir, "TestCategory");
            Directory.CreateDirectory(catDir);
            File.WriteAllText(Path.Combine(catDir, "messages.txt"), "Hello world\nSecond message");

            var result = service.SyncCategories();

            Assert.Contains("Synced", result);
            Assert.Contains("2 messages", result);

            // Verify the category was added to messages
            var category = service.Messages.Categories.FirstOrDefault(c => c.Name == "TestCategory");
            Assert.NotNull(category);
            Assert.Equal(2, category.Messages.Count);
            Assert.Contains("Hello world", category.Messages);
            Assert.Contains("Second message", category.Messages);
            Assert.True(Guid.TryParse(category.Id, out _), "Imported category should have a GUID id.");
        }

        [Fact]
        public void SyncCategories_SkipsFolder_WithNoTextFiles()
        {
            var service = new SettingsService();

            // Create a folder with only an image
            var catDir = Path.Combine(_categoriesDir, "ImageOnly");
            Directory.CreateDirectory(catDir);
            File.WriteAllBytes(Path.Combine(catDir, "avatar.png"), new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header

            var result = service.SyncCategories();

            Assert.Contains("Skipped", result);
            Assert.Contains("no text files", result);
            Assert.DoesNotContain("ImageOnly", service.Messages.Categories.Select(c => c.Name));
        }

        [Fact]
        public void SyncCategories_SkipsFolder_WithEmptyTextFiles()
        {
            var service = new SettingsService();

            var catDir = Path.Combine(_categoriesDir, "EmptyTexts");
            Directory.CreateDirectory(catDir);
            File.WriteAllText(Path.Combine(catDir, "messages.txt"), "   \n\n   ");

            var result = service.SyncCategories();

            Assert.Contains("Skipped", result);
            Assert.Contains("empty", result);
        }

        [Fact]
        public void SyncCategories_WarnsOnMultipleImages()
        {
            var service = new SettingsService();

            var catDir = Path.Combine(_categoriesDir, "MultiImage");
            Directory.CreateDirectory(catDir);
            File.WriteAllText(Path.Combine(catDir, "messages.txt"), "A message");
            File.WriteAllBytes(Path.Combine(catDir, "img1.png"), new byte[] { 0x89 });
            File.WriteAllBytes(Path.Combine(catDir, "img2.jpg"), new byte[] { 0xFF });

            var result = service.SyncCategories();

            Assert.Contains("multiple images", result);
        }

        [Fact]
        public void SyncCategories_CopiesAvatarToImagesFolder()
        {
            var service = new SettingsService();

            var catDir = Path.Combine(_categoriesDir, "WithAvatar");
            Directory.CreateDirectory(catDir);
            File.WriteAllText(Path.Combine(catDir, "messages.txt"), "A message");
            File.WriteAllBytes(Path.Combine(catDir, "avatar.png"), new byte[] { 0x89, 0x50, 0x4E, 0x47 });

            service.SyncCategories();

            var category = service.Messages.Categories.First(c => c.Name == "WithAvatar");
            var expectedImg = Path.Combine(_imagesDir, $"{category.Id}.png");
            Assert.True(File.Exists(expectedImg), "Avatar should be copied to Images folder.");
        }

        [Fact]
        public void SyncCategories_DoesNotTouchBuiltInCategories()
        {
            var service = new SettingsService();

            // Add a built-in category with a non-GUID id
            service.Messages.Categories.Add(new MessageCategory
            {
                Id = "evidence",
                Name = "Evidence Against the Narrative",
                Enabled = true,
                Weight = 1,
                Messages = new List<string> { "Original message" }
            });
            service.SaveMessages();

            // Create a Categories folder with a similarly-named folder
            var catDir = Path.Combine(_categoriesDir, "Evidence Against the Narrative");
            Directory.CreateDirectory(catDir);
            File.WriteAllText(Path.Combine(catDir, "messages.txt"), "Imported message");

            service.SyncCategories();

            // The built-in should be untouched
            var builtin = service.Messages.Categories.First(c => c.Id == "evidence");
            Assert.Single(builtin.Messages);
            Assert.Equal("Original message", builtin.Messages[0]);

            // A new imported one should exist alongside
            var imported = service.Messages.Categories.FirstOrDefault(c => c.Name == "Evidence Against the Narrative" && Guid.TryParse(c.Id, out _));
            Assert.NotNull(imported);
            Assert.Single(imported.Messages);
            Assert.Equal("Imported message", imported.Messages[0]);
        }

        [Fact]
        public void ImportCustomPack_CopiesExternalFolderIntoCategories()
        {
            var service = new SettingsService();

            // Create an external folder with a category
            var extCatDir = Path.Combine(_tempExternalDir, "FriendCategory");
            Directory.CreateDirectory(extCatDir);
            File.WriteAllText(Path.Combine(extCatDir, "messages.txt"), "Friend message 1\nFriend message 2");

            var result = service.ImportCustomPack(_tempExternalDir);

            Assert.Contains("Synced", result);
            Assert.True(Directory.Exists(Path.Combine(_categoriesDir, "FriendCategory")),
                "External folder should be copied into Categories/.");
            
            var category = service.Messages.Categories.FirstOrDefault(c => c.Name == "FriendCategory");
            Assert.NotNull(category);
            Assert.Equal(2, category.Messages.Count);
        }

        [Fact]
        public void ImportCustomPack_ReturnsError_ForInvalidPath()
        {
            var service = new SettingsService();
            var result = service.ImportCustomPack("/nonexistent/path");
            Assert.Contains("invalid", result);
        }

        [Fact]
        public void SyncCategories_HandlesJsonMessageFiles()
        {
            var service = new SettingsService();

            var catDir = Path.Combine(_categoriesDir, "JsonCategory");
            Directory.CreateDirectory(catDir);
            File.WriteAllText(Path.Combine(catDir, "messages.json"), "[\"JSON message 1\", \"JSON message 2\"]");

            service.SyncCategories();

            var category = service.Messages.Categories.First(c => c.Name == "JsonCategory");
            Assert.Equal(2, category.Messages.Count);
            Assert.Contains("JSON message 1", category.Messages);
        }
    }
}

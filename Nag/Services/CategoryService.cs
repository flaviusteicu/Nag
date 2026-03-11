using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using Nag.Models;
using Nag.Interfaces;

namespace Nag.Services
{
    /// <summary>
    /// Manages the Categories/ folder: importing custom packs, syncing folder contents
    /// into messages.json, and scaffolding example content on first run.
    /// </summary>
    public class CategoryService : ICategoryService
    {
        private readonly IMessageService _messageService;

        public CategoryService(IMessageService messageService)
        {
            _messageService = messageService;
        }

        public string ImportCustomPack(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return "⛔ The selected path is invalid or does not exist.";

            var categoriesDir = Path.Combine(AppContext.BaseDirectory, "Categories");
            if (!Directory.Exists(categoriesDir))
                Directory.CreateDirectory(categoriesDir);

            var dirInfo = new DirectoryInfo(folderPath);

            // Check if the selected folder IS the Categories dir itself — skip copy, just sync
            if (Path.GetFullPath(folderPath).Equals(Path.GetFullPath(categoriesDir), StringComparison.OrdinalIgnoreCase))
                return SyncCategories();

            // Check if the folder contains subfolders (it's a pack root with multiple categories)
            var subDirs = Directory.GetDirectories(folderPath);
            if (subDirs.Length > 0)
            {
                // Copy each subfolder into Categories/
                foreach (var sub in subDirs)
                {
                    var subInfo = new DirectoryInfo(sub);
                    var destDir = Path.Combine(categoriesDir, subInfo.Name);
                    CopyDirectory(sub, destDir);
                }
            }
            else
            {
                // It's a single category folder — copy it as-is
                var destDir = Path.Combine(categoriesDir, dirInfo.Name);
                CopyDirectory(folderPath, destDir);
            }

            return SyncCategories();
        }

        public string SyncCategories()
        {
            var categoriesDir = Path.Combine(AppContext.BaseDirectory, "Categories");
            if (!Directory.Exists(categoriesDir))
                return "No Categories folder found.";

            var imagesDir = Path.Combine(AppContext.BaseDirectory, "Images");
            if (!Directory.Exists(imagesDir))
                Directory.CreateDirectory(imagesDir);

            var imageExtensions = new[] { ".png", ".jpg", ".jpeg" };
            var textExtensions = new[] { ".txt", ".json" };

            var subDirs = Directory.GetDirectories(categoriesDir);
            int categoriesAdded = 0;
            int totalMessages = 0;
            var warnings = new List<string>();
            var errors = new List<string>();

            // Track which category IDs came from Categories/ so we know what to keep
            var syncedCategoryIds = new List<string>();

            foreach (var dir in subDirs)
            {
                var dirInfo = new DirectoryInfo(dir);
                string categoryName = dirInfo.Name;

                // Find text files
                var txtFiles = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => textExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToArray();

                if (txtFiles.Length == 0)
                {
                    errors.Add($"⛔ Skipped '{categoryName}' — no text files found.");
                    continue;
                }

                // Read all messages from all text files
                var messages = new List<string>();
                foreach (var file in txtFiles)
                {
                    if (Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        // Try to parse as a JSON array of strings
                        try
                        {
                            var json = File.ReadAllText(file);
                            var parsed = JsonSerializer.Deserialize<List<string>>(json);
                            if (parsed != null)
                                messages.AddRange(parsed.Where(m => !string.IsNullOrWhiteSpace(m)));
                        }
                        catch
                        {
                            // Fallback: read as plain text lines
                            messages.AddRange(File.ReadAllLines(file).Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)));
                        }
                    }
                    else
                    {
                        messages.AddRange(File.ReadAllLines(file).Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)));
                    }
                }

                if (messages.Count == 0)
                {
                    errors.Add($"⛔ Skipped '{categoryName}' — text files were empty.");
                    continue;
                }

                // Find image files
                var imgFiles = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToArray();

                if (imgFiles.Length > 1)
                    warnings.Add($"⚠️ '{categoryName}' has multiple images — using '{Path.GetFileName(imgFiles[0])}'.");

                // Find or create category — match by folder name to allow re-syncing
                // Only match categories that were imported (have a GUID-style id), never touch built-in ones
                var existingCategory = _messageService.Messages.Categories
                    .FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase)
                                         && Guid.TryParse(c.Id, out _));

                if (existingCategory != null)
                {
                    // Update existing imported category
                    existingCategory.Messages = messages;
                    syncedCategoryIds.Add(existingCategory.Id);
                }
                else
                {
                    // Create new category
                    var newCategory = new MessageCategory
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = categoryName,
                        Enabled = true,
                        Weight = 1,
                        Messages = messages
                    };
                    _messageService.Messages.Categories.Add(newCategory);
                    syncedCategoryIds.Add(newCategory.Id);
                    categoriesAdded++;
                }

                // Copy avatar image to Images/{id}.png
                var categoryId = _messageService.Messages.Categories.First(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase) && Guid.TryParse(c.Id, out _)).Id;
                if (imgFiles.Length > 0)
                {
                    var destImgPath = Path.Combine(imagesDir, $"{categoryId}.png");
                    File.Copy(imgFiles[0], destImgPath, overwrite: true);
                }

                totalMessages += messages.Count;
            }

            // Remove imported categories whose folders no longer exist
            int categoriesRemoved = 0;
            var staleCategories = _messageService.Messages.Categories
                .Where(c => Guid.TryParse(c.Id, out _) && !syncedCategoryIds.Contains(c.Id))
                .ToList();

            foreach (var stale in staleCategories)
            {
                _messageService.Messages.Categories.Remove(stale);
                // Clean up avatar
                var staleImg = Path.Combine(imagesDir, $"{stale.Id}.png");
                if (File.Exists(staleImg))
                    File.Delete(staleImg);
                categoriesRemoved++;
            }

            _messageService.SaveMessages();

            // Build result summary
            var parts = new List<string>();

            int activeSynced = subDirs.Length - errors.Count;
            if (activeSynced > 0)
                parts.Add($"✅ Synced {activeSynced} categories ({totalMessages} messages).");

            if (categoriesRemoved > 0)
                parts.Add($"🗑️ Removed {categoriesRemoved} stale categories.");

            if (activeSynced == 0 && categoriesRemoved == 0 && errors.Count == 0)
                parts.Add("✅ Categories are up to date — no changes needed.");

            parts.AddRange(warnings);
            parts.AddRange(errors);

            return string.Join("\n", parts);
        }

        public void EnsureCategoriesScaffold()
        {
            var categoriesDir = Path.Combine(AppContext.BaseDirectory, "Categories");
            var exampleDir = Path.Combine(categoriesDir, "Example");

            if (Directory.Exists(exampleDir))
                return; // Scaffold already exists

            Directory.CreateDirectory(exampleDir);

            // Write sample messages.txt
            File.WriteAllText(
                Path.Combine(exampleDir, "messages.txt"),
                "I'm ready for new messages. Are you?\nThis is an example category — copy this folder and edit it to create your own!"
            );

            // Copy the app icon as the example avatar
            var sourceIcon = Path.Combine(AppContext.BaseDirectory, "app_logo.png");
            if (!File.Exists(sourceIcon))
                sourceIcon = Path.Combine(AppContext.BaseDirectory, "app_icon.ico");

            if (File.Exists(sourceIcon))
                File.Copy(sourceIcon, Path.Combine(exampleDir, "avatar.png"), overwrite: true);
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, new DirectoryInfo(subDir).Name);
                CopyDirectory(subDir, destSubDir);
            }
        }
    }
}

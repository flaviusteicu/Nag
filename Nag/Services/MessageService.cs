using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nag.Core;
using Nag.Models;
using Nag.Interfaces;

namespace Nag.Services
{
    /// <summary>
    /// Manages the reading, writing, and random selection of messages from messages.json.
    /// Handles weighted category selection and anti-repetition logic.
    /// </summary>
    public class MessageService : IMessageService
    {
        private readonly string _messagesPath = Path.Combine(AppContext.BaseDirectory, Constants.MessagesFileName);

        public MessageStore Messages { get; private set; } = new();
        public bool LoadCorrupted { get; private set; }

        private string? _lastMessage;

        public MessageService()
        {
            LoadMessages();
        }

        public void LoadMessages()
        {
            if (File.Exists(_messagesPath))
            {
                try
                {
                    var json = File.ReadAllText(_messagesPath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    Messages = JsonSerializer.Deserialize<MessageStore>(json, options) ?? new MessageStore();
                }
                catch (Exception ex)
                {
                    NagLogger.Error("LoadMessages", ex);
                    LoadCorrupted = true;
                    Messages = new MessageStore();
                }
            }
            else
            {
                SaveMessages();
            }
        }

        public void SaveMessages()
        {
            var json = JsonSerializer.Serialize(Messages, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_messagesPath, json);
        }

        public (string CategoryId, string CategoryName, string Message)? GetRandomMessage()
        {
            var enabledCategories = Messages.Categories.Where(c => c.Enabled && c.Messages.Any()).ToList();
            if (!enabledCategories.Any())
                return ("system", "System", "No messages loaded. Enable a category or edit messages.json.");

            // Build the Raffle-Ticket array at the CATEGORY level.
            // If a category has a Weight of '5', we inject the category 5 times into the selection pool,
            // making its probability of being drawn proportional to its weight alone — independent of
            // how many messages the category contains.
            var categoryPool = enabledCategories
                .SelectMany(c => Enumerable.Repeat(c, Math.Max(1, c.Weight)))
                .ToList();

            // Pure RNG selection across the ticket pool
            var random = new Random();

            // Loop aggressively to select a newly randomized message that was inherently NOT the one
            // the user just previously saw, preserving maximum context novelty.
            // Automatically breaks the anti-repetition constraint if only 1 valid message exists.
            int totalDistinctMessages = enabledCategories.Sum(c => c.Messages.Count);
            for(int i = 0; i < 10; i++)
            {
                var selectedCategory = categoryPool[random.Next(categoryPool.Count)];
                var message = selectedCategory.Messages[random.Next(selectedCategory.Messages.Count)];
                if (message != _lastMessage || totalDistinctMessages <= 1)
                {
                    _lastMessage = message;
                    return (selectedCategory.Id, selectedCategory.Name, message);
                }
            }

            // If the 10 loop cycles inexplicably collided all times, explicitly fallback safely
            var fallbackCategory = categoryPool[random.Next(categoryPool.Count)];
            var fallbackMessage = fallbackCategory.Messages[random.Next(fallbackCategory.Messages.Count)];
            _lastMessage = fallbackMessage;
            return (fallbackCategory.Id, fallbackCategory.Name, fallbackMessage);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nag.Models
{
    /// <summary>
    /// The root envelope capturing the entire `messages.json` file.
    /// </summary>
    public class MessageStore
    {
        [JsonPropertyName("categories")]
        public List<MessageCategory> Categories { get; set; } = new();
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nag.Models
{
    /// <summary>
    /// Represents a discrete theme array (like "Evidence" or "Defiance") stored in `messages.json`.
    /// Its ID strictly aligns with the transparent .png files placed in the `publish/Images` folder 
    /// allowing the scheduler to map a text prompt directly to its visual avatar.
    /// </summary>
    public class MessageCategory
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("weight")]
        public int Weight { get; set; } = 1;

        [JsonPropertyName("messages")]
        public List<string> Messages { get; set; } = new();
    }
}

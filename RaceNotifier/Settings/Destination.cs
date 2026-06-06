using System;

namespace RaceNotifier.Settings
{
    public enum DestinationType
    {
        Discord = 0,
        Telegram = 1,     // Phase 2 (reserved)
        CustomWebhook = 2
    }

    /// <summary>How a custom webhook formats its request body.</summary>
    public enum WebhookBodyFormat
    {
        Json = 0,     // {"content": "..."} as application/json
        PlainText = 1 // raw message as text/plain
    }

    /// <summary>
    /// A single place a notification can be sent. Discord is supported in v1;
    /// Telegram fields exist now so Phase 2 is purely additive.
    /// </summary>
    public class Destination
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "New destination";
        public DestinationType Type { get; set; } = DestinationType.Discord;

        // Discord
        public string DiscordWebhookUrl { get; set; } = "";

        // Custom webhook
        public string WebhookUrl { get; set; } = "";
        public WebhookBodyFormat WebhookBodyFormat { get; set; } = WebhookBodyFormat.Json;

        // Telegram (Phase 2)
        public string TelegramBotToken { get; set; } = "";
        public string TelegramChatId { get; set; } = "";

        /// <summary>True when this destination has the URL it needs for its type.</summary>
        public bool HasUsableTarget
        {
            get
            {
                switch (Type)
                {
                    case DestinationType.Discord: return !string.IsNullOrWhiteSpace(DiscordWebhookUrl);
                    case DestinationType.CustomWebhook: return !string.IsNullOrWhiteSpace(WebhookUrl);
                    default: return false; // Telegram / unknown — no transport yet
                }
            }
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Name) ? Type.ToString() : Name;
        }
    }
}

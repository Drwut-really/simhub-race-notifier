using System;

namespace RaceNotifier.Settings
{
    public enum DestinationType
    {
        Discord = 0,
        Telegram = 1 // Phase 2
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

        // Telegram (Phase 2)
        public string TelegramBotToken { get; set; } = "";
        public string TelegramChatId { get; set; } = "";

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Name) ? Type.ToString() : Name;
        }
    }
}

using System.Collections.Generic;

namespace RaceNotifier.Settings
{
    /// <summary>
    /// One bindable button slot. Each slot maps to a SimHub action
    /// "RaceNotifier.SendMessageN" and sends its preset Text to the chosen destinations.
    /// </summary>
    public class MessageSlot
    {
        public bool Enabled { get; set; } = false;
        public string Text { get; set; } = "";

        /// <summary>Ids of the Destinations this slot sends to.</summary>
        public List<string> TargetDestinationIds { get; set; } = new List<string>();

        /// <summary>Minimum seconds between sends for this slot (anti double-fire / rate limit).</summary>
        public double CooldownSeconds { get; set; } = 3.0;
    }
}

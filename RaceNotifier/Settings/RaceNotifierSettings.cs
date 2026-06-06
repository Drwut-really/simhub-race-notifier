using System.Collections.Generic;

namespace RaceNotifier.Settings
{
    /// <summary>
    /// Root settings object, persisted by SimHub via ReadCommonSettings/SaveCommonSettings
    /// (JSON in SimHub\PluginsData\Common). Secrets live here, never in the repo.
    /// </summary>
    public class RaceNotifierSettings
    {
        public const int SlotCount = 10;

        public string SenderName { get; set; } = "";
        public bool PrefixSenderName { get; set; } = true;

        public List<Destination> Destinations { get; set; } = new List<Destination>();
        public List<MessageSlot> Slots { get; set; } = new List<MessageSlot>();

        /// <summary>Make sure lists exist and there are exactly SlotCount slots.</summary>
        public void EnsureInitialized()
        {
            if (Destinations == null)
                Destinations = new List<Destination>();
            if (Slots == null)
                Slots = new List<MessageSlot>();
            while (Slots.Count < SlotCount)
                Slots.Add(new MessageSlot());
            if (Slots.Count > SlotCount)
                Slots.RemoveRange(SlotCount, Slots.Count - SlotCount);
        }
    }
}

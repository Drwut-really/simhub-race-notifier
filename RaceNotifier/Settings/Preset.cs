using System.Collections.Generic;

namespace RaceNotifier.Settings
{
    /// <summary>
    /// One user-created message preset. <see cref="ActionIndex"/> is the stable identity:
    /// it maps to the SimHub action "RaceNotifierPlugin.SendMessage&lt;ActionIndex&gt;" and is the
    /// per-preset cooldown key. List order is purely cosmetic.
    /// </summary>
    public class Preset
    {
        /// <summary>
        /// Stable button-slot index (>= 1). Assigned once at creation and never changed after.
        /// 0 means "unassigned" — <see cref="RaceNotifierSettings.EnsureInitialized"/> will assign one.
        /// </summary>
        public int ActionIndex { get; set; } = 0;

        /// <summary>Optional friendly label; UI falls back to "Message &lt;ActionIndex&gt;" when blank.</summary>
        public string Name { get; set; } = "";

        public bool Enabled { get; set; } = false;
        public string Text { get; set; } = "";

        /// <summary>Ids of the Destinations this preset sends to.</summary>
        public List<string> TargetDestinationIds { get; set; } = new List<string>();

        /// <summary>Minimum seconds between sends for this preset (anti double-fire / rate limit).</summary>
        public double CooldownSeconds { get; set; } = 3.0;
    }
}

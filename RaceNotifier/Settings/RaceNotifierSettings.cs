using System.Collections.Generic;
using System.Linq;

namespace RaceNotifier.Settings
{
    /// <summary>
    /// Root settings object, persisted by SimHub via ReadCommonSettings/SaveCommonSettings
    /// (JSON in SimHub\PluginsData\Common). Secrets live here, never in the repo.
    /// </summary>
    public class RaceNotifierSettings
    {
        /// <summary>Master switch. When false the dispatcher drops every send (the plugin stays loaded).</summary>
        public bool PluginEnabled { get; set; } = true;

        public string SenderName { get; set; } = "";
        public bool PrefixSenderName { get; set; } = true;

        public List<Destination> Destinations { get; set; } = new List<Destination>();

        /// <summary>Unlimited, user-created message presets. Replaces the old fixed slot array.</summary>
        public List<Preset> Presets { get; set; } = new List<Preset>();

        /// <summary>
        /// Make sure the lists exist and every preset has a valid, unique <see cref="Preset.ActionIndex"/>
        /// (>= 1). Does NOT pad/trim to any fixed count. Tolerant of null/legacy data.
        /// </summary>
        public void EnsureInitialized()
        {
            if (Destinations == null)
                Destinations = new List<Destination>();
            if (Presets == null)
                Presets = new List<Preset>();

            // Drop any null entries a malformed JSON could introduce.
            Presets.RemoveAll(p => p == null);

            // Assign indices to presets that lack one (0) or collide with an earlier preset.
            var used = new HashSet<int>();
            foreach (var p in Presets)
            {
                if (p.ActionIndex >= 1 && used.Add(p.ActionIndex))
                    continue; // already valid and unique
                p.ActionIndex = NextFreeActionIndex(used);
                used.Add(p.ActionIndex);
            }
        }

        /// <summary>
        /// Lowest integer >= 1 not used by any current preset (gaps are reused). Pass an optional
        /// set of indices already claimed in the current pass to also avoid those.
        /// </summary>
        public int NextFreeActionIndex(HashSet<int> alsoReserved = null)
        {
            var taken = new HashSet<int>(
                Presets.Where(p => p != null && p.ActionIndex >= 1).Select(p => p.ActionIndex));
            if (alsoReserved != null)
                foreach (var i in alsoReserved) taken.Add(i);

            int candidate = 1;
            while (taken.Contains(candidate)) candidate++;
            return candidate;
        }

        /// <summary>Highest assigned ActionIndex among presets, or 0 if there are none.</summary>
        public int MaxActionIndex()
        {
            return Presets
                .Where(p => p != null)
                .Select(p => p.ActionIndex)
                .DefaultIfEmpty(0)
                .Max();
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace RaceNotifier.Settings
{
    /// <summary>
    /// Pure, UI-free validation for a message preset. Returns a short human-readable
    /// reason the preset cannot send, or null when it is OK. Advisory only — callers
    /// use it to warn, never to block sending.
    /// </summary>
    public static class PresetValidation
    {
        /// <summary>Reason the preset can't send, or null if it's fine.</summary>
        public static string Describe(Preset preset, IList<Destination> destinations)
        {
            if (preset == null)
                return null;

            if (string.IsNullOrWhiteSpace(preset.Text))
                return "No message text.";

            if (preset.TargetDestinationIds == null || preset.TargetDestinationIds.Count == 0)
                return "No destination selected.";

            var selected = (destinations ?? new List<Destination>())
                .Where(d => d != null && preset.TargetDestinationIds.Contains(d.Id))
                .ToList();

            if (selected.Count == 0)
                return "No destination selected.";

            // Dispatcher sends to EVERY selected target and one unusable target fails the
            // whole send, so warn if ANY selected destination lacks a usable URL.
            if (selected.Any(d => !d.HasUsableTarget))
                return "A selected destination has no URL set.";

            return null;
        }

        public static bool IsOk(Preset preset, IList<Destination> destinations)
            => Describe(preset, destinations) == null;
    }
}

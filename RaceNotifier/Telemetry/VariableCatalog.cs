using System;
using System.Collections.Generic;
using System.Linq;

namespace RaceNotifier.Telemetry
{
    /// <summary>
    /// One message variable: its token (WITHOUT braces), a human description for the UI, and a pure
    /// resolver that reads a telemetry snapshot. Add a native variable by adding one entry below.
    /// </summary>
    public sealed class VariableEntry
    {
        public string Token { get; }
        public string Description { get; }
        public Func<TelemetrySnapshot, string> Resolve { get; }

        public VariableEntry(string token, string description, Func<TelemetrySnapshot, string> resolve)
        {
            Token = token;
            Description = description;
            Resolve = resolve;
        }
    }

    /// <summary>
    /// Single source of truth for message variables — drives BOTH MessageVariables.Render and the
    /// settings-UI variables block. Only {flag} ships today; more native variables are add-only here.
    /// Resolvers must be pure (read only the passed snapshot; never call SimHub APIs).
    /// </summary>
    public static class VariableCatalog
    {
        private static readonly VariableEntry[] _entries =
        {
            new VariableEntry(
                "flag",
                "current track flag (e.g. Yellow, Green, Checkered; \"none\" when clear)",
                s => (s.GameRunning && !string.IsNullOrWhiteSpace(s.FlagName)) ? s.FlagName.Trim() : "none"),
        };

        // Array.AsReadOnly wraps the array in a ReadOnlyCollection, so callers can't cast back and mutate.
        private static readonly IReadOnlyList<VariableEntry> _entriesView = Array.AsReadOnly(_entries);

        /// <summary>Read-only view of the registered variables (for the UI to list).</summary>
        public static IReadOnlyList<VariableEntry> Entries => _entriesView;

        private static readonly Dictionary<string, VariableEntry> _byToken =
            _entries.ToDictionary(e => e.Token, StringComparer.OrdinalIgnoreCase);

        /// <summary>Resolve a token (case-insensitive, no braces) against a snapshot. False if unknown.</summary>
        public static bool TryResolve(string token, TelemetrySnapshot snapshot, out string value)
        {
            if (token != null && _byToken.TryGetValue(token, out var entry))
            {
                value = entry.Resolve(snapshot ?? TelemetrySnapshot.Empty) ?? "";
                return true;
            }
            value = null;
            return false;
        }
    }
}

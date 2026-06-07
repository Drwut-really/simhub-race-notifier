using System.Text.RegularExpressions;

namespace RaceNotifier.Telemetry
{
    /// <summary>
    /// Replaces {token} placeholders in message text with live telemetry values. Pure: the same text
    /// and snapshot always yield the same string. Tokens are case-insensitive; unknown tokens are left
    /// exactly as written. Only {flag} is supported today (the switch is the extension point).
    /// </summary>
    public static class MessageVariables
    {
        // {flag}, {FLAG}, {my_var} — token chars are letters/digits/underscore.
        private static readonly Regex TokenPattern =
            new Regex(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);

        public static string Render(string text, TelemetrySnapshot snapshot)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0)
                return text;

            var snap = snapshot ?? TelemetrySnapshot.Empty;

            // Unknown tokens are left exactly as written (TryResolve returns false).
            return TokenPattern.Replace(text, m =>
                VariableCatalog.TryResolve(m.Groups[1].Value, snap, out var value) ? value : m.Value);
        }
    }
}

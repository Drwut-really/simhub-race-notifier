namespace RaceNotifier.Telemetry
{
    /// <summary>
    /// Immutable point-in-time view of the telemetry the message variables need. Swapped as a single
    /// volatile reference from the SimHub data thread and read lock-free at send time.
    /// </summary>
    public sealed class TelemetrySnapshot
    {
        public static readonly TelemetrySnapshot Empty = new TelemetrySnapshot(false, "");

        public bool GameRunning { get; }
        public string FlagName { get; }

        public TelemetrySnapshot(bool gameRunning, string flagName)
        {
            GameRunning = gameRunning;
            FlagName = flagName ?? "";
        }
    }
}

using System;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using RaceNotifier.Notifications;
using RaceNotifier.Settings;
using RaceNotifier.Telemetry;
using RaceNotifier.UI;
using SimHub.Plugins;

namespace RaceNotifier
{
    [PluginDescription("Send preset messages to Discord or a custom webhook when you press a bound wheel button.")]
    [PluginAuthor("John Ebersole")]
    [PluginName("Race Notifier")]
    public class RaceNotifierPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        public RaceNotifierSettings Settings;
        public NotificationDispatcher Dispatcher;

        // Latest telemetry for message-variable substitution. Written on the SimHub data thread,
        // read lock-free at send time; the value is an immutable snapshot swapped by reference.
        private volatile TelemetrySnapshot _telemetry = TelemetrySnapshot.Empty;

        /// <summary>Latest telemetry snapshot used to resolve message variables like {flag}.</summary>
        public TelemetrySnapshot CurrentTelemetry => _telemetry;

        public PluginManager PluginManager { get; set; }

        public ImageSource PictureIcon => UI.PluginIcon.Default;

        public string LeftMenuTitle => "Race Notifier";

        /// <summary>Bindable button slots pre-registered on a fresh install.</summary>
        public const int StartPool = 10;

        /// <summary>Extra slots registered each time the pool is exhausted at runtime.</summary>
        public const int GrowBy = 3;

        /// <summary>Highest action index actually registered via AddAction this session.</summary>
        public int HighestRegisteredActionIndex { get; private set; }

        /// <summary>True once a runtime AddAction was needed; the UI then shows a restart banner.</summary>
        public bool PendingRestart { get; private set; }

        /// <summary>
        /// SimHub prefixes every plugin action with the plugin's CLASS name, so the real bindable
        /// action names are "&lt;ActionPrefix&gt;.SendMessageN" (e.g. "RaceNotifierPlugin.SendMessage1").
        /// The settings UI must bind to exactly this, or the button press hits a non-existent action.
        /// </summary>
        public string ActionPrefix => GetType().Name;

        public void Init(PluginManager pluginManager)
        {
            SimHub.Logging.Current.Info("[RaceNotifier] Starting plugin v0.1.5a");

            Settings = this.ReadCommonSettings<RaceNotifierSettings>("GeneralSettings", () => new RaceNotifierSettings());
            Settings.EnsureInitialized();

            Dispatcher = new NotificationDispatcher(() => Settings, () => CurrentTelemetry);
            Dispatcher.OnSent = msg => this.TriggerEvent("MessageSent");
            Dispatcher.OnFailed = msg => this.TriggerEvent("MessageFailed");

            // Events you can bind to SimHub sounds / LEDs.
            this.AddEvent("MessageSent");
            this.AddEvent("MessageFailed");

            // Properties you can show on a dashboard.
            this.AttachDelegate("LastSendStatus", () => Dispatcher.LastStatus);
            this.AttachDelegate("LastSendMessage", () => Dispatcher.LastMessage);

            // Pre-register a contiguous pool of bindable actions "RaceNotifierPlugin.SendMessage<n>".
            // Start at StartPool (10) on a fresh install, otherwise keep GrowBy spares above the
            // highest used slot so adding a message rarely needs a SimHub restart.
            int ceiling = Math.Max(StartPool, Settings.MaxActionIndex() + GrowBy);
            for (int i = 1; i <= ceiling; i++)
                RegisterAction(i);
            HighestRegisteredActionIndex = ceiling;

            SimHub.Logging.Current.Info("[RaceNotifier] Registered " + ceiling + " bindable actions as '"
                + ActionPrefix + ".SendMessage1.." + ceiling + "'. " + Settings.Presets.Count
                + " message(s) configured, PluginEnabled=" + Settings.PluginEnabled
                + ". Bind a wheel button to one of those under Controls & Events; SimHub press types (Short/Long/Short-and-long) apply.");
        }

        /// <summary>
        /// Registers the bindable SimHub ACTION for one slot index. Fires the message on
        /// actionStart — the press-type-aware callback — so SimHub's native press types
        /// (Short / Long / Short-and-long, chosen per binding in Controls &amp; Events) are honored.
        /// actionEnd (release) is an intentional no-op.
        /// </summary>
        private void RegisterAction(int idx)
        {
            int captured = idx; // capture for the closure
            // SimHub ACTION (not an input mapping): actions are processed by SimHub's press-type
            // system, so a binding set to "Long press" fires only after the hold threshold, etc.
            // Fire on actionStart so the configured press type is honored; release is a no-op.
            // (Do NOT use the AddAction pressFallback overload — it fires on every plain press
            // regardless of the configured press type, which would defeat press-type filtering.)
            this.AddAction(
                actionName: "SendMessage" + captured,
                actionStart: (a, b) => Dispatcher.FireByActionIndex(captured, "press"),
                actionEnd: (a, b) => { });
        }

        /// <summary>
        /// Ensures an action exists for <paramref name="idx"/>. Returns true if it was already in the
        /// pre-registered pool (immediately bindable). If it overflows the pool, the gap is registered
        /// best-effort at runtime — which may not bind until a SimHub restart — so PendingRestart is set.
        /// Call on the UI thread (the settings panel does).
        /// </summary>
        public bool AddActionForIndex(int idx)
        {
            if (idx <= HighestRegisteredActionIndex)
                return true; // already pooled -> bindable now, no restart needed

            try
            {
                // Grow the pool to keep it contiguous: register up to idx, then GrowBy-1 spares beyond.
                int newCeiling = idx + (GrowBy - 1);
                for (int i = HighestRegisteredActionIndex + 1; i <= newCeiling; i++)
                    RegisterAction(i);
                HighestRegisteredActionIndex = newCeiling;
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Info("[RaceNotifier] Runtime AddAction failed for " + idx + ": " + ex);
            }

            PendingRestart = true;
            return false;
        }

        /// <summary>Asks SimHub to close and relaunch so newly added actions become bindable.</summary>
        public void RestartSimHub()
        {
            try { PluginManager?.RequestApplicationExit(true); }
            catch (Exception ex) { SimHub.Logging.Current.Info("[RaceNotifier] Restart request failed: " + ex); }
        }

        public void End(PluginManager pluginManager)
        {
            this.SaveCommonSettings("GeneralSettings", Settings);
            Dispatcher?.Dispose();
        }

        /// <summary>Persist settings immediately (called by the settings UI after edits).</summary>
        public void PersistSettings()
        {
            this.SaveCommonSettings("GeneralSettings", Settings);
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            // Capture only what the message variables need. Runs ~60x/s, so allocate a new snapshot
            // only when the value actually changes (the volatile swap is the thread-safe handoff).
            bool running = data.GameRunning && data.NewData != null;
            string flag = running ? (data.NewData.Flag_Name ?? "") : "";

            var current = _telemetry;
            if (current.GameRunning != running || current.FlagName != flag)
                _telemetry = new TelemetrySnapshot(running, flag);
        }

        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new SettingsControl(this);
        }
    }
}

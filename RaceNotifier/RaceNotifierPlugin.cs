using System;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using RaceNotifier.Notifications;
using RaceNotifier.Settings;
using RaceNotifier.UI;
using SimHub.Plugins;

namespace RaceNotifier
{
    [PluginDescription("Send preset Discord messages to your team when you press a bound wheel button. (Telegram coming in a later version.)")]
    [PluginAuthor("John Ebersole")]
    [PluginName("Race Notifier")]
    public class RaceNotifierPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        public RaceNotifierSettings Settings;
        public NotificationDispatcher Dispatcher;

        public PluginManager PluginManager { get; set; }

        public ImageSource PictureIcon => null;

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
            SimHub.Logging.Current.Info("[RaceNotifier] Starting plugin");

            Settings = this.ReadCommonSettings<RaceNotifierSettings>("GeneralSettings", () => new RaceNotifierSettings());
            Settings.EnsureInitialized();

            Dispatcher = new NotificationDispatcher(() => Settings);
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
                + ". Bind a wheel button to one of those actions with a 'Short press' / 'Pressed' press type.");
        }

        /// <summary>
        /// Registers the bindable SimHub action for one slot index. We handle BOTH press
        /// (actionStart) and release (actionEnd) so the message fires regardless of the press
        /// type SimHub/ControlMapper assigns to the binding (ShortPress, Pressed, Released,
        /// ShortAndLongPress, …). The per-message cooldown de-duplicates the press/release pair.
        /// </summary>
        private void RegisterAction(int idx)
        {
            int captured = idx; // capture for the closure
            this.AddAction(
                actionName: "SendMessage" + captured,
                actionStart: (a, b) => Dispatcher.FireByActionIndex(captured, "press"),
                actionEnd: (a, b) => Dispatcher.FireByActionIndex(captured, "release"));
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
            // Not used in v1. Reserved for telemetry-enriched messages in a later version.
        }

        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new SettingsControl(this);
        }
    }
}

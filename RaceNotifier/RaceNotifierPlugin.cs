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

            // Register one bindable action per slot -> "RaceNotifier.SendMessage1..10".
            for (int i = 1; i <= RaceNotifierSettings.SlotCount; i++)
            {
                int slot = i; // capture
                this.AddAction(
                    actionName: "SendMessage" + slot,
                    actionStart: (a, b) => Dispatcher.Fire(slot));
            }
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

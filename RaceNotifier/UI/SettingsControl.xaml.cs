using System;
using System.Windows;
using System.Windows.Controls;
using RaceNotifier.Settings;
using SimHub.Plugins.Styles;

namespace RaceNotifier.UI
{
    public partial class SettingsControl : UserControl
    {
        private readonly RaceNotifierPlugin _plugin;

        public SettingsControl()
        {
            InitializeComponent();
        }

        public SettingsControl(RaceNotifierPlugin plugin) : this()
        {
            _plugin = plugin;

            if (_plugin?.Settings != null)
            {
                _plugin.Settings.EnsureInitialized();
                SenderNameBox.Text = _plugin.Settings.SenderName ?? "";
                PrefixCheck.IsChecked = _plugin.Settings.PrefixSenderName;
            }

            SenderNameBox.TextChanged += (s, e) =>
            {
                if (_plugin?.Settings != null)
                {
                    _plugin.Settings.SenderName = SenderNameBox.Text;
                    Persist();
                }
            };
            PrefixCheck.Checked += (s, e) => SetPrefix(true);
            PrefixCheck.Unchecked += (s, e) => SetPrefix(false);

            AddDiscordButton.Click += (s, e) =>
            {
                _plugin.Settings.Destinations.Add(new Destination
                {
                    Name = "Discord " + (_plugin.Settings.Destinations.Count + 1),
                    Type = DestinationType.Discord
                });
                Persist();
                RebuildDestinations();
                RebuildMessages();
            };

            RebuildDestinations();
            RebuildMessages();
        }

        private void SetPrefix(bool value)
        {
            if (_plugin?.Settings != null)
            {
                _plugin.Settings.PrefixSenderName = value;
                Persist();
            }
        }

        private void Persist()
        {
            try { _plugin?.PersistSettings(); } catch { /* best effort */ }
        }

        // ---------- Destinations tab ----------

        private void RebuildDestinations()
        {
            DestinationsContainer.Children.Clear();
            if (_plugin?.Settings == null)
                return;

            if (_plugin.Settings.Destinations.Count == 0)
            {
                DestinationsContainer.Children.Add(new TextBlock
                {
                    Text = "No destinations yet. Click \"+ Add Discord destination\".",
                    Opacity = 0.8,
                    Margin = new Thickness(0, 4, 0, 0)
                });
                return;
            }

            foreach (var dest in _plugin.Settings.Destinations)
            {
                DestinationsContainer.Children.Add(BuildDestinationRow(dest));
            }
        }

        private UIElement BuildDestinationRow(Destination dest)
        {
            var sub = new SHSubSection { Title = dest.Name };
            var panel = new StackPanel { Margin = new Thickness(2) };

            panel.Children.Add(new TextBlock { Text = "Name", Margin = new Thickness(0, 0, 0, 2) });
            var nameBox = new TextBox { Text = dest.Name ?? "", Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
            nameBox.TextChanged += (s, e) =>
            {
                dest.Name = nameBox.Text;
                sub.Title = dest.Name;
                Persist();
            };
            panel.Children.Add(nameBox);

            panel.Children.Add(new TextBlock { Text = "Discord webhook URL", Margin = new Thickness(0, 8, 0, 2) });
            var urlBox = new TextBox { Text = dest.DiscordWebhookUrl ?? "", HorizontalAlignment = HorizontalAlignment.Stretch };
            urlBox.TextChanged += (s, e) =>
            {
                dest.DiscordWebhookUrl = urlBox.Text;
                Persist();
            };
            panel.Children.Add(urlBox);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };

            var testBtn = new SHButtonPrimary { Content = "Send test", Margin = new Thickness(0, 0, 8, 0) };
            testBtn.Click += (s, e) =>
            {
                _plugin.Dispatcher?.SendTest(dest, "Race Notifier test message");
            };
            buttons.Children.Add(testBtn);

            var removeBtn = new SHButtonSecondary { Content = "Remove" };
            removeBtn.Click += (s, e) =>
            {
                _plugin.Settings.Destinations.Remove(dest);
                // Drop this destination from any slot that targeted it.
                foreach (var slot in _plugin.Settings.Slots)
                    slot.TargetDestinationIds.Remove(dest.Id);
                Persist();
                RebuildDestinations();
                RebuildMessages();
            };
            buttons.Children.Add(removeBtn);

            panel.Children.Add(buttons);
            sub.Content = panel;
            return sub;
        }

        // ---------- Messages tab ----------

        private void RebuildMessages()
        {
            MessagesContainer.Children.Clear();
            if (_plugin?.Settings == null)
                return;

            for (int i = 0; i < _plugin.Settings.Slots.Count; i++)
            {
                int oneBased = i + 1;
                MessagesContainer.Children.Add(BuildMessageRow(oneBased, _plugin.Settings.Slots[i]));
            }
        }

        private UIElement BuildMessageRow(int slotNumber, MessageSlot slot)
        {
            var sub = new SHSubSection { Title = "Message " + slotNumber + "  (action: RaceNotifier.SendMessage" + slotNumber + ")" };
            var panel = new StackPanel { Margin = new Thickness(2) };

            var enabled = new SHToggleCheckbox { Content = "Enabled" };
            enabled.IsChecked = slot.Enabled;
            enabled.Checked += (s, e) => { slot.Enabled = true; Persist(); };
            enabled.Unchecked += (s, e) => { slot.Enabled = false; Persist(); };
            panel.Children.Add(enabled);

            panel.Children.Add(new TextBlock { Text = "Message text", Margin = new Thickness(0, 8, 0, 2) });
            var textBox = new TextBox { Text = slot.Text ?? "", HorizontalAlignment = HorizontalAlignment.Stretch };
            textBox.TextChanged += (s, e) => { slot.Text = textBox.Text; Persist(); };
            panel.Children.Add(textBox);

            // Cooldown
            var cdPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            cdPanel.Children.Add(new TextBlock { Text = "Cooldown (s):", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            var cdBox = new TextBox { Text = slot.CooldownSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture), Width = 60 };
            cdBox.TextChanged += (s, e) =>
            {
                if (double.TryParse(cdBox.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) && v >= 0)
                {
                    slot.CooldownSeconds = v;
                    Persist();
                }
            };
            cdPanel.Children.Add(cdBox);
            panel.Children.Add(cdPanel);

            // Destination selection
            panel.Children.Add(new TextBlock { Text = "Send to:", Margin = new Thickness(0, 8, 0, 2) });
            if (_plugin.Settings.Destinations.Count == 0)
            {
                panel.Children.Add(new TextBlock { Text = "(add a destination first)", Opacity = 0.8 });
            }
            else
            {
                foreach (var dest in _plugin.Settings.Destinations)
                {
                    var localDest = dest;
                    var cb = new CheckBox
                    {
                        Content = localDest.Name,
                        IsChecked = slot.TargetDestinationIds.Contains(localDest.Id),
                        Margin = new Thickness(0, 1, 0, 1)
                    };
                    cb.Checked += (s, e) =>
                    {
                        if (!slot.TargetDestinationIds.Contains(localDest.Id))
                            slot.TargetDestinationIds.Add(localDest.Id);
                        Persist();
                    };
                    cb.Unchecked += (s, e) =>
                    {
                        slot.TargetDestinationIds.Remove(localDest.Id);
                        Persist();
                    };
                    panel.Children.Add(cb);
                }
            }

            // In-panel button binding (best effort; falls back to a note if unavailable).
            panel.Children.Add(BuildBindingControl(slotNumber));

            // Send test for this slot
            var testBtn = new SHButtonPrimary { Content = "Send test", Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            testBtn.Click += (s, e) => SendSlotTest(slot);
            panel.Children.Add(testBtn);

            sub.Content = panel;
            return sub;
        }

        private UIElement BuildBindingControl(int slotNumber)
        {
            var holder = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            holder.Children.Add(new TextBlock { Text = "Bind button", Margin = new Thickness(0, 0, 0, 2) });
            try
            {
                var editor = new SimHub.Plugins.UI.ControlsEditor
                {
                    ActionName = "RaceNotifier.SendMessage" + slotNumber,
                    FriendlyName = "Send message " + slotNumber
                };
                holder.Children.Add(editor);
            }
            catch
            {
                holder.Children.Add(new TextBlock
                {
                    Text = "Bind action 'RaceNotifier.SendMessage" + slotNumber + "' under SimHub > Controls & Events.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.8
                });
            }
            return holder;
        }

        private void SendSlotTest(MessageSlot slot)
        {
            if (_plugin?.Dispatcher == null || _plugin.Settings == null)
                return;
            var text = string.IsNullOrWhiteSpace(slot.Text) ? "Race Notifier test" : slot.Text;
            foreach (var dest in _plugin.Settings.Destinations)
            {
                if (slot.TargetDestinationIds.Contains(dest.Id))
                    _plugin.Dispatcher.SendTest(dest, text);
            }
        }
    }
}

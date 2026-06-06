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
                PluginEnabledCheck.IsChecked = _plugin.Settings.PluginEnabled;
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

            PluginEnabledCheck.Checked += (s, e) => SetPluginEnabled(true);
            PluginEnabledCheck.Unchecked += (s, e) => SetPluginEnabled(false);

            AddMessageButton.Click += (s, e) => AddPreset();
            RestartButton.Click += (s, e) => ConfirmRestart();
            BannerRestartButton.Click += (s, e) => ConfirmRestart();

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

            UpdateRestartBanner();
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

        private void SetPluginEnabled(bool value)
        {
            if (_plugin?.Settings != null)
            {
                _plugin.Settings.PluginEnabled = value;
                Persist();
            }
        }

        private void Persist()
        {
            try { _plugin?.PersistSettings(); } catch { /* best effort */ }
        }

        private void UpdateRestartBanner()
        {
            RestartBanner.Visibility = (_plugin?.PendingRestart == true)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ConfirmRestart()
        {
            var r = MessageBox.Show(
                "Restart SimHub now? Any running session or recording will be interrupted.",
                "Restart SimHub",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes)
                _plugin?.RestartSimHub();
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
                // Drop this destination from any preset that targeted it.
                foreach (var preset in _plugin.Settings.Presets)
                    preset.TargetDestinationIds.Remove(dest.Id);
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

        private void AddPreset()
        {
            if (_plugin?.Settings == null)
                return;

            int idx = _plugin.Settings.NextFreeActionIndex();
            _plugin.Settings.Presets.Add(new Preset { ActionIndex = idx });

            // Make sure a bindable action exists for this slot. No-op if already pooled;
            // sets PendingRestart (and shows the banner) only when it overflows the pool.
            bool bindableNow = _plugin.AddActionForIndex(idx);

            Persist();
            UpdateRestartBanner();
            RebuildMessages();

            if (!bindableNow)
                SimHub.Logging.Current.Info("[RaceNotifier] Message " + idx + " added past the pre-registered pool; restart pending.");
        }

        private void RemovePreset(Preset preset)
        {
            if (_plugin?.Settings == null)
                return;
            _plugin.Settings.Presets.Remove(preset);
            // The action stays registered; its index is reusable by the next AddPreset.
            Persist();
            RebuildMessages();
        }

        private void RebuildMessages()
        {
            MessagesContainer.Children.Clear();
            if (_plugin?.Settings == null)
                return;

            if (_plugin.Settings.Presets.Count == 0)
            {
                MessagesContainer.Children.Add(new TextBlock
                {
                    Text = "No messages yet. Click \"+ Add message\".",
                    Opacity = 0.8,
                    Margin = new Thickness(0, 4, 0, 0)
                });
                return;
            }

            foreach (var preset in _plugin.Settings.Presets)
                MessagesContainer.Children.Add(BuildMessageRow(preset));
        }

        private static string RowTitle(Preset preset)
        {
            string label = string.IsNullOrWhiteSpace(preset.Name) ? ("Message " + preset.ActionIndex) : preset.Name;
            return label + "  (action: RaceNotifier.SendMessage" + preset.ActionIndex + ")";
        }

        private UIElement BuildMessageRow(Preset preset)
        {
            int idx = preset.ActionIndex;
            var sub = new SHSubSection { Title = RowTitle(preset) };
            var panel = new StackPanel { Margin = new Thickness(2) };

            // Friendly name (optional)
            panel.Children.Add(new TextBlock { Text = "Name (optional)", Margin = new Thickness(0, 0, 0, 2) });
            var nameBox = new TextBox { Text = preset.Name ?? "", Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
            nameBox.TextChanged += (s, e) =>
            {
                preset.Name = nameBox.Text;
                sub.Title = RowTitle(preset);
                Persist();
            };
            panel.Children.Add(nameBox);

            var enabled = new SHToggleCheckbox { Content = "Enabled", Margin = new Thickness(0, 8, 0, 0) };
            enabled.IsChecked = preset.Enabled;
            enabled.Checked += (s, e) => { preset.Enabled = true; Persist(); };
            enabled.Unchecked += (s, e) => { preset.Enabled = false; Persist(); };
            panel.Children.Add(enabled);

            panel.Children.Add(new TextBlock { Text = "Message text", Margin = new Thickness(0, 8, 0, 2) });
            var textBox = new TextBox { Text = preset.Text ?? "", HorizontalAlignment = HorizontalAlignment.Stretch };
            textBox.TextChanged += (s, e) => { preset.Text = textBox.Text; Persist(); };
            panel.Children.Add(textBox);

            // Cooldown
            var cdPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            cdPanel.Children.Add(new TextBlock { Text = "Cooldown (s):", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            var cdBox = new TextBox { Text = preset.CooldownSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture), Width = 60 };
            cdBox.TextChanged += (s, e) =>
            {
                if (double.TryParse(cdBox.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) && v >= 0)
                {
                    preset.CooldownSeconds = v;
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
                        IsChecked = preset.TargetDestinationIds.Contains(localDest.Id),
                        Margin = new Thickness(0, 1, 0, 1)
                    };
                    cb.Checked += (s, e) =>
                    {
                        if (!preset.TargetDestinationIds.Contains(localDest.Id))
                            preset.TargetDestinationIds.Add(localDest.Id);
                        Persist();
                    };
                    cb.Unchecked += (s, e) =>
                    {
                        preset.TargetDestinationIds.Remove(localDest.Id);
                        Persist();
                    };
                    panel.Children.Add(cb);
                }
            }

            // In-panel button binding (best effort; falls back to a note if unavailable).
            panel.Children.Add(BuildBindingControl(idx));

            // Action buttons: Send test + Remove
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };

            var testBtn = new SHButtonPrimary { Content = "Send test", Margin = new Thickness(0, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Left };
            testBtn.Click += (s, e) => SendPresetTest(preset);
            btnRow.Children.Add(testBtn);

            var removeBtn = new SHButtonSecondary { Content = "Remove" };
            removeBtn.Click += (s, e) => RemovePreset(preset);
            btnRow.Children.Add(removeBtn);

            panel.Children.Add(btnRow);

            sub.Content = panel;
            return sub;
        }

        private UIElement BuildBindingControl(int idx)
        {
            var holder = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            holder.Children.Add(new TextBlock { Text = "Bind button", Margin = new Thickness(0, 0, 0, 2) });
            try
            {
                var editor = new SimHub.Plugins.UI.ControlsEditor
                {
                    ActionName = "RaceNotifier.SendMessage" + idx,
                    FriendlyName = "Send message " + idx
                };
                holder.Children.Add(editor);
            }
            catch
            {
                holder.Children.Add(new TextBlock
                {
                    Text = "Bind action 'RaceNotifier.SendMessage" + idx + "' under SimHub > Controls & Events.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.8
                });
            }
            return holder;
        }

        private void SendPresetTest(Preset preset)
        {
            if (_plugin?.Dispatcher == null || _plugin.Settings == null)
                return;
            var text = string.IsNullOrWhiteSpace(preset.Text) ? "Race Notifier test" : preset.Text;
            foreach (var dest in _plugin.Settings.Destinations)
            {
                if (preset.TargetDestinationIds.Contains(dest.Id))
                    _plugin.Dispatcher.SendTest(dest, text);
            }
        }
    }
}

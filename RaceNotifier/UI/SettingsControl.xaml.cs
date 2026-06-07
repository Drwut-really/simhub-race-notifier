using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RaceNotifier.Settings;
using SimHub.Plugins.Styles;

namespace RaceNotifier.UI
{
    public partial class SettingsControl : UserControl
    {
        private readonly RaceNotifierPlugin _plugin;

        // ActionIndexes whose rows are currently expanded; survives RebuildMessages().
        private readonly HashSet<int> _expanded = new HashSet<int>();

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

            AddDestinationButton.Click += (s, e) =>
            {
                var type = DestTypeCombo.SelectedIndex == 1
                    ? DestinationType.CustomWebhook
                    : DestinationType.Discord;
                string baseName = type == DestinationType.CustomWebhook ? "Custom webhook " : "Discord ";
                _plugin.Settings.Destinations.Add(new Destination
                {
                    Name = baseName + (_plugin.Settings.Destinations.Count + 1),
                    Type = type
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
                    Text = "No destinations yet. Pick a type and click \"+ Add destination\".",
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

            bool isWebhook = dest.Type == DestinationType.CustomWebhook;

            // Type label so the user can tell Discord vs custom-webhook rows apart.
            panel.Children.Add(new TextBlock
            {
                Text = isWebhook ? "Type: Custom webhook" : "Type: Discord",
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 6)
            });

            panel.Children.Add(new TextBlock
            {
                Text = isWebhook ? "Webhook URL" : "Discord webhook URL",
                Margin = new Thickness(0, 8, 0, 2)
            });
            var urlBox = new TextBox
            {
                Text = (isWebhook ? dest.WebhookUrl : dest.DiscordWebhookUrl) ?? "",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            panel.Children.Add(urlBox);

            // Live warning when this destination has no usable URL for its type.
            var urlWarn = new TextBlock
            {
                Text = "⚠ No URL — this destination can't send.",
                Foreground = Brushes.Orange,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = dest.HasUsableTarget ? Visibility.Collapsed : Visibility.Visible
            };
            panel.Children.Add(urlWarn);

            urlBox.TextChanged += (s, e) =>
            {
                if (isWebhook) dest.WebhookUrl = urlBox.Text;
                else dest.DiscordWebhookUrl = urlBox.Text;
                urlWarn.Visibility = dest.HasUsableTarget ? Visibility.Collapsed : Visibility.Visible;
                Persist();
                RebuildMessages(); // refresh type-aware message warnings (Messages tab; no focus theft)
            };

            // Body-format picker — custom webhook only.
            if (isWebhook)
            {
                panel.Children.Add(new TextBlock { Text = "Body format", Margin = new Thickness(0, 8, 0, 2) });
                var fmt = new ComboBox { Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
                fmt.Items.Add("JSON ({\"content\": message})");
                fmt.Items.Add("Plain text");
                fmt.SelectedIndex = dest.WebhookBodyFormat == WebhookBodyFormat.PlainText ? 1 : 0;
                fmt.SelectionChanged += (s, e) =>
                {
                    dest.WebhookBodyFormat = fmt.SelectedIndex == 1
                        ? WebhookBodyFormat.PlainText
                        : WebhookBodyFormat.Json;
                    Persist();
                };
                panel.Children.Add(fmt);
            }

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
            _expanded.Add(idx); // open the new row for immediate editing

            // Ensure a bindable action exists; sets PendingRestart only on pool overflow.
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

            string label = string.IsNullOrWhiteSpace(preset.Name) ? ("Message " + preset.ActionIndex) : preset.Name;
            var r = MessageBox.Show(
                "Remove \"" + label + "\"? This can't be undone.",
                "Remove message",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes)
                return;

            _plugin.Settings.Presets.Remove(preset);
            _expanded.Remove(preset.ActionIndex);
            // The action stays registered; its index is reusable by the next AddPreset.
            Persist();
            RebuildMessages();
        }

        private void MovePreset(Preset preset, int delta)
        {
            if (_plugin?.Settings == null)
                return;
            var list = _plugin.Settings.Presets;
            int i = list.IndexOf(preset);
            int j = i + delta;
            if (i < 0 || j < 0 || j >= list.Count)
                return;
            list.RemoveAt(i);
            list.Insert(j, preset);
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

        private static string TitleFor(Preset preset)
        {
            return string.IsNullOrWhiteSpace(preset.Name) ? ("Message " + preset.ActionIndex) : preset.Name;
        }

        /// <summary>
        /// A borderless, clickable reorder arrow. Rendered as a plain TextBlock (which gets WPF
        /// font fallback, unlike the SimHub button font) so the arrow glyph shows crisp with no
        /// button background. Greyed and inert when <paramref name="enabled"/> is false.
        /// </summary>
        private UIElement MakeReorderArrow(string glyph, string tip, bool enabled, Action onClick)
        {
            var tb = new TextBlock
            {
                Text = glyph,
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = enabled ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                // Transparent (not null) so the whole bounds is hit-testable, not just the glyph pixels.
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0),
                Padding = new Thickness(4, 2, 4, 2),
                ToolTip = tip
            };
            if (enabled)
            {
                tb.Cursor = System.Windows.Input.Cursors.Hand;
                tb.MouseLeftButtonUp += (s, e) => onClick();
            }
            return tb;
        }

        private UIElement BuildMessageRow(Preset preset)
        {
            int idx = preset.ActionIndex;

            // Outer grid: [Enabled] [Expander(title -> body)] [Up] [Down]
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // --- Header (inside expander): title + action name + warning glyph ---
            var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
            var titleText = new TextBlock
            {
                Text = TitleFor(preset),
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerSp.Children.Add(titleText);
            headerSp.Children.Add(new TextBlock
            {
                Text = "  (" + _plugin.ActionPrefix + ".SendMessage" + idx + ")",
                Opacity = 0.6,
                VerticalAlignment = VerticalAlignment.Center
            });
            var headerWarn = new TextBlock
            {
                Text = "  ⚠",
                Foreground = Brushes.Orange,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            headerSp.Children.Add(headerWarn);

            // --- Body (expander content) ---
            var body = new StackPanel { Margin = new Thickness(2, 6, 2, 2) };

            var bodyWarn = new TextBlock
            {
                Foreground = Brushes.Orange,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6),
                Visibility = Visibility.Collapsed
            };
            body.Children.Add(bodyWarn);

            // Live warning refresh (header glyph + body line), no full rebuild.
            Action refreshWarnings = () =>
            {
                var reason = PresetValidation.Describe(preset, _plugin.Settings.Destinations);
                if (reason == null)
                {
                    headerWarn.Visibility = Visibility.Collapsed;
                    bodyWarn.Visibility = Visibility.Collapsed;
                }
                else
                {
                    headerWarn.Visibility = Visibility.Visible;
                    headerWarn.ToolTip = reason;
                    bodyWarn.Text = "⚠ " + reason;
                    bodyWarn.Visibility = Visibility.Visible;
                }
            };

            // Name (optional)
            body.Children.Add(new TextBlock { Text = "Name (optional)", Margin = new Thickness(0, 0, 0, 2) });
            var nameBox = new TextBox { Text = preset.Name ?? "", Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
            nameBox.TextChanged += (s, e) =>
            {
                preset.Name = nameBox.Text;
                titleText.Text = TitleFor(preset); // update header in place (no rebuild, keeps focus)
                Persist();
            };
            body.Children.Add(nameBox);

            // Message text
            body.Children.Add(new TextBlock { Text = "Message text", Margin = new Thickness(0, 8, 0, 2) });
            var textBox = new TextBox { Text = preset.Text ?? "", HorizontalAlignment = HorizontalAlignment.Stretch };
            textBox.TextChanged += (s, e) =>
            {
                preset.Text = textBox.Text;
                Persist();
                refreshWarnings();
            };
            body.Children.Add(textBox);

            // Variable hint — discoverable list of supported {tokens}.
            body.Children.Add(new TextBlock
            {
                Text = "Variables: {flag} — current track flag (e.g. Yellow, Green, Checkered; \"none\" when clear).",
                Opacity = 0.8,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });

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
            body.Children.Add(cdPanel);

            // Destination selection
            body.Children.Add(new TextBlock { Text = "Send to:", Margin = new Thickness(0, 8, 0, 2) });
            if (_plugin.Settings.Destinations.Count == 0)
            {
                body.Children.Add(new TextBlock { Text = "(add a destination first)", Opacity = 0.8 });
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
                        refreshWarnings();
                    };
                    cb.Unchecked += (s, e) =>
                    {
                        preset.TargetDestinationIds.Remove(localDest.Id);
                        Persist();
                        refreshWarnings();
                    };
                    body.Children.Add(cb);
                }
            }

            // In-panel button binding
            body.Children.Add(BuildBindingControl(idx));

            // Send test + Remove
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var testBtn = new SHButtonPrimary { Content = "Send test", Margin = new Thickness(0, 0, 8, 0) };
            testBtn.Click += (s, e) => SendPresetTest(preset);
            btnRow.Children.Add(testBtn);
            var removeBtn = new SHButtonSecondary { Content = "Remove" };
            removeBtn.Click += (s, e) => RemovePreset(preset);
            btnRow.Children.Add(removeBtn);
            body.Children.Add(btnRow);

            // Set initial warning state.
            refreshWarnings();

            // --- Expander wrapping header + body ---
            var expander = new SHExpander
            {
                IsExpanded = _expanded.Contains(idx),
                Header = headerSp,
                Content = body,
                VerticalAlignment = VerticalAlignment.Center
            };
            expander.Expanded += (s, e) => _expanded.Add(idx);
            expander.Collapsed += (s, e) => _expanded.Remove(idx);
            Grid.SetColumn(expander, 1);
            grid.Children.Add(expander);

            // --- Col 0: Enabled toggle (always visible, outside the expander header) ---
            var enabled = new SHToggleCheckbox
            {
                IsChecked = preset.Enabled,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 6, 0)
            };
            enabled.Checked += (s, e) => { preset.Enabled = true; Persist(); };
            enabled.Unchecked += (s, e) => { preset.Enabled = false; Persist(); };
            Grid.SetColumn(enabled, 0);
            grid.Children.Add(enabled);

            // --- Col 2/3: reorder buttons ---
            int pos = _plugin.Settings.Presets.IndexOf(preset);
            int count = _plugin.Settings.Presets.Count;

            var up = MakeReorderArrow("↑", "Move up", pos > 0, () => MovePreset(preset, -1));
            var down = MakeReorderArrow("↓", "Move down", pos < count - 1, () => MovePreset(preset, +1));
            Grid.SetColumn(up, 2);
            Grid.SetColumn(down, 3);
            grid.Children.Add(up);
            grid.Children.Add(down);

            return grid;
        }

        private UIElement BuildBindingControl(int idx)
        {
            var holder = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            holder.Children.Add(new TextBlock { Text = "Bind button", Margin = new Thickness(0, 0, 0, 2) });
            try
            {
                var editor = new SimHub.Plugins.UI.ControlsEditor
                {
                    ActionName = _plugin.ActionPrefix + ".SendMessage" + idx,
                    FriendlyName = "Send message " + idx
                };
                holder.Children.Add(editor);
            }
            catch
            {
                holder.Children.Add(new TextBlock
                {
                    Text = "Bind action '" + _plugin.ActionPrefix + ".SendMessage" + idx + "' under SimHub > Controls & Events.",
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

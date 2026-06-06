# Messages Page Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Messages tab compact and manageable as the message list grows — collapsible rows, ↑/↓ reordering, and advisory validation warnings.

**Architecture:** UI-only change, almost entirely in `UI/SettingsControl.xaml.cs`. Each message renders inside a `Grid` whose middle column is an `SHExpander` (title + editor body) and whose outer columns hold the always-visible **Enabled** toggle and **↑/↓** reorder buttons — keeping interactive controls *outside* the expander's clickable header to avoid toggle conflicts. A new pure helper `Settings/PresetValidation.cs` computes per-message warnings.

**Tech Stack:** C#/.NET Framework 4.8, WPF, SimHub `SimHub.Plugins.Styles` controls (`SHExpander`, `SHToggleCheckbox`, `SHButtonPrimary/Secondary`), MSBuild.

**Testing note:** No automated test harness exists and the UI runs inside SimHub. Each task is gated by: **MSBuild Release succeeds** (post-build XCOPY may fail with a sharing violation while SimHub runs — that's expected) → deploy (close `SimHubWPF.exe`, copy `bin\Release\RaceNotifier.dll`+`.pdb` to `C:\Program Files (x86)\SimHub\`, relaunch) → **observe the stated behavior**. The one piece of pure logic (validation) is isolated in `PresetValidation` so it reads clearly and could be unit-tested later.

**Reusable deploy snippet (PowerShell)** — used in every task's verify step:
```powershell
$p = Get-Process SimHubWPF -ErrorAction SilentlyContinue
if ($p) { $p.CloseMainWindow() | Out-Null; if (-not $p.WaitForExit(8000)) { Stop-Process -Id $p.Id -Force } }
Copy-Item "C:\Users\drwut\projects\simhub-race-notifier\RaceNotifier\bin\Release\RaceNotifier.dll" "C:\Program Files (x86)\SimHub\" -Force
Copy-Item "C:\Users\drwut\projects\simhub-race-notifier\RaceNotifier\bin\Release\RaceNotifier.pdb" "C:\Program Files (x86)\SimHub\" -Force
Start-Process "C:\Program Files (x86)\SimHub\SimHubWPF.exe" -WorkingDirectory "C:\Program Files (x86)\SimHub"
```

**Build command** — used in every task:
```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" "C:\Users\drwut\projects\simhub-race-notifier\RaceNotifier.sln" /p:Configuration=Release /v:minimal /nologo
```

---

## Task 1: Pure validation helper

**Files:**
- Create: `RaceNotifier/Settings/PresetValidation.cs`
- Modify: `RaceNotifier/RaceNotifier.csproj` (add `<Compile Include>`)

- [ ] **Step 1: Create the helper**

`RaceNotifier/Settings/PresetValidation.cs`:
```csharp
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
                .Where(d => preset.TargetDestinationIds.Contains(d.Id))
                .ToList();

            if (selected.Count == 0)
                return "No destination selected.";

            if (selected.All(d => string.IsNullOrWhiteSpace(d.DiscordWebhookUrl)))
                return "Selected destination has no webhook URL.";

            return null;
        }

        public static bool IsOk(Preset preset, IList<Destination> destinations)
            => Describe(preset, destinations) == null;
    }
}
```

- [ ] **Step 2: Register it in the project**

In `RaceNotifier/RaceNotifier.csproj`, add next to the other `Settings\*` includes:
```xml
    <Compile Include="Settings\PresetValidation.cs" />
```

- [ ] **Step 3: Build**

Run the **Build command**. Expected: `RaceNotifier -> ...bin\Release\RaceNotifier.dll`, no compile errors. (A post-build `Sharing violation` line is OK.)

- [ ] **Step 4: Commit**

```bash
git add RaceNotifier/Settings/PresetValidation.cs RaceNotifier/RaceNotifier.csproj
git commit -m "Add pure PresetValidation helper for message warnings"
```

---

## Task 2: Collapsible message rows via SHExpander

Replace the per-message `SHSubSection` with a `Grid` layout: `[Enabled] [SHExpander(title→body)] [↑] [↓]`. This task wires the **expander + body** and the **collapsed-by-default** behavior; Enabled/↑/↓ get their real handlers in later tasks (placeholders here so the layout compiles and renders).

**Files:**
- Modify: `RaceNotifier/UI/SettingsControl.xaml.cs` — replace `BuildMessageRow(Preset)` and add helpers.

- [ ] **Step 1: Add a `using` for the SimHub UI namespace already present**

No new using needed — `SimHub.Plugins.Styles` is already imported. Confirm `SHExpander` resolves (namespace `SimHub.Plugins.Styles`).

- [ ] **Step 2: Replace `BuildMessageRow` with the Grid + expander version**

Replace the entire existing `BuildMessageRow(Preset preset)` method body with:
```csharp
private UIElement BuildMessageRow(Preset preset)
{
    int idx = preset.ActionIndex;

    var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // enabled
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // expander
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // up
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // down

    // Col 0: Enabled toggle (real handler added in Task 4)
    var enabled = new SHToggleCheckbox { IsChecked = preset.Enabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
    Grid.SetColumn(enabled, 0);
    grid.Children.Add(enabled);

    // Col 1: Expander — Header is the title, Content is the editor body
    var expander = new SHExpander
    {
        IsExpanded = false,
        Header = BuildRowHeader(preset),
        Content = BuildRowBody(preset)
    };
    Grid.SetColumn(expander, 1);
    grid.Children.Add(expander);

    // Col 2/3: reorder buttons (real handlers added in Task 5)
    var up = new SHButtonSecondary { Content = "↑", Width = 32, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    var down = new SHButtonSecondary { Content = "↓", Width = 32, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    Grid.SetColumn(up, 2);
    Grid.SetColumn(down, 3);
    grid.Children.Add(up);
    grid.Children.Add(down);

    return grid;
}

private UIElement BuildRowHeader(Preset preset)
{
    int idx = preset.ActionIndex;
    var sp = new StackPanel { Orientation = Orientation.Horizontal };
    sp.Children.Add(new TextBlock
    {
        Text = string.IsNullOrWhiteSpace(preset.Name) ? ("Message " + idx) : preset.Name,
        FontWeight = FontWeights.Bold,
        VerticalAlignment = VerticalAlignment.Center
    });
    sp.Children.Add(new TextBlock
    {
        Text = "  (RaceNotifier.SendMessage" + idx + ")",
        Opacity = 0.6,
        VerticalAlignment = VerticalAlignment.Center
    });
    return sp;
}

private UIElement BuildRowBody(Preset preset)
{
    int idx = preset.ActionIndex;
    var panel = new StackPanel { Margin = new Thickness(2, 6, 2, 2) };

    // Name (optional)
    panel.Children.Add(new TextBlock { Text = "Name (optional)", Margin = new Thickness(0, 0, 0, 2) });
    var nameBox = new TextBox { Text = preset.Name ?? "", Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
    nameBox.TextChanged += (s, e) => { preset.Name = nameBox.Text; Persist(); RebuildMessages(); };
    panel.Children.Add(nameBox);

    // Message text
    panel.Children.Add(new TextBlock { Text = "Message text", Margin = new Thickness(0, 8, 0, 2) });
    var textBox = new TextBox { Text = preset.Text ?? "", HorizontalAlignment = HorizontalAlignment.Stretch };
    textBox.TextChanged += (s, e) => { preset.Text = textBox.Text; Persist(); RebuildMessages(); };
    panel.Children.Add(textBox);

    // Cooldown
    var cdPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
    cdPanel.Children.Add(new TextBlock { Text = "Cooldown (s):", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
    var cdBox = new TextBox { Text = preset.CooldownSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture), Width = 60 };
    cdBox.TextChanged += (s, e) =>
    {
        if (double.TryParse(cdBox.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) && v >= 0)
        { preset.CooldownSeconds = v; Persist(); }
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
                Persist(); RebuildMessages();
            };
            cb.Unchecked += (s, e) => { preset.TargetDestinationIds.Remove(localDest.Id); Persist(); RebuildMessages(); };
            panel.Children.Add(cb);
        }
    }

    // Bind button
    panel.Children.Add(BuildBindingControl(idx));

    // Send test + Remove (Remove confirm added in Task 8)
    var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
    var testBtn = new SHButtonPrimary { Content = "Send test", Margin = new Thickness(0, 0, 8, 0) };
    testBtn.Click += (s, e) => SendPresetTest(preset);
    btnRow.Children.Add(testBtn);
    var removeBtn = new SHButtonSecondary { Content = "Remove" };
    removeBtn.Click += (s, e) => RemovePreset(preset);
    btnRow.Children.Add(removeBtn);
    panel.Children.Add(btnRow);

    return panel;
}
```

(`BuildBindingControl`, `SendPresetTest`, `RemovePreset`, `Persist`, `RebuildMessages` already exist from the current file. The old inline `BuildMessageRow` content is now split across `BuildRowHeader`/`BuildRowBody`.)

- [ ] **Step 3: Add the missing `using` for FontWeights**

At the top of `SettingsControl.xaml.cs`, ensure `using System.Windows;` is present (it is — provides `FontWeights`, `Thickness`, `Visibility`). No change needed; confirm.

- [ ] **Step 4: Build**

Run the **Build command**. Expected: builds clean, no errors about `SHExpander`/`Grid`/`FontWeights`.

- [ ] **Step 5: Deploy & observe**

Run the **deploy snippet**. In SimHub → Race Notifier → Messages: each message is now a single collapsed row showing its title + action name, with an Enabled toggle on the left and ↑/↓ on the right. Clicking the row's title/chevron expands to reveal the editor. Add a message → it appears (expanded-on-add comes in Task 3).

- [ ] **Step 6: Commit**

```bash
git add RaceNotifier/UI/SettingsControl.xaml.cs
git commit -m "Collapsible message rows using SHExpander + Grid layout"
```

---

## Task 3: Preserve expand state + expand newly added message

**Files:**
- Modify: `RaceNotifier/UI/SettingsControl.xaml.cs`

- [ ] **Step 1: Add expand-state tracking field**

Near the top of the class (after `private readonly RaceNotifierPlugin _plugin;`):
```csharp
// ActionIndexes whose rows are currently expanded; survives RebuildMessages().
private readonly System.Collections.Generic.HashSet<int> _expanded = new System.Collections.Generic.HashSet<int>();
```

- [ ] **Step 2: Drive the expander from `_expanded` and write back on toggle**

In `BuildMessageRow`, change the expander construction to:
```csharp
    var expander = new SHExpander
    {
        IsExpanded = _expanded.Contains(idx),
        Header = BuildRowHeader(preset),
        Content = BuildRowBody(preset)
    };
    expander.Expanded += (s, e) => _expanded.Add(idx);
    expander.Collapsed += (s, e) => _expanded.Remove(idx);
```

- [ ] **Step 3: Expand a newly added message**

In `AddPreset()`, after `_plugin.Settings.Presets.Add(new Preset { ActionIndex = idx });` add:
```csharp
    _expanded.Add(idx);
```
(Place it before `RebuildMessages();`.)

- [ ] **Step 4: Build**

Run the **Build command**. Expected: clean build.

- [ ] **Step 5: Deploy & observe**

Run the **deploy snippet**. Verify: expand a row, edit its text (rebuild happens) → row stays expanded. Collapse it → stays collapsed across further edits. Click **+ Add message** → the new row appears expanded; existing rows keep their state.

- [ ] **Step 6: Commit**

```bash
git add RaceNotifier/UI/SettingsControl.xaml.cs
git commit -m "Preserve row expand state across rebuilds; expand new messages"
```

---

## Task 4: Wire the header Enabled toggle

**Files:**
- Modify: `RaceNotifier/UI/SettingsControl.xaml.cs`

- [ ] **Step 1: Attach handlers to the `enabled` toggle in `BuildMessageRow`**

Replace the `enabled` creation block with:
```csharp
    var enabled = new SHToggleCheckbox { IsChecked = preset.Enabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
    enabled.Checked += (s, e) => { preset.Enabled = true; Persist(); RebuildMessages(); };
    enabled.Unchecked += (s, e) => { preset.Enabled = false; Persist(); RebuildMessages(); };
    Grid.SetColumn(enabled, 0);
    grid.Children.Add(enabled);
```

- [ ] **Step 2: Build**

Run the **Build command**. Expected: clean build.

- [ ] **Step 3: Deploy & observe**

Run the **deploy snippet**. Toggle a row's Enabled directly from the collapsed header (no expand needed). Confirm it does NOT expand/collapse the row, and the state persists after reopening the settings page.

- [ ] **Step 4: Commit**

```bash
git add RaceNotifier/UI/SettingsControl.xaml.cs
git commit -m "Enable/disable a message from its collapsed header"
```

---

## Task 5: Reorder with ↑ / ↓

**Files:**
- Modify: `RaceNotifier/UI/SettingsControl.xaml.cs`

- [ ] **Step 1: Add `MovePreset` helper**

Add to the class:
```csharp
private void MovePreset(Preset preset, int delta)
{
    if (_plugin?.Settings == null) return;
    var list = _plugin.Settings.Presets;
    int i = list.IndexOf(preset);
    int j = i + delta;
    if (i < 0 || j < 0 || j >= list.Count) return;
    list.RemoveAt(i);
    list.Insert(j, preset);
    Persist();
    RebuildMessages();
}
```

- [ ] **Step 2: Wire ↑/↓ and disable at the ends**

Replace the reorder-button block in `BuildMessageRow` with:
```csharp
    int pos = _plugin.Settings.Presets.IndexOf(preset);
    int count = _plugin.Settings.Presets.Count;

    var up = new SHButtonSecondary { Content = "↑", Width = 32, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsEnabled = pos > 0 };
    up.Click += (s, e) => MovePreset(preset, -1);
    var down = new SHButtonSecondary { Content = "↓", Width = 32, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsEnabled = pos < count - 1 };
    down.Click += (s, e) => MovePreset(preset, +1);
    Grid.SetColumn(up, 2);
    Grid.SetColumn(down, 3);
    grid.Children.Add(up);
    grid.Children.Add(down);
```

- [ ] **Step 3: Build**

Run the **Build command**. Expected: clean build.

- [ ] **Step 4: Deploy & observe**

Run the **deploy snippet**. With ≥2 messages: ↑/↓ move a row one position; the top row's ↑ and bottom row's ↓ are disabled. Reorder, reopen settings → order persists. Confirm a moved message's **Bind button** still shows its binding and the action name is unchanged (order is cosmetic).

- [ ] **Step 5: Commit**

```bash
git add RaceNotifier/UI/SettingsControl.xaml.cs
git commit -m "Reorder messages with up/down buttons"
```

---

## Task 6: Validation warning in header + body

**Files:**
- Modify: `RaceNotifier/UI/SettingsControl.xaml.cs`

- [ ] **Step 1: Show ⚠ in the header when invalid**

In `BuildRowHeader`, before `return sp;`, add:
```csharp
    var warn = PresetValidation.Describe(preset, _plugin.Settings.Destinations);
    if (warn != null)
    {
        sp.Children.Add(new TextBlock
        {
            Text = "  ⚠",
            Foreground = System.Windows.Media.Brushes.Orange,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = warn
        });
    }
```

- [ ] **Step 2: Show the reason inside the body**

In `BuildRowBody`, at the very start (right after `var panel = ...`), add:
```csharp
    var warn = PresetValidation.Describe(preset, _plugin.Settings.Destinations);
    if (warn != null)
    {
        panel.Children.Add(new TextBlock
        {
            Text = "⚠ " + warn,
            Foreground = System.Windows.Media.Brushes.Orange,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        });
    }
```

- [ ] **Step 3: Build**

Run the **Build command**. Expected: clean build. (`PresetValidation` is in the same `RaceNotifier.Settings` namespace already imported via `using RaceNotifier.Settings;`.)

- [ ] **Step 4: Deploy & observe**

Run the **deploy snippet**. Create a message with blank text → ⚠ shows in its collapsed header (tooltip "No message text.") and as a line in the body. Add text but no destination → "No destination selected." Select a destination whose webhook is blank → "Selected destination has no webhook URL." Fill everything in → ⚠ disappears.

- [ ] **Step 5: Commit**

```bash
git add RaceNotifier/UI/SettingsControl.xaml.cs
git commit -m "Advisory validation warnings on message rows"
```

---

## Task 7: Blank-webhook warning on destinations

**Files:**
- Modify: `RaceNotifier/UI/SettingsControl.xaml.cs`

- [ ] **Step 1: Add a warning when a destination has no webhook**

In `BuildDestinationRow`, immediately after the `urlBox` is added to `panel` (after `panel.Children.Add(urlBox);`), insert a live warning that updates as the URL changes:
```csharp
    var urlWarn = new TextBlock
    {
        Text = "⚠ No webhook URL — this destination can't send.",
        Foreground = System.Windows.Media.Brushes.Orange,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 4, 0, 0),
        Visibility = string.IsNullOrWhiteSpace(dest.DiscordWebhookUrl) ? Visibility.Visible : Visibility.Collapsed
    };
    panel.Children.Add(urlWarn);
```
Then, inside the existing `urlBox.TextChanged` handler, after `dest.DiscordWebhookUrl = urlBox.Text;`, add:
```csharp
        urlWarn.Visibility = string.IsNullOrWhiteSpace(urlBox.Text) ? Visibility.Visible : Visibility.Collapsed;
```

- [ ] **Step 2: Build**

Run the **Build command**. Expected: clean build.

- [ ] **Step 3: Deploy & observe**

Run the **deploy snippet**. On the Destinations tab: a destination with an empty webhook URL shows the ⚠ line; typing a URL hides it live; clearing it shows it again.

- [ ] **Step 4: Commit**

```bash
git add RaceNotifier/UI/SettingsControl.xaml.cs
git commit -m "Warn when a destination has no webhook URL"
```

---

## Task 8: Confirm before removing a message

**Files:**
- Modify: `RaceNotifier/UI/SettingsControl.xaml.cs`

- [ ] **Step 1: Add a confirm dialog to `RemovePreset`**

Replace the existing `RemovePreset(Preset preset)` body with:
```csharp
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
```

- [ ] **Step 2: Build**

Run the **Build command**. Expected: clean build.

- [ ] **Step 3: Deploy & observe**

Run the **deploy snippet**. Click **Remove** on a message → Yes/No dialog appears; **No** keeps it, **Yes** deletes it. Confirm a freed slot is reused by the next **+ Add message** (it takes the lowest free `ActionIndex`).

- [ ] **Step 4: Commit**

```bash
git add RaceNotifier/UI/SettingsControl.xaml.cs
git commit -m "Confirm before removing a message"
```

---

## Task 9: Final verification pass

- [ ] **Step 1: Full build**

Run the **Build command**. Expected: clean.

- [ ] **Step 2: Deploy & run the spec's verification checklist**

Run the **deploy snippet**, then walk the Verification section of
`docs/superpowers/specs/2026-06-05-messages-page-polish-design.md`:
1. Several messages → page is short, all collapsed.
2. Add → opens expanded; collapse it.
3. ↑/↓ reorder persists across reopen; moved message still fires its bound button.
4. Blank text / no destination → ⚠ in header + body; fix → clears.
5. Destination with blank webhook → ⚠ on Destinations tab.
6. Remove → confirm dialog; cancel keeps, confirm deletes.
7. Toggle Enabled from the collapsed header without expanding.

- [ ] **Step 3: Confirm no regressions to v0.1 behavior**

Master switch still mutes sends and tests; Discord send still works; restart banner still appears only after exceeding the action pool.

- [ ] **Step 4: Tag is NOT bumped**

Leave version tags alone — `v1` is reserved for the first confirmed stable release. This work stays on `main` as incremental commits past `v0.1`.

---

## Self-Review (completed during authoring)

- **Spec coverage:** collapsible rows (T2), expand-state + add-expanded (T3), header Enabled (T4), reorder (T5), per-message validation (T6), destination webhook warning (T7), Remove confirm (T8), full verification (T9), pure validation helper (T1). All spec sections mapped.
- **Placeholders:** none — every code step shows complete code.
- **Type consistency:** `PresetValidation.Describe(Preset, IList<Destination>)` defined in T1 and called in T6; `_expanded` defined T3 used in T2's expander (note: apply T2 then T3 in order — T2 sets `IsExpanded = false`, T3 upgrades it to `_expanded.Contains(idx)`), `MovePreset` defined and used in T5, `RemovePreset` redefined in T8. Header/body split (`BuildRowHeader`/`BuildRowBody`) consistent across T2/T3/T6.
- **Ordering caveat:** Tasks are sequential and several edit the same methods; apply in order 1→9.
```

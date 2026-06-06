# Messages Page Polish — Design

**Date:** 2026-06-05
**Component:** `RaceNotifier` SimHub plugin — Settings UI, Messages tab
**Status:** Approved (design); pending implementation plan

## Context

The plugin recently moved from 10 fixed message slots to an **unlimited** list of
user-created messages (`List<Preset> Presets`, each with a stable `ActionIndex`). End-to-end
flow works in SimHub (master switch, Discord send, add/bind, restart fallback all verified).

With unlimited messages, the Messages tab now has a scalability problem: every message renders
as a tall, fully-expanded card, so a handful of messages turns the page into a long scroll. The
user prioritized three improvements: **collapsible rows**, **reordering**, and **validation /
guard rails**. (A fourth option, "status at a glance," was not separately selected — but the
collapsed row header naturally surfaces the key state anyway.)

This is a UI-only change. It does not touch the action-pool/registration logic, the dispatcher,
or persistence semantics beyond reordering/validating the existing `Presets` list.

## Goals

1. Keep the Messages tab compact as the message count grows.
2. Let the user order messages to match how they race.
3. Surface problems (a message that can't send, a destination with no webhook) without blocking.

## Non-goals (YAGNI)

- Drag-and-drop reordering (chose ↑/↓ buttons for reliability in SimHub's embedded WPF panel).
- Search / filter / bulk actions.
- A separate "status badges" system beyond what the collapsed header shows.
- Any change to how actions are registered or how sends are dispatched.

## Design

### Collapsible message rows

Each message renders as a native **`SHExpander`** (SimHub-themed `Expander`; exposes
`Header`, `Content`, `IsExpanded`) instead of the current always-open `SHSubSection`.

- **Header (always visible, single line), left to right:**
  - Compact **Enabled** toggle — togglable without expanding the row.
  - **Title** — the message `Name`, or `"Message <ActionIndex>"` when blank, followed by the
    action name `RaceNotifier.SendMessage<ActionIndex>` in muted text.
  - **⚠ warning glyph** — shown only when the message has a validation issue (see below); a
    tooltip/short text states the reason.
  - **↑ / ↓ reorder buttons** — `↑` disabled on the first row, `↓` disabled on the last.
- **Content (body):** the existing editor, unchanged in substance — Name, Message text,
  Cooldown, "Send to" destination checkboxes, "Bind button" (`ControlsEditor`), Send test,
  Remove.
- **Default state:** all rows **collapsed** when the Messages tab is (re)built, so the page is
  short. A message added via **+ Add message** opens **expanded** so the user can edit it at once.
- **State preservation:** the control keeps a `HashSet<int>` of expanded `ActionIndex` values.
  `RebuildMessages()` consults it so collapse/expand and reorder operations don't reset which
  rows are open. (`ActionIndex` is the stable key; list position is not.)
- **Header click vs header controls:** the toggle and ↑/↓ buttons handle their own clicks so
  interacting with them does not also toggle the expander. The chevron / header background
  toggles expansion as usual.

### Reorder (↑ / ↓)

- `↑` / `↓` swap the message with its immediate neighbor in `Presets`, then `Persist()` and
  `RebuildMessages()`.
- Order is **cosmetic only** — `ActionIndex`, the action name, and the wheel-button binding stay
  attached to the message regardless of list position. Nothing in the dispatcher depends on order.

### Validation & guard rails (advisory; never blocks a send)

A pure function over the live model decides whether a message is "OK" or has a problem. A message
is flagged (⚠ in the header + a one-line reason in the body) when **any** of:

- `Text` is blank/whitespace, **or**
- `TargetDestinationIds` is empty (no destination selected), **or**
- every selected destination is missing/blank `DiscordWebhookUrl`.

Notes:
- Validation is **advisory**: it never disables the Enabled toggle and never prevents a send. It
  only informs. (A disabled message is not "wrong"; it simply shows no warning beyond its toggle.)
- **Destinations tab:** a destination whose webhook URL is blank shows the same ⚠ + reason.
- **Remove confirmation:** the message **Remove** button shows a Yes/No `MessageBox` before
  deleting, to prevent accidental loss. (Destination Remove is left as-is for now.)

Validation runs on (re)build and after edits that can change it (text, destination selection,
webhook URL). Simplest correct approach: recompute on the same `Persist()`-triggering edits that
already cause a rebuild, and compute per-row at build time.

## Affected files

- `UI/SettingsControl.xaml` — no structural change required; the message rows are built in
  code-behind. (XAML stays as-is; `MessagesContainer` still hosts the rows.)
- `UI/SettingsControl.xaml.cs` — the bulk of the work:
  - `BuildMessageRow` returns an `SHExpander` with a custom header panel + body.
  - Add `MovePreset(preset, delta)`, expanded-state `HashSet<int>`, `IsMessageOk(preset)` /
    `MessageWarning(preset)` helper, Remove confirmation, and per-row warning rendering.
  - `BuildDestinationRow` gains a blank-webhook warning.
- No changes to `RaceNotifierPlugin.cs`, `NotificationDispatcher.cs`, or the settings model.

## Risks / edge cases

- **Header interactivity inside an Expander header** — must verify clicking the Enabled toggle or
  ↑/↓ does not also expand/collapse. Mitigation: those controls consume their own click; if the
  event still bubbles, mark it handled.
- **Reorder + expand-state** — preserving open rows across rebuild relies on `ActionIndex` as the
  key; confirmed stable. A reordered row keeps its open/closed state.
- **Validation false comfort** — advisory only; a user can still enable a "broken" message. That's
  intended (they may be mid-edit). The warning is the signal, not a lock.
- **`SHExpander` styling fit** — it's the SimHub-native expander, so visual fit is expected to be
  good; verify in-app and adjust margins/padding (`HeaderPadding`, `ExpanderMargin`) if needed.

## Verification

Deploy to SimHub (close `SimHubWPF.exe`, copy DLL+PDB, relaunch — user authorized restarts), then
in the Messages tab:

1. With several messages, the page is short — all rows collapsed; click a header to expand/edit.
2. Add a message → it appears expanded and editable immediately; collapse it.
3. ↑/↓ reorder a message; confirm order persists across reopen and the binding still fires for the
   moved message.
4. Clear a message's text or unselect its destination → ⚠ appears in the header with a reason;
   fix it → ⚠ clears.
5. A destination with a blank webhook shows ⚠ on the Destinations tab.
6. Click Remove on a message → confirm dialog; cancel keeps it, confirm deletes it.
7. Toggle a row's Enabled directly from the collapsed header without expanding.

# Race Notifier — SimHub plugin

Press a bound steering-wheel button → fire a preset message to your team on **Discord**
(Telegram support is planned for a later version). Built for hands-on-wheel team radio:
"Pitting this lap", "Need a gap", "GG", etc.

## Features (v0.1.1)
- **Unlimited messages** — add/remove as many as you like; each is bindable to any
  controller/wheel button via SimHub's mapper. Button slots are pre-registered in a pool that
  grows on demand; a one-click **Restart SimHub** finishes binding brand-new buttons when needed.
- **Master on/off switch** — mute the whole plugin (button sends *and* tests) without unloading it.
- **Compact, reorderable UI** — collapsible message rows with ↑/↓ ordering and inline validation
  warnings (missing text / destination / webhook).
- **Multiple Discord destinations**; each message targets the destination(s) you choose.
- **Configurable sender prefix** (e.g. `[John] Pitting this lap`).
- **Per-message cooldown** (default 3s) to avoid double-sends / rate limits.
- **One automatic retry** on a failed send.
- **Dashboard properties** `RaceNotifierPlugin.LastSendStatus` / `RaceNotifierPlugin.LastSendMessage`
  and **events** `RaceNotifierPlugin.MessageSent` / `RaceNotifierPlugin.MessageFailed` (bind to SimHub sounds or LEDs).
- **Native SimHub UI** (uses SimHub's own styled WPF controls).

## Requirements
- SimHub (Windows), .NET Framework 4.8 runtime.
- To build: Visual Studio Build Tools 2022 with the **.NET desktop build tools** workload
  (includes MSBuild + the .NET Framework 4.8 targeting pack).

## Install (compiled — no build)
1. Download `RaceNotifier-v0.1.1.zip` from the [latest Release](../../releases/latest).
2. Unzip and copy `RaceNotifier.dll` into your SimHub folder
   (e.g. `C:\Program Files (x86)\SimHub\`).
3. Restart SimHub and enable **Race Notifier** when prompted.

## Build
The project resolves SimHub assemblies via the `SIMHUB_INSTALL_PATH` environment variable
(e.g. `C:\Program Files (x86)\SimHub\`, **with trailing backslash**).

```powershell
# from the repo root
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" `
  RaceNotifier.sln /p:Configuration=Release
```

The post-build step copies `RaceNotifier.dll` (+ `.pdb`) into your SimHub folder.
Restart SimHub and enable the plugin when prompted.

## Setup (Discord)
1. In Discord: **Server Settings → Integrations → Webhooks → New Webhook**, pick the
   channel your team watches, and **Copy Webhook URL**.
2. In SimHub: open **Race Notifier** in the left menu.
3. Add a **Destination** (type Discord) and paste the webhook URL.
4. Click **+ Add message**: enable it, type the text, choose the destination(s), set a cooldown.
5. Bind the message's action (`RaceNotifierPlugin.SendMessageN`) to a wheel button — either in the
   plugin panel or under SimHub's **Controls & Events**.
6. Press the button → the message appears in your Discord channel.

## Security
Webhook URLs (and later Telegram tokens) are **secrets**. They are stored by SimHub in
`SimHub\PluginsData\Common` at runtime and are **never** committed to this repository.

## Roadmap
- **Phase 2:** Telegram support (BotFather bot + group `chat_id`, with an in-app chat-id
  discovery helper). The code is already structured for it (`INotifier` + `DestinationType`).
- Optional telemetry-enriched messages (lap/position/fuel) and on-send sound presets.

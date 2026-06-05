# Race Notifier — SimHub plugin

Press a bound steering-wheel button → fire a preset message to your team on **Discord**
(Telegram support is planned for a later version). Built for hands-on-wheel team radio:
"Pitting this lap", "Need a gap", "GG", etc.

## Features (v0.1)
- **10 message slots**, each bindable to any controller/wheel button via SimHub's mapper.
- **Multiple Discord destinations**; each slot targets the destination(s) you choose.
- **Configurable sender prefix** (e.g. `[John] Pitting this lap`).
- **Per-slot cooldown** (default 3s) to avoid double-sends / rate limits.
- **One automatic retry** on a failed send.
- **Dashboard properties** `RaceNotifier.LastSendStatus` / `RaceNotifier.LastSendMessage`
  and **events** `MessageSent` / `MessageFailed` (bind to SimHub sounds or LEDs).
- **Native SimHub UI** (uses SimHub's own styled WPF controls).

## Requirements
- SimHub (Windows), .NET Framework 4.8 runtime.
- To build: Visual Studio Build Tools 2022 with the **.NET desktop build tools** workload
  (includes MSBuild + the .NET Framework 4.8 targeting pack).

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
4. Fill a **Message slot**: enable it, type the text, choose the destination(s), set a cooldown.
5. Bind the slot's action (`RaceNotifier.SendMessageN`) to a wheel button — either in the
   plugin panel or under SimHub's **Controls & Events**.
6. Press the button → the message appears in your Discord channel.

## Security
Webhook URLs (and later Telegram tokens) are **secrets**. They are stored by SimHub in
`SimHub\PluginsData\Common` at runtime and are **never** committed to this repository.

## Roadmap
- **Phase 2:** Telegram support (BotFather bot + group `chat_id`, with an in-app chat-id
  discovery helper). The code is already structured for it (`INotifier` + `DestinationType`).
- Optional telemetry-enriched messages (lap/position/fuel) and on-send sound presets.

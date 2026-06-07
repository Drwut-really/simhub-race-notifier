# Race Notifier — SimHub plugin

Press a bound steering-wheel button → fire a preset message to your team on **Discord** or
any **custom webhook** (Telegram support is planned for a later version). Built for
hands-on-wheel team radio: "Pitting this lap", "Need a gap", "GG", etc.

## Features (v0.1.2)
- **Unlimited messages** — add/remove as many as you like; each is bindable to any
  controller/wheel button via SimHub's mapper. Button slots are pre-registered in a pool that
  grows on demand; a one-click **Restart SimHub** finishes binding brand-new buttons when needed.
- **Binds as a SimHub input** — fires on a plain button press, so **any press type works** (no
  "Short and long press" gotcha, and ControlMapper roles trigger it fine).
- **Master on/off switch** — mute the whole plugin (button sends *and* tests) without unloading it.
- **Compact, reorderable UI** — collapsible message rows with ↑/↓ ordering and inline validation
  warnings (missing text / destination / webhook).
- **Multiple destinations** — Discord and/or custom webhooks; each message targets the destination(s) you choose.
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
1. Download `RaceNotifier-v0.1.2.zip` from the [latest Release](../../releases/latest).
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
4. Click **+ Add message**: enable it, type the text, choose the destination(s), set a cooldown. You can include `{flag}` in the text to insert the current track flag — filled in live when the message sends (`none` when clear or the game isn't running).
5. Bind the message to a wheel button, either way:
   - **Simple bind** — on the **Messages** tab, click the bind button on the message's row and press the button.
   - **Controls & Events** — bind the input `RaceNotifierPlugin.SendMessageN` under SimHub's **Controls & Events** menu.

   It fires on a plain press, so any press type works.
6. Press the button → the message appears in your Discord channel.

## Setup (custom webhook)
Besides Discord, a destination can be a **Custom webhook** — any HTTP endpoint that accepts an
unauthenticated `POST` (no custom headers, auth, or per-field templating).
1. On the **Destinations** tab, set the type dropdown to **Custom webhook** and click **+ Add destination**.
2. Paste the endpoint URL and pick a **Body format**:
   - **JSON (`{"content": message}`)** — default. Works with Discord-style and other JSON `content` endpoints.
   - **Plain text** — POSTs the raw message as `text/plain`, for endpoints that accept raw bodies (e.g. webhook.site).
3. Target the webhook from a message just like a Discord destination. The optional sender
   prefix (General tab) applies to webhook messages too. A non-2xx response is logged with its
   status code (the URL is never logged).

## Security
Webhook URLs (and later Telegram tokens) are **secrets**. They are stored by SimHub in
`SimHub\PluginsData\Common` at runtime and are **never** committed to this repository.

## Roadmap
- **Phase 2:** Telegram support (BotFather bot + group `chat_id`, with an in-app chat-id
  discovery helper). The code is already structured for it (`INotifier` + `DestinationType`).
- Optional telemetry-enriched messages (lap/position/fuel) and on-send sound presets.

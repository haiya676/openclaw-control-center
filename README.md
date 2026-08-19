# OpenClaw Control Center 🖥️

A lightweight Windows desktop widget for **OpenClaw gateway status + DeepSeek usage monitoring**. Single portable EXE, no installation required.

## Download

Grab the latest portable zip from **[Releases](https://github.com/haiya676/openclaw-control-center/releases)** — unzip anywhere, run `ds_control.exe`. On first run it auto-detects your API key or prompts you to paste one, then saves it as `ds_key.conf` next to the EXE. That's it.

## Features

- **¥ Floating Ball** on desktop — real-time display of **Balance / Today's Tokens / Today's Cost**
- **Control Panel** — gateway status (TCP port probe), gateway URL, current model detection, usage stats, auto-start toggle
- **Live Model Detection** — polls the gateway every 20s (`openclaw status --json`) and shows the model of the most recently active session, so per-session switches (e.g. `/model agnes/agnes-2.0-flash`) are reflected automatically, not just the config default
- **Fancy Buttons** — hover glow + press dim effect (custom `RoundedBtn` control)
- **Rounded dark UI**, semi-transparent draggable floating ball
- **🌐 Built-in i18n** — auto-detects system language: Chinese UI on zh systems, English UI otherwise

## How the API Key Works (zero-config)

The app finds your API key automatically, in this order:

1. `~/.openclaw/main/agent/models.json` or `~/.openclaw/agents/main/agent/models.json`
2. `~/.openclaw/openclaw.json`
3. Environment variable (`DEEPSEEK_API_KEY`, `OPENAI_API_KEY`, `GEMINI_API_KEY`, `MOONSHOT_API_KEY`)
4. `ds_key.conf` next to the EXE

On **first run with no key found anywhere**, a dark-themed dialog pops up asking you to paste your key. It is then saved to `ds_key.conf` next to the EXE — **automatic, portable, one-time setup**.

> 🔒 `ds_key.conf` contains a real secret and is excluded via `.gitignore`. Never commit it.

## Build

Windows + .NET Framework (the built-in `csc` compiler, no extra tooling):

```bat
csc /nologo /target:winexe /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:ds_control.exe ds_float.cs
```

Produces `ds_control.exe` — drop it anywhere and run.

## Usage

- **Double-click the floating ball** → usage card (balance highlighted in green, with official platform link)
- **Control panel buttons** → refresh, open official site, toggle the floating ball, auto-start
- Ball and panel support standard dark-theme interactions

## Known Issues

- **Not fully optimized** — this tool was built for personal use; you may encounter minor issues (UI edge cases, DPI/scaling quirks, etc.).
- Rounded corners are not fully optimized — slight edge artifacts may appear at some DPI/scaling settings.
- The floating ball model icon is only supported for a few models (deepseek / gpt / openai / gemini / kimi / moonshot) — other models fall back to the ¥ symbol.

## Disclaimer

This software is provided **as-is, without warranty of any kind**. It is a personal-use tool, not production software. Use at your own risk. The author is not responsible for any data loss, misconfiguration, or issues arising from its use.

## License

MIT

# NexIsland v0.1.0-beta.1 🏝️

Welcome to the first public beta release of **NexIsland** — the premium, lightweight, interactive Dynamic Island for Windows 10 & 11!

---

## 🌟 What's New in v0.1.0-beta.1

### 1. 🤖 Smart AI Island
- Integrated AI assistant right inside the island capsule.
- **Offline Mode**: Instant text summarization, task checklists, study plans, Windows power shortcuts, and routine ideas.
- **Cloud Connect**: Pluggable support for OpenAI / Groq / Ollama with API keys encrypted locally using **Windows DPAPI** (`ProtectedData`). Never stored in plain text.

### 2. 🔍 Universal Search (`Alt + Space`)
- Global floating launcher available from anywhere in Windows with `Alt + Space`.
- Instantly search installed apps, system utilities (`cmd`, `calc`, `taskmgr`, `explorer`), Windows Settings (`ms-settings:`), files in common user folders, and NexIsland modules.
- **Built-in Math Calculator**: Type `45 * 12 + 8` for immediate evaluation and one-click copy to clipboard.
- Full keyboard navigation: `↑` / `↓` to select, `Enter` to open, `Escape` to close.

### 3. ⚡ Smart Routines Automation
- Automated workspace optimization:
  - **Study Mode**: Triggers Pomodoro focus timer, distraction-free sound, and balanced power.
  - **Gaming Mode**: Auto-detects game processes, lowers polling frequency, and minimizes background load.
  - **Battery Saver Mode**: Auto-triggers at battery `<= 20%` on laptop power.
  - **Music Mode & Scheduled Routines**: Run actions on daily schedules or with a single click.

### 4. 🎛️ Unified Swipe Navigation (21 Modules)
- Fluid horizontal swipe navigation across 21 modules:
  `Music → System → Focus → Lyrics → Battery → Network → AI → Search → Routines → Launcher → Notifications → Weather → Calendar → Clipboard → Visualizer → Quick Controls → Gaming → File Drop → Activity → Themes → Plugins`
- Fully customizable module order directly within the island — no separate settings window needed!

### 5. 🎵 Real-time Media & Live Data
- **GSMTC Integration**: Spotify, Apple Music, YouTube/Chrome/Edge title, artist, album art, Play/Pause, and real-time progress.
- Fixed duration bug (`00:00 / 00:00`) with smooth interactive scrubber seek.
- **WASAPI Audio Visualizer**: Live hardware audio loopback equalizer.
- **Live Status Badges**: Every module displays real-time connectivity status (`LIVE`, `SYNCING`, `OFFLINE`, `UNAVAILABLE`).

### 6. 🔆 Realtime Brightness & Sound Controls
- **Windows Core Audio API (`IAudioEndpointVolume`)**:
  - Realtime master volume slider (0–100%) synced with system hardware in both directions.
  - Interactive Mute / Unmute button with reactive vector speaker icon.
  - Automatic detection of default playback device changes (e.g. Speakers ↔ Headphones) via `IMMNotificationClient`.
  - Active audio output device name display.
- **Hardware Display Brightness (`WmiMonitorBrightness`)**:
  - Real screen brightness control via WMI with 80ms cancellation debounce to protect display drivers.
  - Automatic detection of external or unsupported monitors with `Brightness control unavailable` and quick-access Display Settings fallback.
  - Completely non-blocking background processing.

---

## 📦 Download & Verification

| Asset | Size | SHA-256 Checksum |
| :--- | :--- | :--- |
| **NexIsland-v0.1.0-beta.1-Setup.exe** | ~6.77 MB | `3C22A47E94265D3E02B72F9E0DA269E35F1F1D3FC1442DF67F6F384E02DAD406` |

### Verifying SHA-256 in PowerShell:
```powershell
Get-FileHash -Algorithm SHA256 "NexIsland-v0.1.0-beta.1-Setup.exe"
```

---

## 💻 System Requirements
- **OS**: Windows 10 (version 1809+ / build 17763+) or Windows 11 (64-bit).
- **Runtime**: [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (free Microsoft download).
- **Privileges**: Standard user permissions (Administrator not required).

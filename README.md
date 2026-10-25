# NexIsland 🏝️ — Ultimate Dynamic Island for Windows 10 & 11
### Modern WPF / .NET 8 • 20 Unlocked Modules • Universal Search (`Alt + Space`) • Realtime Engine

**NexIsland** is an ultra-modern, lightweight, modular, and interactive Dynamic Island application engineered natively for **Windows 10 and Windows 11**. Inspired by premium mobile capsule interfaces, NexIsland transforms your desktop experience by providing an always-accessible, fluid hub for real-time media, universal search, workspace automation, system hardware telemetry, focus management, and quick system controls.

[![Release](https://img.shields.io/badge/Release-v0.1.0--beta.1-blue.svg)](https://github.com/Habibramadhan25/NexIsland/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D6.svg)](https://microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0%20LTS-512BD4.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

## 🌟 Flagship Features (v0.1.0-beta.1)

### 1. 🎛️ 20 Unlocked Modules & Direct Navigation Bar
- **Direct 1-Click Navigation Bar**: A scrollable bottom bar with 20 quick access buttons allowing you to jump instantly to any module:
  `Music → Controls → Lyrics → Visualizer → System → Battery → Network → Search → Routines → Launcher → Notifications → Clipboard → Weather → Calendar → Focus → Activity → Gaming → File Drop → Themes → Plugins`
- **Fluid Horizontal Gestures**: Supports chevron button clicking, mouse dragging, mouse wheel scrolling with debounce, and keyboard arrow navigation.
- **Zero Settings Windows**: Module order reordering, Windows startup, and theme presets are all controlled directly inside the island under the *Themes & Preferences* module.

### 2. 🔍 Universal Search (`Alt + Space`)
- **Global Floating Launcher**: Activated anywhere in Windows by pressing `Alt + Space`.
- **Instant Focused Search**: Search installed Start Menu apps, built-in system tools (`taskmgr`, `calc`, `cmd`, `explorer`, `notepad`), Windows Settings (`ms-settings:`), common files (Desktop, Documents, Downloads), and NexIsland modules.
- **Embedded Calculator**: Type any math formula (e.g. `120 * 5 + 45`) for instantaneous evaluation and one-click copy to clipboard.
- **Keyboard Centric**: Up/Down arrow selection, `Enter` to launch, `Escape` to close.

### 3. ⚡ Smart Routines Windows Automation
- **Context-Aware Workspace Modes**:
  - **Study Mode**: Automatically starts the Pomodoro focus timer and enables balanced system performance.
  - **Gaming Mode**: Auto-detects foreground game processes, switches to low-polling mode, and minimizes background CPU latency.
  - **Battery Saver Mode**: Auto-triggers when laptop battery drops below `<= 20%`.
  - **Music Mode & Scheduled Routines**: Trigger playback and custom routines based on time of day.

### 4. 📡 Realtime Core Engine & Live Data Integration
- **Live Status Badges**: Every module displays verified connection state (`LIVE`, `SYNCING`, `OFFLINE`, `UNAVAILABLE`) with real-time diagnostics.
- **Windows Media Session (GSMTC)**: Real-time track metadata, album art, Play/Pause synchronization, accurate time elapsed/duration (no `00:00` bug), and interactive drag-and-seek scrubbing.
- **Hardware Quick Controls**: Real-time display brightness via WMI (`WmiMonitorBrightness`) with debounce and Windows Core Audio (`IAudioEndpointVolume`) master volume control with dynamic speaker/headphone switching.
- **WASAPI Audio Loopback**: Low-latency hardware audio spectrum analyzer via NAudio.
- **Win32 System Sensors**: Hardware CPU (`GetSystemTimes`), RAM (`GlobalMemoryStatusEx`), Storage drive C: (`DriveInfo`), Network bandwidth (`NetworkInterface`), and Battery (`GetSystemPowerStatus`).
- **Live Weather**: Powered by free Open-Meteo REST API with local caching and offline fallback.
- **Clipboard Listener**: Event-driven Windows clipboard updates (`AddClipboardFormatListener`) with password/sensitive data filtering.

---

## 📦 Quick Installation

### Option 1: Standalone Windows Installer (Recommended)
1. Download **`NexIsland-v0.1.0-beta.1-Setup.exe`** from [Releases](https://github.com/Habibramadhan25/NexIsland/releases).
2. Run the installer (standard user permissions; administrator not required).
3. If prompted by Windows SmartScreen on first install, click **More info** > **Run anyway** (see [TROUBLESHOOTING.md](TROUBLESHOOTING.md)).

| Release File | Size | SHA-256 Checksum |
| :--- | :--- | :--- |
| **`NexIsland-v0.1.0-beta.1-Setup.exe`** | ~6.77 MB | `A0C3C9A84D6A8A7E2AE8AC3D6663A06A716B5B1A65AEAA4781FFB3FA31E9E423` |

### Option 2: Portable Package
Extract `dist/NexIsland-win-x64` to any folder and run `NexIsland.exe`.

---

## 🛠️ Technology Stack & Project Structure

| Component | Technology | Rationale |
| :--- | :--- | :--- |
| **Framework** | C# 12 / .NET 8 LTS (WPF) | High performance, memory-efficient, native Windows 10/11 UI |
| **Target OS** | Windows 10 & 11 (x64) | Target framework: `net8.0-windows10.0.19041.0` |
| **Audio Capture** | NAudio.Wasapi (2.2.1) | Direct hardware audio loopback with zero noticeable latency |
| **Media API** | Windows Runtime GSMTC API | Official Windows system media transport controls |
| **Security** | Windows DPAPI (`ProtectedData`) | On-device encryption for sensitive API keys |
| **Packaging** | Inno Setup 6 (LZMA2 Ultra) | High compression ratio, clean desktop & startup integration |

```
d:\NexIsland\
├── src\
│   └── NexIsland\
│       ├── MainWindow.xaml / .cs           # Dynamic Island unified UI, swipe engine, and modules
│       ├── ViewModels\                     # MVVM ViewModel (MainViewModel)
│       ├── Services\                       # RealtimeService, AiService, SearchService, RoutineService, MediaService
│       ├── Settings\                       # AppSettings and JSON auto-persistence
│       ├── Assets\Branding\                # Official NexIsland logos and vector icons
│       └── NexIsland.csproj
├── tests\
│   └── NexIsland.Tests\                    # 22 automated xUnit tests
├── installer\
│   ├── NexIsland.iss                       # Inno Setup Windows installer script
│   └── build-installer.ps1                 # Automated installer build script
├── scripts\
│   └── publish-release.ps1                 # Production publish script
├── dist\
│   ├── NexIsland-win-x64\                  # Published release binaries
│   └── installer\                          # Generated setup executable
├── CHANGELOG.md                            # Version history
├── TROUBLESHOOTING.md                      # Troubleshooting guide & FAQ
└── README.md
```

---

## 🔒 Privacy & Security Commitment
- **No Hidden Telemetry**: NexIsland does not track, collect, or upload personal analytics.
- **Local-Only Storage**: Clipboard history, settings, and routines remain strictly on your local machine in `%APPDATA%\NexIsland`.
- **Zero Elevated Privileges**: Runs entirely under standard user permissions.
- **Safe Execution**: All application launches strictly validate paths before execution.

---

## 📄 License
NexIsland is released under the [MIT License](LICENSE).

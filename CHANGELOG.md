# Changelog

All notable changes to the NexIsland project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.1.0-beta.1] - 2026-10-25

### 🚀 Highlights & Major Features
* **Smart AI Island**: In-island Copilot powered by Windows DPAPI local encryption for cloud API keys (OpenAI/Ollama/Groq) and an intelligent offline fallback engine for summarization, task checklists, Pomodoro study plans, and Windows shortcut recommendations.
* **Universal Search (`Alt + Space`)**: Global floating launcher overlay with instant text focus, debounced async search across Start Menu apps, built-in system tools, Windows Settings URIs (`ms-settings:`), NexIsland modules, common user files, and an instant math calculator.
* **Smart Routines Windows Automation**: Context-aware routine executor supporting Study Mode, Gaming Mode, Music Mode, Work Mode, and Battery Saver Mode with real triggers (Battery threshold, game process foreground detection, schedule, and manual execution).
* **Realtime Core Engine (`RealtimeService`)**: Dedicated live status tracker reporting connection states (`LIVE`, `SYNCING`, `OFFLINE`, `UNAVAILABLE`) for all 21 modules with adaptive polling to minimize idle CPU and battery consumption.
* **Swipe Navigation System**: Gesture-driven horizontal navigation across 21 integrated modules supporting mouse drag, mouse wheel scrolling with debounce, and keyboard arrow keys with interactive control protection.
* **Realtime Brightness & Sound Controls**: True Windows hardware integration directly inside the Quick Controls capsule module:
  * **Master Volume**: Windows Core Audio API (`IAudioEndpointVolume`) slider, interactive Mute/Unmute toggle, dynamic `IMMNotificationClient` device switching (speakers <-> headphones), and real-time external OS volume synchronization.
  * **Screen Brightness**: WMI `WmiMonitorBrightness` / `WmiMonitorBrightnessMethods` hardware brightness slider with 80ms cancellation debounce, periodic polling for FN key changes, and automatic fallback detection (`Brightness control unavailable` + Display Settings launcher).
* **Unified In-Capsule Experience**: Complete removal of external settings windows and gear buttons; preferences and swipe module ordering are managed seamlessly inside the capsule.

### 🛠️ Bug Fixes & Improvements
* **Media Timeline & Duration Fix**: Resolved the GSMTC timeline `00:00 / 00:00` bug by preserving valid duration properties across partial metadata events and calculating accurate seek fractions.
* **Interactive Media Scrubber**: Added smooth drag-and-seek scrubbing with live position preview and mouse capture.
* **WASAPI Audio Visualizer**: Upgraded to low-latency NAudio WASAPI loopback capture with automatic silence sleep detection.
* **Settings Auto-Migration**: Upgraded configuration loader to auto-migrate legacy module sequences to the full 21-module master sequence.
* **Installer & Packaging**: Integrated Inno Setup with LZMA2 ultra compression, generating a standalone `NexIsland-v0.1.0-beta.1-Setup.exe` (~6.7 MB).

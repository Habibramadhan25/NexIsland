# NexIsland Troubleshooting & FAQ

This guide provides solutions for common configuration questions, permission checks, and hardware compatibility.

---

## 1. Windows SmartScreen Warning on First Install
### Problem: Windows displays "Windows protected your PC" (SmartScreen warning)
- **Cause:** NexIsland is a free, open-source community application and does not have an expensive commercial EV Code Signing certificate. Windows Defender SmartScreen shows this prompt for newly released `.exe` files until reputation is established.
- **Solution:**
  1. Click **More info** on the SmartScreen dialog.
  2. Click **Run anyway**.
  3. Verify the installer SHA-256 hash matching the official release checksum in PowerShell:
     ```powershell
     Get-FileHash -Algorithm SHA256 "NexIsland-v0.1.0-beta.1-Setup.exe"
     ```

---

## 2. Universal Search Hotkey (`Alt + Space`)
### Problem: Pressing `Alt + Space` does not trigger Universal Search
- **Cause:** Another running application (e.g., PowerToys Run, Raycast, or an active full-screen game) may have already registered the `Alt + Space` hotkey with higher priority.
- **Solution:**
  - Swipe to the **Search** module directly within NexIsland capsule.
  - Alternatively, if using PowerToys Run, customize the PowerToys shortcut in PowerToys Settings to free up `Alt + Space` for NexIsland.

---

## 3. Media Island & GSMTC Playback
### Problem: Song title displays "No media playing" or media controls do not respond
- **Cause:** The media player or browser web application does not expose Windows System Media Transport Controls by default.
- **Solution:**
  - **Spotify Desktop:** Ensure "Show desktop overlay when using media keys" is enabled in Spotify Settings.
  - **Google Chrome / Microsoft Edge:** Ensure `chrome://flags/#hardware-media-key-handling` is set to **Enabled**.
  - **VLC Media Player:** Ensure Windows Media Integration plugin is enabled in Tools > Preferences > Interface.

---

## 4. Audio Visualizer (WASAPI Loopback)
### Problem: Audio visualizer bars remain flat when audio is playing
- **Cause:** The audio output device might be in exclusive mode or master volume is muted.
- **Solution:**
  - Verify that the default Windows audio output playback device is active in Windows Sound Settings (`ms-settings:sound`).
  - Disable "Allow applications to take exclusive control of this device" in Windows Advanced Sound Properties if another application has locked the audio channel.

---

## 5. Smart AI Island Configuration
### Problem: AI replies with offline templates or connection error
- **Cause:** No external OpenAI API key has been configured, or network is offline.
- **Solution:**
  - NexIsland works completely offline out-of-the-box using the built-in **Offline Copilot** engine for text summarization, checklists, study plans, and Windows shortcuts.
  - To enable cloud LLMs, open the AI module, enter your API key, and it will be securely encrypted into your Windows DPAPI vault (`ai_vault.dat`).

---

## 6. Adjusting Positioning & Preferences
### Problem: How do I change monitor position, order of modules, or startup behavior?
- **Solution:**
  - Swipe to the **Themes & Preferences** module in NexIsland.
  - You can reorder modules using the ▲ / ▼ buttons, toggle Windows startup, enable/disable position lock, or change color presets directly in the capsule. There is no external settings window.

---

## 7. Logs & Diagnostics
Application logs are continuously recorded to:
`%APPDATA%\NexIsland\logs\nexisland.log`

If you encounter an unexpected issue, inspect this log file for timestamped error diagnostics.

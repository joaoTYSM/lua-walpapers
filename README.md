# lua_walpapers

Live wallpaper engine for Windows 10/11. Wallpapers are HTML/JS pages, media files, URLs or **Lua scripts**, rendered behind your desktop icons with WebView2.

## Build

1. Extract the whole folder.
2. Run `build.bat` (downloads WebView2, NAudio and MoonSharp from NuGet, then compiles with the built-in `csc.exe`).
3. Run `dist\lua_walpapers.exe`.

## What's new in this update

- **Wallpaper now applies after a reboot.** The app waits for Explorer (taskbar + desktop) before attaching, retries if WebView2 is not ready yet, re-attaches when Explorer restarts or the display changes, and recovers when the audio device changes. "Start with Windows" is enabled on first run and its shortcut is refreshed on every launch.
- **Mouse interaction.** A global mouse hook forwards desktop clicks to the wallpaper (desktop icons still work). In *Lua Fluid*, hold the left button and drag to paint a smoke trail.
- **Real Lua support.** Lua runs in-process (MoonSharp, sandboxed: no `io`, no `os.execute`, no files). The old "python/lua executable" launch is gone, so nothing needs to be installed.
- **Lua entry in the tray menu.** Right-click the tray icon → **Lua wallpaper...** opens a code editor. The panel also has **+ Lua**, and every Lua wallpaper has a **Code** button.
- **New default wallpaper:** *Lua Trail* (pure Lua, mouse trail + audio pulse).
- **Optimizations**
  - Audio analysis uses an FFT instead of a 128x1024 DFT.
  - The wallpaper pauses automatically while a maximized or fullscreen window covers the desktop (set `"pauseWhenCovered": false` in `%APPDATA%\lua_walpapers\config.json` to disable).
  - Fluid: lower default resolution plus a Quality setting, GPU textures are freed on resize, and the broken audio handler (infinite recursion) is fixed.
  - Panel: previews only run while hovered, the panel is released from memory when closed, background blobs are static.
  - Defaults are refreshed when the app updates; your saved settings are kept.

## Using the Lua editor

Write Lua, press **Save** or **Save & Apply** (Ctrl+S saves). Syntax errors are shown before anything is saved. Defaults are read-only; saving one creates a copy.

See `CREATORS_2.0.md` for the full wallpaper and Lua API.

## 1.1.0

- **Loading video.** `loading-bg.mp4` (repo root) plays while Windows is starting and whenever a wallpaper is loading. It is streamed from `raw.githubusercontent.com`, never saved. Disable with `"loadingVideo": false` in `config.json`.
- **Previews.** Each default folder can have `p.png` or `p.gif`. The panel loads it from raw, it is never downloaded.
- **New defaults.** A new folder in `defaults/` on GitHub shows up in the panel (and as a tray notification) with a **Baixar** button.

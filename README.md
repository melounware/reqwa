# Reqwa

Per-monitor color control for Windows. Brightness, contrast, gamma, RGB gains and color temperature — applied instantly through the gamma ramp. Save looks as presets, share them as short codes, and park the app in the tray.

## Install

Grab `Reqwa-Setup-1.0.0.exe` from [Releases](../../releases) and run it. Per-user install, no admin needed.

## Features

- **Per-monitor sliders** — brightness, contrast, gamma, RGB, temperature, per display
- **Presets** — save, rename, delete, share as a code, import shared codes
- **Tray life** — close minimizes to the tray with a popup; `Insert` shows/hides from anywhere; right-click menu with Open / Restart / Quit
- **Startup options** — start with Windows, start minimized, re-apply last colors, restore defaults on exit
- **Always on top** toggle

## Build

```bash
dotnet build ReqwaColors.sln -c Release
```

Or open `ReqwaColors.sln` in Visual Studio 2026 and press F5. Output goes to `x64/`. The installer lives in `installer/` (Inno Setup script + publish payload).

## Notes

- GPU-level vibrance/saturation has no public Windows API, so it isn't touched — everything here uses documented gamma-ramp calls, no admin required.
- Settings and presets live in `%LOCALAPPDATA%\ReqwaColors`.

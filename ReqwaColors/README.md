# Reqwa Colors

A modern Windows desktop application for controlling and saving display color profiles. Inspired by NVIDIA's display color controls, but with its own clean, premium UI.

## Features

- **Per-Monitor Control**: Adjust brightness, contrast, gamma, and color temperature for each connected display
- **Presets**: Save your look as named presets — apply, rename, share as a code, and import shared ones
- **System Tray**: Close the app to minimize it to the tray (with a notification), or quit from Settings
- **Startup Options**: Start with Windows, start minimized, and re-apply your last colors on launch
- **Persistence**: Settings and presets are saved automatically

## Technology Stack

- **Framework**: .NET 10.0 / WinUI 3 (Windows App SDK 1.7)
- **Architecture**: MVVM with CommunityToolkit.Mvvm
- **Color Control**: GDI gamma ramp API (documented Win32 APIs, no admin required)
- **Storage**: JSON in %LOCALAPPDATA%\ReqwaColors

## Building

```bash
cd ReqwaColors
dotnet build
dotnet publish -c Release -r win-x64 --self-contained true
```

All build output goes into a single `x64/` folder at the repository root
(see `Directory.Build.props`). The runnable app is directly at:

- `x64/ReqwaColors.exe`

Intermediate files stay under `x64/obj`, and a self-contained publish lands in
`x64/publish/`.

## Project Structure

```
ReqwaColors/
├── Controls/          # Reusable UI controls (ColorSlider)
├── Models/            # Data models (ColorSettings, DisplayInfo, Preset, etc.)
├── Native/            # Win32 P/Invoke declarations
├── Resources/         # XAML resource dictionaries (Colors, Styles)
├── Services/          # Business logic (DisplayService, ColorService, etc.)
├── ViewModels/        # MVVM view models
├── Views/             # XAML pages and windows
├── App.xaml           # Application entry point
└── Assets/            # Application icons
```

## Color Controls

| Control | Range | Description |
|---------|-------|-------------|
| Brightness | 50-150% | Display brightness multiplier |
| Contrast | 50-150% | Contrast multiplier |
| Gamma | 0.5-2.5 | Gamma exponent (1.0 = neutral) |
| RGB Red/Green/Blue | 0-200% | Per-channel adjustment |
| Temperature | 2500-9500K | Color temperature (when enabled) |

## Limitations

- **Digital Vibrance**: GPU-level saturation has no public Windows API; vendor-specific undocumented interfaces are not used.
- **Hardware Vibrance**: Vendor-specific undocumented interfaces would be required; not implemented.

## License

 proprietary

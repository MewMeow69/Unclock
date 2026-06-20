# Unclock

GPU and CPU power management for Windows - switch between performance and
power-saving profiles.
<img width="471" height="729" alt="image" src="https://github.com/user-attachments/assets/6ccced46-0a3a-40a7-ade0-13d9f4130f2f" />


## Supported Hardware

- **NVIDIA GPUs** - full support: clock control, live stats (usage, clocks,
  temps, VRAM, power draw)
- **AMD GPUs** - requires building `amd_bridge.exe` from `amd_bridge/` with
  the AMD ADLX SDK and CMake + MSVC. all bundled with the zip file
- **Intel Arc** - detected but not yet supported; shows a notice in the UI
- **CPU** - all modern x86 processors (uses `powercfg` for P-state control)

## Features

- 3 GPU power modes: Performance / Medium / Power Saver
- 4 CPU power modes: Max Perf / High Perf / Mid Save / Max Save
- Windows power plan toggle (High Performance / Balanced / Power Saver)
- Live hardware monitoring (CPU and GPU stats update every 2 seconds)
- Auto-detects current system state on launch - no accidental resets
- System tray icon with live tooltip
- Dark OLED-black interface with Lato font

## Requirements

- Windows 10 or 11 (x64)
- .NET 8.0 Desktop Runtime
- NVIDIA driver (for GPU features)
- Administrator rights (UAC prompt for `nvidia-smi` and `powercfg`)

## Install

1. Download `Unclock-v0.2.zip` from the releases page
2. Extract to any folder
3. Run `Unclock.exe`

Fonts (Lato Regular + Bold) are embedded as WPF resources. No external
dependencies beyond the two NuGet packages in `PowerSaver.csproj`.

## License

MIT - see LICENSE file.

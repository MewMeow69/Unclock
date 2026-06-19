# Unclock

GPU and CPU power management for Windows — switch between performance and
power-saving profiles with one click. Lives in the system tray, stays out of the
way.

## Supported Hardware

- **NVIDIA GPUs** — full support: clock control, live stats (usage, clocks,
  temps, VRAM, power draw)
- **AMD GPUs** — requires building `amd_bridge.exe` from `amd_bridge/` with
  the AMD ADLX SDK and CMake + MSVC
- **Intel Arc** — detected but not yet supported; shows a notice in the UI
- **CPU** — all modern x86 processors (uses `powercfg` for P-state control)

## Features

- 3 GPU power modes: Performance / Medium / Power Saver
- 4 CPU power modes: Max Perf / High Perf / Mid Save / Max Save
- Windows power plan toggle (High Performance / Balanced / Power Saver)
- Live hardware monitoring (CPU and GPU stats update every 2 seconds)
- Auto-detects current system state on launch — no accidental resets
- System tray icon with live tooltip
- Dark OLED-black interface with Lato font

## Requirements

- Windows 10 or 11 (x64)
- .NET 8.0 Desktop Runtime
- NVIDIA driver (for GPU features)
- Administrator rights (UAC prompt for `nvidia-smi` and `powercfg`)

## Install

1. Download `Unclock-v0.2.zip` from the latest release
2. Extract to any folder
3. Run `Unclock.exe`

The app starts minimized to the system tray. Click the tray icon to open.

## AMD GPU Setup

AMD support relies on `amd_bridge.exe`, a small C++ tool that talks to the GPU
through AMD's ADLX SDK. It is not included in the download.

To build it:

1. Download the [ADLX SDK](https://github.com/GPUOpen-LibrariesAndSDKs/ADLX)
2. Place the ADLX files in `amd_bridge/../ADLX/`
3. Run CMake:

```
cd amd_bridge
cmake -B build
cmake --build build --config Release
```

4. Copy `build/Release/amd_bridge.exe` and `ADLX.dll` next to `Unclock.exe`

## Files

- `Unclock.exe` / `Unclock.dll` — main application
- `Unclock.deps.json` / `Unclock.runtimeconfig.json` — .NET runtime config
- `Hardcodet.NotifyIcon.Wpf.dll` — system tray library
- `System.Drawing.Common.dll` — icon rendering
- `app.ico` — app icon

## Building from Source

```
dotnet build -c Release
```

Fonts (Lato Regular + Bold) are embedded as WPF resources. No external
dependencies beyond the two NuGet packages in `PowerSaver.csproj`.

## License

MIT — see LICENSE file.

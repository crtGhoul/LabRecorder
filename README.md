# 🎥 Lab Recorder

> **Zero-bloat, 60 FPS studio screen & facecam recorder for Windows desktops and laptops.**  
> Built with C# (.NET 10), Windows Graphics Capture (WGC), multi-vendor hardware acceleration (AMD AMF, NVIDIA NVENC, Intel QSV, Windows Media Foundation), WASAPI system audio loopback, and instant-save fragmented MP4 container.

---

## ✨ Features

- **⚡ Multi-Vendor GPU Acceleration**: Automatically detects and leverages your hardware encoder:
  - **AMD AMF** (`h264_amf`) for AMD Radeon desktops & laptops
  - **NVIDIA NVENC** (`h264_nvenc`) for GeForce GTX / RTX laptops & desktops
  - **Intel QuickSync** (`h264_qsv`) for Intel Core / Core Ultra laptops
  - **Windows Media Foundation** (`h264_mf`) universal hardware acceleration
  - **High-speed CPU x264** fallback
- **🎮 Smooth 60 FPS Gameplay Capture**: Native DirectX 11/12 and desktop capture via Windows Graphics Capture (`gfxcapture`) with zero stutter and no frame drops.
- **📷 Real-Time Circular Face Cam**: Hardware-composited antialiased circle overlay directly inside the capture graph (`alphamerge` filter). Zero slow post-processing re-encoding.
- **🔊 Studio-Grade Audio Stage**:
  - **WASAPI Loopback**: Pristine system audio capture for games, Discord, Spotify, and browser media.
  - **Microphone Leg**: Hardware DirectShow input with +18 dB studio gain boost and an integrated limiter (`alimiter=limit=0.79`) to eliminate clipping and distortion.
  - **Live VU Meter**: Real-time post-gain audio meter with dB readout.
- **🛡️ Instant-Save Fragmented MP4 with FastStart**: Zero corrupted recordings. Captures live into crash-resilient fragmented MP4, then finalizes in <300ms with `-movflags +faststart` so video players open, seek, and play at locked 60 FPS without stutter or lag.
- **🔄 Built-In In-Place Self-Updater**: 1-click update checks directly against GitHub Releases with atomic in-place binary replacement, release notes preview, and zero-hassle updates.
- **⌨️ Global Game Hotkeys**:
  - `F9`: Start, Pause, and Resume recording without leaving your game.
  - `F10`: Stop and save recording instantly.
- **💻 Desktop & Laptop Optimized**:
  - Dynamic responsive layout adapted for 1366x768, 1080p, 1440p, and 4K displays at 100%, 125%, 150%, and 200% PerMonitorV2 DPI scaling.
  - Symmetrical cards that never clip or overflow small laptop screens.

---

## 🎛️ Quality Presets

| Preset | Target Resolution | Target Framerate | Target Bitrate | Ideal Use Case |
| :--- | :--- | :--- | :--- | :--- |
| **Ultra** | Native / 4K | 60 FPS | 36 Mbps | 4K monitors & high-fidelity esports gameplay |
| **Best** | 1080p | 60 FPS | 12 Mbps | Medal.tv / OBS quality gameplay & streaming clips |
| **Standard** | 1080p | 30 FPS | 8 Mbps | Tutorials, software demos, and meetings |
| **Light** | 720p | 30 FPS | 4 Mbps | Laptop battery saving & lightweight Discord clips |

---

## 🚀 Building from Source

### Prerequisites
- Windows 10 (Build 19041+) or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 / VS Code / Windows Terminal

### Build & Run
```powershell
# Clone the repository
git clone https://github.com/crtGhoul/LabRecorder.git
cd LabRecorder

# Build in Release mode
dotnet build -c Release -p:Platform=x64

# Publish standalone single-file executable
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./dist
```

---

## 🏗️ Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                       LAB RECORDER                           │
├──────────────────────────────────────────────────────────────┤
│  Video Capture: Windows Graphics Capture (WGC API)          │
│  Hardware Video Encoder: AMD AMF / NVENC / QSV / MF         │
│  Audio Capture: WASAPI Loopback (Stereo 48kHz PCM)          │
│  Mic Stage: DirectShow -> Volume (+18dB) -> Limiter         │
│  Muxer: Fragmented MP4 -> FastStart Remux (<150ms)           │
└──────────────────────────────────────────────────────────────┘
```

---

## 📜 License
MIT License. Built by Cesar Velazquez ([@crtGhoul](https://github.com/crtGhoul)) for [Velazquez Lab](https://velazquez-lab.pages.dev/).

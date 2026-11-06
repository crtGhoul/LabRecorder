# Strider2 Operating Playbook & Rules for ScreenRecorder (Lab Recorder)

Connected to the **Strider2 Pack** (`c:\Users\velaz\Downloads\AI\Antigravity\strider2-pack`).
Parent rules at `c:\Users\velaz\Downloads\AI\Antigravity\AGENTS.md`.

---

## 1. Project Invariants for Lab Recorder
- **Working Title:** "Lab Recorder" (Cesar decides final branding).
- **Core Principle:** Single-window recorder. Simplicity is the feature. No settings maze, no telemetry, no watermark, no cloud.
- **Hardware Profile:** AMD Radeon RX 9060 XT (`h264_amf`), Logitech C270 HD WebCam, Plantronics Blackwire 5220 Headset Mic.
- **Video Encoders & Bitrates:**
  - Best (1080p60): `-rc vbr_peak -b:v 12M -maxrate 12M -bufsize 24M -g 120`
  - Standard (1080p30): `-rc vbr_peak -b:v 8M -maxrate 8M -bufsize 16M -g 60`
  - Light (720p30): `-rc vbr_peak -b:v 4M -maxrate 4M -bufsize 8M -g 60`
  - Output tags: `-pix_fmt yuv420p -colorspace bt709 -color_primaries bt709 -color_trc bt709`
- **Audio Stage:**
  - Mic leg: `volume=18dB,alimiter=limit=0.79` before mix.
  - Live meter: Post-gain normalized level display with studio dB labels.
  - System audio: WASAPI loopback mixed via `amix=inputs=2:duration=first:dropout_transition=0`.
- **Face Cam Overlay:**
  - Round circular overlay composited inside FFmpeg filter graph (not a floating desktop window).
  - Sizes: 1080p -> Small = 240px, Medium = 320px, Large = 420px; 720p -> 160px / 220px / 280px.
  - Corner positions: Bottom-Right, Bottom-Left, Top-Right, Top-Left.
  - Background effect options: Normal, Blur Background, Remove Background (face only).

---

## 2. Verification & Testing Protocol
- Every release must be built as a self-contained `.exe` with embedded Common Controls v6 manifest.
- Physical testing must test real mouse movement, video/audio playback, and real speech.
- Report exact byte sizes (not rounded estimates), ffprobe streams, audio peak/mean dB, and whole-process-tree RAM.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace LabRecorder
{
    public enum QualityPreset
    {
        Ultra,    // 4K60 / Native 60 (3840x2160, 36 Mbps)
        Best,     // 1080p60 (1920x1080, 12 Mbps) - Medal/OBS Grade
        Standard, // 1080p30 (1920x1080, 8 Mbps)
        Light     // 720p30 (1280x720, 4 Mbps) - Laptop Battery Saver
    }

    public enum HardwareEncoderType
    {
        Amf,             // AMD Radeon AMF (Radeon Desktop / Mobile)
        Nvenc,           // NVIDIA GeForce NVENC (GTX / RTX Desktop & Laptop)
        Qsv,             // Intel QuickSync (Intel Core / Core Ultra Laptop)
        MediaFoundation, // Windows Media Foundation (Universal Windows Laptop GPU)
        Cpu              // libx264 (Software universal fallback)
    }

    public class ScreenTarget
    {
        public string Name { get; set; } = string.Empty;
        public Rectangle Bounds { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public bool IsFullDesktop { get; set; }
        public bool IsCustomRegion { get; set; }
        public int MonitorIndex { get; set; } = 0;

        public override string ToString() => Name;
    }

    public class RecordingResult
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string DurationFormatted { get; set; } = string.Empty;
        public Image? Thumbnail { get; set; }
    }

    public class RecorderEngine : IDisposable
    {
        public string FFmpegPath { get; private set; } = string.Empty;
        public HardwareEncoderType ActiveEncoder { get; private set; } = HardwareEncoderType.Cpu;
        public string EncoderDisplayName => ActiveEncoder switch
        {
            HardwareEncoderType.Amf => "AMD AMF (GPU)",
            HardwareEncoderType.Nvenc => "NVIDIA NVENC (GPU)",
            HardwareEncoderType.Qsv => "Intel QSV (GPU)",
            HardwareEncoderType.MediaFoundation => "Windows MF (GPU)",
            _ => "CPU x264 (Speed)"
        };

        public string EncoderShortBadge => ActiveEncoder switch
        {
            HardwareEncoderType.Amf => "⚡ AMD AMF",
            HardwareEncoderType.Nvenc => "⚡ NVIDIA NVENC",
            HardwareEncoderType.Qsv => "⚡ INTEL QSV",
            HardwareEncoderType.MediaFoundation => "⚡ WINDOWS MF",
            _ => "⚡ CPU OPTIMIZED"
        };

        public bool HasHardwareAccel => ActiveEncoder != HardwareEncoderType.Cpu;
        public bool HasAmfSupport => ActiveEncoder == HardwareEncoderType.Amf;
        public bool IsRecording { get; private set; }
        public bool IsPaused { get; private set; }
        public int LastElapsedSeconds { get; set; } = 0;
        public int ActiveProcessId => _activeProcessId;
        public int CamProcessId => 0;

        public bool IsProcessAlive()
        {
            if (_hProcessScreen == IntPtr.Zero) return false;
            if (GetExitCodeProcess(_hProcessScreen, out uint exitCode))
            {
                return exitCode == 259; // STILL_ACTIVE (0x103 = 259)
            }
            return false;
        }

        // Recording Process (handles real-time single-pass OBS-grade recording: screen + cam overlay + audio)
        private IntPtr _hProcessScreen = IntPtr.Zero;
        private IntPtr _hThreadScreen = IntPtr.Zero;
        private IntPtr _hStdinScreen = IntPtr.Zero;
        private int _activeProcessId = 0;

        private Thread? _sysAudioThread;
        private CancellationTokenSource? _sysAudioCts;
        private string? _currentSysAudioPipe;

        private readonly List<string> _screenSegments = new();
        private string _targetFinalPath = string.Empty;
        private string _baseTempPrefix = string.Empty;
        private int _segmentIndex = 0;

        // Current recording config
        private ScreenTarget _currentScreen = new();
        private QualityPreset _currentQuality = QualityPreset.Best;
        private bool _currentEnableCam = false;
        private bool _currentHasPhysicalCam = false;
        private string? _currentCamName;
        private FaceCamCorner _currentCamCorner = FaceCamCorner.BottomRight;
        private FaceCamSize _currentCamSize = FaceCamSize.Medium;
        private bool _currentEnableMic = true;
        private string? _currentMicName;
        private bool _currentEnableSysAudio = true;

        #region Win32 Process APIs
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public Int32 dwProcessId;
            public Int32 dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CreateProcess(
            string? lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            ref SECURITY_ATTRIBUTES lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);
        #endregion

        public static string FindFFmpegPath()
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string localExe = Path.Combine(appDir, "ffmpeg.exe");
                if (File.Exists(localExe)) return localExe;

                string? mainModuleDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName);
                if (!string.IsNullOrEmpty(mainModuleDir))
                {
                    string moduleLocal = Path.Combine(mainModuleDir, "ffmpeg.exe");
                    if (File.Exists(moduleLocal)) return moduleLocal;
                }
            }
            catch {}

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDataDir = Path.Combine(localAppData, "LabRecorder");
            string appDataFfmpeg = Path.Combine(appDataDir, "ffmpeg.exe");
            if (File.Exists(appDataFfmpeg) && new FileInfo(appDataFfmpeg).Length > 10000000)
            {
                return appDataFfmpeg;
            }

            string sharexExe = @"C:\Program Files\ShareX\ffmpeg.exe";
            if (File.Exists(sharexExe)) return sharexExe;

            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv != null)
            {
                foreach (string p in pathEnv.Split(';'))
                {
                    try
                    {
                        string candidate = Path.Combine(p.Trim(), "ffmpeg.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                    catch {}
                }
            }

            try
            {
                var asm = typeof(RecorderEngine).Assembly;
                using var stream = asm.GetManifestResourceStream("LabRecorder.ffmpeg.exe");
                if (stream != null)
                {
                    if (!Directory.Exists(appDataDir)) Directory.CreateDirectory(appDataDir);
                    using (var fs = new FileStream(appDataFfmpeg, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fs);
                    }
                    if (File.Exists(appDataFfmpeg)) return appDataFfmpeg;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error extracting embedded ffmpeg: " + ex.Message);
            }

            return "ffmpeg.exe";
        }

        public void Initialize()
        {
            FFmpegPath = FindFFmpegPath();
            ActiveEncoder = DetectBestHardwareEncoder();
        }

        public HardwareEncoderType DetectBestHardwareEncoder()
        {
            // Priority 1: AMD AMF (Fastest on AMD RX 9060 XT and Radeon Mobile)
            if (TestEncoder("h264_amf"))
            {
                return HardwareEncoderType.Amf;
            }

            // Priority 2: NVIDIA NVENC (GeForce GTX / RTX laptops & desktops)
            if (TestEncoder("h264_nvenc"))
            {
                return HardwareEncoderType.Nvenc;
            }

            // Priority 3: Intel QuickSync (Intel Core / Core Ultra laptops)
            if (TestEncoder("h264_qsv"))
            {
                return HardwareEncoderType.Qsv;
            }

            // Priority 4: Windows MediaFoundation (Universal Direct3D acceleration on Windows laptops)
            if (TestEncoder("h264_mf"))
            {
                return HardwareEncoderType.MediaFoundation;
            }

            // Priority 5: High-speed multithreaded CPU x264
            return HardwareEncoderType.Cpu;
        }

        private bool TestEncoder(string encoderName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    Arguments = $"-f lavfi -i testsrc=duration=0.5:size=320x240:rate=30 -c:v {encoderName} -f null -",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    p.WaitForExit(3000);
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Encoder test ({encoderName}) error: {ex.Message}");
            }
            return false;
        }

        public static string EnsureCircleMask(int diameter)
        {
            string maskPath = Path.Combine(Path.GetTempPath(), $"labrec_circle_mask_{diameter}.png");
            try
            {
                if (File.Exists(maskPath) && new FileInfo(maskPath).Length > 100)
                {
                    return maskPath;
                }

                using var bmp = new Bitmap(diameter, diameter, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Black);
                    using var brush = new SolidBrush(Color.White);
                    g.FillEllipse(brush, 0, 0, diameter, diameter);
                }
                bmp.Save(maskPath, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("EnsureCircleMask error: " + ex.Message);
            }
            return maskPath;
        }

        public static string EnsureStudioBackdrop(int width, int height)
        {
            string path = Path.Combine(Path.GetTempPath(), $"labrec_studio_bg_{width}x{height}.png");
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > 1000)
                {
                    return path;
                }

                using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    // Obsidian base to Midnight Cosmic Indigo Gradient (Screen Studio signature palette)
                    using var brush = new LinearGradientBrush(
                        new Point(0, 0),
                        new Point(width, height),
                        Color.FromArgb(9, 11, 16),
                        Color.FromArgb(20, 25, 45)
                    );
                    g.FillRectangle(brush, 0, 0, width, height);

                    // Soft ambient spotlight in upper center
                    using var pathGlow = new GraphicsPath();
                    pathGlow.AddEllipse(width * 0.2f, -height * 0.1f, width * 0.6f, height * 0.8f);
                    using var pgb = new PathGradientBrush(pathGlow)
                    {
                        CenterColor = Color.FromArgb(22, 56, 189, 248),
                        SurroundColors = new[] { Color.Transparent }
                    };
                    g.FillPath(pgb, pathGlow);
                }
                bmp.Save(path, ImageFormat.Png);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("EnsureStudioBackdrop error: " + ex.Message);
            }
            return path;
        }

        public static string EnsureRoundedWindowMask(int width, int height, int radius)
        {
            string path = Path.Combine(Path.GetTempPath(), $"labrec_mask_{width}x{height}_r{radius}.png");
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > 500)
                {
                    return path;
                }

                using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Black);
                    using var pathRound = MainForm.CreateRoundedRectangle(new Rectangle(0, 0, width, height), radius);
                    using var brush = new SolidBrush(Color.White);
                    g.FillPath(brush, pathRound);
                }
                bmp.Save(path, ImageFormat.Png);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("EnsureRoundedWindowMask error: " + ex.Message);
            }
            return path;
        }

        public static string EnsureWindowShadow(int innerW, int innerH, int shadowPad, int radius)
        {
            int shadowW = innerW + shadowPad * 2;
            int shadowH = innerH + shadowPad * 2;
            string path = Path.Combine(Path.GetTempPath(), $"labrec_shadow_{shadowW}x{shadowH}.png");
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > 500)
                {
                    return path;
                }

                using var bmp = new Bitmap(shadowW, shadowH, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    // Multi-layer ambient drop shadow
                    for (int layer = 3; layer >= 1; layer--)
                    {
                        int blur = layer * 8;
                        int alpha = (int)(22 * (4 - layer));
                        int yOffset = layer * 4;
                        var rect = new Rectangle(shadowPad - blur / 2, shadowPad - blur / 2 + yOffset, innerW + blur, innerH + blur);
                        using var shadowPath = MainForm.CreateRoundedRectangle(rect, radius + blur / 2);
                        using var shadowBrush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
                        g.FillPath(shadowBrush, shadowPath);
                    }
                }
                bmp.Save(path, ImageFormat.Png);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("EnsureWindowShadow error: " + ex.Message);
            }
            return path;
        }

        public async Task<string?> ApplyStudioCanvasAsync(string inputPath)
        {
            if (!File.Exists(inputPath)) return null;

            string dir = Path.GetDirectoryName(inputPath) ?? "";
            string baseName = Path.GetFileNameWithoutExtension(inputPath);
            string outputPath = Path.Combine(dir, $"{baseName}_studio.mp4");

            // Extract source video resolution
            int srcW = 1920;
            int srcH = 1080;
            try
            {
                var psiProbe = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    Arguments = $"-i \"{inputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                using var pProbe = Process.Start(psiProbe);
                if (pProbe != null)
                {
                    string err = await pProbe.StandardError.ReadToEndAsync();
                    await pProbe.WaitForExitAsync();

                    var m = Regex.Match(err, @"Video:.*?, (\d{3,5})x(\d{3,5})");
                    if (m.Success)
                    {
                        srcW = int.Parse(m.Groups[1].Value);
                        srcH = int.Parse(m.Groups[2].Value);
                    }
                }
            }
            catch {}

            // Output canvas size
            int canvasW = srcW >= 2560 ? 2560 : 1920;
            int canvasH = srcH >= 1440 ? 1440 : 1080;

            // Frame size: 85% of canvas (giving balanced studio padding around the recording)
            int innerW = ((int)(canvasW * 0.86) / 2) * 2;
            int innerH = ((int)(innerW * ((double)srcH / srcW)) / 2) * 2;
            if (innerH > (int)(canvasH * 0.88))
            {
                innerH = ((int)(canvasH * 0.86) / 2) * 2;
                innerW = ((int)(innerH * ((double)srcW / srcH)) / 2) * 2;
            }

            int posX = (canvasW - innerW) / 2;
            int posY = (canvasH - innerH) / 2;

            int shadowPad = 32;
            int shadowPosX = posX - shadowPad;
            int shadowPosY = posY - shadowPad;

            string bgPath = EnsureStudioBackdrop(canvasW, canvasH);
            string maskPath = EnsureRoundedWindowMask(innerW, innerH, 16);
            string shadowPath = EnsureWindowShadow(innerW, innerH, shadowPad, 16);

            string encoderArgs = ActiveEncoder switch
            {
                HardwareEncoderType.Amf => "-c:v h264_amf -usage transcoding -quality speed -rc vbr_peak -b:v 14M -maxrate 14M -bufsize 28M -g 60 -forced_idr 1 -header_spacing 60",
                HardwareEncoderType.Nvenc => "-c:v h264_nvenc -preset p3 -tune zerolatency -b:v 14M -maxrate 14M -bufsize 28M -g 60 -forced_idr 1",
                HardwareEncoderType.Qsv => "-c:v h264_qsv -preset veryfast -b:v 14M -maxrate 14M -bufsize 28M -g 60 -forced_idr 1",
                HardwareEncoderType.MediaFoundation => "-c:v h264_mf -b:v 14M -g 60",
                _ => "-c:v libx264 -preset veryfast -crf 18 -g 60"
            };

            string filterComplex =
                $"[0:v]scale={innerW}:{innerH}:flags=lanczos,format=yuva420p[scrv];" +
                $"[2:v]scale={innerW}:{innerH},format=gray[mask];" +
                $"[scrv][mask]alphamerge[rounded_scr];" +
                $"[1:v][3:v]overlay={shadowPosX}:{shadowPosY}[bg_shadow];" +
                $"[bg_shadow][rounded_scr]overlay={posX}:{posY}[vout]";

            string arguments = $"-y -i \"{inputPath}\" -loop 1 -i \"{bgPath}\" -loop 1 -i \"{maskPath}\" -loop 1 -i \"{shadowPath}\" " +
                               $"-filter_complex \"{filterComplex}\" -map \"[vout]\" -map 0:a? " +
                               $"{encoderArgs} -c:a copy -shortest -movflags +faststart \"{outputPath}\"";

            bool success = await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = FFmpegPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit();
                    return p?.ExitCode == 0 && File.Exists(outputPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ApplyStudioCanvasAsync error: " + ex.Message);
                    return false;
                }
            });

            return success ? outputPath : null;
        }

        public async Task<string?> ExportGifAsync(string inputPath)
        {
            if (!File.Exists(inputPath)) return null;

            string dir = Path.GetDirectoryName(inputPath) ?? "";
            string baseName = Path.GetFileNameWithoutExtension(inputPath);
            string outputPath = Path.Combine(dir, $"{baseName}_clip.gif");

            // Optimal 30 FPS, Lanczos scaler, Bayer dithered palette
            string filter = "fps=30,scale=800:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=128[p];[s1][p]paletteuse=dither=bayer";
            string arguments = $"-y -i \"{inputPath}\" -vf \"{filter}\" \"{outputPath}\"";

            bool success = await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = FFmpegPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit();
                    return p?.ExitCode == 0 && File.Exists(outputPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ExportGifAsync error: " + ex.Message);
                    return false;
                }
            });

            return success ? outputPath : null;
        }

        public void StartRecording(
            ScreenTarget screen,
            QualityPreset quality,
            bool enableCam,
            bool hasPhysicalCam,
            string? camName,
            FaceCamCorner camCorner,
            FaceCamSize camSize,
            bool enableMic,
            string? micName,
            bool enableSysAudio)
        {
            if (IsRecording) return;

            _currentScreen = screen;
            _currentQuality = quality;
            _currentEnableCam = enableCam;
            _currentHasPhysicalCam = hasPhysicalCam;
            _currentCamName = camName;
            _currentCamCorner = camCorner;
            _currentCamSize = camSize;
            _currentEnableMic = enableMic;
            _currentMicName = micName;
            _currentEnableSysAudio = enableSysAudio;

            _screenSegments.Clear();
            _segmentIndex = 0;

            string recordingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Videos", "Lab Recordings");
            if (!Directory.Exists(recordingsDir))
            {
                Directory.CreateDirectory(recordingsDir);
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
            string fileName = $"Recording {timestamp}.mp4";
            _targetFinalPath = Path.Combine(recordingsDir, fileName);
            _baseTempPrefix = Path.Combine(Path.GetTempPath(), $"labrec_{Guid.NewGuid():N}");

            StartSegment();
            IsRecording = true;
            IsPaused = false;
        }

        private void StartSegment()
        {
            // Record segment to temp file with fragmented MP4 for crash resilience
            string screenSegPath = $"{_baseTempPrefix}_scr{_segmentIndex}.mp4";
            _screenSegments.Add(screenSegPath);

            // Handle system audio WASAPI loopback pipe
            if (_currentEnableSysAudio)
            {
                _currentSysAudioPipe = $"labrec_audio_{Guid.NewGuid():N}";
                _sysAudioCts = new CancellationTokenSource();
                var token = _sysAudioCts.Token;
                string pipeName = _currentSysAudioPipe;

                _sysAudioThread = new Thread(() => AudioService.RunSystemAudioLoopback(pipeName, token))
                {
                    IsBackground = true,
                    Name = $"SysAudioWorker_{_segmentIndex}"
                };
                _sysAudioThread.Start();
            }
            else
            {
                _currentSysAudioPipe = null;
            }

            // Launch Real-Time Single-Pass OBS-Grade Recording Process (composites screen + cam + audio directly)
            string screenArgs = BuildScreenArguments(screenSegPath);
            _hProcessScreen = LaunchProcess(screenArgs, out _hThreadScreen, out _hStdinScreen, out _activeProcessId);
        }

        private IntPtr LaunchProcess(string arguments, out IntPtr hThread, out IntPtr hStdinWrite, out int processId)
        {
            string fullCmd = $"\"{FFmpegPath}\" {arguments}";
            Debug.WriteLine("FFmpeg launch: " + fullCmd);

            var sa = new SECURITY_ATTRIBUTES();
            sa.nLength = Marshal.SizeOf(sa);
            sa.bInheritHandle = true;

            IntPtr hStdinRead;
            CreatePipe(out hStdinRead, out hStdinWrite, ref sa, 0);
            SetHandleInformation(hStdinWrite, 1, 0);

            // Redirect stderr to %TEMP%\labrec_ffmpeg.log so all FFmpeg diagnostics are captured
            string logPath = Path.Combine(Path.GetTempPath(), "labrec_ffmpeg.log");
            var saFile = new SECURITY_ATTRIBUTES();
            saFile.nLength = Marshal.SizeOf(saFile);
            saFile.bInheritHandle = true;

            IntPtr hStdErr = CreateFile(
                logPath,
                0x40000000, // GENERIC_WRITE
                1 | 2,      // FILE_SHARE_READ | FILE_SHARE_WRITE
                ref saFile,
                2,          // CREATE_ALWAYS
                0x80,       // FILE_ATTRIBUTE_NORMAL
                IntPtr.Zero);

            var si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = @"WinSta0\Default";
            si.dwFlags = 0x00000100; // STARTF_USESTDHANDLES
            si.hStdInput = hStdinRead;
            si.hStdOutput = IntPtr.Zero;
            si.hStdError = (hStdErr != IntPtr.Zero && hStdErr != new IntPtr(-1)) ? hStdErr : IntPtr.Zero;

            var pi = new PROCESS_INFORMATION();
            // Launch with HIGH_PRIORITY_CLASS (0x00000080) + CREATE_NO_WINDOW (0x08000000) so game loads never starve capture
            uint creationFlags = 0x08000000 | 0x00000080;
            bool success = CreateProcess(null, fullCmd, IntPtr.Zero, IntPtr.Zero, true, creationFlags, IntPtr.Zero, null, ref si, out pi);
            CloseHandle(hStdinRead);
            if (hStdErr != IntPtr.Zero && hStdErr != new IntPtr(-1))
            {
                CloseHandle(hStdErr);
            }

            if (success)
            {
                try
                {
                    using var proc = Process.GetProcessById(pi.dwProcessId);
                    proc.PriorityClass = ProcessPriorityClass.High;
                }
                catch {}

                hThread = pi.hThread;
                processId = pi.dwProcessId;
                return pi.hProcess;
            }
            else
            {
                hThread = IntPtr.Zero;
                processId = 0;
                return IntPtr.Zero;
            }
        }

        private string GetEncoderArguments(QualityPreset preset)
        {
            return ActiveEncoder switch
            {
                HardwareEncoderType.Amf => preset switch
                {
                    QualityPreset.Ultra => "-c:v h264_amf -usage transcoding -quality speed -rc vbr_peak -b:v 36M -maxrate 36M -bufsize 72M -g 60 -forced_idr 1 -header_spacing 60 -bf 0",
                    QualityPreset.Best => "-c:v h264_amf -usage transcoding -quality speed -rc vbr_peak -b:v 12M -maxrate 12M -bufsize 24M -g 60 -forced_idr 1 -header_spacing 60 -bf 0",
                    QualityPreset.Standard => "-c:v h264_amf -usage transcoding -quality balanced -rc vbr_peak -b:v 8M -maxrate 8M -bufsize 16M -g 30 -forced_idr 1 -header_spacing 30 -bf 0",
                    QualityPreset.Light => "-c:v h264_amf -usage transcoding -quality balanced -rc vbr_peak -b:v 4M -maxrate 4M -bufsize 8M -g 30 -forced_idr 1 -header_spacing 30 -bf 0",
                    _ => "-c:v h264_amf -usage transcoding -quality speed -rc vbr_peak -b:v 12M -maxrate 12M -bufsize 24M -g 60 -forced_idr 1 -header_spacing 60 -bf 0"
                },
                HardwareEncoderType.Nvenc => preset switch
                {
                    QualityPreset.Ultra => "-c:v h264_nvenc -preset p3 -tune zerolatency -rc vbr -b:v 36M -maxrate 36M -bufsize 72M -g 120 -bf 0",
                    QualityPreset.Best => "-c:v h264_nvenc -preset p3 -tune zerolatency -rc vbr -b:v 12M -maxrate 12M -bufsize 24M -g 120 -bf 0",
                    QualityPreset.Standard => "-c:v h264_nvenc -preset p4 -tune zerolatency -rc vbr -b:v 8M -maxrate 8M -bufsize 16M -g 60 -bf 0",
                    QualityPreset.Light => "-c:v h264_nvenc -preset p4 -tune zerolatency -rc vbr -b:v 4M -maxrate 4M -bufsize 8M -g 60 -bf 0",
                    _ => "-c:v h264_nvenc -preset p3 -tune zerolatency -rc vbr -b:v 12M -maxrate 12M -bufsize 24M -g 120 -bf 0"
                },
                HardwareEncoderType.Qsv => preset switch
                {
                    QualityPreset.Ultra => "-c:v h264_qsv -preset veryfast -b:v 36M -maxrate 36M -bufsize 72M -g 120 -bf 0",
                    QualityPreset.Best => "-c:v h264_qsv -preset veryfast -b:v 12M -maxrate 12M -bufsize 24M -g 120 -bf 0",
                    QualityPreset.Standard => "-c:v h264_qsv -preset veryfast -b:v 8M -maxrate 8M -bufsize 16M -g 60 -bf 0",
                    QualityPreset.Light => "-c:v h264_qsv -preset faster -b:v 4M -maxrate 4M -bufsize 8M -g 60 -bf 0",
                    _ => "-c:v h264_qsv -preset veryfast -b:v 12M -maxrate 12M -bufsize 24M -g 120 -bf 0"
                },
                HardwareEncoderType.MediaFoundation => preset switch
                {
                    QualityPreset.Ultra => "-c:v h264_mf -b:v 36M -g 120",
                    QualityPreset.Best => "-c:v h264_mf -b:v 12M -g 120",
                    QualityPreset.Standard => "-c:v h264_mf -b:v 8M -g 60",
                    QualityPreset.Light => "-c:v h264_mf -b:v 4M -g 60",
                    _ => "-c:v h264_mf -b:v 12M -g 120"
                },
                _ => preset switch
                {
                    QualityPreset.Ultra => "-c:v libx264 -preset ultrafast -crf 17 -tune zerolatency",
                    QualityPreset.Best => "-c:v libx264 -preset veryfast -crf 18 -tune zerolatency",
                    QualityPreset.Standard => "-c:v libx264 -preset veryfast -crf 23 -tune zerolatency",
                    QualityPreset.Light => "-c:v libx264 -preset faster -crf 28 -tune zerolatency",
                    _ => "-c:v libx264 -preset veryfast -crf 18 -tune zerolatency"
                }
            };
        }

        private string BuildScreenArguments(string outputPath)
        {
            var sb = new StringBuilder();
            sb.Append("-y ");

            int targetFps = (_currentQuality == QualityPreset.Ultra || _currentQuality == QualityPreset.Best) ? 60 : 30;

            int screenX = (_currentScreen.Bounds.X / 2) * 2;
            int screenY = (_currentScreen.Bounds.Y / 2) * 2;
            int screenW = (_currentScreen.Bounds.Width / 4) * 4;
            int screenH = (_currentScreen.Bounds.Height / 2) * 2;

            if (screenW < 32) screenW = 32;
            if (screenH < 32) screenH = 32;

            bool useGfxCapture = !_currentScreen.IsFullDesktop;

            // Target dimensions
            int outW = screenW;
            int outH = screenH;

            // Only downscale if Light (720p) was explicitly requested and the screen is larger than 720p
            if (_currentQuality == QualityPreset.Light && (screenW > 1280 || screenH > 720))
            {
                outW = 1280;
                outH = 720;
            }

            // Input 0: Screen Capture
            if (useGfxCapture)
            {
                int monIdx = _currentScreen.MonitorIndex;
                var screens = Screen.AllScreens;
                if (monIdx < 0 || monIdx >= screens.Length)
                {
                    for (int i = 0; i < screens.Length; i++)
                    {
                        if (screens[i].Bounds.Contains(_currentScreen.Bounds.Location) || screens[i].Bounds.IntersectsWith(_currentScreen.Bounds))
                        {
                            monIdx = i;
                            break;
                        }
                    }
                    if (monIdx < 0 || monIdx >= screens.Length) monIdx = 0;
                }

                if (_currentScreen.IsCustomRegion)
                {
                    int cropLeft = 0;
                    int cropTop = 0;
                    if (monIdx >= 0 && monIdx < screens.Length)
                    {
                        cropLeft = Math.Max(0, (_currentScreen.Bounds.Left - screens[monIdx].Bounds.Left) / 2 * 2);
                        cropTop = Math.Max(0, (_currentScreen.Bounds.Top - screens[monIdx].Bounds.Top) / 2 * 2);
                    }
                    sb.Append($"-thread_queue_size 8192 -probesize 32M -analyzeduration 0 -f lavfi -i \"gfxcapture=monitor_idx={monIdx}:crop_left={cropLeft}:crop_top={cropTop}:width={screenW}:height={screenH}:max_framerate={targetFps}:capture_cursor=1:display_border=0\" ");
                }
                else
                {
                    sb.Append($"-thread_queue_size 8192 -probesize 32M -analyzeduration 0 -f lavfi -i \"gfxcapture=monitor_idx={monIdx}:max_framerate={targetFps}:capture_cursor=1:display_border=0\" ");
                }
            }
            else
            {
                sb.Append($"-thread_queue_size 8192 -rtbufsize 2048M -probesize 32M -analyzeduration 0 -f gdigrab -offset_x {screenX} -offset_y {screenY} -video_size {screenW}x{screenH} -framerate {targetFps} -draw_mouse 1 -i desktop ");
            }

            int nextInputIndex = 1;
            int camInputIndex = -1;
            int maskInputIndex = -1;

            // Cam diameter and margin calculations
            int camDiameter = _currentCamSize switch
            {
                FaceCamSize.Small => outH >= 2160 ? 480 : (outH <= 720 ? 160 : 240),
                FaceCamSize.Medium => outH >= 2160 ? 640 : (outH <= 720 ? 220 : 320),
                FaceCamSize.Large => outH >= 2160 ? 840 : (outH <= 720 ? 280 : 420),
                _ => outH >= 2160 ? 640 : (outH <= 720 ? 220 : 320)
            };
            int margin = outH >= 2160 ? 60 : 30;

            // Input: Webcam (if enabled) + Antialiased Circular Mask
            // Universal laptop & desktop support: do not force fixed video_size so any camera resolution works!
            if (_currentEnableCam && (!string.IsNullOrEmpty(_currentCamName) || !_currentHasPhysicalCam))
            {
                camInputIndex = nextInputIndex++;
                if (_currentHasPhysicalCam && !string.IsNullOrEmpty(_currentCamName))
                {
                    sb.Append($"-thread_queue_size 4096 -rtbufsize 512M -f dshow -i video=\"{_currentCamName}\" ");
                }
                else
                {
                    sb.Append("-f lavfi -i testsrc2=duration=7200:size=640x480:rate=30 ");
                }

                maskInputIndex = nextInputIndex++;
                string maskPath = EnsureCircleMask(camDiameter);
                sb.Append($"-loop 1 -i \"{maskPath}\" ");
            }

            // Input: Microphone
            int micInputIndex = -1;
            if (_currentEnableMic && !string.IsNullOrEmpty(_currentMicName))
            {
                micInputIndex = nextInputIndex++;
                sb.Append($"-thread_queue_size 4096 -rtbufsize 256M -f dshow -i audio=\"{_currentMicName}\" ");
            }

            // Input: System Audio Loopback
            int sysAudioInputIndex = -1;
            if (_currentEnableSysAudio && !string.IsNullOrEmpty(_currentSysAudioPipe))
            {
                sysAudioInputIndex = nextInputIndex++;
                sb.Append($"-thread_queue_size 4096 -rtbufsize 256M -f s16le -ar 48000 -ac 2 -i \"\\\\.\\pipe\\{_currentSysAudioPipe}\" ");
            }

            // Filters
            var filterParts = new List<string>();
            string finalVideoLabel = "basev";

            if (useGfxCapture)
            {
                string scaleClause = (screenW != outW || screenH != outH)
                    ? $",scale={outW}:{outH}:flags=fast_bilinear"
                    : "";
                filterParts.Add($"[0:v]hwdownload,format=bgra{scaleClause},format=yuv420p[basev]");
            }
            else
            {
                string scaleClause = (screenW != outW || screenH != outH)
                    ? $",scale={outW}:{outH}:flags=fast_bilinear"
                    : "";
                filterParts.Add($"[0:v]format=yuv420p{scaleClause}[basev]");
            }

            // If webcam enabled: composited in real-time via alphamerge
            if (camInputIndex >= 0 && maskInputIndex >= 0)
            {
                string overlayCoords = _currentCamCorner switch
                {
                    FaceCamCorner.TopLeft => $"x={margin}:y={margin}",
                    FaceCamCorner.TopRight => $"x=W-w-{margin}:y={margin}",
                    FaceCamCorner.BottomLeft => $"x={margin}:y=H-h-{margin}",
                    FaceCamCorner.BottomRight => $"x=W-w-{margin}:y=H-h-{margin}",
                    _ => $"x=W-w-{margin}:y=H-h-{margin}"
                };

                filterParts.Add($"[{camInputIndex}:v]hflip,crop=min(iw\\,ih):min(iw\\,ih),scale={camDiameter}:{camDiameter},format=yuva420p[cam_sq]");
                filterParts.Add($"[{maskInputIndex}:v]scale={camDiameter}:{camDiameter},format=gray[mask]");
                filterParts.Add($"[cam_sq][mask]alphamerge[cam_circ]");
                filterParts.Add($"[basev][cam_circ]overlay={overlayCoords}:shortest=1[vout]");
                finalVideoLabel = "vout";
            }

            // Audio Filters
            string? finalAudioLabel = null;
            if (micInputIndex >= 0 && sysAudioInputIndex >= 0)
            {
                filterParts.Add($"[{micInputIndex}:a]volume=18dB,alimiter=limit=0.79[mic_proc]");
                filterParts.Add($"[{sysAudioInputIndex}:a]volume=1.0[sys_proc]");
                filterParts.Add($"[mic_proc][sys_proc]amix=inputs=2:duration=first:dropout_transition=0[aout]");
                finalAudioLabel = "aout";
            }
            else if (micInputIndex >= 0)
            {
                filterParts.Add($"[{micInputIndex}:a]volume=18dB,alimiter=limit=0.79[mic_proc]");
                finalAudioLabel = "mic_proc";
            }
            else if (sysAudioInputIndex >= 0)
            {
                finalAudioLabel = $"{sysAudioInputIndex}:a";
            }

            if (filterParts.Count > 0)
            {
                sb.Append($"-filter_complex \"{string.Join("; ", filterParts)}\" ");
            }

            sb.Append($"-map \"[{finalVideoLabel}]\" ");

            if (finalAudioLabel != null)
            {
                if (finalAudioLabel.Contains(":")) sb.Append($"-map {finalAudioLabel} ");
                else sb.Append($"-map \"[{finalAudioLabel}]\" ");
                sb.Append("-c:a aac -b:a 192k ");
            }
            else
            {
                sb.Append("-an ");
            }

            // Video Encoder: dynamically configured for AMD AMF, NVIDIA NVENC, Intel QSV, Windows MF, or CPU
            sb.Append($"{GetEncoderArguments(_currentQuality)} ");
            sb.Append("-pix_fmt yuv420p -colorspace bt709 -color_primaries bt709 -color_trc 1 ");
            sb.Append("-fps_mode cfr -flags +low_delay ");
            sb.Append("-max_muxing_queue_size 8192 ");
            // Fragmented MP4: Writes empty MOOV at byte 0 and flushes every keyframe fragment live.
            // Eliminates "moov atom not found" black screen corruption completely!
            sb.Append("-movflags +frag_keyframe+empty_moov+default_base_moof ");
            sb.Append($"\"{outputPath}\"");

            return sb.ToString();
        }

        public void PauseRecording()
        {
            if (!IsRecording || IsPaused) return;

            StopAllProcesses();
            IsPaused = true;
        }

        public void ResumeRecording()
        {
            if (!IsRecording || !IsPaused) return;

            _segmentIndex++;
            StartSegment();
            IsPaused = false;
        }

        private void StopAllProcesses()
        {
            StopSingleProcess(ref _hProcessScreen, ref _hThreadScreen, ref _hStdinScreen);
            _activeProcessId = 0;

            if (_sysAudioCts != null)
            {
                _sysAudioCts.Cancel();
                _sysAudioThread?.Join(500);
                _sysAudioCts.Dispose();
                _sysAudioCts = null;
                _sysAudioThread = null;
            }
        }

        private void StopSingleProcess(ref IntPtr hProcess, ref IntPtr hThread, ref IntPtr hStdinWrite)
        {
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    if (hStdinWrite != IntPtr.Zero)
                    {
                        byte[] qBytes = new byte[] { (byte)'q', 10 };
                        using (var fs = new FileStream(new SafeFileHandle(hStdinWrite, true), FileAccess.Write))
                        {
                            fs.Write(qBytes, 0, qBytes.Length);
                            fs.Flush();
                        }
                        hStdinWrite = IntPtr.Zero;
                    }
                    if (WaitForSingleObject(hProcess, 10000) != 0)
                    {
                        TerminateProcess(hProcess, 1);
                    }
                }
                catch
                {
                    try { TerminateProcess(hProcess, 1); } catch {}
                }
                finally
                {
                    CloseHandle(hProcess);
                    CloseHandle(hThread);
                    hProcess = IntPtr.Zero;
                    hThread = IntPtr.Zero;
                }
            }
        }

        public RecordingResult StopRecording()
        {
            if (!IsRecording) return new RecordingResult();

            StopAllProcesses();
            IsRecording = false;
            IsPaused = false;

            string finalFile = _targetFinalPath;

            // Filter valid segments
            var validSegments = new List<string>();
            foreach (var seg in _screenSegments)
            {
                if (File.Exists(seg) && new FileInfo(seg).Length > 1000)
                {
                    validSegments.Add(seg);
                }
            }

            if (validSegments.Count == 1)
            {
                string singleSeg = validSegments[0];
                try
                {
                    // Remux the single segment with -movflags +faststart to produce finalFile with valid MOOV header
                    var psi = new ProcessStartInfo
                    {
                        FileName = FFmpegPath,
                        Arguments = $"-y -i \"{singleSeg}\" -c copy -movflags +faststart \"{finalFile}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(20000);
                    if (File.Exists(finalFile) && new FileInfo(finalFile).Length > 1000)
                    {
                        try { File.Delete(singleSeg); } catch {}
                    }
                    else
                    {
                        if (File.Exists(finalFile)) File.Delete(finalFile);
                        File.Move(singleSeg, finalFile);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Finalize faststart error: " + ex.Message);
                    if (!File.Exists(finalFile) && File.Exists(singleSeg))
                    {
                        try { File.Move(singleSeg, finalFile); } catch {}
                    }
                }
            }
            else if (validSegments.Count > 1)
            {
                // Concat paused segments
                string concatFile = $"{_baseTempPrefix}_concat.txt";
                var lines = new List<string>();
                foreach (var seg in validSegments)
                {
                    lines.Add($"file '{seg.Replace("\\", "/")}'");
                }
                File.WriteAllLines(concatFile, lines);

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = FFmpegPath,
                        Arguments = $"-y -f concat -safe 0 -i \"{concatFile}\" -c copy -movflags +faststart \"{finalFile}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(30000);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Concat segments error: " + ex.Message);
                }

                try { File.Delete(concatFile); } catch {}
                foreach (var seg in validSegments)
                {
                    try { File.Delete(seg); } catch {}
                }
            }

            var result = new RecordingResult
            {
                FilePath = finalFile,
                FileName = Path.GetFileName(finalFile)
            };

            if (File.Exists(finalFile) && new FileInfo(finalFile).Length > 1000)
            {
                var fi = new FileInfo(finalFile);
                result.FileSizeBytes = fi.Length;
                result.FileSizeFormatted = FormatBytes(fi.Length);
                result.Duration = ExtractDuration(finalFile);
                if (result.Duration == TimeSpan.Zero && LastElapsedSeconds > 0)
                {
                    result.Duration = TimeSpan.FromSeconds(LastElapsedSeconds);
                }
                result.DurationFormatted = $"{(int)result.Duration.TotalMinutes:00}:{result.Duration.Seconds:00}";
                result.Thumbnail = ExtractThumbnail(finalFile);
            }

            return result;
        }

        public TimeSpan ExtractDuration(string videoPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    Arguments = $"-i \"{videoPath}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    string err = p.StandardError.ReadToEnd();
                    p.WaitForExit(3000);
                    var match = Regex.Match(err, @"Duration:\s+(\d+):(\d+):(\d+\.\d+)");
                    if (match.Success)
                    {
                        int hours = int.Parse(match.Groups[1].Value);
                        int mins = int.Parse(match.Groups[2].Value);
                        double secs = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                        return new TimeSpan(0, hours, mins, (int)secs, (int)((secs - (int)secs) * 1000));
                    }
                }
            }
            catch {}
            return TimeSpan.Zero;
        }

        public Image? ExtractThumbnail(string videoPath)
        {
            string thumbPath = $"{_baseTempPrefix}_thumb.jpg";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    Arguments = $"-y -ss 00:00:00.5 -i \"{videoPath}\" -vframes 1 -q:v 2 \"{thumbPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(3000);

                if (File.Exists(thumbPath))
                {
                    byte[] bytes = File.ReadAllBytes(thumbPath);
                    File.Delete(thumbPath);
                    using var ms = new MemoryStream(bytes);
                    return Image.FromStream(ms);
                }
            }
            catch {}
            return null;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):0.0} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{(bytes / (1024.0 * 1024.0)):0.1} MB";
            return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):0.2} GB";
        }

        public void Dispose()
        {
            if (IsRecording)
            {
                StopRecording();
            }
        }
    }
}

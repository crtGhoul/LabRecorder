using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LabRecorder
{
    internal static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint uMilliseconds);

        [STAThread]
        static void Main(string[] args)
        {
            TimeBeginPeriod(1); // Enforce 1ms high-precision Windows timer for the entire process tree (eliminates 15.6ms sleep jitter)
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                string msg = $"Unhandled Exception: {e.ExceptionObject}";
                try { File.AppendAllText("crash.log", $"{DateTime.Now}: {msg}\r\n"); } catch {}
                MessageBox.Show(msg, "Lab Recorder Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            Application.ThreadException += (s, e) =>
            {
                string msg = $"Thread Exception: {e.Exception}";
                try { File.AppendAllText("crash.log", $"{DateTime.Now}: {msg}\r\n"); } catch {}
                MessageBox.Show(msg, "Lab Recorder Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            if (args.Length > 0 && args[0] == "--acceptance-test")
            {
                AttachConsole(-1);
                RunAcceptanceTest();
                return;
            }

            if (args.Length > 0 && args[0] == "--test-all")
            {
                AttachConsole(-1);
                RunAutomatedTests();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.Run(new MainForm());
        }

        static void RunAcceptanceTest()
        {
            try { File.Delete("acceptance_test_report.log"); } catch {}
            void Log(string msg)
            {
                Console.WriteLine(msg);
                try { File.AppendAllText("acceptance_test_report.log", msg + Environment.NewLine); } catch {}
            }

            Log("=================================================");
            Log("LAB RECORDER - FIX 4 ACCEPTANCE TEST");
            Log("=================================================");

            var engine = new RecorderEngine();
            engine.Initialize();

            Log($"FFmpeg: {engine.FFmpegPath}");
            Log($"Hardware Acceleration: {engine.EncoderDisplayName} ({engine.EncoderShortBadge})");

            var mics = AudioService.EnumerateMicrophones();
            string? testMic = mics.Count > 0 ? mics[0].Name : null;
            Log($"Mic: {testMic ?? "None"}");

            var cams = CameraService.DetectCameras(engine.FFmpegPath);
            bool hasRealCam = cams.Count > 0;
            string? testCam = hasRealCam ? cams[0] : null;
            Log($"Camera: {testCam ?? "None"}");

            var screens = Screen.AllScreens;
            int primIdx = 0;
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i].Primary) { primIdx = i; break; }
            }
            var primaryScreen = new ScreenTarget
            {
                Name = $"Monitor {primIdx + 1} (Primary)",
                Bounds = screens[primIdx].Bounds,
                IsFullDesktop = false,
                MonitorIndex = primIdx
            };
            Log($"Screen: {primaryScreen.Bounds.Width}x{primaryScreen.Bounds.Height}");

            long idleRam = Process.GetCurrentProcess().WorkingSet64;
            Log($"Process Idle RAM: {(idleRam / (1024 * 1024.0)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} MB");

            // Start background audio playback (system audio loopback)
            System.Media.SoundPlayer? player = null;
            try
            {
                if (File.Exists(@"C:\Windows\Media\Alarm01.wav"))
                {
                    player = new System.Media.SoundPlayer(@"C:\Windows\Media\Alarm01.wav");
                    player.PlayLooping();
                    Log("System audio playing Alarm01.wav loop...");
                }
            }
            catch (Exception ex)
            {
                Log("Audio playback notice: " + ex.Message);
            }

            Log("\n[1/3] Starting Segment 1 (Target: 30s) at Best (1080p60)...");
            engine.StartRecording(
                primaryScreen,
                QualityPreset.Best,
                enableCam: true,
                hasPhysicalCam: hasRealCam,
                camName: testCam,
                FaceCamCorner.BottomRight,
                FaceCamSize.Small, // Verify Small diameter (240px)
                enableMic: testMic != null,
                micName: testMic,
                enableSysAudio: true
            );

            // Wait 2s to allow processes to stabilize, then measure RAM
            Thread.Sleep(2000);
            long recRamTree = MeasureProcessTreeMemory(engine.ActiveProcessId, engine.CamProcessId);
            Log($"Recording RAM (Whole Process Tree: App + FFmpeg Screen + FFmpeg Cam): {(recRamTree / (1024 * 1024.0)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} MB");

            // Launch moving window to simulate window dragging and active UI motion
            var animCts = new CancellationTokenSource();
            var animThread = new Thread(() =>
            {
                try
                {
                    var animForm = new Form
                    {
                        Text = "Motion Generator - Lab Recorder Acceptance Test",
                        Size = new Size(500, 350),
                        StartPosition = FormStartPosition.Manual,
                        Location = new Point(200, 200),
                        TopMost = true,
                        BackColor = Color.FromArgb(20, 30, 50)
                    };
                    var lbl = new Label
                    {
                        Text = "Motion & Audio Test Active\n1080p60 AMF Capture",
                        Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                        ForeColor = Color.Yellow,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    animForm.Controls.Add(lbl);

                    int step = 0;
                    animForm.Show();
                    while (!animCts.IsCancellationRequested)
                    {
                        step++;
                        int nx = 200 + (int)(300 * Math.Sin(step * 0.05));
                        int ny = 150 + (int)(150 * Math.Cos(step * 0.05));
                        animForm.Location = new Point(nx, ny);
                        lbl.ForeColor = (step % 2 == 0) ? Color.Yellow : Color.Cyan;
                        Application.DoEvents();
                        Thread.Sleep(25);
                    }
                    animForm.Close();
                }
                catch {}
            });
            animThread.SetApartmentState(ApartmentState.STA);
            animThread.Start();

            // Move mouse around to generate real 60fps screen changes for 28 more seconds
            int steps = 280;
            for (int i = 0; i < steps; i++)
            {
                int cx = primaryScreen.Bounds.X + (primaryScreen.Bounds.Width / 2);
                int cy = primaryScreen.Bounds.Y + (primaryScreen.Bounds.Height / 2);
                int x = cx + (int)(400 * Math.Sin(i * 0.1));
                int y = cy + (int)(250 * Math.Cos(i * 0.1));
                SetCursorPos(x, y);
                Thread.Sleep(100);
            }

            Log("\n[2/3] Pausing for ~5 seconds...");
            engine.PauseRecording();
            Thread.Sleep(5000);

            Log("\n[3/3] Resuming for Segment 2 (Target: 30s)...");
            engine.ResumeRecording();

            // Run second segment for 30 seconds with continuous screen motion
            for (int i = 0; i < 300; i++)
            {
                int cx = primaryScreen.Bounds.X + (primaryScreen.Bounds.Width / 2);
                int cy = primaryScreen.Bounds.Y + (primaryScreen.Bounds.Height / 2);
                int x = cx + (int)(350 * Math.Cos(i * 0.1));
                int y = cy + (int)(200 * Math.Sin(i * 0.1));
                SetCursorPos(x, y);
                Thread.Sleep(100);
            }

            Log("\nStopping recording and compositing...");
            animCts.Cancel();
            animThread.Join(500);

            var result = engine.StopRecording();

            try { player?.Stop(); } catch {}

            Log("\n=================================================");
            Log("ACCEPTANCE TEST FINISHED");
            Log($"File: {result.FilePath}");
            Log($"Size: {result.FileSizeBytes} bytes ({result.FileSizeFormatted})");
            Log($"Duration: {result.DurationFormatted} ({result.Duration.TotalSeconds:0.2} seconds)");
            Log($"Thumbnail extracted: {result.Thumbnail != null}");
            Log("=================================================");
        }

        static void RunAutomatedTests()
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("LAB RECORDER - AUTOMATED HARDWARE TEST SUITE");
            Console.WriteLine("=================================================");

            var engine = new RecorderEngine();
            engine.Initialize();

            Console.WriteLine($"FFmpeg Path: {engine.FFmpegPath}");
            Console.WriteLine($"AMF Hardware Encoding Support: {engine.HasAmfSupport}");

            var mics = AudioService.EnumerateMicrophones();
            Console.WriteLine($"Microphones Found: {mics.Count}");
            foreach (var m in mics)
            {
                Console.WriteLine($"  - {m.Name} ({m.Id})");
            }
            string? testMic = mics.Count > 0 ? mics[0].Name : null;

            var cams = CameraService.DetectCameras(engine.FFmpegPath);
            Console.WriteLine($"Cameras Found: {cams.Count}");
            foreach (var c in cams)
            {
                Console.WriteLine($"  - {c}");
            }
            bool hasRealCam = cams.Count > 0;
            string? testCam = hasRealCam ? cams[0] : null;

            var screens = Screen.AllScreens;
            Console.WriteLine($"Screens Found: {screens.Length}");
            for (int i = 0; i < screens.Length; i++)
            {
                Console.WriteLine($"  - Monitor {i + 1}: {screens[i].Bounds.Width}x{screens[i].Bounds.Height} at ({screens[i].Bounds.X},{screens[i].Bounds.Y})");
            }

            int cliPrimIdx = 0;
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i].Primary) { cliPrimIdx = i; break; }
            }
            var primaryScreen = new ScreenTarget
            {
                Name = $"Monitor {cliPrimIdx + 1}",
                Bounds = screens[cliPrimIdx].Bounds,
                IsFullDesktop = false,
                MonitorIndex = cliPrimIdx
            };

            // Test 1: Best Preset (1080p60) + Measure RAM
            Console.WriteLine("\n--- TEST 1: BEST PRESET (1080p60) ---");
            long idleRam = Process.GetCurrentProcess().WorkingSet64;
            Console.WriteLine($"Process Idle RAM: {idleRam / (1024 * 1024.0):0.1} MB");

            engine.StartRecording(
                primaryScreen,
                QualityPreset.Best,
                enableCam: true,
                hasPhysicalCam: hasRealCam,
                camName: testCam,
                FaceCamCorner.BottomRight,
                FaceCamSize.Medium,
                enableMic: testMic != null,
                micName: testMic,
                enableSysAudio: true
            );

            Thread.Sleep(2000);

            long recRamTree = MeasureProcessTreeMemory(engine.ActiveProcessId);
            Console.WriteLine($"Recording RAM (Whole Process Tree: App + FFmpeg): {recRamTree / (1024 * 1024.0):0.1} MB");

            Thread.Sleep(2500);
            var resBest = engine.StopRecording();
            Console.WriteLine($"Test 1 Finished: {resBest.FileName}");
            Console.WriteLine($"  Path: {resBest.FilePath}");
            Console.WriteLine($"  File Size: {resBest.FileSizeFormatted} ({resBest.FileSizeBytes} bytes)");
            Console.WriteLine($"  Duration: {resBest.DurationFormatted} ({resBest.Duration.TotalSeconds:0.1}s)");
            Console.WriteLine($"  Has Thumbnail: {resBest.Thumbnail != null}");

            // Test 2: Standard Preset (1080p30)
            Console.WriteLine("\n--- TEST 2: STANDARD PRESET (1080p30) ---");
            engine.StartRecording(
                primaryScreen,
                QualityPreset.Standard,
                enableCam: true,
                hasPhysicalCam: hasRealCam,
                camName: testCam,
                FaceCamCorner.BottomRight,
                FaceCamSize.Medium,
                enableMic: testMic != null,
                micName: testMic,
                enableSysAudio: true
            );
            Thread.Sleep(4500);
            var resStd = engine.StopRecording();
            Console.WriteLine($"Test 2 Finished: {resStd.FileName}");
            Console.WriteLine($"  Path: {resStd.FilePath}");
            Console.WriteLine($"  File Size: {resStd.FileSizeFormatted} ({resStd.FileSizeBytes} bytes)");
            Console.WriteLine($"  Duration: {resStd.DurationFormatted} ({resStd.Duration.TotalSeconds:0.1}s)");
            Console.WriteLine($"  Has Thumbnail: {resStd.Thumbnail != null}");

            // Test 3: Light Preset (720p30)
            Console.WriteLine("\n--- TEST 3: LIGHT PRESET (720p30) ---");
            engine.StartRecording(
                primaryScreen,
                QualityPreset.Light,
                enableCam: true,
                hasPhysicalCam: hasRealCam,
                camName: testCam,
                FaceCamCorner.BottomRight,
                FaceCamSize.Medium,
                enableMic: testMic != null,
                micName: testMic,
                enableSysAudio: true
            );
            Thread.Sleep(4500);
            var resLight = engine.StopRecording();
            Console.WriteLine($"Test 3 Finished: {resLight.FileName}");
            Console.WriteLine($"  Path: {resLight.FilePath}");
            Console.WriteLine($"  File Size: {resLight.FileSizeFormatted} ({resLight.FileSizeBytes} bytes)");
            Console.WriteLine($"  Duration: {resLight.DurationFormatted} ({resLight.Duration.TotalSeconds:0.1}s)");
            Console.WriteLine($"  Has Thumbnail: {resLight.Thumbnail != null}");

            // Test 4: Pause / Resume Test
            Console.WriteLine("\n--- TEST 4: PAUSE / RESUME TEST ---");
            engine.StartRecording(
                primaryScreen,
                QualityPreset.Standard,
                enableCam: true,
                hasPhysicalCam: hasRealCam,
                camName: testCam,
                FaceCamCorner.BottomRight,
                FaceCamSize.Medium,
                enableMic: testMic != null,
                micName: testMic,
                enableSysAudio: true
            );
            Thread.Sleep(2000);
            Console.WriteLine("Pausing recording...");
            engine.PauseRecording();
            Thread.Sleep(1500);
            Console.WriteLine("Resuming recording...");
            engine.ResumeRecording();
            Thread.Sleep(2000);
            var resPause = engine.StopRecording();
            Console.WriteLine($"Test 4 Finished: {resPause.FileName}");
            Console.WriteLine($"  Path: {resPause.FilePath}");
            Console.WriteLine($"  File Size: {resPause.FileSizeFormatted} ({resPause.FileSizeBytes} bytes)");
            Console.WriteLine($"  Duration: {resPause.DurationFormatted} ({resPause.Duration.TotalSeconds:0.1}s)");
            Console.WriteLine($"  Has Thumbnail: {resPause.Thumbnail != null}");

            Console.WriteLine("\n=================================================");
            Console.WriteLine("ALL AUTOMATED TESTS COMPLETED!");
            Console.WriteLine("=================================================");
        }

        static long MeasureProcessTreeMemory(int screenPid, int camPid = 0)
        {
            long total = 0;
            try
            {
                var cur = Process.GetCurrentProcess();
                total += cur.WorkingSet64;
                if (screenPid > 0)
                {
                    try
                    {
                        var child = Process.GetProcessById(screenPid);
                        total += child.WorkingSet64;
                    }
                    catch {}
                }
                if (camPid > 0)
                {
                    try
                    {
                        var child = Process.GetProcessById(camPid);
                        total += child.WorkingSet64;
                    }
                    catch {}
                }
            }
            catch {}
            return total;
        }
    }
}

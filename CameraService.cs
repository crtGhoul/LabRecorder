using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace LabRecorder
{
    public enum FaceCamCorner
    {
        BottomRight,
        BottomLeft,
        TopRight,
        TopLeft
    }

    public enum FaceCamSize
    {
        Small,
        Medium,
        Large
    }

    public class CameraService : IDisposable
    {
        public bool HasPhysicalCamera { get; private set; }
        public string? CameraName { get; private set; }
        private string _ffmpegPath = string.Empty;

        private Thread? _previewThread;
        private CancellationTokenSource? _previewCts;
        private Process? _previewProcess;
        private Action<Image>? _onPreviewFrame;

        public static List<string> DetectCameras(string ffmpegPath)
        {
            var cameras = new List<string>();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-list_devices true -f dshow -i dummy",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    string output = p.StandardError.ReadToEnd();
                    p.WaitForExit(3000);

                    var matches = Regex.Matches(output, @"\""([^\""]+)\""\s+\(video\)");
                    foreach (Match m in matches)
                    {
                        if (m.Groups.Count > 1)
                        {
                            cameras.Add(m.Groups[1].Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DetectCameras error: " + ex.Message);
            }
            return cameras;
        }

        public void Initialize(string ffmpegPath)
        {
            _ffmpegPath = ffmpegPath;
            var list = DetectCameras(ffmpegPath);
            if (list.Count > 0)
            {
                HasPhysicalCamera = true;
                CameraName = list[0];
            }
            else
            {
                HasPhysicalCamera = false;
                CameraName = null;
            }
        }

        public void StartPreview(Action<Image> onFrame)
        {
            StopPreview();
            _onPreviewFrame = onFrame;
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            if (HasPhysicalCamera && !string.IsNullOrEmpty(CameraName))
            {
                _previewThread = new Thread(() => RunRealCamPreview(token))
                {
                    IsBackground = true,
                    Name = "RealCamPreviewThread"
                };
                _previewThread.Start();
            }
            else
            {
                _previewThread = new Thread(() => RunFallbackPreview(token))
                {
                    IsBackground = true,
                    Name = "FallbackCamPreviewThread"
                };
                _previewThread.Start();
            }
        }

        private void RunRealCamPreview(CancellationToken token)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = $"-y -f dshow -i video=\"{CameraName}\" -vf \"scale=240:240\" -f image2pipe -vcodec mjpeg -r 15 pipe:1",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _previewProcess = Process.Start(psi);
                if (_previewProcess == null) return;

                var stream = _previewProcess.StandardOutput.BaseStream;
                using var ms = new MemoryStream();
                bool inJpeg = false;

                while (!token.IsCancellationRequested && !_previewProcess.HasExited)
                {
                    int b = stream.ReadByte();
                    if (b == -1) break;

                    if (b == 0xFF)
                    {
                        int b2 = stream.ReadByte();
                        if (b2 == -1) break;

                        if (b2 == 0xD8) // SOI
                        {
                            ms.SetLength(0);
                            ms.WriteByte(0xFF);
                            ms.WriteByte(0xD8);
                            inJpeg = true;
                            continue;
                        }
                        else if (b2 == 0xD9 && inJpeg) // EOI
                        {
                            ms.WriteByte(0xFF);
                            ms.WriteByte(0xD9);
                            inJpeg = false;

                            byte[] jpegData = ms.ToArray();
                            if (jpegData.Length > 100)
                            {
                                try
                                {
                                    using var rawMs = new MemoryStream(jpegData);
                                    using var rawImg = Image.FromStream(rawMs);
                                    var circle = CreateCircularPreview(rawImg, 160);
                                    _onPreviewFrame?.Invoke(circle);
                                }
                                catch {}
                            }
                            continue;
                        }
                        else if (inJpeg)
                        {
                            ms.WriteByte(0xFF);
                            ms.WriteByte((byte)b2);
                            continue;
                        }
                    }

                    if (inJpeg)
                    {
                        ms.WriteByte((byte)b);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RunRealCamPreview exception: " + ex.Message);
            }
            finally
            {
                try
                {
                    if (_previewProcess != null && !_previewProcess.HasExited)
                    {
                        _previewProcess.Kill();
                        _previewProcess.Dispose();
                        _previewProcess = null;
                    }
                }
                catch {}
            }
        }

        private void RunFallbackPreview(CancellationToken token)
        {
            int counter = 0;
            while (!token.IsCancellationRequested)
            {
                counter++;
                var bmp = GenerateFallbackImage(160, 160, counter);
                _onPreviewFrame?.Invoke(bmp);
                Thread.Sleep(50);
            }
        }

        private static Bitmap CreateCircularPreview(Image source, int targetSize)
        {
            var bmp = new Bitmap(targetSize, targetSize);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(28, 30, 36));

            int diameter = targetSize - 12;
            int x = (targetSize - diameter) / 2;
            int y = (targetSize - diameter) / 2;

            using (var path = new GraphicsPath())
            {
                path.AddEllipse(x, y, diameter, diameter);
                g.SetClip(path);

                // Mirror for natural selfie preview
                var destRect = new Rectangle(x, y, diameter, diameter);
                g.DrawImage(source, destRect, new Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
                g.ResetClip();
            }

            using (var borderPen = new Pen(Color.FromArgb(0, 140, 255), 3f))
            {
                g.DrawEllipse(borderPen, x, y, diameter, diameter);
            }

            return bmp;
        }

        private static Bitmap GenerateFallbackImage(int width, int height, int counter)
        {
            var bmp = new Bitmap(width, height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(28, 30, 36));

            int diameter = Math.Min(width, height) - 12;
            int x = (width - diameter) / 2;
            int y = (height - diameter) / 2;

            using (var path = new GraphicsPath())
            {
                path.AddEllipse(x, y, diameter, diameter);
                g.SetClip(path);

                using var bg = new SolidBrush(Color.FromArgb(38, 44, 56));
                g.FillRectangle(bg, x, y, diameter, diameter);

                using var avatarBrush = new SolidBrush(Color.FromArgb(70, 80, 100));
                int headSize = diameter / 3;
                int headX = (width - headSize) / 2;
                int headY = y + 25;
                g.FillEllipse(avatarBrush, headX, headY, headSize, headSize);

                int bodyWidth = (int)(diameter * 0.7);
                int bodyHeight = diameter / 2;
                int bodyX = (width - bodyWidth) / 2;
                int bodyY = headY + headSize + 6;
                g.FillEllipse(avatarBrush, bodyX, bodyY, bodyWidth, bodyHeight);

                int pulse = (int)(Math.Sin(counter * 0.15) * 4);
                using var pulsePen = new Pen(Color.FromArgb(0, 180, 255), 2);
                g.DrawEllipse(pulsePen, headX - pulse, headY - pulse, headSize + pulse * 2, headSize + pulse * 2);

                using var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.FromArgb(200, 210, 225));
                var sz = g.MeasureString("No Camera", font);
                g.DrawString("No Camera", font, textBrush, (width - sz.Width) / 2, y + diameter - 26);

                g.ResetClip();
            }

            using (var borderPen = new Pen(Color.FromArgb(0, 140, 255), 3f))
            {
                g.DrawEllipse(borderPen, x, y, diameter, diameter);
            }

            return bmp;
        }

        public void StopPreview()
        {
            if (_previewCts != null)
            {
                _previewCts.Cancel();
                try
                {
                    if (_previewProcess != null && !_previewProcess.HasExited)
                    {
                        _previewProcess.Kill();
                        _previewProcess.WaitForExit(1000);
                        _previewProcess.Dispose();
                        _previewProcess = null;
                    }
                }
                catch {}

                _previewThread?.Join(500);
                _previewCts.Dispose();
                _previewCts = null;
                _previewThread = null;
            }
            _onPreviewFrame = null;
        }

        public void Dispose()
        {
            StopPreview();
        }
    }
}

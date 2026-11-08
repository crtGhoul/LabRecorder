using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LabRecorder
{
    public class ModernButton : Button
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color NormalColor { get; set; } = Color.FromArgb(20, 25, 36);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color HoverColor { get; set; } = Color.FromArgb(32, 40, 58);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color PressedColor { get; set; } = Color.FromArgb(14, 18, 26);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; } = Color.FromArgb(42, 52, 74);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int BorderRadius { get; set; } = 8;

        private bool _isHovered;
        private bool _isPressed;

        public ModernButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.25f, FontStyle.Bold);
            ForeColor = Color.White;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent) { _isPressed = true; Invalidate(); base.OnMouseDown(mevent); }
        protected override void OnMouseUp(MouseEventArgs mevent) { _isPressed = false; Invalidate(); base.OnMouseUp(mevent); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg = !Enabled ? Color.FromArgb(16, 20, 28) :
                       _isPressed ? PressedColor :
                       _isHovered ? HoverColor : NormalColor;
            Color textColor = !Enabled ? Color.FromArgb(80, 95, 120) : ForeColor;
            Color border = !Enabled ? Color.FromArgb(28, 35, 48) : BorderColor;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = MainForm.CreateRoundedRectangle(rect, BorderRadius);
            using var brush = new SolidBrush(bg);
            g.FillPath(brush, path);

            // Subtle top edge specular highlight (Hallmark craft)
            if (Enabled && !_isPressed && Height > 20)
            {
                using var highlightPen = new Pen(Color.FromArgb(24, 255, 255, 255), 1f);
                g.DrawLine(highlightPen, BorderRadius / 2, 1, Width - 1 - BorderRadius / 2, 1);
            }

            if (border != Color.Transparent)
            {
                using var pen = new Pen(border, 1f);
                g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, Text, Font, rect, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    public class ModernCard : Panel
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color CardBackground { get; set; } = Color.FromArgb(16, 20, 29);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; } = Color.FromArgb(32, 40, 58);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int BorderRadius { get; set; } = 12;

        public ModernCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
            Padding = new Padding(16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = MainForm.CreateRoundedRectangle(rect, BorderRadius);
            using var brush = new SolidBrush(CardBackground);
            g.FillPath(brush, path);

            // Subtle top specular hairline
            if (Height > 24)
            {
                using var highlightPen = new Pen(Color.FromArgb(20, 255, 255, 255), 1f);
                g.DrawLine(highlightPen, BorderRadius / 2, 1, Width - 1 - BorderRadius / 2, 1);
            }

            using var pen = new Pen(BorderColor, 1.2f);
            g.DrawPath(pen, path);
        }
    }

    public class ModernToggle : Control
    {
        private bool _checked;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string LabelText { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color ActiveTrackColor { get; set; } = Color.FromArgb(16, 185, 129); // Emerald

        public event EventHandler? CheckedChanged;

        public ModernToggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            Size = new Size(240, 26);
        }

        protected override void OnClick(EventArgs e)
        {
            if (Enabled)
            {
                Checked = !Checked;
            }
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int trackW = 34;
            int trackH = 18;
            int trackY = (Height - trackH) / 2;
            var trackRect = new Rectangle(0, trackY, trackW, trackH);

            Color trackBg = !Enabled ? Color.FromArgb(24, 29, 40) :
                            Checked ? ActiveTrackColor : Color.FromArgb(32, 40, 56);
            using var trackPath = MainForm.CreateRoundedRectangle(trackRect, 9);
            using var trackBrush = new SolidBrush(trackBg);
            g.FillPath(trackBrush, trackPath);

            using var trackBorder = new Pen(Checked ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(45, 56, 78), 1f);
            g.DrawPath(trackBorder, trackPath);

            // Thumb circle
            int thumbSize = 12;
            int thumbY = trackY + (trackH - thumbSize) / 2;
            int thumbX = Checked ? (trackW - thumbSize - 3) : 3;
            var thumbRect = new Rectangle(thumbX, thumbY, thumbSize, thumbSize);
            using var thumbBrush = new SolidBrush(!Enabled ? Color.FromArgb(148, 163, 184) : Color.White);
            g.FillEllipse(thumbBrush, thumbRect);

            // Label text
            int textX = trackW + 10;
            Color textCol = !Enabled ? Color.FromArgb(80, 95, 120) : Color.FromArgb(241, 245, 249);
            var textRect = new Rectangle(textX, 0, Width - textX, Height);
            TextRenderer.DrawText(g, LabelText, Font, textRect, textCol, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    public class SectionHeader : Control
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SectionType { get; set; } = "video"; // "video" or "audio"

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color AccentColor { get; set; } = Color.FromArgb(56, 189, 248);

        public SectionHeader()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
            Height = 24;
            Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (SectionType == "video")
            {
                // Vector Monitor Icon
                var screenRect = new Rectangle(0, 3, 15, 11);
                using var p = new Pen(AccentColor, 1.4f);
                using var path = MainForm.CreateRoundedRectangle(screenRect, 2);
                g.DrawPath(p, path);
                g.DrawLine(p, 7, 14, 7, 17);
                g.DrawLine(p, 4, 17, 11, 17);
            }
            else if (SectionType == "audio")
            {
                // Vector Audio Waveform
                using var b = new SolidBrush(AccentColor);
                int[] heights = new int[] { 5, 11, 15, 9, 13, 7 };
                for (int i = 0; i < heights.Length; i++)
                {
                    int h = heights[i];
                    int x = i * 3;
                    int y = (Height - h) / 2;
                    g.FillRectangle(b, x, y, 2, h);
                }
            }

            var textRect = new Rectangle(22, 0, Width - 22, Height);
            TextRenderer.DrawText(g, Title, Font, textRect, AccentColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    public class MainForm : Form
    {
        #region Win32 Dark Mode & Hotkeys APIs
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID_RECORD = 9001; // F9
        private const int HOTKEY_ID_STOP = 9002;   // F10
        private const uint VK_F9 = 0x78;
        private const uint VK_F10 = 0x79;
        private const int WM_HOTKEY = 0x0312;
        #endregion

        // Engine & Services
        private readonly RecorderEngine _engine = new();
        private readonly AudioService _audioService = new();
        private readonly CameraService _cameraService = new();

        // UI Controls - Top Section
        private ModernCard _cardTop = null!;
        private Label _lblTimer = null!;
        private Label _lblStatusPill = null!;
        private ModernButton _btnRecord = null!;
        private ModernButton _btnPause = null!;
        private ModernButton _btnStop = null!;
        private ModernToggle _chkCountdown = null!;
        private Label _lblLogoBadge = null!;
        private ModernButton _btnUpdate = null!;
        private UpdateInfo? _latestUpdateInfo;

        // Layout Containers
        private Panel _pnlMain = null!;
        private Panel _pnlLeftCol = null!;
        private Panel _pnlRightCol = null!;

        // UI Controls - Left Column (Video & Display)
        private ModernCard _cardVideo = null!;
        private ComboBox _cmbScreen = null!;
        private ModernButton _btnSelectArea = null!;
        private ComboBox _cmbQuality = null!;
        private ModernToggle _chkFaceCam = null!;
        private PictureBox _picFaceCamPreview = null!;
        private ComboBox _cmbCamCorner = null!;
        private ComboBox _cmbCamSize = null!;
        private Panel _pnlFaceCamSettings = null!;

        // UI Controls - Right Column (Audio & Sound)
        private ModernCard _cardAudio = null!;
        private ModernToggle _chkMic = null!;
        private ComboBox _cmbMicDevice = null!;
        private Label _lblMicDevice = null!;
        private Panel _pnlMicMeter = null!;
        private Label _lblDbValue = null!;
        private ModernToggle _chkSysAudio = null!;

        // UI Controls - Finished Recording Drawer
        private ModernCard _pnlFinished = null!;
        private PictureBox _picThumbnail = null!;
        private Label _lblFinishedName = null!;
        private Label _lblFinishedDuration = null!;
        private Label _lblFinishedSize = null!;
        private ModernButton _btnPlay = null!;
        private ModernButton _btnOpenFolder = null!;
        private ModernButton _btnCopyFile = null!;
        private ModernButton _btnAutoEdit = null!;

        // State & Timers
        private System.Windows.Forms.Timer _recTimer = null!;
        private System.Windows.Forms.Timer _countdownTimer = null!;
        private System.Windows.Forms.Timer _pulseTimer = null!;
        private int _elapsedSeconds = 0;
        private int _countdownValue = 0;
        private float _currentMicPeak = 0f;
        private float _currentMicDb = -60f;
        private bool _pulseGlowState = false;
        private RecordingResult? _lastResult;
        private ScreenTarget? _customAreaTarget;

        public MainForm()
        {
            InitializeComponent();
            InitializeEngineAndDevices();
            RegisterGlobalHotkeys();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int darkMode = 1;
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref darkMode, sizeof(int));
            }
            catch {}
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            StartBackgroundUpdateCheck();
        }

        #region Self-Update Feature
        private void StartBackgroundUpdateCheck()
        {
            Task.Run(async () =>
            {
                await Task.Delay(2500);
                var info = await UpdateService.CheckForUpdateAsync();
                if (info.HasUpdate && !IsDisposed && IsHandleCreated)
                {
                    try
                    {
                        Invoke(new Action(() =>
                        {
                            _latestUpdateInfo = info;
                            _btnUpdate.Text = $"{info.LatestVersion} 🚀";
                            _btnUpdate.NormalColor = Color.FromArgb(16, 185, 129);
                            _btnUpdate.HoverColor = Color.FromArgb(5, 150, 105);
                            _btnUpdate.PressedColor = Color.FromArgb(4, 120, 87);
                            _btnUpdate.BorderColor = Color.FromArgb(52, 211, 153);
                            _btnUpdate.ForeColor = Color.White;
                        }));
                    }
                    catch {}
                }
            });
        }

        private async Task CheckForUpdatesUserAsync()
        {
            if (_latestUpdateInfo != null && _latestUpdateInfo.HasUpdate)
            {
                await PromptAndApplyUpdateAsync(_latestUpdateInfo);
                return;
            }

            _btnUpdate.Text = "Checking...";
            _btnUpdate.Enabled = false;

            var info = await UpdateService.CheckForUpdateAsync();
            _latestUpdateInfo = info;
            _btnUpdate.Enabled = true;

            if (info.HasUpdate)
            {
                _btnUpdate.Text = $"{info.LatestVersion} 🚀";
                _btnUpdate.NormalColor = Color.FromArgb(16, 185, 129);
                _btnUpdate.HoverColor = Color.FromArgb(5, 150, 105);
                _btnUpdate.PressedColor = Color.FromArgb(4, 120, 87);
                _btnUpdate.BorderColor = Color.FromArgb(52, 211, 153);
                _btnUpdate.ForeColor = Color.White;
                await PromptAndApplyUpdateAsync(info);
            }
            else
            {
                _btnUpdate.Text = $"{UpdateService.CurrentVersion} ✓";
                MessageBox.Show(
                    $"You are running the latest version of Lab Recorder ({UpdateService.CurrentVersion})!\n\nAll features and encoders are up to date.",
                    "Lab Recorder Update",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _btnUpdate.Text = $"{UpdateService.CurrentVersion} 🔄";
            }
        }

        private async Task PromptAndApplyUpdateAsync(UpdateInfo info)
        {
            string prompt = $"A new version of Lab Recorder is available!\n\n" +
                            $"Installed Version: {UpdateService.CurrentVersion}\n" +
                            $"Latest Version:    {info.LatestVersion}\n\n" +
                            (string.IsNullOrWhiteSpace(info.ReleaseNotes) ? "" : $"Release Notes:\n{info.ReleaseNotes.Trim()}\n\n") +
                            "Click 'Yes' to update automatically now in-place.\n" +
                            "Click 'No' to view release on GitHub.\n" +
                            "Click 'Cancel' to skip for now.";

            var dialogResult = MessageBox.Show(prompt, "Lab Recorder Update Available", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
            if (dialogResult == DialogResult.Yes)
            {
                if (string.IsNullOrEmpty(info.ExeDownloadUrl))
                {
                    Process.Start(new ProcessStartInfo(info.HtmlUrl) { UseShellExecute = true });
                    return;
                }

                _btnUpdate.Text = "Updating...";
                _btnUpdate.Enabled = false;

                var progress = new Progress<int>(pct =>
                {
                    _btnUpdate.Text = $"{pct}%";
                });

                bool success = await UpdateService.DownloadAndApplyUpdateAsync(info.ExeDownloadUrl, progress);
                if (!success)
                {
                    _btnUpdate.Text = $"{info.LatestVersion} 🚀";
                    _btnUpdate.Enabled = true;
                    var r = MessageBox.Show(
                        "Automatic in-place update could not complete.\n\nWould you like to open the GitHub Release page to download it directly?",
                        "Update Notice",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (r == DialogResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(info.HtmlUrl) { UseShellExecute = true });
                    }
                }
            }
            else if (dialogResult == DialogResult.No)
            {
                Process.Start(new ProcessStartInfo(info.HtmlUrl) { UseShellExecute = true });
            }
        }
        #endregion

        private void RegisterGlobalHotkeys()
        {
            try
            {
                RegisterHotKey(Handle, HOTKEY_ID_RECORD, 0, VK_F9);
                RegisterHotKey(Handle, HOTKEY_ID_STOP, 0, VK_F10);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RegisterHotKey exception: " + ex.Message);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HOTKEY_ID_RECORD)
                {
                    if (!_engine.IsRecording)
                    {
                        if (_btnRecord.Enabled) BtnRecord_Click(this, EventArgs.Empty);
                    }
                    else
                    {
                        if (_btnPause.Enabled) BtnPause_Click(this, EventArgs.Empty);
                    }
                }
                else if (id == HOTKEY_ID_STOP)
                {
                    if (_btnStop.Enabled) BtnStop_Click(this, EventArgs.Empty);
                }
            }
            base.WndProc(ref m);
        }

        public static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Bitmap GetLogoBitmap(int sz)
        {
            var bmp = new Bitmap(sz, sz);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int pad = Math.Max(1, (int)(sz * 0.04));
            var baseRect = new Rectangle(pad, pad, sz - 2 * pad, sz - 2 * pad);
            int radius = (int)(sz * 0.22);
            using var path = CreateRoundedRectangle(baseRect, radius);
            using var bgBrush = new LinearGradientBrush(new PointF(0, 0), new PointF(0, sz), Color.FromArgb(28, 35, 49), Color.FromArgb(13, 16, 24));
            g.FillPath(bgBrush, path);
            using var borderPen = new Pen(Color.FromArgb(60, 75, 105), Math.Max(1.0f, sz * 0.035f));
            g.DrawPath(borderPen, path);

            // Aperture ring (Cyan)
            int ringMargin = (int)(sz * 0.22);
            var ringRect = new Rectangle(ringMargin, ringMargin, sz - 2 * ringMargin, sz - 2 * ringMargin);
            using var ringPen = new Pen(Color.FromArgb(56, 189, 248), Math.Max(1.5f, sz * 0.065f));
            g.DrawEllipse(ringPen, ringRect);

            // Red dot
            int dotMargin = (int)(sz * 0.35);
            var dotRect = new Rectangle(dotMargin, dotMargin, sz - 2 * dotMargin, sz - 2 * dotMargin);
            using var dotBrush = new LinearGradientBrush(new PointF(0, dotMargin), new PointF(0, sz - dotMargin), Color.FromArgb(239, 68, 68), Color.FromArgb(185, 28, 28));
            g.FillEllipse(dotBrush, dotRect);

            // Gleam
            if (sz >= 24)
            {
                int gw = (int)(sz * 0.12);
                int gh = (int)(sz * 0.08);
                var gleamRect = new Rectangle((int)(sz * 0.38), (int)(sz * 0.36), gw, gh);
                using var gleamBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
                g.FillEllipse(gleamBrush, gleamRect);
            }

            return bmp;
        }

        private static void SetupModernComboBox(ComboBox cmb)
        {
            cmb.DrawMode = DrawMode.OwnerDrawFixed;
            cmb.ItemHeight = 28;
            cmb.BackColor = Color.FromArgb(15, 19, 28);
            cmb.ForeColor = Color.FromArgb(241, 245, 249);
            cmb.FlatStyle = FlatStyle.Flat;
            cmb.Font = new Font("Segoe UI", 9.25f);
            cmb.DrawItem += (sender, e) =>
            {
                if (e.Index < 0) return;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                bool isSelected = (e.State & DrawItemState.Selected) != 0;
                Color bg = isSelected ? Color.FromArgb(37, 99, 235) : Color.FromArgb(15, 19, 28);
                Color fg = isSelected ? Color.White : Color.FromArgb(241, 245, 249);

                using var brush = new SolidBrush(bg);
                g.FillRectangle(brush, e.Bounds);

                string text = cmb.Items[e.Index]?.ToString() ?? "";
                using var font = new Font("Segoe UI", 9.25f);
                var textRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height);
                TextRenderer.DrawText(g, text, font, textRect, fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };
        }

        private void InitializeComponent()
        {
            Text = "Lab Recorder";
            // Laptop-friendly dimension: fits comfortably on 1366x768 and 1080p @ 125%/150% scaling
            Size = new Size(860, 590);
            MinimumSize = new Size(800, 520);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(9, 11, 16); // Obsidian base canvas (#090B10)
            ForeColor = Color.FromArgb(241, 245, 249);
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            Padding = new Padding(14, 10, 14, 12);

            try
            {
                if (File.Exists("app.ico"))
                {
                    Icon = new Icon("app.ico");
                }
                else
                {
                    var asm = typeof(MainForm).Assembly;
                    using var s = asm.GetManifestResourceStream("LabRecorder.app.ico");
                    if (s != null) Icon = new Icon(s);
                }
            }
            catch {}

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            // ==========================================
            // TOP ACTION CARD (Header & Recording Controls)
            // ==========================================
            _cardTop = new ModernCard
            {
                Dock = DockStyle.Top,
                Height = 98,
                CardBackground = Color.FromArgb(16, 20, 29),
                BorderColor = Color.FromArgb(32, 40, 58),
                BorderRadius = 12,
                Padding = new Padding(16, 10, 16, 10)
            };

            _lblTimer = new Label
            {
                Text = "00:00",
                Font = new Font("Consolas", 30f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Location = new Point(16, 10)
            };

            _lblStatusPill = new Label
            {
                Text = "READY",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                AutoSize = true,
                Location = new Point(20, 68)
            };

            _btnRecord = new ModernButton
            {
                Text = "● Record [F9]",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                NormalColor = Color.FromArgb(225, 29, 72),   // Studio Crimson Ember (#E11D48)
                HoverColor = Color.FromArgb(244, 63, 94),
                PressedColor = Color.FromArgb(190, 18, 60),
                BorderColor = Color.FromArgb(251, 113, 133),
                Size = new Size(130, 42),
                Location = new Point(200, 18)
            };
            _btnRecord.Click += BtnRecord_Click;

            _btnPause = new ModernButton
            {
                Text = "⏸ Pause",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                NormalColor = Color.FromArgb(20, 25, 36),
                HoverColor = Color.FromArgb(32, 40, 58),
                PressedColor = Color.FromArgb(14, 18, 26),
                BorderColor = Color.FromArgb(42, 52, 74),
                Size = new Size(105, 42),
                Location = new Point(338, 18),
                Enabled = false
            };
            _btnPause.Click += BtnPause_Click;

            _btnStop = new ModernButton
            {
                Text = "⏹ Stop [F10]",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                NormalColor = Color.FromArgb(20, 25, 36),
                HoverColor = Color.FromArgb(32, 40, 58),
                PressedColor = Color.FromArgb(14, 18, 26),
                BorderColor = Color.FromArgb(42, 52, 74),
                Size = new Size(110, 42),
                Location = new Point(450, 18),
                Enabled = false
            };
            _btnStop.Click += BtnStop_Click;

            _chkCountdown = new ModernToggle
            {
                LabelText = "3s Countdown",
                Location = new Point(202, 68),
                Size = new Size(160, 24),
                Checked = false,
                ActiveTrackColor = Color.FromArgb(56, 189, 248)
            };

            _cardTop.Controls.Add(_lblTimer);
            _cardTop.Controls.Add(_lblStatusPill);
            _cardTop.Controls.Add(_btnRecord);
            _cardTop.Controls.Add(_btnPause);
            _cardTop.Controls.Add(_btnStop);
            _cardTop.Controls.Add(_chkCountdown);

            // Studio Branding Logo Badge (Anchor Right)
            var pnlLogo = new Panel
            {
                Size = new Size(245, 68),
                Location = new Point(_cardTop.Width - 255, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            var picLogo = new PictureBox
            {
                Size = new Size(42, 42),
                Location = new Point(0, 6),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Image = GetLogoBitmap(42)
            };
            var lblLogoTitle = new Label
            {
                Text = "LAB RECORDER",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(241, 245, 249),
                Location = new Point(48, 5),
                AutoSize = true
            };
            _lblLogoBadge = new Label
            {
                Text = "⚡ DETECTING GPU",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                Location = new Point(50, 26),
                AutoSize = true
            };

            _btnUpdate = new ModernButton
            {
                Text = $"{UpdateService.CurrentVersion} 🔄",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                NormalColor = Color.FromArgb(20, 26, 38),
                HoverColor = Color.FromArgb(32, 42, 60),
                PressedColor = Color.FromArgb(14, 18, 28),
                BorderColor = Color.FromArgb(42, 54, 76),
                ForeColor = Color.FromArgb(148, 163, 184),
                BorderRadius = 6,
                Size = new Size(86, 22),
                Location = new Point(50, 44),
                Cursor = Cursors.Hand
            };
            var toolTipUpdate = new ToolTip();
            toolTipUpdate.SetToolTip(_btnUpdate, $"Lab Recorder {UpdateService.CurrentVersion}\nClick to check for updates");
            _btnUpdate.Click += async (s, e) => await CheckForUpdatesUserAsync();

            pnlLogo.Controls.Add(picLogo);
            pnlLogo.Controls.Add(lblLogoTitle);
            pnlLogo.Controls.Add(_lblLogoBadge);
            pnlLogo.Controls.Add(_btnUpdate);
            _cardTop.Controls.Add(pnlLogo);

            // ==========================================
            // MAIN 2-COLUMN SECTION (Symmetric & Responsive)
            // ==========================================
            _pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 10)
            };

            _pnlLeftCol = new Panel
            {
                Dock = DockStyle.Left,
                Padding = new Padding(0, 0, 6, 0)
            };

            _pnlRightCol = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(6, 0, 0, 0)
            };

            // ------------------------------------------
            // Left Card: Display & Video
            // ------------------------------------------
            _cardVideo = new ModernCard
            {
                Dock = DockStyle.Fill,
                CardBackground = Color.FromArgb(16, 20, 29),
                BorderColor = Color.FromArgb(32, 40, 58),
                BorderRadius = 12,
                Padding = new Padding(16, 12, 16, 12)
            };

            var hdrVideo = new SectionHeader
            {
                Title = "CAPTURE & DISPLAY",
                SectionType = "video",
                AccentColor = Color.FromArgb(56, 189, 248),
                Location = new Point(14, 12),
                Size = new Size(260, 24)
            };

            var divVideo = new Panel
            {
                Location = new Point(14, 38),
                Height = 1,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(28, 35, 50)
            };

            var lblScreen = new Label
            {
                Text = "Display Source:",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(14, 46),
                AutoSize = true
            };

            _cmbScreen = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(14, 66),
                Width = 265
            };
            SetupModernComboBox(_cmbScreen);
            _cmbScreen.SelectedIndexChanged += CmbScreen_SelectedIndexChanged;

            _btnSelectArea = new ModernButton
            {
                Text = "Area Crop",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                NormalColor = Color.FromArgb(20, 25, 36),
                HoverColor = Color.FromArgb(32, 40, 58),
                PressedColor = Color.FromArgb(14, 18, 26),
                BorderColor = Color.FromArgb(56, 189, 248),
                Size = new Size(85, 28),
                Location = new Point(285, 66)
            };
            _btnSelectArea.Click += (s, e) => LaunchAreaSelector();

            var lblQuality = new Label
            {
                Text = "Quality & Framerate:",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(14, 104),
                AutoSize = true
            };

            _cmbQuality = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(14, 124),
                Width = 356
            };
            _cmbQuality.Items.Add("Ultra — Native 60 FPS (Pristine, ~100 MB/min)");
            _cmbQuality.Items.Add("Best — 1080p 60 FPS (Medal/OBS Smooth, ~70 MB/min)");
            _cmbQuality.Items.Add("Standard — 1080p 30 FPS (~45 MB/min)");
            _cmbQuality.Items.Add("Light — 720p 30 FPS (Battery Saver, ~25 MB/min)");
            _cmbQuality.SelectedIndex = 1; // Best (1080p60) default
            SetupModernComboBox(_cmbQuality);

            _chkFaceCam = new ModernToggle
            {
                LabelText = "Circular Face Cam Overlay",
                Location = new Point(14, 160),
                Size = new Size(356, 26),
                Checked = false,
                ActiveTrackColor = Color.FromArgb(56, 189, 248)
            };
            _chkFaceCam.CheckedChanged += ChkFaceCam_CheckedChanged;

            // Face Cam Subpanel
            _pnlFaceCamSettings = new Panel
            {
                Location = new Point(14, 190),
                Size = new Size(356, 120),
                Visible = false
            };

            _picFaceCamPreview = new PictureBox
            {
                Size = new Size(105, 105),
                Location = new Point(2, 4),
                BackColor = Color.FromArgb(12, 16, 24)
            };
            _picFaceCamPreview.Paint += PicFaceCamPreview_Paint;

            var lblCorner = new Label
            {
                Text = "Corner Position:",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(120, 6),
                AutoSize = true
            };

            _cmbCamCorner = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(120, 24),
                Width = 230
            };
            _cmbCamCorner.Items.Add("Bottom-Right");
            _cmbCamCorner.Items.Add("Bottom-Left");
            _cmbCamCorner.Items.Add("Top-Right");
            _cmbCamCorner.Items.Add("Top-Left");
            _cmbCamCorner.SelectedIndex = 0;
            SetupModernComboBox(_cmbCamCorner);

            var lblSize = new Label
            {
                Text = "Overlay Size:",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(120, 60),
                AutoSize = true
            };

            _cmbCamSize = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(120, 78),
                Width = 230
            };
            _cmbCamSize.Items.Add("Small");
            _cmbCamSize.Items.Add("Medium");
            _cmbCamSize.Items.Add("Large");
            _cmbCamSize.SelectedIndex = 1;
            SetupModernComboBox(_cmbCamSize);

            _pnlFaceCamSettings.Controls.Add(_picFaceCamPreview);
            _pnlFaceCamSettings.Controls.Add(lblCorner);
            _pnlFaceCamSettings.Controls.Add(_cmbCamCorner);
            _pnlFaceCamSettings.Controls.Add(lblSize);
            _pnlFaceCamSettings.Controls.Add(_cmbCamSize);

            _cardVideo.Controls.Add(hdrVideo);
            _cardVideo.Controls.Add(divVideo);
            _cardVideo.Controls.Add(lblScreen);
            _cardVideo.Controls.Add(_cmbScreen);
            _cardVideo.Controls.Add(_btnSelectArea);
            _cardVideo.Controls.Add(lblQuality);
            _cardVideo.Controls.Add(_cmbQuality);
            _cardVideo.Controls.Add(_chkFaceCam);
            _cardVideo.Controls.Add(_pnlFaceCamSettings);

            _pnlLeftCol.Controls.Add(_cardVideo);

            // ------------------------------------------
            // Right Card: Audio & Monitoring
            // ------------------------------------------
            _cardAudio = new ModernCard
            {
                Dock = DockStyle.Fill,
                CardBackground = Color.FromArgb(16, 20, 29),
                BorderColor = Color.FromArgb(32, 40, 58),
                BorderRadius = 12,
                Padding = new Padding(16, 12, 16, 12)
            };

            var hdrAudio = new SectionHeader
            {
                Title = "AUDIO & MONITORING",
                SectionType = "audio",
                AccentColor = Color.FromArgb(56, 189, 248),
                Location = new Point(14, 12),
                Size = new Size(260, 24)
            };

            var divAudio = new Panel
            {
                Location = new Point(14, 38),
                Height = 1,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(28, 35, 50)
            };

            _chkMic = new ModernToggle
            {
                LabelText = "Microphone Input",
                Location = new Point(14, 44),
                Size = new Size(356, 26),
                Checked = true,
                ActiveTrackColor = Color.FromArgb(16, 185, 129)
            };
            _chkMic.CheckedChanged += ChkMic_CheckedChanged;

            _lblMicDevice = new Label
            {
                Text = "Microphone Device:",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(14, 72),
                AutoSize = true,
                Visible = false
            };

            _cmbMicDevice = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(14, 90),
                Width = 356,
                Visible = false
            };
            _cmbMicDevice.SelectedIndexChanged += CmbMicDevice_SelectedIndexChanged;
            SetupModernComboBox(_cmbMicDevice);

            var lblMeter = new Label
            {
                Text = "Live Mic Level (+18dB Studio Gain):",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(14, 124),
                AutoSize = true
            };

            _lblDbValue = new Label
            {
                Text = "-inf dB",
                Font = new Font("Consolas", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(280, 124),
                Size = new Size(90, 16),
                TextAlign = ContentAlignment.TopRight
            };

            _pnlMicMeter = new Panel
            {
                Location = new Point(14, 144),
                Size = new Size(356, 24),
                BackColor = Color.FromArgb(12, 16, 24)
            };
            _pnlMicMeter.Paint += PnlMicMeter_Paint;

            var lblMeterTicks = new Label
            {
                Text = "-36 dB              -24 dB              -12 dB        -6 dB       0 dB",
                Font = new Font("Consolas", 7.5f),
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(14, 172),
                AutoSize = true
            };

            _chkSysAudio = new ModernToggle
            {
                LabelText = "System Audio (WASAPI Loopback)",
                Location = new Point(14, 202),
                Size = new Size(356, 26),
                Checked = true,
                ActiveTrackColor = Color.FromArgb(16, 185, 129)
            };

            var lblSysAudioHint = new Label
            {
                Text = "Captures games, Discord, Spotify & browser audio directly via WASAPI loopback.",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(48, 228),
                Size = new Size(320, 32)
            };

            var pnlHotkeyPill = new Panel
            {
                Location = new Point(14, 268),
                Size = new Size(356, 32),
                BackColor = Color.FromArgb(12, 16, 24)
            };
            pnlHotkeyPill.Paint += (s, pe) =>
            {
                var g = pe.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, pnlHotkeyPill.Width - 1, pnlHotkeyPill.Height - 1);
                using var p = CreateRoundedRectangle(r, 6);
                using var bg = new SolidBrush(Color.FromArgb(12, 16, 24));
                g.FillPath(bg, p);
                using var pen = new Pen(Color.FromArgb(32, 42, 60), 1f);
                g.DrawPath(pen, p);

                using var keyBg = new SolidBrush(Color.FromArgb(24, 32, 48));
                using var keyBorder = new Pen(Color.FromArgb(50, 66, 96), 1f);
                using var keyFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
                using var textFont = new Font("Segoe UI", 8f);

                int curX = 12;
                var f9Rect = new Rectangle(curX, (pnlHotkeyPill.Height - 18) / 2, 28, 18);
                using var f9Path = CreateRoundedRectangle(f9Rect, 4);
                g.FillPath(keyBg, f9Path);
                g.DrawPath(keyBorder, f9Path);
                TextRenderer.DrawText(g, "F9", keyFont, f9Rect, Color.FromArgb(220, 230, 245), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                curX += 34;

                TextRenderer.DrawText(g, "Record / Pause", textFont, new Point(curX, (pnlHotkeyPill.Height - 14) / 2), Color.FromArgb(148, 163, 184));
                curX += 95;

                var f10Rect = new Rectangle(curX, (pnlHotkeyPill.Height - 18) / 2, 32, 18);
                using var f10Path = CreateRoundedRectangle(f10Rect, 4);
                g.FillPath(keyBg, f10Path);
                g.DrawPath(keyBorder, f10Path);
                TextRenderer.DrawText(g, "F10", keyFont, f10Rect, Color.FromArgb(220, 230, 245), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                curX += 38;

                TextRenderer.DrawText(g, "Stop", textFont, new Point(curX, (pnlHotkeyPill.Height - 14) / 2), Color.FromArgb(148, 163, 184));
                curX += 36;

                TextRenderer.DrawText(g, "• AMF 60 FPS", textFont, new Point(curX, (pnlHotkeyPill.Height - 14) / 2), Color.FromArgb(56, 189, 248));
            };

            _cardAudio.Controls.Add(hdrAudio);
            _cardAudio.Controls.Add(divAudio);
            _cardAudio.Controls.Add(_chkMic);
            _cardAudio.Controls.Add(_lblMicDevice);
            _cardAudio.Controls.Add(_cmbMicDevice);
            _cardAudio.Controls.Add(lblMeter);
            _cardAudio.Controls.Add(_lblDbValue);
            _cardAudio.Controls.Add(_pnlMicMeter);
            _cardAudio.Controls.Add(lblMeterTicks);
            _cardAudio.Controls.Add(_chkSysAudio);
            _cardAudio.Controls.Add(lblSysAudioHint);
            _cardAudio.Controls.Add(pnlHotkeyPill);

            _pnlRightCol.Controls.Add(_cardAudio);

            _pnlMain.Controls.Add(_pnlRightCol);
            _pnlMain.Controls.Add(_pnlLeftCol);

            // ==========================================
            // BOTTOM FINISHED RECORDING DRAWER (Bento Style)
            // ==========================================
            _pnlFinished = new ModernCard
            {
                Dock = DockStyle.Bottom,
                Height = 125,
                CardBackground = Color.FromArgb(16, 20, 29),
                BorderColor = Color.FromArgb(16, 185, 129), // Emerald
                BorderRadius = 12,
                Padding = new Padding(14, 10, 14, 10),
                Visible = false
            };

            var lblFinishedTitle = new Label
            {
                Text = "RECORDING SAVED",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(14, 8),
                AutoSize = true
            };

            _picThumbnail = new PictureBox
            {
                Size = new Size(140, 78),
                Location = new Point(14, 30),
                BackColor = Color.FromArgb(12, 16, 24),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            _picThumbnail.Paint += PicThumbnail_Paint;

            _lblFinishedName = new Label
            {
                Text = "Recording 2026-11-06 00-00-00.mp4",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(166, 28),
                AutoSize = true
            };

            _lblFinishedDuration = new Label
            {
                Text = "Duration: 00:00",
                Font = new Font("Consolas", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                Location = new Point(166, 52),
                AutoSize = true
            };

            _lblFinishedSize = new Label
            {
                Text = "Size: 0.0 MB",
                Font = new Font("Consolas", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 211, 153),
                Location = new Point(285, 52),
                AutoSize = true
            };

            _btnPlay = new ModernButton
            {
                Text = "Play Video",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                NormalColor = Color.FromArgb(37, 99, 235), // Primary Blue
                HoverColor = Color.FromArgb(59, 130, 246),
                PressedColor = Color.FromArgb(29, 78, 216),
                BorderColor = Color.FromArgb(96, 165, 250),
                Size = new Size(100, 32),
                Location = new Point(166, 78)
            };
            _btnPlay.Click += BtnPlay_Click;

            _btnOpenFolder = new ModernButton
            {
                Text = "Reveal in Folder",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                NormalColor = Color.FromArgb(20, 25, 36),
                HoverColor = Color.FromArgb(32, 40, 58),
                PressedColor = Color.FromArgb(14, 18, 26),
                BorderColor = Color.FromArgb(42, 52, 74),
                Size = new Size(120, 32),
                Location = new Point(274, 78)
            };
            _btnOpenFolder.Click += BtnOpenFolder_Click;

            _btnCopyFile = new ModernButton
            {
                Text = "Copy Path",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                NormalColor = Color.FromArgb(20, 25, 36),
                HoverColor = Color.FromArgb(32, 40, 58),
                PressedColor = Color.FromArgb(14, 18, 26),
                BorderColor = Color.FromArgb(42, 52, 74),
                Size = new Size(95, 32),
                Location = new Point(402, 78)
            };
            _btnCopyFile.Click += BtnCopyFile_Click;

            _btnAutoEdit = new ModernButton
            {
                Text = "Auto-Edit (Free)",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                NormalColor = Color.FromArgb(16, 185, 129),
                HoverColor = Color.FromArgb(52, 211, 153),
                PressedColor = Color.FromArgb(5, 150, 105),
                BorderColor = Color.FromArgb(110, 231, 183),
                Size = new Size(125, 32),
                Location = new Point(505, 78)
            };
            _btnAutoEdit.Click += BtnAutoEdit_Click;

            _pnlFinished.Controls.Add(lblFinishedTitle);
            _pnlFinished.Controls.Add(_picThumbnail);
            _pnlFinished.Controls.Add(_lblFinishedName);
            _pnlFinished.Controls.Add(_lblFinishedDuration);
            _pnlFinished.Controls.Add(_lblFinishedSize);
            _pnlFinished.Controls.Add(_btnPlay);
            _pnlFinished.Controls.Add(_btnOpenFolder);
            _pnlFinished.Controls.Add(_btnCopyFile);
            _pnlFinished.Controls.Add(_btnAutoEdit);

            // Add containers to Form
            Controls.Add(_pnlMain);
            Controls.Add(_pnlFinished);
            Controls.Add(_cardTop);

            // Dynamic responsive resize handler
            Resize += MainForm_Resize;
            MainForm_Resize(this, EventArgs.Empty);

            // Timers
            _recTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _recTimer.Tick += RecTimer_Tick;

            _countdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _countdownTimer.Tick += CountdownTimer_Tick;

            _pulseTimer = new System.Windows.Forms.Timer { Interval = 600 };
            _pulseTimer.Tick += PulseTimer_Tick;
        }

        private void MainForm_Resize(object? sender, EventArgs e)
        {
            if (_pnlLeftCol != null && _pnlMain != null)
            {
                int halfWidth = Math.Max(380, (_pnlMain.ClientSize.Width - 12) / 2);
                _pnlLeftCol.Width = halfWidth;

                // Adjust combo widths to match card size
                int innerW = Math.Max(340, _cardVideo.Width - 32);
                _cmbQuality.Width = innerW;
                _cmbScreen.Width = Math.Max(240, innerW - 95);
                _btnSelectArea.Left = _cmbScreen.Right + 8;
                _chkFaceCam.Width = innerW;
                _pnlFaceCamSettings.Width = innerW;
                _cmbCamCorner.Width = Math.Max(180, innerW - 126);
                _cmbCamSize.Width = Math.Max(180, innerW - 126);

                int innerRightW = Math.Max(340, _cardAudio.Width - 32);
                _chkMic.Width = innerRightW;
                _cmbMicDevice.Width = innerRightW;
                _pnlMicMeter.Width = innerRightW;
                _lblDbValue.Left = innerRightW - 80;
                _chkSysAudio.Width = innerRightW;
            }
        }

        private void PulseTimer_Tick(object? sender, EventArgs e)
        {
            if (_engine.IsRecording && !_engine.IsPaused)
            {
                _pulseGlowState = !_pulseGlowState;
                _lblStatusPill.ForeColor = _pulseGlowState ? Color.FromArgb(225, 29, 72) : Color.FromArgb(251, 113, 133);
                _lblTimer.ForeColor = _pulseGlowState ? Color.White : Color.FromArgb(220, 230, 245);
                _lblStatusPill.Invalidate();
            }
        }

        private void PicFaceCamPreview_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int size = Math.Min(_picFaceCamPreview.Width, _picFaceCamPreview.Height);
            var rect = new Rectangle(2, 2, size - 5, size - 5);

            if (_picFaceCamPreview.Image != null)
            {
                using var path = new GraphicsPath();
                path.AddEllipse(rect);
                g.SetClip(path);
                g.DrawImage(_picFaceCamPreview.Image, rect);
                g.ResetClip();

                using var pen = new Pen(Color.FromArgb(56, 189, 248), 2f);
                g.DrawEllipse(pen, rect);
            }
            else
            {
                using var brush = new SolidBrush(Color.FromArgb(12, 16, 24));
                g.FillEllipse(brush, rect);
                using var pen = new Pen(Color.FromArgb(32, 42, 60), 1.5f);
                g.DrawEllipse(pen, rect);
                using var font = new Font("Segoe UI", 8f, FontStyle.Bold);
                TextRenderer.DrawText(g, "Camera Ready", font, rect, Color.FromArgb(100, 116, 139), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void PicThumbnail_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, _picThumbnail.Width - 1, _picThumbnail.Height - 1);
            using var path = CreateRoundedRectangle(rect, 8);
            g.SetClip(path);
            if (_picThumbnail.Image != null)
            {
                g.DrawImage(_picThumbnail.Image, rect);
            }
            else
            {
                using var b = new SolidBrush(Color.FromArgb(12, 16, 24));
                g.FillRectangle(b, rect);
            }
            g.ResetClip();

            using var pen = new Pen(Color.FromArgb(36, 46, 66), 1.2f);
            g.DrawPath(pen, path);
        }

        private void InitializeEngineAndDevices()
        {
            _engine.Initialize();

            // Set hardware badge on header
            _lblLogoBadge.Text = _engine.EncoderShortBadge;

            // Populate monitors - Primary monitor ALWAYS listed first
            var screens = Screen.AllScreens;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            var monitorTargets = new List<ScreenTarget>();

            for (int i = 0; i < screens.Length; i++)
            {
                var s = screens[i];
                string label = s.Primary
                    ? $"Primary Display ({s.Bounds.Width}x{s.Bounds.Height})"
                    : $"Display {i + 1} ({s.Bounds.Width}x{s.Bounds.Height})";

                var target = new ScreenTarget
                {
                    Name = label,
                    Bounds = s.Bounds,
                    DeviceName = s.DeviceName,
                    IsFullDesktop = false,
                    MonitorIndex = i
                };

                if (s.Primary)
                {
                    monitorTargets.Insert(0, target);
                }
                else
                {
                    monitorTargets.Add(target);
                }

                if (s.Bounds.Left < minX) minX = s.Bounds.Left;
                if (s.Bounds.Top < minY) minY = s.Bounds.Top;
                if (s.Bounds.Right > maxX) maxX = s.Bounds.Right;
                if (s.Bounds.Bottom > maxY) maxY = s.Bounds.Bottom;
            }

            foreach (var target in monitorTargets)
            {
                _cmbScreen.Items.Add(target);
            }

            // Full desktop option
            int totalW = maxX - minX;
            int totalH = maxY - minY;
            _cmbScreen.Items.Add(new ScreenTarget
            {
                Name = $"Full Desktop Spanning ({totalW}x{totalH})",
                Bounds = new Rectangle(minX, minY, totalW, totalH),
                IsFullDesktop = true,
                MonitorIndex = -1
            });

            // Trigger item to select custom area with mouse
            _cmbScreen.Items.Add(new ScreenTarget
            {
                Name = "Select Custom Region...",
                Bounds = Rectangle.Empty,
                IsCustomRegion = true,
                MonitorIndex = -99
            });

            _cmbScreen.SelectedIndex = 0;

            // Cameras
            _cameraService.Initialize(_engine.FFmpegPath);

            // Microphones
            var mics = AudioService.EnumerateMicrophones();
            if (mics.Count > 1)
            {
                _lblMicDevice.Visible = true;
                _cmbMicDevice.Visible = true;
                foreach (var mic in mics)
                {
                    _cmbMicDevice.Items.Add(mic);
                }
                _cmbMicDevice.SelectedIndex = 0;
            }
            else if (mics.Count == 1)
            {
                _cmbMicDevice.Items.Add(mics[0]);
                _cmbMicDevice.SelectedIndex = 0;
                _lblMicDevice.Visible = false;
                _cmbMicDevice.Visible = false;
            }

            // Wire audio meter
            _audioService.OnMicLevel += level =>
            {
                // Post-gain level (+18 dB = 7.943x), clamped to 1.0f
                _currentMicPeak = Math.Clamp(level * 7.943f, 0f, 1f);

                // Calculate approximate dB
                if (_currentMicPeak > 0.001f)
                {
                    _currentMicDb = 20f * (float)Math.Log10(_currentMicPeak);
                }
                else
                {
                    _currentMicDb = -60f;
                }

                if (IsHandleCreated && !IsDisposed)
                {
                    try
                    {
                        _pnlMicMeter.BeginInvoke(new Action(() =>
                        {
                            _lblDbValue.Text = _currentMicDb > -50f ? $"{_currentMicDb:0.0} dB" : "-inf dB";
                            _lblDbValue.ForeColor = _currentMicDb > -3f ? Color.FromArgb(239, 68, 68) :
                                                    _currentMicDb > -12f ? Color.FromArgb(245, 158, 11) : Color.FromArgb(52, 211, 153);
                            _pnlMicMeter.Invalidate();
                        }));
                    }
                    catch {}
                }
            };

            if (_chkMic.Checked)
            {
                string? selectedMicId = (_cmbMicDevice.SelectedItem as AudioDeviceInfo)?.Id;
                _audioService.StartMicMeter(selectedMicId);
            }
        }

        private void PnlMicMeter_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = _pnlMicMeter.Width;
            int h = _pnlMicMeter.Height;
            var rect = new Rectangle(0, 0, w - 1, h - 1);
            using var path = CreateRoundedRectangle(rect, 6);

            using var bgBrush = new SolidBrush(Color.FromArgb(14, 17, 24));
            g.FillPath(bgBrush, path);

            if (!_chkMic.Checked)
            {
                using var font = new Font("Segoe UI", 7.5f);
                TextRenderer.DrawText(g, "Microphone muted", font, rect, Color.FromArgb(90, 105, 128), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                using var borderMuted = new Pen(Color.FromArgb(30, 36, 50), 1f);
                g.DrawPath(borderMuted, path);
                return;
            }

            float peak = Math.Clamp(_currentMicPeak, 0f, 1f);
            int fillW = (int)(w * peak);

            if (fillW > 4)
            {
                var fillRect = new Rectangle(1, 1, fillW - 2, h - 2);
                using var fillPath = CreateRoundedRectangle(fillRect, 5);
                using var brush = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.Transparent, Color.Transparent, LinearGradientMode.Horizontal);
                ColorBlend blend = new ColorBlend();
                blend.Colors = new Color[]
                {
                    Color.FromArgb(16, 185, 129), // Emerald (-36 dB to -12 dB)
                    Color.FromArgb(52, 211, 153), // Mint (-12 dB)
                    Color.FromArgb(245, 158, 11), // Warm Amber (-6 dB to -3 dB)
                    Color.FromArgb(239, 68, 68)   // Vibrant Red (0 dB clipping zone)
                };
                blend.Positions = new float[] { 0f, 0.55f, 0.85f, 1.0f };
                brush.InterpolationColors = blend;
                g.FillPath(brush, fillPath);
            }

            // Tick dividers for studio precision
            float[] tickPositions = new float[] { 0.20f, 0.40f, 0.65f, 0.85f };
            using var tickPen = new Pen(Color.FromArgb(10, 12, 18), 1.5f);
            foreach (var pos in tickPositions)
            {
                int tx = (int)(w * pos);
                g.DrawLine(tickPen, tx, 2, tx, h - 3);
            }

            using var borderPen = new Pen(Color.FromArgb(42, 50, 70), 1f);
            g.DrawPath(borderPen, path);
        }

        private void ChkFaceCam_CheckedChanged(object? sender, EventArgs e)
        {
            bool enabled = _chkFaceCam.Checked;
            _pnlFaceCamSettings.Visible = enabled;
            if (enabled)
            {
                _cameraService.StartPreview(img =>
                {
                    if (IsHandleCreated && !IsDisposed)
                    {
                        try { _picFaceCamPreview.BeginInvoke(new Action(() => { _picFaceCamPreview.Image = img; _picFaceCamPreview.Invalidate(); })); } catch {}
                    }
                });
            }
            else
            {
                _cameraService.StopPreview();
                _picFaceCamPreview.Image = null;
                _picFaceCamPreview.Invalidate();
            }
        }

        private void ChkMic_CheckedChanged(object? sender, EventArgs e)
        {
            bool enabled = _chkMic.Checked;
            _lblMicDevice.Enabled = enabled;
            _cmbMicDevice.Enabled = enabled;
            if (enabled)
            {
                string? selectedMicId = (_cmbMicDevice.SelectedItem as AudioDeviceInfo)?.Id;
                _audioService.StartMicMeter(selectedMicId);
            }
            else
            {
                _audioService.StopMicMeter();
                _lblDbValue.Text = "-inf dB";
            }
            _pnlMicMeter.Invalidate();
        }

        private void CmbMicDevice_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_chkMic.Checked)
            {
                string? selectedMicId = (_cmbMicDevice.SelectedItem as AudioDeviceInfo)?.Id;
                _audioService.StartMicMeter(selectedMicId);
            }
        }

        private void BtnRecord_Click(object? sender, EventArgs e)
        {
            if (_engine.IsRecording) return;

            if (_chkCountdown.Checked)
            {
                _countdownValue = 3;
                _lblStatusPill.Text = "COUNTDOWN 3s...";
                _lblStatusPill.ForeColor = Color.FromArgb(245, 158, 11);
                _lblTimer.Text = "00:03";
                _lblTimer.ForeColor = Color.FromArgb(245, 158, 11);
                _btnRecord.Enabled = false;
                _btnPause.Enabled = false;
                _btnStop.Enabled = true;
                _countdownTimer.Start();
            }
            else
            {
                DoStartRecording();
            }
        }

        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            _countdownValue--;
            if (_countdownValue > 0)
            {
                _lblStatusPill.Text = $"COUNTDOWN {_countdownValue}s...";
                _lblTimer.Text = $"00:0{_countdownValue}";
            }
            else
            {
                _countdownTimer.Stop();
                DoStartRecording();
            }
        }

        private void CmbScreen_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_cmbScreen.SelectedItem is ScreenTarget target)
            {
                if (target.Bounds.IsEmpty && target.IsCustomRegion)
                {
                    LaunchAreaSelector();
                }
                else if (target.IsCustomRegion)
                {
                    _btnSelectArea.Text = "Crop Area";
                }
                else
                {
                    _btnSelectArea.Text = "Area Crop";
                }
            }
        }

        private void LaunchAreaSelector()
        {
            if (_engine.IsRecording) return;

            Hide();
            Thread.Sleep(200);

            Rectangle selected = Rectangle.Empty;
            using (var selector = new RegionSelectorForm())
            {
                if (selector.ShowDialog() == DialogResult.OK && !selector.SelectedBounds.IsEmpty)
                {
                    selected = selector.SelectedBounds;
                }
            }

            Show();
            BringToFront();
            Activate();

            if (!selected.IsEmpty)
            {
                string label = $"Custom Region ({selected.Width}×{selected.Height})";

                if (_customAreaTarget == null)
                {
                    _customAreaTarget = new ScreenTarget
                    {
                        Name = label,
                        Bounds = selected,
                        IsFullDesktop = false,
                        IsCustomRegion = true,
                        MonitorIndex = -99
                    };
                    int insertIdx = Math.Max(0, _cmbScreen.Items.Count - 1);
                    _cmbScreen.Items.Insert(insertIdx, _customAreaTarget);
                }
                else
                {
                    _customAreaTarget.Name = label;
                    _customAreaTarget.Bounds = selected;
                    int idx = _cmbScreen.Items.IndexOf(_customAreaTarget);
                    if (idx >= 0)
                    {
                        _cmbScreen.Items[idx] = _customAreaTarget;
                    }
                }

                _cmbScreen.SelectedItem = _customAreaTarget;
                _btnSelectArea.Text = "Crop Area";
            }
            else
            {
                if (_cmbScreen.SelectedItem is ScreenTarget st && st.Bounds.IsEmpty)
                {
                    _cmbScreen.SelectedIndex = _customAreaTarget != null ? _cmbScreen.Items.IndexOf(_customAreaTarget) : 0;
                }
            }
        }

        private void DoStartRecording()
        {
            var screen = (ScreenTarget)_cmbScreen.SelectedItem!;
            var quality = _cmbQuality.SelectedIndex switch
            {
                0 => QualityPreset.Ultra,
                1 => QualityPreset.Best,
                2 => QualityPreset.Standard,
                3 => QualityPreset.Light,
                _ => QualityPreset.Best
            };

            var corner = _cmbCamCorner.SelectedIndex switch
            {
                0 => FaceCamCorner.BottomRight,
                1 => FaceCamCorner.BottomLeft,
                2 => FaceCamCorner.TopRight,
                3 => FaceCamCorner.TopLeft,
                _ => FaceCamCorner.BottomRight
            };

            var size = _cmbCamSize.SelectedIndex switch
            {
                0 => FaceCamSize.Small,
                1 => FaceCamSize.Medium,
                2 => FaceCamSize.Large,
                _ => FaceCamSize.Medium
            };

            string? micName = (_cmbMicDevice.SelectedItem as AudioDeviceInfo)?.Name;

            // Stop preview so the camera is free for recording
            if (_chkFaceCam.Checked)
            {
                _cameraService.StopPreview();
            }

            _pnlFinished.Visible = false;
            _lblStatusPill.Text = "REC · 60 FPS";
            _lblStatusPill.ForeColor = Color.FromArgb(225, 29, 72);
            _lblTimer.Text = "00:00";
            _lblTimer.ForeColor = Color.White;
            _elapsedSeconds = 0;

            _engine.StartRecording(
                screen,
                quality,
                _chkFaceCam.Checked,
                _cameraService.HasPhysicalCamera,
                _cameraService.CameraName,
                corner,
                size,
                _chkMic.Checked,
                micName,
                _chkSysAudio.Checked
            );

            // Verify recording process actually started
            Thread.Sleep(250);
            if (!_engine.IsProcessAlive())
            {
                _lblStatusPill.Text = "READY";
                _lblStatusPill.ForeColor = Color.FromArgb(16, 185, 129);
                _lblTimer.Text = "00:00";
                _lblTimer.ForeColor = Color.FromArgb(148, 163, 184);

                string logPath = Path.Combine(Path.GetTempPath(), "labrec_ffmpeg.log");
                string errorDetail = File.Exists(logPath) ? File.ReadAllText(logPath) : "Could not initialize video capture engine.";
                if (errorDetail.Length > 600) errorDetail = errorDetail.Substring(errorDetail.Length - 600);
                MessageBox.Show($"Failed to start recording:\n\n{errorDetail}", "Lab Recorder Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _btnRecord.Enabled = false;
            _btnPause.Enabled = true;
            _btnPause.Text = "⏸ Pause [F9]";
            _btnStop.Enabled = true;
            _btnStop.NormalColor = Color.FromArgb(190, 18, 60); // Studio Crimson Stop
            _btnStop.HoverColor = Color.FromArgb(225, 29, 72);
            _btnStop.BorderColor = Color.FromArgb(251, 113, 133);
            _btnStop.Invalidate();

            // Lock source controls during recording
            _cmbScreen.Enabled = false;
            _btnSelectArea.Enabled = false;
            _cmbQuality.Enabled = false;
            _chkFaceCam.Enabled = false;
            _cmbCamCorner.Enabled = false;
            _cmbCamSize.Enabled = false;
            _chkMic.Enabled = false;
            _cmbMicDevice.Enabled = false;
            _chkSysAudio.Enabled = false;
            _chkCountdown.Enabled = false;

            _recTimer.Start();
            _pulseTimer.Start();
        }

        private void BtnPause_Click(object? sender, EventArgs e)
        {
            if (!_engine.IsRecording) return;

            if (!_engine.IsPaused)
            {
                _engine.PauseRecording();
                _recTimer.Stop();
                _lblStatusPill.Text = "PAUSED";
                _lblStatusPill.ForeColor = Color.FromArgb(245, 158, 11);
                _lblTimer.ForeColor = Color.FromArgb(245, 158, 11);
                _btnPause.Text = "▶ Resume [F9]";
            }
            else
            {
                _engine.ResumeRecording();
                _recTimer.Start();
                _lblStatusPill.Text = "REC · 60 FPS";
                _lblStatusPill.ForeColor = Color.FromArgb(225, 29, 72);
                _lblTimer.ForeColor = Color.White;
                _btnPause.Text = "⏸ Pause [F9]";
            }
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            if (_countdownTimer.Enabled)
            {
                _countdownTimer.Stop();
                _lblStatusPill.Text = "READY";
                _lblStatusPill.ForeColor = Color.FromArgb(16, 185, 129);
                _lblTimer.Text = "00:00";
                _lblTimer.ForeColor = Color.FromArgb(148, 163, 184);
                _btnRecord.Enabled = true;
                _btnPause.Enabled = false;
                _btnStop.Enabled = false;
                _btnStop.NormalColor = Color.FromArgb(20, 25, 36);
                _btnStop.HoverColor = Color.FromArgb(32, 40, 58);
                _btnStop.BorderColor = Color.FromArgb(42, 52, 74);
                _btnStop.Invalidate();
                return;
            }

            if (!_engine.IsRecording) return;

            _recTimer.Stop();
            _pulseTimer.Stop();
            _lblStatusPill.Text = "FINALIZING...";
            _lblStatusPill.ForeColor = Color.FromArgb(56, 189, 248);
            Refresh();

            _engine.LastElapsedSeconds = _elapsedSeconds;
            _lastResult = _engine.StopRecording();

            _btnRecord.Enabled = true;
            _btnPause.Enabled = false;
            _btnPause.Text = "⏸ Pause [F9]";
            _btnStop.Enabled = false;
            _btnStop.NormalColor = Color.FromArgb(20, 25, 36);
            _btnStop.HoverColor = Color.FromArgb(32, 40, 58);
            _btnStop.BorderColor = Color.FromArgb(42, 52, 74);
            _btnStop.Invalidate();

            // Unlock source controls
            _cmbScreen.Enabled = true;
            _btnSelectArea.Enabled = true;
            _cmbQuality.Enabled = true;
            _chkFaceCam.Enabled = true;
            _cmbCamCorner.Enabled = true;
            _cmbCamSize.Enabled = true;
            _chkMic.Enabled = true;
            _cmbMicDevice.Enabled = true;
            _chkSysAudio.Enabled = true;
            _chkCountdown.Enabled = true;

            if (_lastResult != null && !string.IsNullOrEmpty(_lastResult.FilePath) && File.Exists(_lastResult.FilePath))
            {
                _lblStatusPill.Text = "SAVED";
                _lblStatusPill.ForeColor = Color.FromArgb(16, 185, 129);
                _lblTimer.ForeColor = Color.FromArgb(148, 163, 184);
                ShowFinishedCard(_lastResult);

                // Automatically reveal the recording in Windows Explorer
                try
                {
                    Process.Start("explorer.exe", $"/select,\"{_lastResult.FilePath}\"");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Explorer launch error: " + ex.Message);
                }
            }
            else
            {
                _lblStatusPill.Text = "SAVE FAILED";
                _lblStatusPill.ForeColor = Color.FromArgb(225, 29, 72);
                string logPath = Path.Combine(Path.GetTempPath(), "labrec_ffmpeg.log");
                string detail = File.Exists(logPath) ? File.ReadAllText(logPath) : "No diagnostic log available.";
                if (detail.Length > 800) detail = detail.Substring(detail.Length - 800);
                MessageBox.Show($"Recording failed to save.\n\n{detail}", "Lab Recorder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Resume live preview if face cam is checked
            if (_chkFaceCam.Checked)
            {
                _cameraService.StartPreview(img =>
                {
                    if (IsHandleCreated && !IsDisposed)
                    {
                        try { _picFaceCamPreview.BeginInvoke(new Action(() => { _picFaceCamPreview.Image = img; _picFaceCamPreview.Invalidate(); })); } catch {}
                    }
                });
            }
        }

        private void RecTimer_Tick(object? sender, EventArgs e)
        {
            if (_engine.IsRecording && !_engine.IsPaused && !_engine.IsProcessAlive())
            {
                _recTimer.Stop();
                _pulseTimer.Stop();
                _lblStatusPill.Text = "STOPPED UNEXPECTEDLY";
                _lblStatusPill.ForeColor = Color.FromArgb(225, 29, 72);
                BtnStop_Click(this, EventArgs.Empty);
                return;
            }

            _elapsedSeconds++;
            int mins = _elapsedSeconds / 60;
            int secs = _elapsedSeconds % 60;
            _lblTimer.Text = $"{mins:00}:{secs:00}";
        }

        private void ShowFinishedCard(RecordingResult result)
        {
            if (string.IsNullOrEmpty(result.FilePath) || !File.Exists(result.FilePath))
            {
                _pnlFinished.Visible = false;
                return;
            }

            _lblFinishedName.Text = result.FileName;
            _lblFinishedDuration.Text = $"Duration: {result.DurationFormatted}";
            _lblFinishedSize.Text = $"Size: {result.FileSizeFormatted}";
            _picThumbnail.Image = result.Thumbnail;
            _picThumbnail.Invalidate();
            _pnlFinished.Visible = true;
        }

        private void BtnPlay_Click(object? sender, EventArgs e)
        {
            if (_lastResult != null && File.Exists(_lastResult.FilePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _lastResult.FilePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not launch video: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnOpenFolder_Click(object? sender, EventArgs e)
        {
            if (_lastResult != null && File.Exists(_lastResult.FilePath))
            {
                try
                {
                    Process.Start("explorer.exe", $"/select,\"{_lastResult.FilePath}\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not open folder: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnCopyFile_Click(object? sender, EventArgs e)
        {
            if (_lastResult != null && File.Exists(_lastResult.FilePath))
            {
                try
                {
                    var fileList = new System.Collections.Specialized.StringCollection { _lastResult.FilePath };
                    Clipboard.SetFileDropList(fileList);
                    MessageBox.Show("Video copied to clipboard! You can now paste directly into Discord, Slack, Explorer, etc.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not copy file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void BtnAutoEdit_Click(object? sender, EventArgs e)
        {
            if (_lastResult != null && File.Exists(_lastResult.FilePath))
            {
                string inputFile = _lastResult.FilePath;
                string outputFile = Path.Combine(
                    Path.GetDirectoryName(inputFile)!,
                    Path.GetFileNameWithoutExtension(inputFile) + "_edited.mp4"
                );
                string engineDir = @"c:\Users\velaz\Downloads\AI\Antigravity\Vid Editor";

                _btnAutoEdit.Enabled = false;
                _btnAutoEdit.Text = "Editing...";
                _lblStatusPill.Text = "AUTO-EDITING...";
                _lblStatusPill.ForeColor = Color.FromArgb(56, 189, 248);

                bool success = false;
                await Task.Run(() =>
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "py.exe",
                            Arguments = $"-3.12 -m engine.pipeline \"{inputFile}\" -o \"{outputFile}\"",
                            WorkingDirectory = engineDir,
                            UseShellExecute = false,
                            CreateNoWindow = false
                        };
                        using var proc = Process.Start(psi);
                        proc?.WaitForExit();
                        success = File.Exists(outputFile);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Auto-Edit error: " + ex.Message);
                    }
                });

                _btnAutoEdit.Enabled = true;
                _btnAutoEdit.Text = "Auto-Edit (Free)";

                if (success)
                {
                    _lblStatusPill.Text = "EDITED";
                    _lblStatusPill.ForeColor = Color.FromArgb(16, 185, 129);

                    var fi = new FileInfo(outputFile);
                    var newDuration = _engine.ExtractDuration(outputFile);
                    var newThumb = _engine.ExtractThumbnail(outputFile) ?? _lastResult.Thumbnail;

                    _lastResult = new RecordingResult
                    {
                        FilePath = outputFile,
                        FileName = fi.Name,
                        FileSizeBytes = fi.Length,
                        FileSizeFormatted = RecorderEngine.FormatBytes(fi.Length),
                        Duration = newDuration,
                        DurationFormatted = $"{(int)newDuration.TotalMinutes:00}:{newDuration.Seconds:00}",
                        Thumbnail = newThumb
                    };
                    ShowFinishedCard(_lastResult);

                    MessageBox.Show(
                        $"100% Free Auto-Edit Complete!\n\nDead air cut, stumbles removed, normalized to -14 LUFS, and burned styled ASS captions.\n\nFile:\n{outputFile}",
                        "Antigravity Auto-Editor",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else
                {
                    _lblStatusPill.Text = "Auto-Edit skipped.";
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                UnregisterHotKey(Handle, HOTKEY_ID_RECORD);
                UnregisterHotKey(Handle, HOTKEY_ID_STOP);
            }
            catch {}

            if (_engine.IsRecording)
            {
                var r = MessageBox.Show("A recording is currently in progress. Stop and save before closing?", "Exit Lab Recorder", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Yes)
                {
                    _engine.StopRecording();
                }
                else if (r == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            _audioService.Dispose();
            _cameraService.Dispose();
            _engine.Dispose();
            base.OnFormClosing(e);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LabRecorder
{
    public class ClickOverlayForm : Form
    {
        private class ClickRipple
        {
            public int X { get; }
            public int Y { get; }
            public bool IsRight { get; }
            public long StartTime { get; }

            public ClickRipple(int x, int y, bool isRight)
            {
                X = x;
                Y = y;
                IsRight = isRight;
                StartTime = Environment.TickCount64;
            }
        }

        private readonly System.Windows.Forms.Timer _pollTimer;
        private readonly List<ClickRipple> _ripples = new();
        private bool _prevLeftDown;
        private bool _prevRightDown;
        private const int RIPPLE_DURATION_MS = 280;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;

        public ClickOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            DoubleBuffered = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            _pollTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 FPS polling
            _pollTimer.Tick += PollTimer_Tick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // WS_EX_LAYERED (0x80000) | WS_EX_TRANSPARENT (0x20) | WS_EX_NOACTIVATE (0x08000000) | WS_EX_TOOLWINDOW (0x80)
                cp.ExStyle |= 0x08000000 | 0x00080000 | 0x00000020 | 0x00000080;
                return cp;
            }
        }

        public void Start(Rectangle bounds)
        {
            Bounds = bounds;
            _ripples.Clear();
            _prevLeftDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
            _prevRightDown = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
            Show();
            _pollTimer.Start();
        }

        public void Stop()
        {
            _pollTimer.Stop();
            _ripples.Clear();
            Hide();
        }

        private void PollTimer_Tick(object? sender, EventArgs e)
        {
            bool leftDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
            bool rightDown = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;

            if (leftDown && !_prevLeftDown)
            {
                if (GetCursorPos(out POINT pt) && Bounds.Contains(pt.X, pt.Y))
                {
                    _ripples.Add(new ClickRipple(pt.X - Left, pt.Y - Top, isRight: false));
                }
            }

            if (rightDown && !_prevRightDown)
            {
                if (GetCursorPos(out POINT pt) && Bounds.Contains(pt.X, pt.Y))
                {
                    _ripples.Add(new ClickRipple(pt.X - Left, pt.Y - Top, isRight: true));
                }
            }

            _prevLeftDown = leftDown;
            _prevRightDown = rightDown;

            if (_ripples.Count > 0)
            {
                long now = Environment.TickCount64;
                for (int i = _ripples.Count - 1; i >= 0; i--)
                {
                    if (now - _ripples[i].StartTime > RIPPLE_DURATION_MS)
                    {
                        _ripples.RemoveAt(i);
                    }
                }
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_ripples.Count == 0) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            long now = Environment.TickCount64;
            foreach (var r in _ripples)
            {
                float t = (now - r.StartTime) / (float)RIPPLE_DURATION_MS;
                if (t < 0f || t > 1f) continue;

                // Easing: ease-out quad
                float progress = 1f - (1f - t) * (1f - t);
                int radius = (int)(6 + progress * 24); // Expands from 6px to 30px
                int alpha = (int)(220 * (1f - progress));

                // Left click: Cyan/Sky Blue (#38BDF8), Right click: Violet/Purple (#A855F7)
                Color baseCol = r.IsRight ? Color.FromArgb(168, 85, 247) : Color.FromArgb(56, 189, 248);
                Color ringCol = Color.FromArgb(alpha, baseCol);

                // Outer expanding ring
                using var pen = new Pen(ringCol, 2.2f);
                g.DrawEllipse(pen, r.X - radius, r.Y - radius, radius * 2, radius * 2);

                // Inner core pulse
                int innerRad = (int)(radius * 0.45f);
                int innerAlpha = (int)(alpha * 0.65f);
                using var innerPen = new Pen(Color.FromArgb(innerAlpha, Color.White), 1.4f);
                g.DrawEllipse(innerPen, r.X - innerRad, r.Y - innerRad, innerRad * 2, innerRad * 2);
            }
        }
    }
}

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LabRecorder
{
    public class RegionSelectorForm : Form
    {
        public Rectangle SelectedBounds { get; private set; } = Rectangle.Empty;

        private Bitmap? _desktopSnapshot;
        private bool _isDragging = false;
        private Point _startPoint = Point.Empty;
        private Rectangle _selectedRect = Rectangle.Empty;
        private readonly Rectangle _virtualScreen;

        public RegionSelectorForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            _virtualScreen = SystemInformation.VirtualScreen;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = _virtualScreen.Location;
            Size = _virtualScreen.Size;
            TopMost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            BackColor = Color.Black;

            CaptureDesktopSnapshot();
        }

        private void CaptureDesktopSnapshot()
        {
            try
            {
                _desktopSnapshot = new Bitmap(_virtualScreen.Width, _virtualScreen.Height);
                using var g = Graphics.FromImage(_desktopSnapshot);
                g.CopyFromScreen(_virtualScreen.X, _virtualScreen.Y, 0, 0, _virtualScreen.Size, CopyPixelOperation.SourceCopy);
            }
            catch
            {
                _desktopSnapshot = null;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw captured desktop snapshot as frozen background
            if (_desktopSnapshot != null)
            {
                g.DrawImageUnscaled(_desktopSnapshot, 0, 0);
            }
            else
            {
                g.Clear(Color.FromArgb(16, 20, 28));
            }

            // Dark translucent wash over the unselected area
            using var dimBrush = new SolidBrush(Color.FromArgb(140, 10, 15, 25)); // 55% dark tint

            if (_selectedRect.Width > 0 && _selectedRect.Height > 0)
            {
                // Exclude selected rectangle from dark dimming so the selected area is 100% bright and clear
                using var region = new Region(new Rectangle(0, 0, Width, Height));
                region.Exclude(_selectedRect);
                g.FillRegion(dimBrush, region);

                // Cyan selection border
                using var borderPen = new Pen(Color.FromArgb(56, 189, 248), 2.5f);
                g.DrawRectangle(borderPen, _selectedRect);

                // Subtle inner dashed line
                using var dashPen = new Pen(Color.FromArgb(255, 255, 255), 1f) { DashStyle = DashStyle.Dash };
                var innerRect = new Rectangle(_selectedRect.X + 2, _selectedRect.Y + 2, Math.Max(0, _selectedRect.Width - 4), Math.Max(0, _selectedRect.Height - 4));
                g.DrawRectangle(dashPen, innerRect);

                // Corner handles
                int hs = 8;
                using var handleBrush = new SolidBrush(Color.White);
                using var handlePen = new Pen(Color.FromArgb(2, 132, 199), 1.5f);
                Point[] corners = new Point[]
                {
                    new Point(_selectedRect.Left - hs/2, _selectedRect.Top - hs/2),
                    new Point(_selectedRect.Right - hs/2, _selectedRect.Top - hs/2),
                    new Point(_selectedRect.Left - hs/2, _selectedRect.Bottom - hs/2),
                    new Point(_selectedRect.Right - hs/2, _selectedRect.Bottom - hs/2),
                };
                foreach (var cp in corners)
                {
                    g.FillRectangle(handleBrush, cp.X, cp.Y, hs, hs);
                    g.DrawRectangle(handlePen, cp.X, cp.Y, hs, hs);
                }

                // Floating info badge
                string dimText = $"{_selectedRect.Width} × {_selectedRect.Height} px";
                string tipText = "Release mouse or press Enter to confirm • Esc to cancel";
                using var fontDim = new Font("Segoe UI", 11f, FontStyle.Bold);
                using var fontTip = new Font("Segoe UI", 8.5f, FontStyle.Regular);

                Size szDim = TextRenderer.MeasureText(dimText, fontDim);
                Size szTip = TextRenderer.MeasureText(tipText, fontTip);
                int badgeW = Math.Max(szDim.Width, szTip.Width) + 28;
                int badgeH = 50;

                int badgeX = Math.Max(12, Math.Min(Width - badgeW - 12, _selectedRect.X + (_selectedRect.Width - badgeW) / 2));
                int badgeY = _selectedRect.Y - badgeH - 12;
                if (badgeY < 20) badgeY = _selectedRect.Bottom + 12;

                var badgeRect = new Rectangle(badgeX, badgeY, badgeW, badgeH);
                using var badgePath = MainForm.CreateRoundedRectangle(badgeRect, 8);
                using var badgeBg = new SolidBrush(Color.FromArgb(235, 18, 22, 32));
                using var badgeBorder = new Pen(Color.FromArgb(56, 189, 248), 1.5f);
                g.FillPath(badgeBg, badgePath);
                g.DrawPath(badgeBorder, badgePath);

                TextRenderer.DrawText(g, dimText, fontDim, new Point(badgeX + 14, badgeY + 6), Color.FromArgb(56, 189, 248));
                TextRenderer.DrawText(g, tipText, fontTip, new Point(badgeX + 14, badgeY + 28), Color.FromArgb(203, 213, 225));
            }
            else
            {
                // Initial guide box
                g.FillRectangle(dimBrush, 0, 0, Width, Height);

                string guide = "📐 Click and drag with mouse to select recording area\nPress ESC to cancel";
                using var guideFont = new Font("Segoe UI", 15f, FontStyle.Bold);
                Size sz = TextRenderer.MeasureText(guide, guideFont);
                Point center = new Point((Width - sz.Width) / 2, (Height - sz.Height) / 2);

                var pillRect = new Rectangle(center.X - 24, center.Y - 14, sz.Width + 48, sz.Height + 28);
                using var pillPath = MainForm.CreateRoundedRectangle(pillRect, 12);
                using var pillBg = new SolidBrush(Color.FromArgb(240, 18, 22, 32));
                using var pillBorder = new Pen(Color.FromArgb(56, 189, 248), 2f);
                g.FillPath(pillBg, pillPath);
                g.DrawPath(pillBorder, pillPath);

                TextRenderer.DrawText(g, guide, guideFont, pillRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                _startPoint = e.Location;
                _selectedRect = new Rectangle(e.Location, Size.Empty);
                Invalidate();
            }
            else if (e.Button == MouseButtons.Right)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging)
            {
                int x = Math.Min(_startPoint.X, e.X);
                int y = Math.Min(_startPoint.Y, e.Y);
                int w = Math.Abs(_startPoint.X - e.X);
                int h = Math.Abs(_startPoint.Y - e.Y);
                _selectedRect = new Rectangle(x, y, w, h);
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                if (_selectedRect.Width >= 40 && _selectedRect.Height >= 40)
                {
                    ConfirmSelection();
                }
                else
                {
                    _selectedRect = Rectangle.Empty;
                    Invalidate();
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
            else if (e.KeyCode == Keys.Enter && _selectedRect.Width >= 40 && _selectedRect.Height >= 40)
            {
                ConfirmSelection();
            }
        }

        private void ConfirmSelection()
        {
            int screenX = _virtualScreen.X + _selectedRect.X;
            int screenY = _virtualScreen.Y + _selectedRect.Y;
            int w = _selectedRect.Width;
            int h = _selectedRect.Height;

            // Enforce even dimensions for video encoders
            if (w % 2 != 0) w--;
            if (h % 2 != 0) h--;

            SelectedBounds = new Rectangle(screenX, screenY, w, h);
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _desktopSnapshot?.Dispose();
            _desktopSnapshot = null;
            base.OnFormClosed(e);
        }
    }
}

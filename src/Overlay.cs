using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MuMuBeans
{
    sealed class Overlay : Form
    {
        public Label ClockLabel = Theme.Label("00.00", 30, true), Detail = Theme.Label("等待画面", 8), Target = Theme.Label("等待少豆", 8);
        public bool NoBeans;
        public event EventHandler SurfacePresented;
        readonly Action restore;
        readonly Action<int> choose;
        readonly FontFamily textFamily = new FontFamily("Microsoft YaHei UI"), numberFamily = new FontFamily("Segoe UI");
        readonly StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        readonly StringFormat leading = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        int side = 1, scalePercent = 100, backgroundPercent = 18;
        OverlayStyle style = OverlayStyle.Classic;
        bool dragging, wholeSeconds, showRecovery = true, resourcesDisposed;
        Point offset;
        string lastPresentedKey;
        long lastPresentedStamp;
        internal int SurfaceBuildCount, PresentationCount;
        public OverlayStyle CurrentStyle { get { return style; } }
        float ScaleFactor { get { return scalePercent / 100f; } }

        public Overlay(Action restore, Action<int> chooseSide = null)
        {
            this.restore = restore; choose = chooseSide;
            Text = "豆子 · 轻透悬浮窗"; FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false; TopMost = true; AutoScaleMode = AutoScaleMode.None;
            ClientSize = LogicalSize(); ClockLabel.ForeColor = Theme.Blue;
            AccessibleName = "替身倒计时悬浮窗";
            AccessibleDescription = "点击左或右切换监测侧；拖动移动；双击返回主窗口。";
        }
        protected override CreateParams CreateParams { get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x08000000; return cp; } }
        protected override bool ShowWithoutActivation { get { return true; } }

        public void ApplySettings(DesktopSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (!Enum.IsDefined(typeof(OverlayStyle), settings.OverlayStyle)) throw new ArgumentException("悬浮窗风格不正确。");
            style = settings.OverlayStyle; scalePercent = Math.Max(80, Math.Min(140, settings.OverlayScale));
            backgroundPercent = Math.Max(0, Math.Min(40, settings.OverlayBackground));
            wholeSeconds = settings.WholeSeconds; showRecovery = settings.ShowRecovery;
            Size logical = LogicalSize();
            ClientSize = new Size((int)Math.Round(logical.Width * ScaleFactor), (int)Math.Round(logical.Height * ScaleFactor));
            RefreshSurface();
        }
        Size LogicalSize()
        {
            if (style == OverlayStyle.Focus) return new Size(244, showRecovery ? 105 : 85);
            if (style == OverlayStyle.Digits) return new Size(226, showRecovery ? 98 : 79);
            if (style == OverlayStyle.Ribbon) return new Size(284, showRecovery ? 83 : 68);
            return new Size(268, showRecovery ? 121 : 99);
        }
        RectangleF SideButton(int value)
        {
            if (style == OverlayStyle.Focus) return new RectangleF(value == 0 ? 174 : 209, 6, 29, 20);
            if (style == OverlayStyle.Digits) return new RectangleF(value == 0 ? 165 : 194, 4, 25, 20);
            if (style == OverlayStyle.Ribbon) return new RectangleF(value == 0 ? 221 : 252, 3, 25, 20);
            return new RectangleF(value == 0 ? 184 : 227, 7, 34, 21);
        }
        internal Rectangle SideButtonBounds(int value)
        {
            RectangleF r = SideButton(value); float s = ScaleFactor;
            return Rectangle.Round(new RectangleF(r.X * s, r.Y * s, r.Width * s, r.Height * s));
        }
        public void SetSide(int value) { side = value == 0 ? 0 : 1; }
        internal void ClickSideForTest(int value) { SetSide(value); if (choose != null) choose(side); RefreshSurface(); }
        internal void ClickPointForTest(Point point) { OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)); OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)); }

        public string DisplayClock()
        {
            if (NoBeans) return "对方已无豆";
            double remaining;
            if (!wholeSeconds || !Double.TryParse(ClockLabel.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out remaining) || Double.IsNaN(remaining) || Double.IsInfinity(remaining))
                return ClockLabel.Text;
            return Math.Max(0, Math.Ceiling(remaining)).ToString("00", CultureInfo.InvariantCulture);
        }
        Color ClockColor()
        {
            if (NoBeans) return Color.FromArgb(216, 231, 240);
            double remaining;
            if (wholeSeconds && Double.TryParse(ClockLabel.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out remaining))
            {
                // Quantize urgency together with the displayed second; no per-frame color animation.
                double second = Math.Ceiling(remaining);
                if (second > 0 && second <= 5) return Color.FromArgb(255, 105, 101);
                return second > 0 ? Color.FromArgb(226, 238, 232) : Color.FromArgb(216, 231, 240);
            }
            Color color = ClockLabel.ForeColor;
            if (color == Theme.Blue) return Color.FromArgb(226, 238, 232);
            if (color == Theme.Muted) return Color.FromArgb(216, 231, 240);
            return color;
        }

        protected override void OnShown(EventArgs e) { base.OnShown(e); RefreshSurface(); }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) { lastPresentedKey = null; RefreshSurface(); }
        }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); lastPresentedKey = null; RefreshSurface(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return;
            PointF point = new PointF(e.X / ScaleFactor, e.Y / ScaleFactor);
            if (SideButton(0).Contains(point)) { ClickSideForTest(0); return; }
            if (SideButton(1).Contains(point)) { ClickSideForTest(1); return; }
            dragging = true; offset = e.Location; Capture = true;
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) Location = new Point(Cursor.Position.X - offset.X, Cursor.Position.Y - offset.Y); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; Capture = false; RefreshSurface(); }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (dragging && !Capture) { dragging = false; RefreshSurface(); }
        }
        protected override void OnDoubleClick(EventArgs e) { base.OnDoubleClick(e); if (restore != null) restore(); }

        void TextPath(Graphics g, string text, float size, RectangleF area, Color color, bool bold, bool outline, bool left = false, bool numeric = false)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddString(text ?? "", numeric ? numberFamily : textFamily, (int)(bold ? FontStyle.Bold : FontStyle.Regular), Math.Max(1, size), area, left ? leading : centered);
                GraphicsState clip = g.Save(); g.SetClip(area);
                if (outline) using (Pen pen = new Pen(Color.FromArgb(235, 8, 18, 25), size > 25 ? 2.4f : 1.6f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
                using (Brush brush = new SolidBrush(color)) g.FillPath(brush, path);
                g.Restore(clip);
            }
        }
        void DrawBackdrop(Graphics g, Size logical)
        {
            if (style == OverlayStyle.Digits || backgroundPercent == 0) return;
            int alpha = (int)Math.Round(backgroundPercent * 255 / 100.0);
            float radius = style == OverlayStyle.Ribbon ? 14 : style == OverlayStyle.Focus ? 15 : 17;
            using (GraphicsPath path = Theme.Round(new RectangleF(1, 1, logical.Width - 2, logical.Height - 2), radius))
            {
                using (Brush brush = new SolidBrush(Color.FromArgb(alpha, 12, 25, 34))) g.FillPath(brush, path);
                using (Pen border = new Pen(Color.FromArgb(Math.Min(90, alpha + 10), 209, 235, 239), .8f)) g.DrawPath(border, path);
            }
        }
        void DrawButtons(Graphics g)
        {
            for (int i = 0; i < 2; i++)
            {
                RectangleF rect = SideButton(i);
                using (GraphicsPath path = Theme.Round(rect, 7))
                using (Brush brush = new SolidBrush(i == side ? Color.FromArgb(168, 13, 105, 113) : Color.FromArgb(50, 13, 30, 39))) g.FillPath(brush, path);
                TextPath(g, i == 0 ? "左" : "右", 10.5f, rect, Color.FromArgb(i == side ? 255 : 214, 239, 249, 252), i == side, true);
            }
        }
        internal RectangleF PrimaryValueBounds(bool recovery)
        {
            Size logical = LogicalSize(); float top = style == OverlayStyle.Ribbon ? 34 : style == OverlayStyle.Classic ? 43 : 40;
            float height = style == OverlayStyle.Classic ? 46 : style == OverlayStyle.Ribbon ? 33 : 38;
            if (!showRecovery) return new RectangleF(8, top, logical.Width - 16, height);
            float width = (logical.Width - 28) / 2f;
            return new RectangleF(recovery ? logical.Width / 2f + 6 : 8, top, width, height);
        }
        internal float PrimaryValueFontSize { get { return style == OverlayStyle.Classic ? 38 : style == OverlayStyle.Ribbon ? 32 : style == OverlayStyle.Digits ? 32 : 34; } }
        void DrawContent(Graphics g)
        {
            Size logical = LogicalSize();
            Color white = Color.FromArgb(233, 242, 240), muted = Color.FromArgb(219, 232, 232);
            bool ribbon = style == OverlayStyle.Ribbon, classic = style == OverlayStyle.Classic;
            float headerY = ribbon ? 3 : classic ? 7 : style == OverlayStyle.Digits ? 4 : 6;
            TextPath(g, Detail.Text, 10, new RectangleF(10, headerY, SideButton(0).Left - 15, 20), white, false, true, true);
            RectangleF clock = PrimaryValueBounds(false); float labelTop = clock.Y - 16;
            TextPath(g, "现实冷却", 10, new RectangleF(clock.X, labelTop, clock.Width, 16), muted, false, true);
            if (NoBeans)
                TextPath(g, DisplayClock(), showRecovery ? 18 : 25, clock, ClockColor(), true, true);
            else TextPath(g, DisplayClock(), PrimaryValueFontSize, clock, ClockColor(), true, true, false, true);
            if (showRecovery)
            {
                RecoveryDisplay recovery = RecoveryDisplay.Parse(Target.Text); RectangleF value = PrimaryValueBounds(true);
                TextPath(g, recovery.Caption + (recovery.IsApproximate ? " ≈" : ""), 10, new RectangleF(value.X, labelTop, value.Width, 16), muted, false, true);
                // Keep the estimate marker in the caption and explicit status, so all five
                // significant digits fit at the same size as the real-time cooldown.
                TextPath(g, recovery.IsApproximate ? recovery.Value.Substring(1) : recovery.Value, PrimaryValueFontSize, value, white, true, true, false, true);
                // Unknown, insufficient-round and estimated states remain explicit beside the main value.
                float statusTop = value.Bottom + (classic ? 3 : 1);
                TextPath(g, recovery.Status, classic ? 10 : 9, new RectangleF(8, statusTop, logical.Width - 16, 17), muted, false, true);
                using (Pen separator = new Pen(Color.FromArgb(55, 218, 235, 232), .7f))
                    g.DrawLine(separator, logical.Width / 2f, labelTop + 5, logical.Width / 2f, value.Bottom - 3);
            }
        }
        public Bitmap Surface()
        {
            SurfaceBuildCount++;
            Bitmap bitmap = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.ScaleTransform(ScaleFactor, ScaleFactor);
                Size logical = LogicalSize(); DrawBackdrop(g, logical);
                DrawContent(g);
                DrawButtons(g);
            }
            return bitmap;
        }
        string StateKey()
        {
            return CurrentStyle + "|" + scalePercent + "|" + backgroundPercent + "|" + showRecovery + "|" + Width + "|" + Height + "|" + side + "|" + NoBeans + "|" +
                DisplayClock() + "\u001f" + Detail.Text + "\u001f" + (showRecovery ? Target.Text : "") + "|" + ClockColor().ToArgb();
        }
        public void RefreshSurface()
        {
            if (IsDisposed || !IsHandleCreated || !Visible) return;
            string key = StateKey(); if (key == lastPresentedKey) return;
            // Moving the existing layered surface needs no new bitmap. While dragging, limit
            // decimal-clock repaints to 10 Hz; release flushes the newest state immediately.
            if (dragging && lastPresentedKey != null && (Stopwatch.GetTimestamp() - lastPresentedStamp) / (double)Stopwatch.Frequency < .1) return;
            using (Bitmap bitmap = Surface())
            {
                IntPtr screen = IntPtr.Zero, dc = IntPtr.Zero, dib = IntPtr.Zero, previous = IntPtr.Zero;
                try
                {
                    screen = GetDC(IntPtr.Zero); if (screen == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
                    dc = CreateCompatibleDC(screen); if (dc == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
                    dib = bitmap.GetHbitmap(Color.FromArgb(0));
                    previous = SelectObject(dc, dib); if (previous == IntPtr.Zero || previous == new IntPtr(-1)) throw new System.ComponentModel.Win32Exception();
                    P point = new P { X = Left, Y = Top }, origin = new P(); S size = new S { W = Width, H = Height }; B blend = new B { Alpha = 255, Format = 1 };
                    if (!UpdateLayeredWindow(Handle, screen, ref point, ref size, dc, ref origin, 0, ref blend, 2)) throw new System.ComponentModel.Win32Exception();
                    lastPresentedKey = key; lastPresentedStamp = Stopwatch.GetTimestamp(); PresentationCount++;
                }
                finally
                {
                    if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(dc, previous);
                    if (dib != IntPtr.Zero) DeleteObject(dib);
                    if (dc != IntPtr.Zero) DeleteDC(dc);
                    if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
                }
            }
            if (SurfacePresented != null) SurfacePresented(this, EventArgs.Empty);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true;
                Font clockFont = ClockLabel.Font, detailFont = Detail.Font, targetFont = Target.Font;
                ClockLabel.Dispose(); Detail.Dispose(); Target.Dispose();
                clockFont.Dispose(); detailFont.Dispose(); targetFont.Dispose();
                textFamily.Dispose(); numberFamily.Dispose(); centered.Dispose(); leading.Dispose();
            }
            base.Dispose(disposing);
        }
        [StructLayout(LayoutKind.Sequential)] struct P { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct S { public int W, H; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct B { public byte Op, Flags, Alpha, Format; }
        [DllImport("user32.dll", SetLastError = true)] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr d, ref P p, ref S s, IntPtr source, ref P origin, int key, ref B blend, int flags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    }
}

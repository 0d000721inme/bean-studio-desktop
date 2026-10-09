using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MuMuBeans
{
    // Only captures a window owned by this test; no emulator is opened or enumerated.
    static class BeanRelocationTests
    {
        sealed class Fixture : Form
        {
            public Bitmap Frame;
            public int Paints, Ticks;
            readonly System.Windows.Forms.Timer repaint = new System.Windows.Forms.Timer();
            public Fixture(Size imageSize)
            {
                Rectangle screen = Screen.PrimaryScreen.WorkingArea;
                double scale = Math.Min(.5, Math.Min((screen.Width - 80.0) / imageSize.Width,
                    (screen.Height - 100.0) / imageSize.Height));
                Text = "Bean Studio 豆子重定位回归（自建测试窗口）";
                FormBorderStyle = FormBorderStyle.FixedSingle; StartPosition = FormStartPosition.Manual;
                Location = new Point(screen.Left + 30, screen.Top + 30);
                ClientSize = new Size((int)(imageSize.Width * scale), (int)(imageSize.Height * scale));
                TopMost = true; ShowInTaskbar = true; DoubleBuffered = true;
                repaint.Interval = 33; repaint.Tick += delegate { Ticks++; Invalidate(); }; repaint.Start();
            }
            protected override bool ShowWithoutActivation { get { return true; } }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                if (Frame != null) e.Graphics.DrawImage(Frame, ClientRectangle);
                Paints++;
                // WGC can omit unchanged presentations. Animate only outside both HUD ROIs.
                using (Brush marker = new SolidBrush(Color.FromArgb(60 + Ticks % 190, 90, 160)))
                    e.Graphics.FillRectangle(marker, ClientSize.Width - 12, ClientSize.Height - 12, 6, 6);
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) repaint.Dispose();
                base.Dispose(disposing);
            }
        }

        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static bool Wait(Func<bool> condition, int milliseconds)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < milliseconds)
            {
                Application.DoEvents(); if (condition()) return true; Thread.Sleep(10);
            }
            return condition();
        }
        static void Check(bool ok, string text, List<string> rows)
        {
            rows.Add((ok ? "PASS " : "FAIL ") + text);
            if (!ok) throw new InvalidOperationException(text);
        }
        static double Deadline(Engine engine)
        {
            object gate = typeof(Engine).GetField("gate", Private).GetValue(engine);
            lock (gate) return ((Counter)typeof(Engine).GetField("counter", Private).GetValue(engine)).Deadline;
        }
        static double NextLocate(Engine engine)
        {
            object gate = typeof(Engine).GetField("gate", Private).GetValue(engine);
            lock (gate) return (double)typeof(Engine).GetField("nextLocate", Private).GetValue(engine);
        }
        static void Diagnostics(Engine engine, Fixture fixture, string folder, List<string> rows)
        {
            Snapshot s = engine.GetSnapshot();
            rows.Add("DIAGNOSTIC Status=" + s.Status + " Count=" + s.Count + " Left=" + s.LeftRegion +
                " CaptureSize=" + s.CaptureSize + " Raw=" + (s.Reading == null ? "null" :
                "valid=" + s.Reading.Valid + " count=" + s.Reading.Count + " reason=" + s.Reading.Reason) +
                " Samples=" + engine.Samples + " WindowCapture=" + WindowCapture.Status +
                " Readbacks=" + WindowCapture.Readbacks);
            Rectangle bounds = Native.ClientBounds(fixture.Handle);
            rows.Add("DIAGNOSTIC FixtureClient=" + fixture.ClientSize + " Bounds=" + bounds +
                " Uncovered=" + Native.Uncovered(fixture.Handle, bounds) + " Paints=" + fixture.Paints +
                " Ticks=" + fixture.Ticks);
            try { rows.Add("DIAGNOSTIC Export=" + engine.Export(folder)); }
            catch (Exception ex) { rows.Add("DIAGNOSTIC Export error=" + ex.Message); }
            try
            {
                using (Bitmap frame = WindowCapture.Capture(fixture.Handle, bounds))
                {
                    frame.Save(Path.Combine(folder, "自建窗口失败画面.png"));
                    LocatedPair pair = AutoLocator.Find(frame);
                    rows.Add("DIAGNOSTIC Captured AutoLeft=" + pair.Left + " count=" +
                        (pair.LeftReading == null ? "null" : pair.LeftReading.Count.ToString()));
                }
            }
            catch (Exception ex) { rows.Add("DIAGNOSTIC Capture error=" + ex.Message); }
        }

        public static void Run(string[] args)
        {
            // --bean-relocation-test old-dark-frame.png new-dark-frame.png result-folder
            string folder = args[3]; Directory.CreateDirectory(folder);
            List<string> rows = new List<string>(); int failures = 0;
            try
            {
                using (Bitmap oldZero = new Bitmap(args[1]))
                using (Bitmap newZero = new Bitmap(args[2]))
                using (Bitmap oldOne = (Bitmap)oldZero.Clone())
                using (Bitmap black = new Bitmap(oldZero.Width, oldZero.Height))
                using (Bitmap blocked = (Bitmap)newZero.Clone())
                using (Fixture fixture = new Fixture(oldZero.Size))
                {
                    LocatedPair oldPair = AutoLocator.Find(oldZero), newPair = AutoLocator.Find(newZero);
                    Check(oldZero.Size == newZero.Size && oldPair.LeftReading != null && oldPair.LeftReading.Valid &&
                        oldPair.LeftReading.Count == 0 && newPair.LeftReading != null && newPair.LeftReading.Valid &&
                        newPair.LeftReading.Count == 0 && oldPair.Left != newPair.Left,
                        "两张同尺寸真实暗豆样本具有不同且有效的左侧0豆位置", rows);
                    Check(!Detector.Analyze(newZero, oldPair.Left, 150).Valid &&
                        !Detector.Analyze(oldZero, newPair.Left, 150).Valid,
                        "旧框到新图与新框到旧图均复现锁定位置失效", rows);
                    Check(oldPair.RightReading != null && oldPair.RightReading.Count == 4,
                        "旧图右侧满豆用作测试的一颗亮豆", rows);
                    Rectangle source = oldPair.Right, destination = oldPair.Left;
                    source.X += source.Width / 4; source.Width /= 4;
                    destination.X += destination.Width / 4; destination.Width /= 4;
                    using (Graphics g = Graphics.FromImage(oldOne))
                        g.DrawImage(oldZero, destination, source, GraphicsUnit.Pixel);
                    Check(Detector.Analyze(oldOne, oldPair.Left, 150).Valid &&
                        Detector.Analyze(oldOne, oldPair.Left, 150).Count == 1,
                        "合成1豆只用于建立实际消耗事件", rows);
                    using (Bitmap presented = new Bitmap(fixture.ClientSize.Width, fixture.ClientSize.Height))
                    {
                        using (Graphics g = Graphics.FromImage(presented))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                            g.DrawImage(oldOne, new Rectangle(Point.Empty, presented.Size));
                        }
                        LocatedPair halfPair = AutoLocator.Find(presented);
                        Check(halfPair.LeftReading != null && halfPair.LeftReading.Valid && halfPair.LeftReading.Count == 1,
                            "Fixture实际客户区缩放后的合成1豆仍可自动定位确认", rows);
                    }
                    Rectangle mask = newPair.Left; mask.Width = (int)Math.Ceiling(mask.Width / 4.0);
                    using (Graphics g = Graphics.FromImage(blocked)) g.FillRectangle(Brushes.Black, mask);
                    fixture.Frame = oldOne; fixture.Show(); fixture.Refresh();
                    using (Engine engine = new Engine())
                    {
                        try
                        {
                        engine.ConfigureAuto(fixture.Handle, 0, true);
                        Check(Wait(delegate { return engine.GetSnapshot().Count == 1; }, 5000),
                            "自建半尺寸动态窗口：真实Engine确认1豆基准", rows);
                        Check(engine.GetSnapshot().Triggers == 0, "首次基准不启动倒计时", rows);
                        fixture.Frame = oldZero; fixture.Refresh();
                        Check(Wait(delegate { return engine.GetSnapshot().Count == 0 && engine.GetSnapshot().Triggers == 1; }, 3000),
                            "真实Engine确认1→0产生一次15秒倒计时", rows);
                        double deadline = Deadline(engine); RectangleF oldLocked = engine.GetSnapshot().LeftRegion;
                        fixture.Frame = newZero; fixture.Refresh();
                        Check(Wait(delegate { Snapshot s = engine.GetSnapshot(); return s.Reading != null && !s.Reading.Valid; }, 2000),
                            "同尺寸切换HUD后旧锁定框实际产生Invalid", rows);
                        Check(Wait(delegate
                        {
                            Snapshot s = engine.GetSnapshot(); return s.Count == 0 && s.Reading != null &&
                                s.Reading.Valid && s.LeftRegion != oldLocked;
                        }, 4000), "持续失效触发自动校准并恢复新位置0豆确认", rows);
                        Snapshot relocated = engine.GetSnapshot();
                        Check(Deadline(engine) == deadline && relocated.Triggers == 1 && relocated.Remaining > 0,
                            "换框只建立新基准，保留精确截止时间且不伪造消耗", rows);
                        RectangleF newLocked = relocated.LeftRegion;

                        fixture.Frame = black; fixture.Refresh();
                        double previousLocate = NextLocate(engine); int scans = 0;
                        Wait(delegate
                        {
                            double next = NextLocate(engine); if (next != previousLocate) { scans++; previousLocate = next; }
                            return false;
                        }, 2200);
                        Snapshot missing = engine.GetSnapshot();
                        Check(missing.Reading != null && !missing.Reading.Valid && !missing.Count.HasValue,
                            "黑屏不伪装0豆，连续确认基准正常失效", rows);
                        Check(missing.LeftRegion == newLocked && Deadline(engine) == deadline && missing.Triggers == 1 &&
                            missing.Remaining > 0 && missing.Remaining < relocated.Remaining,
                            "黑屏重定位失败保留锁定位置，原倒计时继续", rows);
                        Check(scans >= 1 && scans <= 3, "黑屏2.2秒重新定位次数限频，实际=" + scans, rows);
                        fixture.Frame = newZero; fixture.Refresh();
                        Check(Wait(delegate { return engine.GetSnapshot().Count == 0; }, 2500),
                            "黑屏返回后再次确认0颗而不追加消耗事件", rows);
                        fixture.Frame = blocked; fixture.Refresh();
                        Wait(delegate { return false; }, 1400);
                        Snapshot occluded = engine.GetSnapshot();
                        Check(occluded.Reading != null && !occluded.Reading.Valid && !occluded.Count.HasValue &&
                            occluded.LeftRegion == newLocked && Deadline(engine) == deadline && occluded.Triggers == 1,
                            "单格遮挡不会确认无豆或清除/重启已有倒计时", rows);
                        fixture.Frame = newZero; fixture.Refresh();
                        Check(Wait(delegate { return engine.GetSnapshot().Count == 0; }, 2500) &&
                            engine.GetSnapshot().Triggers == 1 && Deadline(engine) == deadline,
                            "遮挡移除后恢复基准，截止时间仍原封不动", rows);
                        rows.Add("INFO 最后捕获模式=" + WindowCapture.Status + "；仅捕获测试自己创建的窗口。");
                        }
                        catch { Diagnostics(engine, fixture, folder, rows); throw; }
                    }
                    fixture.Hide();
                }
            }
            catch (Exception ex) { failures++; rows.Add("ERROR " + ex); }
            finally
            {
                WindowCapture.SetPaused(true);
                if (!WindowCapture.PauseCompleted.Wait(10000)) { failures++; rows.Add("FAIL 最终捕获释放超时"); }
                rows.Add("FAILURES=" + failures);
                File.WriteAllLines(Path.Combine(folder, "豆子自动重定位验证.txt"), rows.ToArray(), new UTF8Encoding(true));
                Environment.ExitCode = failures == 0 ? 0 : 1;
            }
        }
    }
}

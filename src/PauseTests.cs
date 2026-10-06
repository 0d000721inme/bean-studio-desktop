using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MuMuBeans
{
    // Exercises actual WGC lifecycle with a synthetic window. No emulator or user frame is used.
    static class PauseTests
    {
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly List<string> rows = new List<string>();
        static int failures;

        public static void Run(string folder)
        {
            Directory.CreateDirectory(folder); rows.Clear(); failures = 0;
            string config = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clock-region.txt");
            byte[] savedConfig = File.Exists(config) ? File.ReadAllBytes(config) : null;
            try
            {
                CounterChecks();
                using (TestWindowHost host = new TestWindowHost())
                {
                    CaptureProbe(host, folder);
                    CaptureChecks(host, folder);
                    EngineChecks(host);
                    UiChecks(host, folder);
                }
            }
            catch (Exception ex) { Check(false, "回归测试异常：" + ex); }
            finally
            {
                WindowCapture.SetPaused(true);
                if (!WindowCapture.PauseCompleted.Wait(10000)) Check(false, "最终捕获释放超时");
                if (savedConfig != null) File.WriteAllBytes(config, savedConfig);
                else if (File.Exists(config)) File.Delete(config);
                rows.Add("FAILURES=" + failures.ToString(CultureInfo.InvariantCulture));
                File.WriteAllLines(Path.Combine(folder, "暂停回归验证.txt"), rows.ToArray(), new UTF8Encoding(true));
                Environment.ExitCode = failures == 0 ? 0 : 1;
                Console.WriteLine(rows[rows.Count - 1]);
            }
        }

        static void Check(bool ok, string title)
        {
            rows.Add((ok ? "PASS " : "FAIL ") + title);
            if (!ok) failures++;
        }

        static bool Until(Func<bool> condition, int timeout)
        {
            Stopwatch wait = Stopwatch.StartNew();
            while (wait.ElapsedMilliseconds < timeout)
            {
                if (condition()) return true;
                Application.DoEvents(); Thread.Sleep(15);
            }
            return condition();
        }

        static bool Stopped()
        {
            return WindowCapture.PauseCompleted.Wait(10000) && WindowCapture.Paused;
        }

        static bool Stopped(Engine engine)
        {
            return Stopped() && Until(delegate { return engine.PauseCompleted; }, 10000);
        }

        static void CounterChecks()
        {
            Counter c = new Counter();
            c.Observe(4, 0); c.Observe(4, .02); c.Observe(4, .04);
            c.Observe(3, .10); c.Observe(3, .12);
            bool triggered = c.Observe(3, .14);
            Check(triggered && c.Triggers == 1 && Math.Abs(c.Deadline - 15.14) < .000001,
                "真实少豆设置完整 15.000 秒 deadline");
            double deadline = c.Deadline; c.Forget();
            Check(!c.Baseline.HasValue && c.Deadline == deadline && Math.Abs(c.Remaining(1.14) - 14) < .000001,
                "暂停忘记豆数基准，已存在的冷却按现实时间继续");
            c.Observe(2, 2); c.Observe(2, 2.02); c.Observe(2, 2.04);
            Check(c.Baseline == 2 && c.Triggers == 1 && c.Deadline == deadline,
                "恢复后较少豆数建立新基准，不把暂停期间变化误判为新消耗");
        }

        static void CaptureChecks(TestWindowHost host, string folder)
        {
            WindowCapture.SetPaused(false);
            long initial = WindowCapture.Readbacks;
            long initialTicks = host.Ticks, initialPaints = host.Paints;
            WindowCapture.Available(host.Handle, host.Bounds);
            bool active = Until(delegate { return BrokerFrame(host, initial + 3); }, 10000);
            Check(active, "自建动态窗实际 WGC 完成 GPU 读回且 Capture 返回完整非空 Bitmap（" + WindowCapture.Status + "）");
            BrokerDiagnostics(host, folder, "initial", initial, initialTicks, initialPaints, active);
            WindowCapture.SetPaused(true);
            Check(Stopped(), "暂停完成任务等待 WGC 会话与初始化任务释放");
            long stoppedCount = WindowCapture.Readbacks;
            bool blocked = false;
            try { using (Bitmap b = WindowCapture.Capture(host.Handle, host.Bounds)) { } }
            catch (InvalidOperationException) { blocked = true; }
            bool unavailable = !WindowCapture.Available(host.Handle, host.Bounds);
            Thread.Sleep(700);
            Check(blocked && unavailable && WindowCapture.Readbacks == stoppedCount,
                "暂停拒绝 Capture/Available 重建，动态窗连续重绘 700 ms 读回计数不变");
            WindowCapture.SetPaused(false); WindowCapture.Available(host.Handle, host.Bounds);
            long resumedTicks = host.Ticks, resumedPaints = host.Paints;
            bool resumed = Until(delegate { return BrokerFrame(host, stoppedCount + 3); }, 10000);
            Check(resumed,
                "恢复重新创建 WGC 会话，成功读回复增且 Capture 返回完整非空 Bitmap，无兼容截图代替");
            BrokerDiagnostics(host, folder, "resume", stoppedCount, resumedTicks, resumedPaints, resumed);
            WindowCapture.SetPaused(true); Stopped();

            bool racesSafe = true;
            for (int i = 0; i < 8; i++)
            {
                WindowCapture.SetPaused(false);
                WindowCapture.Available(host.Handle, host.Bounds);
                WindowCapture.SetPaused(true);
                if (!Stopped()) racesSafe = false;
                long before = WindowCapture.Readbacks;
                Thread.Sleep(80);
                if (WindowCapture.Readbacks != before || WindowCapture.Available(host.Handle, host.Bounds)) racesSafe = false;
            }
            Check(racesSafe, "WGC 初始化与暂停竞态 8 轮，过期初始化不复活会话且无死锁");
        }

        static void BrokerDiagnostics(TestWindowHost host, string folder, string phase, long startReads,
            long startTicks, long startPaints, bool passed)
        {
            StringBuilder text = new StringBuilder();
            long endedReads = WindowCapture.Readbacks;
            text.AppendLine("Phase=" + phase + " AssertionPassed=" + passed + " RequiredCompletedCopies=3");
            text.AppendLine("ReadbacksStart=" + startReads + " ReadbacksEnd=" + endedReads + " Delta=" + (endedReads - startReads));
            text.AppendLine("FixtureTickDelta=" + (host.Ticks - startTicks) + " FixturePaintDelta=" + (host.Paints - startPaints));
            text.AppendLine("ClientBounds=" + host.Bounds + " Uncovered=" + Native.Uncovered(host.Handle, host.Bounds));
            text.AppendLine("BrokerStatusBeforeDiagnostics=" + WindowCapture.Status);
            object captureGate = typeof(WindowCapture).GetField("gate", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            lock (captureGate)
            {
                WindowFrames frame = (WindowFrames)typeof(WindowCapture).GetField("frames", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                text.AppendLine("WindowFramesPresent=" + (frame != null) + " FrameError=" + (frame == null ? "none" : frame.Error));
            }
            BrokerCropTrace(text, host, "STA diagnostic Capture");
            string mta = Task.Run(delegate
            {
                StringBuilder trace = new StringBuilder(); BrokerCropTrace(trace, host, "MTA diagnostic Capture"); return trace.ToString();
            }).GetAwaiter().GetResult();
            text.Append(mta);
            text.AppendLine("ReadbacksAfterDiagnostics=" + WindowCapture.Readbacks);
            File.WriteAllText(Path.Combine(folder, "wgc-broker-" + phase + ".txt"), text.ToString(), new UTF8Encoding(true));
            rows.Add("INFO " + phase + " 实际完成读回=" + (endedReads - startReads) + "/3；fixture Tick=" +
                (host.Ticks - startTicks) + " Paint=" + (host.Paints - startPaints) + "；详细取帧诊断见 wgc-broker-" + phase + ".txt");
        }

        static void BrokerCropTrace(StringBuilder text, TestWindowHost host, string phase)
        {
            text.AppendLine(phase + " Apartment=" + Thread.CurrentThread.GetApartmentState() + " StatusBefore=" + WindowCapture.Status);
            try
            {
                using (Bitmap b = WindowCapture.Capture(host.Handle, host.Bounds))
                {
                    string size = b == null ? "null" : b.Size.ToString();
                    text.AppendLine("Bitmap=" + size + " StatusAfter=" + WindowCapture.Status);
                    if (b != null && b.Width > 8 && b.Height > 192)
                        text.AppendLine("SyntheticPixel(8,160)=" + b.GetPixel(8, 160));
                }
            }
            catch (Exception ex) { ProbeException(text, phase, ex); text.AppendLine("StatusAfterException=" + WindowCapture.Status); }
        }

        static void CaptureProbe(TestWindowHost host, string folder)
        {
            WindowCapture.SetPaused(true); Stopped();
            StringBuilder text = new StringBuilder();
            text.AppendLine("只捕获自建普通 WinForms 合成测试窗，未枚举或捕获用户窗口。");
            text.AppendLine("HWND=0x" + host.Handle.ToInt64().ToString("X", CultureInfo.InvariantCulture));
            text.AppendLine("ClientBounds=" + host.Bounds + " IsWindow=" + Native.IsWindow(host.Handle) +
                " IsVisible=" + Native.IsWindowVisible(host.Handle) + " IsIconic=" + Native.IsIconic(host.Handle));
            text.AppendLine("CallerApartment=" + Thread.CurrentThread.GetApartmentState());
            text.AppendLine("Fixture=FixedSingle, ShowInTaskbar=true, ShowWithoutActivation=true, TopMost=false");
            // Match production Prepare: WinRT initialization, frame access, and disposal on MTA.
            string result = Task.Run(delegate
            {
                StringBuilder trace = new StringBuilder();
                trace.AppendLine("CaptureApartment=" + Thread.CurrentThread.GetApartmentState());
                try
                {
                    long before = WindowCapture.Readbacks;
                    using (WindowFrames frames = new WindowFrames(host.Handle))
                    {
                        trace.AppendLine("DIRECT MTA CONSTRUCTOR: SUCCESS");
                        Stopwatch wait = Stopwatch.StartNew();
                        while (WindowCapture.Readbacks <= before && wait.ElapsedMilliseconds < 5000) Thread.Sleep(15);
                        bool arrived = WindowCapture.Readbacks > before;
                        trace.AppendLine("ReadbackCompleted=" + arrived + " ReadbacksDelta=" + (WindowCapture.Readbacks - before));
                        try
                        {
                            using (Bitmap crop = frames.Crop(host.Bounds))
                                trace.AppendLine("Crop=" + crop.Size + " NonEmpty=" + (crop.Width > 0 && crop.Height > 0));
                        }
                        catch (Exception ex) { ProbeException(trace, "MTA CROP", ex); }
                        trace.AppendLine("FrameStatus=" + frames.Error);
                    }
                }
                catch (Exception ex) { ProbeException(trace, "DIRECT MTA CONSTRUCTOR/DISPOSE", ex); }
                return trace.ToString();
            }).GetAwaiter().GetResult();
            text.Append(result);
            File.WriteAllText(Path.Combine(folder, "wgc-probe.txt"), text.ToString(), new UTF8Encoding(true));
            rows.Add("INFO 生产同样 MTA 线程的 WGC 构造/取帧/处置探针、完整异常栈及 HResult 已保存 wgc-probe.txt");
        }

        static bool BrokerFrame(TestWindowHost host, long minimumReadbacks)
        {
            if (WindowCapture.Readbacks < minimumReadbacks) return false;
            try
            {
                using (Bitmap b = WindowCapture.Capture(host.Handle, host.Bounds))
                    return b != null && b.Width == host.Bounds.Width && b.Height == host.Bounds.Height &&
                        WindowCapture.Status == "WGC 窗口捕获" && WindowCapture.Readbacks >= minimumReadbacks;
            }
            catch (InvalidOperationException) { return false; }
        }

        static void ProbeException(StringBuilder text, string phase, Exception error)
        {
            text.AppendLine(phase + ": FAILURE");
            int depth = 0;
            for (Exception current = error; current != null; current = current.InnerException)
                text.AppendLine("Exception[" + depth++ + "]=" + current.GetType().FullName +
                    " HResult=0x" + current.HResult.ToString("X8", CultureInfo.InvariantCulture) + " Message=" + current.Message);
            text.AppendLine(error.ToString());
        }

        static void EngineChecks(TestWindowHost host)
        {
            using (Engine e = new Engine())
            {
                RectangleF roi = new RectangleF(.08f, .20f, .40f, .18f);
                e.SetPaused(false); e.Configure(host.Handle, roi, 150, true);
                long beanStart = e.Samples, clockStart = e.Clock.Samples;
                Check(Until(delegate { return e.Samples > beanStart + 2 && e.Clock.Samples > clockStart; }, 10000),
                    "真实动态窗驱动豆子与游戏时间两个识别线程采样");

                // Switch off observation before seeding a genuine Counter decrease under Engine's lock.
                e.Configure(host.Handle, roi, 150, false);
                object gate = typeof(Engine).GetField("gate", Private).GetValue(e);
                Counter counter = (Counter)typeof(Engine).GetField("counter", Private).GetValue(e);
                Stopwatch time = (Stopwatch)typeof(Engine).GetField("time", Private).GetValue(e);
                lock (gate)
                {
                    double now = time.Elapsed.TotalSeconds;
                    counter.Observe(4, now - .10); counter.Observe(4, now - .08); counter.Observe(4, now - .06);
                    counter.Observe(3, now - .04); counter.Observe(3, now - .02); counter.Observe(3, now);
                }
                double remaining = e.GetSnapshot().Remaining;
                double deadline = counter.Deadline;
                Stopwatch pausedWallTime = Stopwatch.StartNew();
                e.SetPaused(true); Check(Stopped(e), "Engine 暂停等待捕获及两个识别线程真正空闲");
                Thread.Sleep(150);
                long beans = e.Samples, clocks = e.Clock.Samples, reads = WindowCapture.Readbacks;
                e.ConfigureAuto(host.Handle, 0, true, true);
                e.ConfigureAuto(host.Handle, 1, true, true);
                e.Configure(host.Handle, roi, 150, true);
                e.Clock.SetRegion(new RectangleF(.40f, .05f, .20f, .20f));
                e.Clock.Refresh();
                Thread.Sleep(700);
                Snapshot paused = e.GetSnapshot();
                double expectedRemaining = Math.Max(0, remaining - pausedWallTime.Elapsed.TotalSeconds);
                Check(paused.Paused && e.Samples == beans && e.Clock.Samples == clocks && WindowCapture.Readbacks == reads,
                    "暂停时切左右、改区域、Configure(run=true)、Refresh 均不启动捕获或两识别线程");
                Check(remaining > 14.8 && paused.Remaining < remaining - .65 && Math.Abs(paused.Remaining - expectedRemaining) < .10 && counter.Deadline == deadline,
                    "Engine 暂停保留既有 deadline，浮窗剩余时间继续下降而不清零/重置");
                Check(!paused.Count.HasValue, "暂停清除已确认豆数，恢复不会沿用陈旧基准");
                e.SetPaused(false); e.Configure(host.Handle, roi, 150, true);
                Check(Until(delegate { return e.Samples > beans && e.Clock.Samples > clocks && BrokerFrame(host, reads + 1); }, 10000),
                    "Engine 恢复后两个识别线程重新采样，WGC 成功读回且返回完整 Bitmap");
                bool cyclesSafe = true;
                for (int i = 0; i < 8; i++)
                {
                    e.SetPaused(true); e.SetPaused(true);
                    if (!Stopped(e)) cyclesSafe = false;
                    e.ConfigureAuto(host.Handle, i % 2, false, true);
                    e.SetPaused(false); e.SetPaused(false);
                    e.Configure(host.Handle, roi, 150, true);
                    Thread.Sleep(35);
                }
                e.SetPaused(true); if (!Stopped(e)) cyclesSafe = false;
                long lastBeans = e.Samples, lastClocks = e.Clock.Samples;
                Thread.Sleep(250);
                Check(cyclesSafe && e.Samples == lastBeans && e.Clock.Samples == lastClocks,
                    "重复暂停/恢复各 8 轮保持幂等，无死锁，最终采样归零");
            }
            Check(Stopped(), "Dispose 释放暂停中的捕获会话");
        }

        static void Click(Control control)
        {
            typeof(Control).GetMethod("OnClick", Private).Invoke(control, new object[] { EventArgs.Empty });
            Application.DoEvents();
        }

        static void UiChecks(TestWindowHost host, string folder)
        {
            // The settings form stays hidden; connect only our explicitly constructed HWND.
            using (MainForm form = new MainForm(false))
            {
                WindowPicker picker = (WindowPicker)typeof(MainForm).GetField("windows", Private).GetValue(form);
                Engine e = (Engine)typeof(MainForm).GetField("engine", Private).GetValue(form);
                Control run = (Control)typeof(MainForm).GetField("run", Private).GetValue(form);
                Control left = (Control)typeof(MainForm).GetField("left", Private).GetValue(form);
                picker.Items.Add(new WindowItem { Handle = host.Handle, Title = "暂停回归合成窗口" });
                picker.SelectedIndex = 0;
                Click(run);
                Check(!e.GetSnapshot().Paused && e.GetSnapshot().Enabled && run.Text == "暂停监测", "真实开始按钮 Click 接通监测");
                Click(run); bool released = Stopped(e);
                Thread.Sleep(150);
                long beans = e.Samples, clocks = e.Clock.Samples, reads = WindowCapture.Readbacks;
                Click(left); Thread.Sleep(300);
                Check(released && e.GetSnapshot().Paused && run.Text == "开始监测" && e.GetSnapshot().Side == 0 &&
                    e.Samples == beans && e.Clock.Samples == clocks && WindowCapture.Readbacks == reads,
                    "真实暂停按钮及左侧按钮 Click：保持暂停，两识别线程与捕获停止，主窗未显示");
                typeof(MainForm).GetMethod("Tick", Private).Invoke(form, new object[] { null, EventArgs.Empty });
                form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000); form.Show(); Application.DoEvents();
                typeof(MainForm).GetMethod("Tick", Private).Invoke(form, new object[] { null, EventArgs.Empty });
                typeof(MainForm).GetMethod("SaveRender", Private).Invoke(form, new object[] { Path.Combine(folder, "pause-ui.png") });
                bool offscreen = !SystemInformation.VirtualScreen.IntersectsWith(form.Bounds);
                form.Hide(); Application.DoEvents();
                Check(offscreen && !form.Visible && !form.TopMost && !form.ShowInTaskbar,
                    "暂停状态主界面在屏幕外无激活渲染并隐藏，未置顶或进入任务栏");
                Click(run);
                Check(!e.GetSnapshot().Paused && e.GetSnapshot().Enabled && run.Text == "暂停监测", "真实开始按钮可恢复暂停状态");
                Click(run); Stopped(e);
            }
        }

        sealed class TestWindowHost : IDisposable
        {
            readonly Thread thread; readonly ManualResetEvent ready = new ManualResetEvent(false);
            AnimatedWindow form; Exception error; public IntPtr Handle; public Rectangle Bounds;
            public long Ticks { get { return form == null ? 0 : form.Ticks; } }
            public long Paints { get { return form == null ? 0 : form.Paints; } }
            public TestWindowHost()
            {
                thread = new Thread(delegate()
                {
                    try
                    {
                        using (AnimatedWindow f = new AnimatedWindow())
                        {
                            form = f;
                            f.Shown += delegate { Handle = f.Handle; Bounds = Native.ClientBounds(Handle); ready.Set(); };
                            Application.Run(f);
                        }
                    }
                    catch (Exception ex) { error = ex; ready.Set(); }
                }) { IsBackground = true, Name = "Synthetic WGC pause test window" };
                thread.SetApartmentState(ApartmentState.STA); thread.Start();
                if (!ready.WaitOne(5000)) throw new TimeoutException("合成测试窗未就绪");
                if (error != null) throw new InvalidOperationException("合成测试窗创建失败", error);
            }
            public void Dispose()
            {
                if (form != null && !form.IsDisposed) form.BeginInvoke((Action)delegate { form.Close(); });
                if (!thread.Join(5000)) throw new TimeoutException("合成测试窗关闭超时");
                ready.Dispose();
            }
        }

        sealed class AnimatedWindow : Form
        {
            readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer { Interval = 25 };
            int frame; long ticks, paints;
            public long Ticks { get { return Interlocked.Read(ref ticks); } }
            public long Paints { get { return Interlocked.Read(ref paints); } }
            public AnimatedWindow()
            {
                Text = "Bean Studio 暂停回归测试（合成画面）"; ShowInTaskbar = true;
                FormBorderStyle = FormBorderStyle.FixedSingle; TopMost = false;
                ClientSize = new Size(420, 240); StartPosition = FormStartPosition.Manual;
                Rectangle area = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(area.Left + 8, area.Bottom - Height - 8);
                BackColor = Color.FromArgb(24, 36, 49); DoubleBuffered = true;
                tick.Tick += delegate { frame++; Interlocked.Increment(ref ticks); Invalidate(); }; tick.Start();
            }
            protected override bool ShowWithoutActivation { get { return true; } }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Interlocked.Increment(ref paints);
                for (int i = 0; i < 4; i++)
                {
                    int x = 38 + i * 40, y = 66;
                    Point[] diamond = { new Point(x, y - 17), new Point(x + 12, y), new Point(x, y + 17), new Point(x - 12, y) };
                    using (Brush b = new SolidBrush(Color.FromArgb(95, 215, 250))) e.Graphics.FillPolygon(b, diamond);
                }
                using (Font font = new Font("Segoe UI", 28, FontStyle.Bold))
                using (Brush b = new SolidBrush(Color.White)) e.Graphics.DrawString("49", font, b, 176, 16);
                using (Brush b = new SolidBrush(Color.FromArgb(100 + frame % 150, 110, 180)))
                    e.Graphics.FillRectangle(b, 8 + frame % 350, 160, 38, 32);
            }
            protected override void Dispose(bool disposing) { if (disposing) tick.Dispose(); base.Dispose(disposing); }
        }
    }
}

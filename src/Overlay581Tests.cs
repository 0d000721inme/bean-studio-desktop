using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MuMuBeans
{
    static class Overlay581Tests
    {
        static readonly List<string> rows = new List<string>();
        static int failures;
        static void Check(bool pass, string title) { rows.Add((pass ? "PASS " : "FAIL ") + title); if (!pass) failures++; }
        static void Save(Overlay hud, string path) { using (Bitmap image = hud.Surface()) image.Save(path, ImageFormat.Png); }
        static bool Inside(RectangleF r, Rectangle limit) { return r.Left >= 0 && r.Top >= 0 && r.Right <= limit.Width && r.Bottom <= limit.Height; }
        public static void Run(string[] args)
        {
            string folder = args[1]; Directory.CreateDirectory(folder); rows.Clear(); failures = 0;
            try
            {
                RecoveryDisplay confirmed = RecoveryDisplay.Parse("恢复点：34 秒（游戏）");
                Check(confirmed.HasValue && confirmed.Value == "34" && !confirmed.IsApproximate, "确认恢复点使用原游戏数字，不减现实秒");
                RecoveryDisplay decimals = RecoveryDisplay.Parse("恢复点：7.97 秒（游戏）");
                Check(decimals.HasValue && decimals.Value == "7.97", "恢复点小数位保持原值");
                RecoveryDisplay estimate = RecoveryDisplay.Parse("恢复点≈24.38 秒（中途补算）");
                Check(estimate.HasValue && estimate.IsApproximate && estimate.Value == "≈24.38" && estimate.Status.Contains("估算"), "中途补算保留约算符和明确状态");
                Check(!RecoveryDisplay.Parse("恢复点：时间未确认（见时间校准）").HasValue && RecoveryDisplay.Parse("恢复点：时间未确认（见时间校准）").Value == "—", "时间未确认不显示编造数字");
                Check(RecoveryDisplay.Parse("恢复点：读取中").Status == "读取中", "读取中保留专门状态");
                Check(RecoveryDisplay.Parse("本回合剩余不足 15 秒").Status == "本回合不足 15 秒" && !RecoveryDisplay.Parse("本回合剩余不足 15 秒").HasValue, "回合不足15秒不造恢复点");
                Check(RecoveryDisplay.Parse("本回合剩余不足冷却时间").Status == "本回合不足冷却时间", "中途补算不足冷却时间明确提示");
                Check(RecoveryDisplay.Parse(null).Status == "等待少豆" && RecoveryDisplay.Parse("等待少豆 · 自动读取游戏时间").Value == "—", "空值和未触发状态安全展示");
                Check(!RecoveryDisplay.Parse("恢复点：99 秒（游戏）").HasValue && !RecoveryDisplay.Parse("恢复点：NaN 秒（游戏）").HasValue, "越界或非数字恢复点不被当作确认值");
                foreach (OverlayStyle style in Enum.GetValues(typeof(OverlayStyle)))
                {
                    int selected = -1;
                    using (Overlay hud = new Overlay(delegate { }, delegate(int value) { selected = value; }))
                    {
                        DesktopSettings settings = new DesktopSettings { OverlayStyle = style, WholeSeconds = false };
                        hud.ApplySettings(settings); hud.ClockLabel.Text = "09.87"; hud.Detail.Text = "3 / 4 颗";
                        hud.Target.Text = "恢复点：34.23 秒（游戏）";
                        Check(hud.DisplayClock() == "09.87", style + " 小数默认显示不中途取整");
                        RectangleF left = hud.PrimaryValueBounds(false), right = hud.PrimaryValueBounds(true);
                        Check(left.Width == right.Width && left.Height == right.Height && left.Top == right.Top && left.Right < right.Left,
                            style + " 现实冷却与游戏恢复点使用等宽等高同字号主信息列");
                        Check(Inside(left, hud.ClientRectangle) && Inside(right, hud.ClientRectangle), style + " 双主数字区域完全在窗口内");
                        using (FontFamily family = new FontFamily("Segoe UI"))
                        using (GraphicsPath path = new GraphicsPath())
                        {
                            path.AddString("60.00", family, (int)FontStyle.Bold, hud.PrimaryValueFontSize, PointF.Empty, StringFormat.GenericDefault);
                            Check(path.GetBounds().Width <= right.Width, style + " 完整五位数恢复点能按等字号显示而不省略");
                        }
                        Save(hud, Path.Combine(folder, "paired-" + style + ".png"));
                        hud.Target.Text = "恢复点≈24.38 秒（中途补算）";
                        Save(hud, Path.Combine(folder, "estimated-" + style + ".png"));
                        hud.Target.Text = "本回合剩余不足冷却时间";
                        Save(hud, Path.Combine(folder, "insufficient-" + style + ".png"));
                        hud.Target.Text = "恢复点：时间未确认（见时间校准）";
                        Save(hud, Path.Combine(folder, "unknown-" + style + ".png"));
                        hud.Target.Text = "恢复点：34.23 秒（游戏）"; hud.NoBeans = true;
                        Check(hud.DisplayClock() == "对方已无豆" && RecoveryDisplay.Parse(hud.Target.Text).Value == "34.23", style + " 无豆提示不覆盖已保存的恢复点");
                        HudHost.UpdateHud(hud, new Snapshot { Enabled = true, ConfirmedReading = new Reading { Valid = true }, Remaining = 9.87, Recovery = "恢复点：34.23 秒（游戏）" });
                        Check(hud.NoBeans && hud.DisplayClock() == "对方已无豆" && RecoveryDisplay.Parse(hud.Target.Text).Value == "34.23", style + " HudHost真实零豆接线保留原恢复点");
                        Save(hud, Path.Combine(folder, "empty-" + style + ".png")); hud.NoBeans = false;
                        foreach (int scale in new int[] { 80, 140 })
                        {
                            settings.OverlayScale = scale; hud.ApplySettings(settings);
                            Rectangle button = hud.SideButtonBounds(0); hud.ClickPointForTest(new Point(button.Left + button.Width / 2, button.Top + button.Height / 2));
                            Check(selected == 0, style + " " + scale + "% 左右命中坐标适配新布局");
                            Save(hud, Path.Combine(folder, "scale-" + style + "-" + scale + ".png"));
                        }
                        if (style == OverlayStyle.Digits)
                            using (Bitmap bitmap = hud.Surface()) Check(bitmap.GetPixel(0, 0).A == 0, "纯数字样式没有背景表面");
                    }
                }
                using (Overlay hud = new Overlay(delegate { }))
                {
                    hud.ApplySettings(new DesktopSettings { WholeSeconds = false });
                    hud.Detail.Text = "3 / 4 颗"; hud.ClockLabel.Text = "14.99"; hud.Target.Text = "恢复点：34 秒（游戏）";
                    hud.StartPosition = FormStartPosition.Manual; hud.Location = new Point(-18000, -18000);
                    hud.Show(); Application.DoEvents(); hud.RefreshSurface();
                    int before = hud.SurfaceBuildCount;
                    for (int i = 0; i < 50; i++) { hud.Location = new Point(-18000 + i, -18000 + i); hud.RefreshSurface(); }
                    Check(hud.SurfaceBuildCount == before, "50次位置变化复用Windows表面，无Bitmap重建");
                    // Invoke actual mouse handlers, without moving the user's physical cursor.
                    typeof(Overlay).GetMethod("OnMouseDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(hud,
                        new object[] { new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0) });
                    before = hud.PresentationCount; int builds = hud.SurfaceBuildCount;
                    Stopwatch drag = Stopwatch.StartNew();
                    for (int i = 0; i < 30; i++) { hud.ClockLabel.Text = (14.98 - i * .01).ToString("F2", System.Globalization.CultureInfo.InvariantCulture); hud.RefreshSurface(); }
                    Check(drag.ElapsedMilliseconds < 100 && hud.PresentationCount == before && hud.SurfaceBuildCount == builds,
                        "拖动100ms内30次小数更新不重新分配/提交表面");
                    typeof(Overlay).GetMethod("OnMouseUp", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(hud,
                        new object[] { new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0) });
                    Check(hud.PresentationCount == before + 1 && hud.DisplayClock() == "14.69", "松手立即补交最新小数读数");
                    before = hud.PresentationCount; hud.ClockLabel.Text = "14.68"; hud.RefreshSurface();
                    Check(hud.PresentationCount == before + 1, "停止拖动后正常小数帧不受100ms节流限制");
                    typeof(Overlay).GetMethod("OnMouseDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(hud,
                        new object[] { new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0) });
                    before = hud.PresentationCount; hud.ClockLabel.Text = "14.67"; hud.RefreshSurface(); hud.Capture = false;
                    Check(hud.PresentationCount == before + 1, "鼠标Capture意外丢失立即取消拖动并补最新帧");
                    before = hud.PresentationCount; hud.ClockLabel.Text = "14.66"; hud.RefreshSurface();
                    Check(hud.PresentationCount == before + 1, "丢失Capture后不遗留永久节流");
                    hud.Hide();
                }
                Sheet(folder);
            }
            catch (Exception ex) { Check(false, "测试异常：" + ex); }
            rows.Add("PASS_COUNT=" + rows.FindAll(delegate(string row) { return row.StartsWith("PASS "); }).Count);
            rows.Add("FAILURES=" + failures);
            File.WriteAllLines(Path.Combine(folder, "overlay581-tests.txt"), rows.ToArray(), new UTF8Encoding(true));
            Environment.ExitCode = failures == 0 ? 0 : 1;
        }
        static void Sheet(string folder)
        {
            using (Bitmap sheet = new Bitmap(1240, 550))
            using (Graphics g = Graphics.FromImage(sheet))
            using (Font font = new Font("Segoe UI", 14, FontStyle.Bold))
            {
                g.Clear(Color.FromArgb(45, 59, 59)); int i = 0;
                foreach (OverlayStyle style in Enum.GetValues(typeof(OverlayStyle)))
                {
                    int x = i % 2 * 620 + 24, y = i / 2 * 275 + 18;
                    g.DrawString(style + " / 2x preview", font, Brushes.White, x, y);
                    using (Bitmap hud = new Bitmap(Path.Combine(folder, "paired-" + style + ".png"))) g.DrawImage(hud, new Rectangle(x, y + 35, hud.Width * 2, hud.Height * 2));
                    i++;
                }
                sheet.Save(Path.Combine(folder, "paired-style-sheet.png"), ImageFormat.Png);
            }
        }
    }
}

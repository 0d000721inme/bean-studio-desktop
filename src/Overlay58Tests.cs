using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace MuMuBeans
{
    static class Overlay58Tests
    {
        static readonly List<string> rows = new List<string>();
        static int failures;
        static void Check(bool pass, string title) { rows.Add((pass ? "PASS " : "FAIL ") + title); if (!pass) failures++; }
        static void Save(Overlay overlay, string path) { using (Bitmap image = overlay.Surface()) image.Save(path, ImageFormat.Png); }
        static DesktopSettings Settings(OverlayStyle style) { return new DesktopSettings { OverlayStyle = style, WholeSeconds = true }; }
        static bool RedDigits(Overlay overlay)
        {
            using (Bitmap image = overlay.Surface())
                for (int y = 27; y < Math.Min(image.Height, 60); y++)
                    for (int x = 12; x < image.Width - 12; x++)
                    {
                        Color pixel = image.GetPixel(x, y);
                        if (pixel.A > 200 && pixel.R > 235 && pixel.G < 150 && pixel.B < 150) return true;
                    }
            return false;
        }
        public static void Run(string[] args)
        {
            string folder = args[1]; Directory.CreateDirectory(folder); rows.Clear(); failures = 0;
            try
            {
                using (Overlay legacy = new Overlay(delegate { }))
                {
                    legacy.ClockLabel.Text = "12.34";
                    Check(legacy.CurrentStyle == OverlayStyle.Classic && legacy.ClientSize == new Size(268, 121) && legacy.DisplayClock() == "12.34",
                        "直接构造保持 Classic/小数显示并采用双主信息布局");
                    Save(legacy, Path.Combine(folder, "legacy-classic.png"));
                }
                Size[] sizes = { new Size(244, 105), new Size(226, 98), new Size(284, 83), new Size(268, 121) };
                int index = 0;
                foreach (OverlayStyle style in Enum.GetValues(typeof(OverlayStyle)))
                {
                    int selected = -1;
                    using (Overlay overlay = new Overlay(delegate { }, delegate(int side) { selected = side; }))
                    {
                        DesktopSettings settings = Settings(style); overlay.ApplySettings(settings);
                        overlay.ClockLabel.Text = "10.42"; overlay.Detail.Text = "3 / 4 颗"; overlay.Target.Text = "恢复点：34 秒（游戏）";
                        Check(overlay.CurrentStyle == style && overlay.ClientSize == sizes[index] && overlay.DisplayClock() == "11", style + " 默认尺寸和整秒显示");
                        Save(overlay, Path.Combine(folder, "overlay-" + style + ".png"));
                        overlay.NoBeans = true;
                        Check(overlay.DisplayClock() == "对方已无豆", style + " 零豆状态明确而不显示 00.00");
                        Save(overlay, Path.Combine(folder, "no-beans-" + style + ".png"));
                        overlay.NoBeans = false;
                        settings.ShowRecovery = false; overlay.ApplySettings(settings);
                        Check(overlay.ClientSize.Width == sizes[index].Width && overlay.ClientSize.Height < sizes[index].Height, style + " 关闭恢复点收紧高度");
                        Save(overlay, Path.Combine(folder, "compact-" + style + ".png"));
                        foreach (int scale in new int[] { 80, 140 })
                        {
                            settings.ShowRecovery = true; settings.OverlayScale = scale; overlay.ApplySettings(settings);
                            Check(overlay.ClientSize == new Size((int)Math.Round(sizes[index].Width * scale / 100.0), (int)Math.Round(sizes[index].Height * scale / 100.0)), style + " " + scale + "% 尺寸一致缩放");
                            Rectangle left = overlay.SideButtonBounds(0), right = overlay.SideButtonBounds(1);
                            overlay.ClickPointForTest(new Point(left.Left + left.Width / 2, left.Top + left.Height / 2));
                            Check(selected == 0, style + " " + scale + "% 左按钮命中与绘制一致");
                            overlay.ClickPointForTest(new Point(right.Left + right.Width / 2, right.Top + right.Height / 2));
                            Check(selected == 1, style + " " + scale + "% 右按钮命中与绘制一致");
                        }
                        Save(overlay, Path.Combine(folder, "scaled-" + style + ".png"));
                    }
                    index++;
                }

                using (Overlay overlay = new Overlay(delegate { }))
                {
                    DesktopSettings settings = Settings(OverlayStyle.Focus); overlay.ApplySettings(settings);
                    overlay.ClockLabel.Text = "14.999"; Check(overlay.DisplayClock() == "15", "整秒 Ceiling：14.999 显示 15，绝不早跳至 14");
                    overlay.ClockLabel.Text = "14.000"; Check(overlay.DisplayClock() == "14", "真实经过 1 秒才进入 14");
                    overlay.ClockLabel.Text = "00.00"; Check(overlay.DisplayClock() == "00", "到期整秒显示 00");
                    overlay.ClockLabel.Text = "5.00"; Check(RedDigits(overlay), "最后 5 秒已使用紧急红色，无闪烁动画");
                    Save(overlay, Path.Combine(folder, "urgent-focus.png"));
                    overlay.ClockLabel.Text = "6.00"; Check(!RedDigits(overlay), "超过最后 5 秒没有提前红色提示");
                    overlay.ClockLabel.Text = "10.99"; overlay.Detail.Text = "3 / 4 颗"; overlay.Target.Text = "恢复点：34 秒（游戏）";
                    overlay.StartPosition = FormStartPosition.Manual; overlay.Location = new Point(-20000, -20000);
                    int events = 0; overlay.SurfacePresented += delegate { events++; };
                    overlay.Show(); Application.DoEvents(); overlay.RefreshSurface();
                    Check(overlay.PresentationCount == 1 && events == 1, "Show 实际提交一次，Shown/VisibleChanged 不重复提交");
                    int surfaces = overlay.SurfaceBuildCount, presentations = overlay.PresentationCount;
                    for (int i = 0; i < 30; i++)
                    {
                        overlay.ClockLabel.Text = (10.99 - i * .01).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                        overlay.ClockLabel.ForeColor = Color.FromArgb(10 + i, 90 + i, 110 + i);
                        overlay.RefreshSurface();
                    }
                    Check(overlay.SurfaceBuildCount == surfaces && overlay.PresentationCount == presentations && events == presentations,
                        "整秒内原始读数/颜色 30 次变化不再分配 Bitmap/GDI/提交 layered surface");
                    overlay.ClockLabel.Text = "10.00"; overlay.RefreshSurface();
                    Check(overlay.PresentationCount == presentations + 1 && events == overlay.PresentationCount, "显示秒变化立即成功提交并发事件");
                    presentations = overlay.PresentationCount; overlay.Detail.Text = "2 / 4 颗"; overlay.RefreshSurface();
                    Check(overlay.PresentationCount == presentations + 1, "豆数文字变化及时提交");
                    presentations = overlay.PresentationCount; overlay.SetSide(0); overlay.RefreshSurface();
                    Check(overlay.PresentationCount == presentations + 1, "切换监测侧及时提交");
                    presentations = overlay.PresentationCount; overlay.Target.Text = "恢复点：29 秒（游戏）"; overlay.RefreshSurface();
                    Check(overlay.PresentationCount == presentations + 1, "恢复点文字变化及时提交");
                    presentations = overlay.PresentationCount; surfaces = overlay.SurfaceBuildCount;
                    overlay.Location = new Point(-19980, -19980); overlay.RefreshSurface();
                    Check(overlay.SurfaceBuildCount == surfaces && overlay.PresentationCount == presentations,
                        "拖动由 Windows 移动已有表面，不为位置变化重绘 Bitmap");
                    overlay.Hide(); overlay.RefreshSurface();
                    Check(overlay.PresentationCount == presentations, "隐藏窗口不提交表面");
                    overlay.Show(); Application.DoEvents(); overlay.RefreshSurface();
                    Check(overlay.PresentationCount == presentations + 1, "同内容重新显示仍正确提交一次");
                    settings.ShowRecovery = false; overlay.ApplySettings(settings); presentations = overlay.PresentationCount;
                    overlay.Target.Text = "此恢复点已隐藏"; overlay.RefreshSurface();
                    Check(overlay.PresentationCount == presentations, "关闭恢复点时其变化不触发无意义重绘");
                    settings.OverlayScale = 140; overlay.ApplySettings(settings);
                    Check(overlay.PresentationCount == presentations + 1 && overlay.ClientSize == new Size(342, 119), "可见窗口缩放立即提交正确尺寸表面");
                    overlay.Hide();
                    settings.OverlayScale = 100; settings.OverlayBackground = 0; overlay.ApplySettings(settings);
                    using (Bitmap transparent = overlay.Surface()) Check(transparent.GetPixel(0, 0).A == 0, "背景 0% 真实透明，文字和按钮保持独立透明度");
                }
                StyleSheet(folder);
            }
            catch (Exception ex) { Check(false, "浮窗测试异常：" + ex); }
            rows.Add("PASS_COUNT=" + rows.FindAll(delegate(string row) { return row.StartsWith("PASS "); }).Count);
            rows.Add("FAILURES=" + failures);
            File.WriteAllLines(Path.Combine(folder, "overlay58-tests.txt"), rows.ToArray(), new UTF8Encoding(true));
            Environment.ExitCode = failures == 0 ? 0 : 1;
        }
        static void StyleSheet(string folder)
        {
            using (Bitmap sheet = new Bitmap(1280, 640))
            using (Graphics g = Graphics.FromImage(sheet))
            using (Font heading = new Font("Segoe UI", 14, FontStyle.Bold))
            {
                g.Clear(Color.FromArgb(47, 62, 64));
                using (Pen grid = new Pen(Color.FromArgb(25, 230, 244, 235)))
                    for (int x = 0; x < sheet.Width; x += 40) g.DrawLine(grid, x, 0, x, sheet.Height);
                int i = 0;
                foreach (OverlayStyle style in Enum.GetValues(typeof(OverlayStyle)))
                {
                    int x = i % 2 * 640 + 30, y = i / 2 * 320 + 20;
                    g.DrawString(style + " / 100% / x2 preview", heading, Brushes.White, x, y);
                    using (Bitmap hud = new Bitmap(Path.Combine(folder, "overlay-" + style + ".png")))
                        g.DrawImage(hud, new Rectangle(x, y + 40, hud.Width * 2, hud.Height * 2));
                    i++;
                }
                sheet.Save(Path.Combine(folder, "overlay-style-sheet.png"), ImageFormat.Png);
            }
        }
    }
}

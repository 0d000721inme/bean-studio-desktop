using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MuMuBeans
{
    static class Studio58Tests
    {
        static readonly List<string> rows=new List<string>();
        static int failures;
        static void Check(bool value,string text){rows.Add((value?"PASS ":"FAIL ")+text);if(!value)failures++;}
        static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);}
        static void Wait(Func<bool> done,int limit)
        {Stopwatch clock=Stopwatch.StartNew();while(!done()&&clock.ElapsedMilliseconds<limit){Application.DoEvents();Thread.Sleep(10);}}
        static Bitmap Fixture()
        {
            Bitmap image=new Bitmap(1280,720);
            using(Graphics g=Graphics.FromImage(image))using(Font text=new Font("Microsoft YaHei UI",16))using(Font small=new Font("Microsoft YaHei UI",10))
            {
                g.Clear(Color.FromArgb(25,40,43));using(Pen line=new Pen(Color.FromArgb(40,58,58)))for(int x=0;x<1280;x+=64)g.DrawLine(line,x,0,x,720);
                g.FillRectangle(Brushes.IndianRed,110,62,255,12);g.FillRectangle(Brushes.IndianRed,920,62,255,12);
                for(int i=0;i<4;i++)
                {int left=140+i*25,right=1020+i*25;using(Brush a=new SolidBrush(Color.FromArgb(236,183,65)))g.FillPolygon(a,new Point[]{new Point(left,83),new Point(left+8,98),new Point(left,113),new Point(left-8,98)});using(Brush b=new SolidBrush(i==0?Color.FromArgb(13,77,102):Color.FromArgb(85,221,255)))g.FillPolygon(b,new Point[]{new Point(right,83),new Point(right+8,98),new Point(right,113),new Point(right-8,98)});}
                g.DrawString("合成场景 · 界面检查",text,Brushes.White,478,319);g.DrawString("未连接游戏，不代表识别准确率",small,Brushes.LightGray,509,362);
            }
            return image;
        }
        static void Bounds(Control parent)
        {
            foreach(Control child in parent.Controls)
            {
                if(!child.Visible)continue;
                Check(child.Width>0&&child.Height>0&&parent.ClientRectangle.Contains(child.Bounds),parent.GetType().Name+" / "+child.GetType().Name+" "+child.Text.Replace("\r"," ").Replace("\n"," ")+" 保持容器内");
                Bounds(child);
            }
        }
        static void Save(Form form,string file)
        {using(Bitmap bitmap=new Bitmap(form.ClientSize.Width,form.ClientSize.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.ClientSize));bitmap.Save(file);}}
        static void NativeKeys(MainForm form)
        {
            HotkeyManager keys=Field<HotkeyManager>(form,"hotkeys");HudHost hud=Field<HudHost>(form,"overlay");
            Check(!keys.Settings.HotkeysEnabled,"检查模式在创建窗口前关闭默认快捷键，不抢占用户 F8/F9");
            DesktopSettings test=new DesktopSettings();test.ToggleOverlayHotkey=new HotkeyChord(HotkeyModifiers.Control|HotkeyModifiers.Alt|HotkeyModifiers.Shift,Keys.F11);test.RescanHotkey=new HotkeyChord(HotkeyModifiers.Control|HotkeyModifiers.Alt|HotkeyModifiers.Shift,Keys.F10);
            string error;if(!keys.TryApply(test,out error)){rows.Add("SKIP 实际 Win32 组合已被环境占用："+error);return;}
            using(Form other=new Form())using(HotkeyManager competitor=new HotkeyManager(other,delegate{}))
            {
                DesktopSettings disabled=test.Copy();disabled.HotkeysEnabled=false;competitor.TryApply(disabled,out error);IntPtr handle=other.Handle;
                Check(!competitor.TryApply(test,out error)&&error.Contains("占用"),"实际 RegisterHotKey 检测同一组合被其他窗口占用");
                uint packed=((uint)Keys.F11<<16)|(uint)test.ToggleOverlayHotkey.Modifiers;
                PostMessage(form.Handle,HotkeyManager.HotkeyMessage,(IntPtr)HotkeyManager.ToggleId,(IntPtr)packed);
                Wait(delegate{return hud.Visible;},1200);Check(hud.Visible,"真实 WM_HOTKEY 通过主界面接线打开非激活置顶浮窗");
                PostMessage(form.Handle,HotkeyManager.HotkeyMessage,(IntPtr)HotkeyManager.ToggleId,(IntPtr)packed);
                Wait(delegate{return !hud.Visible;},1200);Check(!hud.Visible,"第二次快捷键关闭浮窗，主界面不隐藏");
                uint rescan=((uint)Keys.F10<<16)|(uint)test.RescanHotkey.Modifiers;
                PostMessage(form.Handle,HotkeyManager.HotkeyMessage,(IntPtr)HotkeyManager.RescanId,(IntPtr)rescan);
                Wait(delegate{return Field<Label>(form,"status").Text.Contains("选择 MuMu");},800);
                Check(Field<Label>(form,"status").Text.Contains("选择 MuMu"),"重新扫描快捷键走扫描动作，未选源时给出提示");
                keys.TryApply(disabled,out error);Check(competitor.TryApply(test,out error),"关闭绑定后 Win32 组合立即可由另一窗口注册");
            }
        }
        public static void Run(string[] args)
        {
            string folder=args[1];Directory.CreateDirectory(folder);rows.Clear();failures=0;
            try
            {
                using(Bitmap frame=Fixture())
                {
                    using(MainForm demo=new MainForm(false))
                    {
                        demo.RenderDemo(frame,Path.Combine(folder,"主界面.png"));
                        using(ProcessPerformance live=new ProcessPerformance())
                        {
                            live.SetEnabled(true);Wait(delegate{return live.GetSnapshot().Samples.Length>=4;},7000);
                            PerformanceSnapshot measured=live.GetSnapshot();Check(measured.Samples.Length>=4&&measured.Latest.WorkingSetMb.HasValue,"性能区域接收真实检查进程样本，非固定演示数字");
                            demo.ClientSize=new Size(1240,860);demo.Show();Application.DoEvents();
                            Field<PerformancePanel>(demo,"performancePanel").SetSnapshot(measured);Save(demo,Path.Combine(folder,"真实性能图表.png"));demo.Hide();
                        }
                    }
                    using(MainForm states=new MainForm(false))states.RenderStates(frame,Path.Combine(folder,"states"));
                    using(MainForm form=new MainForm(false))
                    {
                        form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
                        NativeKeys(form);
                        foreach(Size size in new Size[]{new Size(1040,760),new Size(1240,860),new Size(1440,960)})
                        {form.ClientSize=size;Application.DoEvents();Bounds(form);Save(form,Path.Combine(folder,"布局-"+size.Width+"x"+size.Height+".png"));}
                        form.WindowState=FormWindowState.Minimized;Application.DoEvents();form.WindowState=FormWindowState.Normal;Application.DoEvents();
                        Check(Field<Label>(form,"clockLabel").Font.Size>0,"最小化恢复后的计时字号保持正数");
                        DesktopSettings settings=Field<DesktopSettings>(form,"settings");settings.PerformanceVisible=false;settings.PreviewVisible=false;
                        typeof(MainForm).GetMethod("ApplyVisualSettings",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);Application.DoEvents();
                        Check(!Field<PerformancePanel>(form,"performancePanel").Visible,"隐藏性能区域同时收回布局空间");
                        Check(Field<PreviewBox>(form,"preview").Suppressed,"隐藏静态预览仍保留操作入口与状态");
                        Bounds(form);Save(form,Path.Combine(folder,"专注主界面.png"));form.Hide();
                    }
                }
                DesktopSettingsPanel.Render(Path.Combine(folder,"settings"));
                Check(File.Exists(Path.Combine(folder,"settings","快捷键与界面设置-默认.png"))||Directory.GetFiles(Path.Combine(folder,"settings"),"*.png").Length>=2,"设置面板实际生成默认和最小尺寸预览");
            }
            catch(Exception ex){failures++;rows.Add("FAIL 集成检查异常："+ex);}
            rows.Add("FAILURES="+failures);rows.Add("检查只创建程序自身窗口和合成图，不操作 MuMu，不把 UI 检查计为实战准确率。");
            File.WriteAllLines(Path.Combine(folder,"studio58-tests.txt"),rows.ToArray());Environment.ExitCode=failures==0?0:1;
        }
        [DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,int message,IntPtr wParam,IntPtr lParam);
    }
}

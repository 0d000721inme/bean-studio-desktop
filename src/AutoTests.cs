using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
namespace MuMuBeans
{
    static class AutoTests
    {
        static List<string> report=new List<string>();static int failed;
        static void Check(bool ok,string label){report.Add((ok?"PASS ":"FAIL ")+label);if(!ok)failed++;}
        public static void Run(string[] args)
        {
            string folder=args[1];Directory.CreateDirectory(folder);
            for(int n=2;n<args.Length;n++)using(Bitmap image=new Bitmap(args[n]))
            {
                foreach(double scale in new double[]{.5,.75,1.0})using(Bitmap b=new Bitmap((int)(image.Width*scale),(int)(image.Height*scale)))
                {
                    using(Graphics g=Graphics.FromImage(b)){g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.DrawImage(image,new Rectangle(Point.Empty,b.Size));}
                    Stopwatch time=Stopwatch.StartNew();LocatedPair p=AutoLocator.Find(b);time.Stop();int expected=n==3?4:3;
                    Check(p.LeftReading!=null&&p.LeftReading.Count==4,"参考 "+(n-1)+" 缩放 "+scale+" 左侧4颗");
                    Check(p.RightReading!=null&&p.RightReading.Count==expected,"参考 "+(n-1)+" 缩放 "+scale+" 右侧"+expected+"颗 / "+time.ElapsedMilliseconds+" ms / "+p.Right);
                }
                if(n==2)
                {
                    LocatedPair initial=AutoLocator.Find(image);
                    for(int mask=0;mask<16;mask++)using(Bitmap b=(Bitmap)image.Clone())
                    {
                        using(Graphics g=Graphics.FromImage(b))for(int i=0;i<4;i++)g.DrawImage(image,new Rectangle(1576+i*30,98,30,38),new Rectangle((mask&(1<<i))!=0?1607:1576,98,30,38),GraphicsUnit.Pixel);
                        int expected=0;for(int i=0;i<4;i++)if((mask&(1<<i))!=0)expected++;
                        Reading fixedReading=Detector.Analyze(b,initial.Right,150);Check(fixedReading.Valid&&fixedReading.Count==expected,"锁定后组合 "+mask+" = "+expected+" / valid="+fixedReading.Valid+" count="+fixedReading.Count);
                        LocatedPair p=AutoLocator.Find(b);Check(p.RightReading!=null&&p.RightReading.Count==expected,"首次定位组合 "+mask+" = "+expected+" / "+p.Right);
                    }
                    using(Bitmap blocked=(Bitmap)image.Clone())
                    {using(Graphics g=Graphics.FromImage(blocked))g.FillRectangle(Brushes.Black,1550,80,165,75);Check(AutoLocator.Find(blocked).RightReading==null,"右侧遮挡不猜成零颗");}
                    using(Bitmap onlyRight=(Bitmap)image.Clone())
                    {using(Graphics g=Graphics.FromImage(onlyRight))g.FillRectangle(Brushes.Black,0,0,600,250);LocatedPair p=AutoLocator.Find(onlyRight);Check(p.LeftReading==null&&p.RightReading!=null&&p.RightReading.Count==3,"左侧遮挡仍能独立定位右侧");}
                }
                if(n==5)
                {
                    LocatedPair originalPair=AutoLocator.Find(image);
                    Reading oldCrop=Detector.Analyze(image,new Rectangle(1659,156,131,41),150);
                    Check(!oldCrop.Valid||oldCrop.Count==3,"用户新图：偏移识别框不再把三颗亮豆误判成0/2颗");
                    for(int mask=0;mask<16;mask++)using(Bitmap b=(Bitmap)image.Clone())
                    {
                        using(Graphics g=Graphics.FromImage(b))for(int i=0;i<4;i++)g.DrawImage(image,new Rectangle(1672+i*32,148,32,40),new Rectangle((mask&(1<<i))!=0?1705:1672,148,32,40),GraphicsUnit.Pixel);
                        int expected=0;for(int i=0;i<4;i++)if((mask&(1<<i))!=0)expected++;
                        Reading fixedRead=Detector.Analyze(b,originalPair.Right,150);Check(fixedRead.Valid&&fixedRead.Count==expected,"新图锁定后组合 "+mask+" = "+expected+" / valid="+fixedRead.Valid+" count="+fixedRead.Count);
                        LocatedPair pair=AutoLocator.Find(b);Check(pair.RightReading!=null&&pair.RightReading.Count==expected,"新图自动定位组合 "+mask+" = "+expected+" / "+pair.Right);
                    }
                }
            }
            foreach(Color color in new Color[]{Color.Black,Color.FromArgb(70,150,210),Color.FromArgb(220,180,25)})using(Bitmap b=new Bitmap(1024,600))
            {using(Graphics g=Graphics.FromImage(b))g.Clear(color);LocatedPair p=AutoLocator.Find(b);Check(p.LeftReading==null&&p.RightReading==null,"纯色画面无定位 "+color);}
            JitterAndRound();
            if(Environment.GetEnvironmentVariable("BEAN_SCREEN_TEST")=="1"){MenuLifetime();Screen(args[2]);}
            report.Add("通过 "+(report.Count-failed)+" 项，失败 "+failed+" 项。合成变化不等同于真实对局。");File.WriteAllLines(Path.Combine(folder,"自动定位验证.txt"),report.ToArray());if(failed>0)Environment.ExitCode=1;
        }
        sealed class Fixture:Form
        {
            public Bitmap Frame;
            public Fixture(){FormBorderStyle=FormBorderStyle.None;ClientSize=new Size(1024,577);Location=new Point(30,30);StartPosition=FormStartPosition.Manual;TopMost=true;DoubleBuffered=true;Text="V4 自动定位验证窗口";}
            protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Frame!=null)e.Graphics.DrawImage(Frame,ClientRectangle);}
        }
        static void Confirm(Counter counter,int count,double at,double duration)
        {for(int i=0;i<=(int)Math.Round(duration/.02);i++)counter.Observe(count,at+i*.02);}
        static void JitterAndRound()
        {
            Counter c=new Counter();Confirm(c,4,0,.04);Confirm(c,3,1,.04);double deadline=c.Deadline;
            Check(c.Triggers==1&&Math.Abs(c.Remaining(3.04)-13)<.000001,"跳秒回归：初次减少后正常走到13秒");
            Confirm(c,4,3.10,.08);Confirm(c,3,3.20,.04);
            Check(c.Triggers==1&&c.Deadline==deadline&&c.Remaining(3.24)<13,"模拟80ms假增长3→4→3：不再跳回15/14秒");
            Confirm(c,4,4,.12);Check(c.Baseline==4&&c.Triggers==1,"真实持续增长120ms更新基准，不重置计时");
            Confirm(c,3,4.2,.04);Check(c.Triggers==2&&Math.Abs(c.Deadline-19.24)<.000001,"真实增长后减少：仍重置完整15秒");
            deadline=c.Deadline;c.Observe(null,5);c.Observe(null,5.1);Confirm(c,3,5.12,.04);Check(c.Deadline==deadline,"100ms识别闪烁恢复：不误作废计时");
            c.Observe(null,6);c.Observe(null,6.17);Check(c.Remaining(6.17)>0,"持续缺失不足180ms：保留原倒计时");
            c.Observe(null,6.18);Check(c.Deadline==deadline&&c.Remaining(6.18)>0,"技能黑屏180ms：保留原截止时间");
            c.Observe(null,6.9);Confirm(c,4,7,.04);Check(c.Deadline==deadline&&c.Baseline==4&&c.Triggers==2,"长黑屏恢复：建立基准，原计时继续");
            Confirm(c,3,8,.04);Check(c.Triggers==3&&Math.Abs(c.Remaining(8.04)-15)<.000001,"下一局第一次减少：正常启动15秒");
            Confirm(c,0,8.2,.04);Check(c.Baseline==0&&c.Remaining(8.24)>14.99,"四颗暗豆的0颗状态仍有效，不等同于区域消失");
        }
        static Bitmap Make(Bitmap original,int rightCount,int leftCount)
        {
            Bitmap b=(Bitmap)original.Clone();using(Graphics g=Graphics.FromImage(b))
            for(int i=0;i<4;i++)
            {
                g.DrawImage(original,new Rectangle(1576+i*30,98,30,38),new Rectangle(i<4-rightCount?1576:1607,98,30,38),GraphicsUnit.Pixel);
                if(leftCount!=4)g.DrawImage(original,new Rectangle(180+i*30,94,30,38),new Rectangle(i<4-leftCount?1576:1607,98,30,38),GraphicsUnit.Pixel);
            }return b;
        }
        static bool Wait(Func<bool> condition,int ms)
        {Stopwatch watch=Stopwatch.StartNew();while(!condition()&&watch.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(3);}return condition();}
        static void MenuLifetime()
        {
            using(Form owner=new Form{FormBorderStyle=FormBorderStyle.None,ClientSize=new Size(330,100),Location=new Point(-20000,-20000),StartPosition=FormStartPosition.Manual})
            {
                WindowPicker picker=new WindowPicker{Bounds=new Rectangle(10,10,300,42)};picker.Items.Add(new WindowItem{Title="测试窗口 A"});picker.Items.Add(new WindowItem{Title="测试窗口 B"});owner.Controls.Add(picker);picker.SelectedIndexChanged+=delegate{owner.Hide();};
                bool alive=true;for(int i=0;i<60;i++){owner.Show();picker.OpenMenu();((ToolStripMenuItem)picker.Popup.Items[i%2]).PerformClick();picker.Popup.Close(ToolStripDropDownCloseReason.ItemClicked);Application.DoEvents();alive&=!picker.Popup.IsDisposed&&picker.SelectedIndex==i%2;}
                Check(alive,"菜单反复打开/选择/关闭60次，包含选择时隐藏父窗体：不提前释放 ContextMenuStrip");picker.Dispose();Check(picker.Popup.IsDisposed,"控件最终销毁时才释放菜单资源");owner.Hide();
            }
        }
        static void Screen(string reference)
        {
            using(Bitmap original=new Bitmap(reference))using(Bitmap four=Make(original,4,4))using(Bitmap three=Make(original,3,4))using(Bitmap two=Make(original,2,4))using(Bitmap leftThree=Make(original,2,3))
            using(Fixture form=new Fixture())using(Engine engine=new Engine())using(HudHost hud=new HudHost(engine,delegate{}))
            {
                form.Frame=four;form.Show();form.Refresh();Application.DoEvents();hud.ShowAt(new Point(1080,40));engine.ConfigureAuto(form.Handle,1,true);
                bool locked=Wait(delegate{return engine.GetSnapshot().Count==4;},3000);Check(locked,"真实屏幕：自动锁定右侧4颗 / "+engine.GetSnapshot().Status);if(!locked)return;
                for(int i=0;i<6;i++)
                {
                    form.Frame=four;form.Refresh();Check(Wait(delegate{return engine.GetSnapshot().Count==4;},1000),"自动屏幕 "+i+" 增长更新基准");int before=engine.GetSnapshot().Triggers;
                    long changed=Stopwatch.GetTimestamp();form.Frame=i%2==0?three:two;form.Refresh();if(i==3)Thread.Sleep(300);
                    bool triggered=Wait(delegate{return engine.GetSnapshot().Triggers>before;},1000);Snapshot s=engine.GetSnapshot();Check(triggered&&s.Triggers==before+1,"自动屏幕 "+i+" 减少重置一次");
                    double ms=(s.TriggerStamp-changed)*1000.0/Stopwatch.Frequency;Check(triggered&&ms<=200,"自动定位后屏幕变化 → 触发 "+ms.ToString("F2")+" ms ≤200 ms");
                    bool drawn=Wait(delegate{return hud.PaintedTrigger>=s.Triggers;},1000);double display=(hud.PaintedStamp-changed)*1000.0/Stopwatch.Frequency;Check(drawn&&display<=200,"悬浮文字更新 "+display.ToString("F2")+" ms ≤200 ms"+(i==3?"（主 UI 阻塞300 ms）":""));
                }
                int triggers=engine.GetSnapshot().Triggers;engine.ConfigureAuto(form.Handle,0,true);Check(Wait(delegate{return engine.GetSnapshot().Count==4;},3000),"切到左侧自动建立4豆基准");Check(engine.GetSnapshot().Triggers==triggers,"切侧不触发倒计时");
                form.Frame=leftThree;form.Refresh();Check(Wait(delegate{return engine.GetSnapshot().Triggers==triggers+1;},1000),"左侧4→3触发倒计时");triggers=engine.GetSnapshot().Triggers;
                form.ClientSize=new Size(900,507);form.Refresh();Check(Wait(delegate{return engine.GetSnapshot().CaptureSize.Width==900&&engine.GetSnapshot().Count==3;},3000),"窗口缩放后自动重新定位");Check(engine.GetSnapshot().Triggers==triggers,"缩放不误触发");
                using(Form cover=new Form{FormBorderStyle=FormBorderStyle.None,BackColor=Color.White,TopMost=true,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(form.Left,form.Top,450,130)})
                {cover.Show();cover.Refresh();Wait(delegate{return engine.GetSnapshot().Reading==null;},1500);Check(engine.GetSnapshot().Reading==null&&engine.GetSnapshot().Triggers==triggers,"豆子被遮挡时停止判定，不猜成零颗");Check(engine.GetSnapshot().Remaining>0,"遮挡期间：原倒计时继续");cover.Hide();}
                Check(Wait(delegate{return engine.GetSnapshot().Count==3;},3000),"区域重新出现：重新建立基准");Check(engine.GetSnapshot().Remaining>0&&engine.GetSnapshot().Triggers==triggers,"恢复画面不重置现有计时");
                hud.Hide();form.Hide();
            }
        }
        public static void RunLatest(string source,string folder)
        {
            Directory.CreateDirectory(folder);
            using(Bitmap three=new Bitmap(source))using(Bitmap two=(Bitmap)three.Clone())using(Bitmap absent=new Bitmap(three.Width,three.Height))using(Fixture form=new Fixture())using(Engine engine=new Engine())
            {
                using(Graphics g=Graphics.FromImage(two))for(int i=0;i<4;i++)g.DrawImage(three,new Rectangle(1672+i*32,148,32,40),new Rectangle(i<2?1672:1705,148,32,40),GraphicsUnit.Pixel);
                using(Graphics g=Graphics.FromImage(absent))g.Clear(Color.Black);
                form.ClientSize=new Size(1025,596);form.Frame=three;form.Show();form.Refresh();Application.DoEvents();engine.ConfigureAuto(form.Handle,1,true);
                bool detected=Wait(delegate{return engine.GetSnapshot().Count==3;},3000);Check(detected,"用户新图真实屏幕采集：三颗稳定识别为3颗 / "+engine.GetSnapshot().Status);
                if(!detected)using(Bitmap captured=Native.Capture(Native.ClientBounds(form.Handle))){captured.Save(Path.Combine(folder,"定位失败样本.png"));LocatedPair pair=AutoLocator.Find(captured);report.Add("DEBUG L="+pair.Left+" R="+pair.Right);}
                if(detected)
                {
                    Stopwatch hold=Stopwatch.StartNew();bool stable=true;while(hold.ElapsedMilliseconds<600){Application.DoEvents();Snapshot s=engine.GetSnapshot();stable&=s.Count==3&&s.Triggers==0;Thread.Sleep(5);}Check(stable,"新图静止600ms：豆数不抖到2颗，不产生误触发");
                    using(Bitmap roi=engine.GetFrame())if(roi!=null)using(Bitmap proof=new Bitmap(400,155))
                    {using(Graphics g=Graphics.FromImage(proof)){g.Clear(Color.FromArgb(239,243,250));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.DrawImage(roi,new Rectangle(20,15,360,105));using(Font font=new Font("Microsoft YaHei UI",11,FontStyle.Bold))g.DrawString("修正后：1 颗暗豆 + 3 颗亮豆 = 3 / 4",font,Brushes.DarkSlateBlue,13,127);}proof.Save(Path.Combine(folder,"新图识别修正.png"));}
                    form.ClientSize=three.Size;form.Refresh();Check(Wait(delegate{return engine.GetSnapshot().CaptureSize==three.Size&&engine.GetSnapshot().Count==3;},3000),"用户新图原始尺寸：重新定位后仍为3颗");
                    form.Frame=two;long changed=Stopwatch.GetTimestamp();form.Refresh();Check(Wait(delegate{return engine.GetSnapshot().Triggers==1;},1500),"新图3→2：启动15秒");Snapshot drop=engine.GetSnapshot();report.Add("MEASURE 新图减少确认 "+((drop.TriggerStamp-changed)*1000.0/Stopwatch.Frequency).ToString("F2")+" ms");
                    form.Frame=absent;form.Refresh();Check(Wait(delegate{return engine.GetSnapshot().Reading==null||!engine.GetSnapshot().Reading.Valid;},1500),"技能黑屏：暂停识别");Check(engine.GetSnapshot().Remaining>0&&engine.GetSnapshot().Triggers==1,"无豆子场景继续原计时，不清零、不重置");
                    form.Frame=three;form.Refresh();Check(Wait(delegate{return engine.GetSnapshot().Count==3;},3000),"新局三颗出现：恢复识别基准");Check(engine.GetSnapshot().Remaining>0&&engine.GetSnapshot().Triggers==1,"画面恢复后不重新触发计时");
                    form.Frame=two;form.Refresh();Check(Wait(delegate{return engine.GetSnapshot().Triggers==2;},1500),"新局再次少豆：重新启动倒计时");
                }form.Hide();
            }
            File.WriteAllLines(Path.Combine(folder,"新图实际屏幕验证.txt"),report.ToArray());Environment.ExitCode=failed==0?0:1;
        }
    }
}

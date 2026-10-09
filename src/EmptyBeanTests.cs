using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace MuMuBeans
{
    static class EmptyBeanTests
    {
        static readonly List<string> rows=new List<string>();
        static int failures;
        static void Check(bool pass,string title){rows.Add((pass?"PASS ":"FAIL ")+title);if(!pass)failures++;}
        static Reading Count(int count){Reading r=new Reading();for(int i=4-count;i<4;i++)r.Lit[i]=true;return r;}
        static Snapshot Confirmed(Reading r){return new Snapshot{Enabled=true,Reading=r,ConfirmedReading=r,Count=r.Count,Remaining=8,Recovery="恢复点：测试",Side=0};}
        static void Stable(Counter c,int n,double start){c.Observe(n,start);c.Observe(n,start+.02);c.Observe(n,start+.04);}

        public static void Run(string[] args)
        {
            string folder=args[2];Directory.CreateDirectory(folder);rows.Clear();failures=0;
            try
            {
                using(Bitmap original=new Bitmap(args[1]))
                {
                    foreach(double scale in new double[]{.5,.75,1,1.25,1.5})
                    using(Bitmap frame=new Bitmap((int)Math.Round(original.Width*scale),(int)Math.Round(original.Height*scale)))
                    {
                        using(Graphics g=Graphics.FromImage(frame)){g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.DrawImage(original,new Rectangle(Point.Empty,frame.Size));}
                        LocatedPair pair=AutoLocator.Find(frame);
                        Check(pair.LeftReading!=null&&pair.LeftReading.Valid&&pair.LeftReading.Count==0,"四空豆自动定位，scale="+scale.ToString(CultureInfo.InvariantCulture));
                        Check(pair.RightReading!=null&&pair.RightReading.Valid&&pair.RightReading.Count==4,"同图右侧四亮豆，scale="+scale.ToString(CultureInfo.InvariantCulture));
                        Rectangle roi=pair.Left;
                        if(roi.IsEmpty)continue;
                        Rectangle truncated=new Rectangle((int)(frame.Width*(roi.X/(float)frame.Width)),(int)(frame.Height*(roi.Y/(float)frame.Height)),(int)(frame.Width*(roi.Width/(float)frame.Width)),(int)(frame.Height*(roi.Height/(float)frame.Height)));
                        Reading read=Detector.Analyze(frame,truncated,150);
                        Check(read.Valid&&read.Count==0,"实际归一化裁剪确认0颗，scale="+scale.ToString(CultureInfo.InvariantCulture));
                        for(int i=-1;i<4;i++)using(Bitmap blocked=(Bitmap)frame.Clone())
                        {
                            Rectangle cover=i<0?roi:new Rectangle(roi.X+roi.Width*i/4,roi.Y,roi.Width*(i+1)/4-roi.Width*i/4,roi.Height);
                            using(Graphics g=Graphics.FromImage(blocked))g.FillRectangle(Brushes.Black,cover);
                            Check(!Detector.Analyze(blocked,roi,150).Valid,"整框/单格遮挡不猜0，scale="+scale+",slot="+i);
                            Check(AutoLocator.Find(blocked).LeftReading==null,"遮挡不接受新位置，scale="+scale+",slot="+i);
                        }
                    }
                    using(Bitmap mirrored=(Bitmap)original.Clone())
                    {
                        mirrored.RotateFlip(RotateFlipType.RotateNoneFlipX);LocatedPair pair=AutoLocator.Find(mirrored);
                        Check(pair.RightReading!=null&&pair.RightReading.Valid&&pair.RightReading.Count==0,"右侧镜像四空豆同样确认0颗");
                    }
                }

                Reading zero=Count(0),one=Count(1);
                Check(Confirmed(zero).NoBeans,"监测时确认0颗进入对方已无豆状态");
                Check(!new Snapshot{Enabled=true,Reading=zero}.NoBeans,"原始零读数尚未连续确认不能显示已无豆");
                Check(!Confirmed(new Reading{Valid=false}).NoBeans&&!new Snapshot{Enabled=true}.NoBeans,"Invalid/无画面与确认零豆区分");
                Check(!Confirmed(one).NoBeans,"恢复一豆自动退出无豆状态");
                Snapshot paused=Confirmed(zero);paused.Paused=true;Check(!paused.NoBeans,"暂停不继续宣称已确认无豆");
                Counter counter=new Counter();Stable(counter,0,0);Check(counter.Baseline==0&&counter.Triggers==0,"首次四空豆只建立0基准");
                counter.Observe(1,1);counter.Observe(1,1.06);counter.Observe(1,1.12);Check(counter.Baseline==1&&counter.Triggers==0,"零豆恢复一豆不启动倒计时");
                Stable(counter,0,2);double deadline=counter.Deadline;Check(counter.Triggers==1&&Math.Abs(deadline-17.04)<.000001,"最后一豆消耗仍按原规则设置15秒截止时间");
                for(int i=0;i<100;i++)counter.Observe(0,2.1+i*.02);Check(counter.Triggers==1&&counter.Deadline==deadline,"持续0颗不重复触发或跳回15秒");
                counter.Observe(null,5);counter.Observe(null,6);Check(counter.Deadline==deadline&&!counter.Baseline.HasValue,"黑屏不清原截止时间，且失效后不冒充零豆");

                using(Overlay hud=new Overlay(delegate{}))
                {
                    HudHost.UpdateHud(hud,Confirmed(zero));Check(hud.NoBeans&&hud.Detail.Text=="0 / 4 颗"&&hud.Target.Text.Contains("等待豆子恢复"),"生产HUD接线显示确认0颗与无豆提示");
                    using(Bitmap image=hud.Surface())image.Save(Path.Combine(folder,"实际状态四空豆浮窗.png"));
                    HudHost.UpdateHud(hud,Confirmed(one));Check(!hud.NoBeans&&hud.ClockLabel.Text=="08.00"&&hud.Detail.Text=="1 / 4 颗","恢复一豆后HUD还原数字计时");
                    HudHost.UpdateHud(hud,new Snapshot{Enabled=true,Remaining=7,Recovery="未知"});Check(!hud.NoBeans&&hud.Detail.Text=="画面待确认","真正未确认保留原未知提示");
                }
                using(MainForm form=new MainForm(false))
                {
                    const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    Label label=(Label)typeof(MainForm).GetField("clockLabel",flags).GetValue(form);
                    Label state=(Label)typeof(MainForm).GetField("timerState",flags).GetValue(form);
                    form.SetCountdownDisplay(true,8);Font font=label.Font;
                    Check(label.Text=="对方已无豆"&&state.Text.Contains("0 / 4 颗")&&font.Size>0&&font.Size<=26,"主界面无豆文字与适配字号");
                    for(int i=0;i<60;i++)form.SetCountdownDisplay(true,7);
                    Check(object.ReferenceEquals(font,label.Font),"同一无豆状态连续刷新不反复分配字体");
                    form.ClientSize=new Size(1440,960);form.SetCountdownDisplay(false,8);
                    Check(label.Text=="08.00"&&label.Font.Style==FontStyle.Regular&&label.Font.Size>26,"退出无豆状态恢复数字字体和字号");
                }
            }
            catch(Exception ex){Check(false,ex.ToString());}
            rows.Add("PASS_COUNT="+rows.FindAll(s=>s.StartsWith("PASS ")).Count);rows.Add("FAILURES="+failures);
            File.WriteAllLines(Path.Combine(folder,"四空豆专项验证.txt"),rows.ToArray(),new UTF8Encoding(true));Environment.ExitCode=failures==0?0:1;
        }
    }
}

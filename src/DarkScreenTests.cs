using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Diagnostics;using System.IO;using System.Threading;using System.Collections.Generic;using System.Windows.Forms;
namespace MuMuBeans {static class DarkScreenTests {
 sealed class Fixture:Form {public Bitmap Frame;public Fixture(){FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Location=new Point(50,500);ClientSize=new Size(930,523);TopMost=true;DoubleBuffered=true;}protected override void OnPaint(PaintEventArgs e){e.Graphics.InterpolationMode=InterpolationMode.HighQualityBilinear;e.Graphics.DrawImage(Frame,ClientRectangle);}}
 static bool Wait(Func<bool> condition,int ms){Stopwatch t=Stopwatch.StartNew();while(t.ElapsedMilliseconds<ms){Application.DoEvents();if(condition())return true;Thread.Sleep(5);}return false;}
 static void Assert(bool b,string text,List<string> rows){if(!b)throw new Exception(text);rows.Add("PASS "+text);}
 public static void Run(string[] args){List<string> rows=new List<string>();try{using(Bitmap zero=new Bitmap(args[1]))using(Bitmap blue=new Bitmap(args[2]))using(Bitmap one=(Bitmap)zero.Clone())using(Bitmap black=new Bitmap(zero.Width,zero.Height))using(Fixture f=new Fixture()){
 using(Graphics g=Graphics.FromImage(one))g.DrawImage(blue,new Rectangle(183,101,30,38),new Rectangle(1607,98,30,38),GraphicsUnit.Pixel);
 f.Frame=zero;f.Show();f.Refresh();Application.DoEvents();Native.SetForegroundWindow(f.Handle);Rectangle client=Native.ClientBounds(f.Handle);
 if(!Wait(()=>Native.Uncovered(f.Handle,client),1500)){rows.Add("NOT_RUN 测试窗口不可见或被遮挡，未采集其他窗口");Environment.ExitCode=2;return;}
 try{using(Bitmap check=Native.Capture(client)){if(check==null){rows.Add("NOT_RUN 当前运行环境无法进行屏幕采集");Environment.ExitCode=2;return;}}}catch(System.ComponentModel.Win32Exception ex){rows.Add("NOT_RUN 桌面采集不可用："+ex.Message);Environment.ExitCode=2;return;}
 using(Engine engine=new Engine()){engine.ConfigureAuto(f.Handle,0,true);Assert(Wait(()=>engine.GetSnapshot().Count==0,4000),"真实屏幕：空血条四暗豆确认0颗",rows);Snapshot initial=engine.GetSnapshot();RectangleF locked=initial.LeftRegion;Assert(initial.Triggers==0,"初始0颗不触发计时",rows);
 f.Frame=one;f.Refresh();Assert(Wait(()=>engine.GetSnapshot().Count==1,2000),"锁定位置：0→1增长正常更新",rows);Assert(engine.GetSnapshot().Triggers==0,"增长不触发",rows);
 long start=Stopwatch.GetTimestamp();f.Frame=zero;f.Refresh();Assert(Wait(()=>engine.GetSnapshot().Triggers==1,2000),"锁定位置：1→0触发15秒",rows);Snapshot drop=engine.GetSnapshot();rows.Add("MEASURE 请求变化到触发 "+((drop.TriggerStamp-start)*1000.0/Stopwatch.Frequency).ToString("F2")+"ms");
 f.Frame=black;f.Refresh();Wait(()=>false,1100);Snapshot missing=engine.GetSnapshot();Assert(missing.LeftRegion==locked,"黑屏1.1秒：四格位置保持锁定",rows);Assert(missing.Remaining>0&&missing.Remaining<drop.Remaining&&missing.Triggers==1,"黑屏原倒计时继续，不清零、不重触发",rows);
 f.Frame=zero;f.Refresh();Assert(Wait(()=>engine.GetSnapshot().Count==0,2000),"画面返回：恢复0颗确认",rows);Assert(engine.GetSnapshot().Triggers==1&&engine.GetSnapshot().LeftRegion==locked,"返回不重新定位或重触发",rows);
 Rectangle roi=new Rectangle(client.X+(int)(locked.X*client.Width),client.Y+(int)(locked.Y*client.Height),(int)(locked.Width*client.Width),(int)(locked.Height*client.Height));if(Native.Uncovered(f.Handle,roi))using(Bitmap proof=Native.Capture(roi)){if(proof!=null)proof.Save(Path.Combine(args[3],"真实屏幕暗豆区域.png"));}
 }f.Hide();}}
 catch(Exception e){rows.Add("FAIL "+e.ToString());Environment.ExitCode=1;}finally{Directory.CreateDirectory(args[3]);File.WriteAllLines(Path.Combine(args[3],"暗豆屏幕验证.txt"),rows);}
 }
}}

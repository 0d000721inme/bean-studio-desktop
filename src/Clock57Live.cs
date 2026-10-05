using System;using System.Collections.Generic;using System.Diagnostics;using System.Drawing;using System.Globalization;using System.IO;using System.Threading;using System.Windows.Forms;
namespace MuMuBeans {
// Captures only the handle of the fixture created here. No emulator or user game is opened.
static class Clock57Live {
 sealed class Fixture:Form {
  public Bitmap Image;readonly System.Windows.Forms.Timer tick=new System.Windows.Forms.Timer{Interval=30};int pulse;
  public Fixture(){FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;TopMost=true;DoubleBuffered=true;Location=new Point(Screen.PrimaryScreen.WorkingArea.Left+40,Screen.PrimaryScreen.WorkingArea.Top+70);tick.Tick+=delegate{pulse=(pulse+11)%255;Invalidate(new Rectangle(0,Math.Max(0,ClientSize.Height-4),4,4));Update();};tick.Start();}
  protected override void OnPaint(PaintEventArgs e){if(Image!=null)e.Graphics.DrawImage(Image,ClientRectangle);using(Brush b=new SolidBrush(Color.FromArgb(pulse,60,130)))e.Graphics.FillRectangle(b,0,Math.Max(0,ClientSize.Height-4),4,4);}
  protected override void Dispose(bool d){if(d){tick.Stop();tick.Dispose();}base.Dispose(d);}
 }
 static bool Wait(Func<bool> condition,int ms){Stopwatch sw=Stopwatch.StartNew();while(sw.ElapsedMilliseconds<ms){Application.DoEvents();if(condition())return true;Thread.Sleep(10);}return false;}
 static string StateLine(GameClockWorker w){ClockState s=w.State();return "capture="+WindowCapture.Status+" raw="+(s.Raw.HasValue?s.Raw.Value.ToString("0.00",CultureInfo.InvariantCulture):"null")+" confirmed="+(s.Value.HasValue?s.Value.Value.ToString("0.00",CultureInfo.InvariantCulture):"null")+" age="+s.Age.ToString("F3",CultureInfo.InvariantCulture)+"s process="+s.ProcessMs.ToString("F2",CultureInfo.InvariantCulture)+"ms match="+s.Confidence.ToString("F3",CultureInfo.InvariantCulture)+" "+s.Method+" "+s.Status;}
 static bool Same(RectangleF a,RectangleF b){return Math.Abs(a.X-b.X)<.00001&&Math.Abs(a.Y-b.Y)<.00001&&Math.Abs(a.Width-b.Width)<.00001&&Math.Abs(a.Height-b.Height)<.00001;}
 public static void Run(string[] a){
  Directory.CreateDirectory(a[2]);string config=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"clock-region.txt");byte[] previous=File.Exists(config)?File.ReadAllBytes(config):null;
  var rows=new List<string>();int failures=0;GameClockWorker worker=null;
  try{
   using(Bitmap image=new Bitmap(a[1]))using(Bitmap black=new Bitmap(image.Width,image.Height))using(Fixture fixture=new Fixture())using(GameClockWorker w=new GameClockWorker())using(Form cover=new Form()){
    using(Graphics g=Graphics.FromImage(black))g.Clear(Color.Black);
    worker=w;fixture.ClientSize=new Size(550,310);fixture.Image=image;fixture.Show();fixture.Refresh();Application.DoEvents();
    RectangleF defaultRegion=new RectangleF(.4f,.055f,.2f,.16f);w.SetRegion(defaultRegion);w.Configure(fixture.Handle);
    if(!Wait(delegate{ClockState s=w.State();return s.Value==37&&s.Raw==37&&s.Age<.25&&WindowCapture.Status=="WGC 窗口捕获";},6000))throw new Exception("测试窗口初始37未确认或WGC不可用："+StateLine(w));
    rows.Add("PASS 自建550×310窗口：WGC → 时钟区域 → OCR → 连续确认37");rows.Add(StateLine(w));
    if(!w.RecoveryAt(Stopwatch.GetTimestamp()).Contains("22 秒"))throw new Exception("37-15 恢复点错误");rows.Add("PASS 新鲜37可计算固定恢复点22");
    cover.FormBorderStyle=FormBorderStyle.None;cover.StartPosition=FormStartPosition.Manual;cover.Location=fixture.Location;cover.ClientSize=fixture.ClientSize;cover.BackColor=Color.FromArgb(27,22,45);cover.TopMost=true;cover.Show();cover.BringToFront();cover.Refresh();Application.DoEvents();
    Rectangle client=Native.ClientBounds(fixture.Handle),clock=ClockReader.Area(client.Size);clock.Offset(client.Location);
    if(Native.Uncovered(fixture.Handle,clock))throw new Exception("自建遮挡窗未实际遮挡时钟区域");
    var ages=new List<double>();Stopwatch observed=Stopwatch.StartNew();while(observed.ElapsedMilliseconds<1100){Application.DoEvents();ClockState s=w.State();if(s.Value==37&&s.Raw==37&&s.Age<.65)ages.Add(s.Age*1000);Thread.Sleep(10);}
    ClockState covered=w.State();if(covered.Value!=37||covered.Raw!=37||covered.Age>=.25||WindowCapture.Status!="WGC 窗口捕获"||ages.Count<30)throw new Exception("完全遮挡后不能持续确认："+StateLine(w));
    ages.Sort();rows.Add("PASS 自建遮挡窗覆盖源窗口1.1秒后，持续WGC读取37；GDI屏幕兼容捕获不可用");rows.Add(StateLine(w));rows.Add("FIXTURE_CONFIRMED_AGE_MS median="+ages[ages.Count/2].ToString("F1",CultureInfo.InvariantCulture)+" p95="+ages[Math.Min(ages.Count-1,(int)(ages.Count*.95))].ToString("F1",CultureInfo.InvariantCulture)+" n="+ages.Count);
    using(Bitmap crop=w.Preview()){if(crop==null)throw new Exception("遮挡下没有实际时钟裁图");crop.Save(Path.Combine(a[2],"遮挡下实际时间区域.png"));}
    fixture.Image=black;fixture.Refresh();w.Refresh();if(!Wait(delegate{ClockState s=w.State();return !s.Raw.HasValue&&s.Age>.70;},3000))throw new Exception("黑图后旧读数未过期："+StateLine(w));
    if(w.State().Value!=37||!w.RecoveryAt(Stopwatch.GetTimestamp()).Contains("未确认"))throw new Exception("黑图用了过期值或猜测/清除了历史读数："+StateLine(w));
    rows.Add("PASS 黑图使读数过期：保留历史37，不刷新时效、不猜60；RecoveryAt返回未确认");rows.Add(StateLine(w));w.Export(a[2]);
    fixture.Image=image;fixture.Refresh();w.Refresh();if(!Wait(delegate{ClockState s=w.State();return s.Raw==37&&s.Value==37&&s.Age<.25;},3000))throw new Exception("恢复原图后未重新确认："+StateLine(w));
    rows.Add("PASS 源图恢复后重新连续确认37");rows.Add(StateLine(w));
    w.SetRegion(defaultRegion);if(!File.Exists(config))throw new Exception("校准区域未保存");using(GameClockWorker loaded=new GameClockWorker()){if(!Same(loaded.State().Region,defaultRegion))throw new Exception("默认校准区域重新加载不一致");}rows.Add("PASS 默认校准ROI保存并从新worker重新加载");
    if(!Wait(delegate{ClockState s=w.State();return s.Raw==37&&s.Value==37&&s.Age<.25;},3000))throw new Exception("校准后未再次确认37："+StateLine(w));
    cover.Hide();fixture.Hide();
   }
  }catch(Exception ex){failures++;rows.Add("FAIL "+ex);if(worker!=null)try{rows.Add(StateLine(worker));}catch{} }
  finally{WindowCapture.Shutdown();try{if(previous==null){if(File.Exists(config))File.Delete(config);}else File.WriteAllBytes(config,previous);}catch(Exception ex){failures++;rows.Add("FAIL 恢复原配置："+ex.Message);}}
  rows.Add("验证范围仅为本机自建窗口和保存图片，不代表真实MuMu实战准确率或端到端游戏延迟。");rows.Add("FAILURES="+failures);File.WriteAllLines(Path.Combine(a[2],"实际时钟链路验证.txt"),rows);Environment.ExitCode=failures==0?0:1;
 }
}
}

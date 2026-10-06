using System;using System.Collections.Generic;using System.Diagnostics;using System.Drawing;using System.Drawing.Imaging;using System.Globalization;using System.IO;using System.Threading;
namespace MuMuBeans {
// Game time may pause: never extrapolate it from elapsed wall-clock time.
sealed class ClockTracker {
 public double? Value,Candidate;public double Stamp,CandidateStamp;int confirmations;
 public string Status="等待连续读数";
 public void Reset(){Value=Candidate=null;confirmations=0;Stamp=0;}
 public bool Observe(double? raw,double now){
  if(!raw.HasValue||raw<0||raw>60){Candidate=null;confirmations=0;Status="本帧数字未确认";return false;}
  double age=now-Stamp;
  bool follows=Value.HasValue&&age>=0&&age<=.65&&raw<=Value&&Value-raw<=age+(raw>=10?1.05:.25);
  if(follows&&raw==Value){Stamp=now;Candidate=null;confirmations=0;Status="已确认";return true;}
  bool consistent=Candidate.HasValue&&now-CandidateStamp<=.35&&raw<=Candidate+.015&&Candidate-raw<=now-CandidateStamp+(raw>=10?1.05:.25);
  confirmations=consistent?confirmations+1:1;Candidate=raw;CandidateStamp=now;
  int required=Value.HasValue&&age<=.65&&!follows?3:2;
  if(confirmations>=required){Value=raw;Stamp=now;Candidate=null;confirmations=0;Status="已重新确认";return true;}
  Status="读数待确认 "+confirmations+"/"+required+" · "+raw.Value.ToString("0.##",CultureInfo.InvariantCulture);return false;
 }
}
sealed class ClockState {public string Status,Backend,Method;public double? Raw,Value;public double Age,ProcessMs,Confidence;public RectangleF Region;}
sealed class GameClockWorker:IDisposable {
 readonly object gate=new object();readonly Thread thread;readonly AutoResetEvent wake=new AutoResetEvent(false);readonly ClockTracker tracker=new ClockTracker();
 IntPtr window;bool stopped,paused;int version,idle;double? value;long stamp,samples;RectangleF region=new RectangleF(.40f,.055f,.20f,.16f);
 string status="请选择 MuMu 窗口",backend="初始化中",method="未确认";double? raw;double processMs,confidence;Bitmap preview;long previewStamp;
 sealed class Failure {public Bitmap Image;public string Reason;public long Stamp;}
 readonly Queue<Failure> failures=new Queue<Failure>();readonly Queue<string> records=new Queue<string>();long lastFailure;
 readonly string config=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"clock-region.txt");
 public GameClockWorker(){try{if(File.Exists(config)){string[] p=File.ReadAllText(config).Split(',');RectangleF r=new RectangleF(float.Parse(p[0],CultureInfo.InvariantCulture),float.Parse(p[1],CultureInfo.InvariantCulture),float.Parse(p[2],CultureInfo.InvariantCulture),float.Parse(p[3],CultureInfo.InvariantCulture));if(ValidRegion(r))region=r;}}catch{}thread=new Thread(Loop){IsBackground=true,Name="Game clock continuous OCR",Priority=ThreadPriority.BelowNormal};thread.Start();}
 public static bool ValidRegion(RectangleF r){return !float.IsNaN(r.X)&&!float.IsNaN(r.Y)&&r.X>=0&&r.Y>=0&&r.Width>=.025&&r.Height>=.025&&r.Width<=.4&&r.Height<=.3&&r.Right<=1&&r.Bottom<=1;}
 public void SetRegion(RectangleF r){if(!ValidRegion(r))throw new ArgumentException("请只框选中间时间数字，保留少量边距；不要框整幅画面。");lock(gate){region=r;version++;tracker.Reset();value=raw=null;stamp=0;confidence=0;method="未确认";status="区域已更新 · 等待连续读数";if(preview!=null){preview.Dispose();preview=null;}}File.WriteAllText(config,string.Join(",",new string[]{r.X.ToString(CultureInfo.InvariantCulture),r.Y.ToString(CultureInfo.InvariantCulture),r.Width.ToString(CultureInfo.InvariantCulture),r.Height.ToString(CultureInfo.InvariantCulture)}));wake.Set();}
 public void Configure(IntPtr h){lock(gate){if(window!=h){window=h;version++;tracker.Reset();value=raw=null;stamp=0;confidence=0;method="未确认";status="来源已切换 · 等待读数";if(preview!=null){preview.Dispose();preview=null;}}}wake.Set();}
 public long Samples{get{return Interlocked.Read(ref samples);}}
 public bool PausedIdle{get{lock(gate)return paused&&Interlocked.CompareExchange(ref idle,0,0)==1;}}
 public void SetPaused(bool value){lock(gate){if(paused==value)return;paused=value;Interlocked.Exchange(ref idle,0);version++;tracker.Reset();raw=this.value=null;stamp=0;confidence=0;method="未确认";status=value?"正在暂停游戏时间识别":"等待新的连续读数";}wake.Set();}
 public ClockState State(){lock(gate)return new ClockState{Status=status,Backend=backend,Method=method,Confidence=confidence,Raw=raw,Value=value,Age=stamp==0?double.PositiveInfinity:(Stopwatch.GetTimestamp()-stamp)/(double)Stopwatch.Frequency,ProcessMs=processMs,Region=region};}
 public Bitmap Preview(){lock(gate)return preview==null?null:(Bitmap)preview.Clone();}
 public static string Recovery(double? time){if(!time.HasValue||time<0||time>60)return "恢复点：时间未确认";if(time<15)return "本回合剩余不足 15 秒";return "恢复点："+(time.Value-15).ToString(time.Value%1==0?"0":"0.00",CultureInfo.InvariantCulture)+" 秒（游戏）";}
 public static string LateRecovery(double? time,double remaining){if(!time.HasValue||time<0||time>60||remaining<=0||remaining>15)return "恢复点：时间未确认";if(time<remaining)return "本回合剩余不足冷却时间";return "恢复点≈"+(time.Value-remaining).ToString("0.00",CultureInfo.InvariantCulture)+" 秒（中途补算）";}
 public string RecoveryAt(long trigger){lock(gate){double age=(trigger-stamp)/(double)Stopwatch.Frequency;string text=Recovery(value.HasValue&&age>=-.2&&age<=.6?value:null);return age<0?text.Replace("恢复点：","恢复点≈"):text;}}
 public void Refresh(){wake.Set();}
 public void Export(string folder){lock(gate){string dir=Path.Combine(folder,"游戏时间");Directory.CreateDirectory(dir);File.WriteAllLines(Path.Combine(dir,"连续读数.csv"),records.ToArray());int i=0;foreach(Failure f in failures){string name=string.Format("{0:00}_{1}",i++,f.Stamp);f.Image.Save(Path.Combine(dir,name+".png"));File.WriteAllText(Path.Combine(dir,name+".txt"),f.Reason);}File.WriteAllText(Path.Combine(dir,"状态.txt"),status+"\r\n"+backend+"\r\n区域="+region+"\r\n失败图片是最近最多20张局部缓存，不包含完整屏幕。采样时间为单调时钟秒。没有截图可能是画面被遮挡，或尚未采到帧。");}}
 void Loop(){try{using(ClockReader reader=new ClockReader()){lock(gate)backend=reader.BackendStatus;while(true){IntPtr h;int v;RectangleF roi;bool pause;lock(gate){if(stopped)return;h=window;v=version;roi=region;pause=paused;if(pause){Interlocked.Exchange(ref idle,1);status="游戏时间识别已暂停";}else Interlocked.Exchange(ref idle,0);}if(pause){wake.WaitOne();continue;}
  Stopwatch elapsed=Stopwatch.StartNew();long sampled=Stopwatch.GetTimestamp();Bitmap b=null;double? found=null;string reason;
  try{Rectangle client=Native.ClientBounds(h);if(h==IntPtr.Zero||client.Width<320)reason="请选择有效 MuMu 窗口";else if(Native.IsIconic(h))reason="窗口已最小化，无法读取时间";else{
   Rectangle area=new Rectangle(client.X+(int)(client.Width*roi.X),client.Y+(int)(client.Height*roi.Y),Math.Max(1,(int)(client.Width*roi.Width)),Math.Max(1,(int)(client.Height*roi.Height)));
   if(!WindowCapture.Available(h,area))reason="时间区域被遮挡或超出屏幕 · 移开主窗口/浮窗";else{b=WindowCapture.Capture(h,area);sampled=Stopwatch.GetTimestamp();Interlocked.Increment(ref samples);found=reader.Read(b);reason=reader.LastReason;}
  }}catch(Exception e){reason="时间识别异常："+e.Message;}
  lock(gate){if(v==version){raw=found;confidence=found.HasValue?reader.LastConfidence:0;method=found.HasValue?reader.LastMethod:"未确认";bool accepted=tracker.Observe(found,sampled/(double)Stopwatch.Frequency);if(accepted){value=tracker.Value;stamp=sampled;}status=found.HasValue?tracker.Status+" · "+reason:reason;processMs=elapsed.Elapsed.TotalMilliseconds;
   if(b!=null){if(preview!=null)preview.Dispose();preview=(Bitmap)b.Clone();previewStamp=sampled;if(!accepted&&(sampled-lastFailure)/(double)Stopwatch.Frequency>=.5){failures.Enqueue(new Failure{Image=(Bitmap)b.Clone(),Stamp=sampled,Reason=status});lastFailure=sampled;while(failures.Count>20)failures.Dequeue().Image.Dispose();}}
   records.Enqueue(string.Format(CultureInfo.InvariantCulture,"{0:F6},{1},{2},{3},{4:F1},{5:F3},\"{6}\"",sampled/(double)Stopwatch.Frequency,found,value,accepted,processMs,confidence,status.Replace("\"","'")));while(records.Count>400)records.Dequeue();
  }}if(b!=null)b.Dispose();wake.WaitOne(Math.Max(10,50-(int)elapsed.ElapsedMilliseconds));
 }}}catch(Exception ex){lock(gate){status="时间识别线程异常："+ex.Message;backend="识别已停止，请保存诊断";}}}
 public void Dispose(){lock(gate)stopped=true;wake.Set();if(thread.Join(1500))ReleaseBuffers();else System.Threading.Tasks.Task.Run(()=>{thread.Join();ReleaseBuffers();});}
 void ReleaseBuffers(){wake.Dispose();lock(gate){if(preview!=null){preview.Dispose();preview=null;}foreach(Failure f in failures)f.Image.Dispose();failures.Clear();}}
}
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MuMuBeans
{
    // Real WGC integration against this test's synthetic window only. Never
    // activates a window, reads a user screenshot, or discovers an emulator.
    static class Resize58Tests
    {
        [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        static readonly List<string> rows=new List<string>();static int failures;
        static void Check(bool ok,string name){rows.Add((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
        static double Milliseconds(long later,long earlier){return (later-earlier)*1000.0/Stopwatch.Frequency;}
        static bool Until(Func<bool> condition,int timeout)
        {
            Stopwatch time=Stopwatch.StartNew();
            while(time.ElapsedMilliseconds<timeout){if(condition())return true;Thread.Sleep(10);}
            return condition();
        }
        static bool Known(Engine engine,int count,Size size)
        {
            Snapshot s=engine.GetSnapshot();return s.CaptureSize==size&&s.Count==count&&s.Reading!=null&&s.Reading.Valid&&s.Reading.Count==count;
        }
        static bool WaitStopped(Engine engine)
        {return WindowCapture.PauseCompleted.Wait(8000)&&Until(delegate{return engine.PauseCompleted;},8000);}
        static void DrainPreview(Engine engine)
        {Bitmap image;while(engine.TryTakeScanPreview(out image))image.Dispose();}
        static Bitmap TakePreview(Engine engine,int timeout)
        {
            Bitmap image=null;
            Until(delegate{return engine.TryTakeScanPreview(out image);},timeout);
            return image;
        }
        static void Details(Engine engine,string name)
        {
            Snapshot s=engine.GetSnapshot();
            rows.Add(string.Format(CultureInfo.InvariantCulture,"INFO {0} count={1} reading={2} size={3} triggers={4} deadline={5:F9} remaining={6:F3} reads={7} samples={8} reuse={9} state={10} capture={11}",name,s.Count,s.Reading==null?"null":s.Reading.Valid+"/"+s.Reading.Count,s.CaptureSize,s.Triggers,s.CooldownDeadline,s.Remaining,WindowCapture.Readbacks,engine.Samples,s.ReusedClassifications,s.Status,WindowCapture.Status));
        }
        static Bitmap Scene(Size size,int rightCount)
        {
            Bitmap image=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(image))
            {
                g.Clear(Color.FromArgb(12,19,31));double pitch=size.Width*.0159,y=size.Height*.115;
                for(int side=0;side<2;side++)
                {
                    double x=size.Width*(side==0?.105:.83);
                    using(Brush bar=new SolidBrush(Color.FromArgb(220,48,30)))
                        g.FillRectangle(bar,(float)(side==0?x-pitch*.5:x-pitch*4),(float)(y-pitch*1.55),(float)(pitch*10),(float)(pitch*.32));
                    for(int i=0;i<4;i++)
                    {
                        float cx=(float)(x+i*pitch),cy=(float)y;
                        bool lit=side==0||i>=4-rightCount;
                        using(Brush bean=new SolidBrush(side==0?Color.Gold:lit?Color.Cyan:Color.FromArgb(10,138,191)))
                            g.FillPolygon(bean,new PointF[]{new PointF(cx,cy-(float)(pitch*.58)),new PointF(cx+(float)(pitch*.42),cy),new PointF(cx,cy+(float)(pitch*.58)),new PointF(cx-(float)(pitch*.42),cy)});
                        if(!lit)using(Brush core=new SolidBrush(Color.FromArgb(22,43,112)))
                            g.FillPolygon(core,new PointF[]{new PointF(cx,cy-(float)(pitch*.50)),new PointF(cx+(float)(pitch*.29),cy),new PointF(cx,cy+(float)(pitch*.50)),new PointF(cx-(float)(pitch*.29),cy)});
                    }
                }
            }
            return image;
        }
        public static void Run(string[] args)
        {
            string folder=args.Length>1?args[1]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"resize58-tests");
            Directory.CreateDirectory(folder);rows.Clear();failures=0;Engine disposed=null;
            try
            {
                Native.SetProcessDPIAware();
                Size first=new Size(960,540),second=new Size(1280,720);
                Rectangle desktop=Screen.PrimaryScreen.WorkingArea;
                int width=Math.Min(1280,Math.Min(desktop.Width-48,(desktop.Height-96)*16/9));
                if(width<1280){width=Math.Max(400,width/16*16);second=new Size(width,width*9/16);int small=Math.Max(336,Math.Min(960,width-128))/16*16;first=new Size(small,small*9/16);}
                bool fixture=true;
                foreach(Size size in new Size[]{first,second})foreach(int count in new int[]{4,3})
                using(Bitmap image=Scene(size,count))
                {
                    LocatedPair pair=AutoLocator.Find(image);
                    bool valid=pair.RightReading!=null&&pair.RightReading.Valid&&pair.RightReading.Count==count&&!pair.Right.IsEmpty;
                    Check(valid,"合成HUD静态前检 "+size+" 右侧 "+count+" 豆，独立定位/分类正确");fixture&=valid;
                }
                if(!fixture)throw new InvalidOperationException("合成HUD不符合检测器，停止窗口集成测试");
                using(SyntheticHost host=new SyntheticHost(first))
                using(Engine engine=new Engine())
                {
                    disposed=engine;Check(GetForegroundWindow()!=host.Handle,"合成测试窗不激活、不置顶、不抢前台");
                    rows.Add("INFO source_handle="+host.Handle+" valid="+Native.IsWindow(host.Handle)+" visible="+Native.IsWindowVisible(host.Handle)+" iconic="+Native.IsIconic(host.Handle)+" client="+host.Bounds);
                    long reads=WindowCapture.Readbacks;
                    engine.ConfigureAuto(host.Handle,1,true,true);
                    bool ready=Until(delegate{return Known(engine,4,first)&&WindowCapture.Readbacks>=reads+3&&WindowCapture.Status=="WGC 窗口捕获";},8000);
                    Check(ready,"实际WGC完成回读并确认960级合成HUD为4豆基准");Details(engine,"baseline");
                    if(!ready)throw new InvalidOperationException("WGC/初始豆数未就绪，后续结果无效");
                    int beforeTriggers=engine.GetSnapshot().Triggers;long command=Stopwatch.GetTimestamp();
                    long painted=host.SetCount(3);
                    bool dropped=Until(delegate{return Known(engine,3,first)&&engine.GetSnapshot().Triggers==beforeTriggers+1;},5000);
                    Snapshot drop=engine.GetSnapshot();long observed=Stopwatch.GetTimestamp();
                    Check(dropped&&drop.CooldownDeadline>drop.ClockNow&&drop.Remaining>0&&drop.Remaining<=15,"真实WGC合成4→3豆仅触发一次完整15秒deadline");Details(engine,"drop");
                    rows.Add(string.Format(CultureInfo.InvariantCulture,"TIMING synthetic_count_change command_to_paint_ms={0:F2} paint_to_trigger_ms={1:F2} trigger_to_test_observed_ms={2:F2} confirmation_ms={3:F2}",Milliseconds(painted,command),Milliseconds(drop.TriggerStamp,painted),Milliseconds(observed,drop.TriggerStamp),drop.ConfirmationMs));
                    if(!dropped)throw new InvalidOperationException("合成少豆未触发，停止后续deadline守恒检查");
                    double deadline=drop.CooldownDeadline;int triggers=drop.Triggers;

                    DrainPreview(engine);Stopwatch request=Stopwatch.StartNew();bool accepted=engine.RequestScan();request.Stop();
                    Check(accepted&&request.Elapsed.TotalMilliseconds<100,"RequestScan异步请求立即返回（<100ms），不在调用线程扫描");
                    using(Bitmap image=TakePreview(engine,5000))
                    {
                        Check(image!=null&&image.Size==first,"后台重新扫描移交一次完整Client区域预览");
                        if(image!=null)image.Save(Path.Combine(folder,"scan-before-resize.png"));
                    }
                    Bitmap extra;bool twice=engine.TryTakeScanPreview(out extra);if(extra!=null)extra.Dispose();
                    Check(!twice,"TryTakeScanPreview消费后不重复返回同一预览");
                    bool rebased=Until(delegate{return Known(engine,3,first);},5000);
                    Snapshot rescanned=engine.GetSnapshot();
                    Check(rebased&&rescanned.Triggers==triggers&&rescanned.CooldownDeadline==deadline,"手动重扫重新确认3豆基准，保持已有deadline，不产生假消费");
                    rows.Add(string.Format(CultureInfo.InvariantCulture,"TIMING request_scan_api_ms={0:F3}",request.Elapsed.TotalMilliseconds));Details(engine,"manual-rescan");

                    DrainPreview(engine);long resizeCommand=Stopwatch.GetTimestamp();long beforeResizeReads=WindowCapture.Readbacks;
                    long resizePaint=host.ResizeTo(second);
                    bool relocated=Until(delegate{return Known(engine,3,second)&&WindowCapture.Readbacks>=beforeResizeReads+2&&WindowCapture.Status=="WGC 窗口捕获";},8000);
                    long resizeObserved=Stopwatch.GetTimestamp();Snapshot resized=engine.GetSnapshot();
                    Check(relocated,"真实ClientSize改变后无需按键自动重新定位、恢复有效3豆和WGC回读");
                    Check(resized.Triggers==triggers&&resized.CooldownDeadline==deadline,"分辨率重定位不产生假消费，不清除或重置原deadline");
                    using(Bitmap image=TakePreview(engine,2000))
                    {
                        Check(image!=null&&image.Size==second,"自动尺寸校准提供新分辨率完整预览");
                        if(image!=null)image.Save(Path.Combine(folder,"scan-after-resize.png"));
                    }
                    rows.Add(string.Format(CultureInfo.InvariantCulture,"TIMING synthetic_resize command_to_paint_ms={0:F2} paint_to_valid_relocated_observed_ms={1:F2}",Milliseconds(resizePaint,resizeCommand),Milliseconds(resizeObserved,resizePaint)));Details(engine,"resized");

                    engine.SetPaused(true);Check(WaitStopped(engine)&&!engine.TimerPrecisionActive,"暂停实际关闭WGC/两识别线程并释放WinMM精度申请");
                    DrainPreview(engine);long pausedReads=WindowCapture.Readbacks,pausedClock=engine.Clock.Samples;
                    bool uncovered=Native.Uncovered(host.Handle,host.Bounds);
                    accepted=engine.RequestScan();Check(accepted,"暂停状态接受显式单次重新扫描请求");
                    bool finished=Until(delegate{return !engine.GetSnapshot().ScanPending&&engine.PauseCompleted;},5000);
                    using(Bitmap image=TakePreview(engine,1000))
                    {
                        if(uncovered)Check(finished&&image!=null&&image.Size==second&&engine.GetSnapshot().Reading!=null&&engine.GetSnapshot().Reading.Valid,"暂停单次兼容截图正向定位完成，并移交一次新预览");
                        else rows.Add("LIMIT 暂停单次兼容截图被遮挡/超出屏幕，无法验证正向扫描；这是兼容截图已知限制，不会偷偷恢复WGC。");
                        if(image!=null)image.Save(Path.Combine(folder,"paused-single-scan.png"));
                    }
                    Thread.Sleep(120);
                    Snapshot paused=engine.GetSnapshot();
                    Check(finished&&paused.Paused&&WindowCapture.CaptureStopped&&WindowCapture.Readbacks==pausedReads&&engine.Clock.Samples==pausedClock&&!engine.TimerPrecisionActive,"暂停单次扫描结束仍不创建WGC/OCR会话、不申请WinMM精度");
                    Check(paused.CooldownDeadline==deadline&&paused.Triggers==triggers,"暂停单次扫描保留已有deadline与触发次数");

                    engine.SetPaused(false);engine.ConfigureAuto(host.Handle,1,true);
                    Check(Until(delegate{return Known(engine,3,second);},5000),"继续监测重新确认基准，未沿用暂停豆数");
                    host.Close();Check(Until(delegate{return !Native.IsWindow(host.Handle);},2000),"合成源窗口关闭完成");
                    Thread.Sleep(400);Details(engine,"source-closed");
                    bool cleared=Until(delegate{Snapshot s=engine.GetSnapshot();return !engine.TimerPrecisionActive&&WindowCapture.CaptureStopped&&!s.Count.HasValue&&s.Reading==null&&s.Status.Contains("来源已关闭");},2000);
                    Snapshot closed=engine.GetSnapshot();long closedClock=engine.Clock.Samples;Thread.Sleep(100);
                    Check(cleared&&!engine.RequestScan()&&closed.CooldownDeadline==deadline&&closed.Triggers==triggers&&engine.Clock.Samples==closedClock&&!engine.Clock.State().Value.HasValue,"源窗口关闭后清除基准/Clock来源、拒绝重扫、停WGC并释放WinMM，deadline仍保持");
                    engine.ConfigureAuto(IntPtr.Zero,1,true,true);
                    Check(!engine.RequestScan()&&!engine.TimerPrecisionActive&&WindowCapture.PauseCompleted.Wait(8000)&&WindowCapture.CaptureStopped,"无来源即使run=true也拒绝扫描、释放计时请求和旧捕获会话");
                    Check(GetForegroundWindow()!=host.Handle,"全程测试源未抢前台");
                }
                Check(disposed!=null&&!disposed.TimerPrecisionActive&&WindowCapture.PauseCompleted.Wait(8000)&&WindowCapture.CaptureStopped,"Dispose释放测试源捕获与计时请求");
            }
            catch(Exception ex){Check(false,"集成异常："+ex);}
            finally
            {
                WindowCapture.SetPaused(true);WindowCapture.PauseCompleted.Wait(8000);
                rows.Add("NOTICE 所有图像为代码绘制的合成HUD。分段耗时不代表真实MuMu/游戏延迟、OCR准确率或用户环境性能。暂停单次截图需要源窗口露出。没有使用用户截图、模拟器或wgcprobe.exe。");
                rows.Add("FAILURES="+failures);File.WriteAllLines(Path.Combine(folder,"resize58-report.txt"),rows.ToArray(),new UTF8Encoding(true));Environment.ExitCode=failures==0?0:1;
            }
        }
        sealed class SyntheticHost:IDisposable
        {
            readonly Thread thread;readonly ManualResetEvent ready=new ManualResetEvent(false);SyntheticWindow form;Exception error;
            public IntPtr Handle;public Rectangle Bounds{get{return Native.ClientBounds(Handle);}}
            public SyntheticHost(Size size)
            {
                thread=new Thread(delegate(){try{using(SyntheticWindow window=new SyntheticWindow(size)){form=window;window.Shown+=delegate{Handle=window.Handle;ready.Set();};Application.Run(window);}}catch(Exception ex){error=ex;ready.Set();}}){IsBackground=true,Name="Synthetic resize and scan integration window"};
                thread.SetApartmentState(ApartmentState.STA);thread.Start();if(!ready.WaitOne(5000))throw new TimeoutException("合成窗口未就绪");if(error!=null)throw new InvalidOperationException("合成窗口失败",error);
            }
            public long SetCount(int count){form.Invoke((Action)delegate{form.SetCount(count);form.Refresh();});return form.ChangePaint;}
            public long ResizeTo(Size size){form.Invoke((Action)delegate{form.ClientSize=size;form.Reposition();form.Refresh();});return form.ChangePaint;}
            public void Close(){if(form!=null&&!form.IsDisposed)form.BeginInvoke((Action)delegate{form.Close();});if(!thread.Join(5000))throw new TimeoutException("合成窗口关闭超时");}
            public void Dispose(){Close();ready.Dispose();}
        }
        sealed class SyntheticWindow:Form
        {
            readonly System.Windows.Forms.Timer tick=new System.Windows.Forms.Timer{Interval=33};Bitmap scene;int rightCount=4,frame,changeVersion,paintedVersion=-1;long changePaint;
            public long ChangePaint{get{return Interlocked.Read(ref changePaint);}}
            public SyntheticWindow(Size size)
            {
                Text="Bean Studio V5.8 合成HUD扫描/分辨率测试";ShowInTaskbar=true;TopMost=false;DoubleBuffered=true;StartPosition=FormStartPosition.Manual;FormBorderStyle=FormBorderStyle.Sizable;ClientSize=size;Reposition();Rebuild();
                tick.Tick+=delegate{frame++;Invalidate();};tick.Start();
            }
            protected override bool ShowWithoutActivation{get{return true;}}
            public void Reposition(){Rectangle screen=Screen.PrimaryScreen.WorkingArea;Location=new Point(screen.Left+16,screen.Top+16);}
            void Rebuild(){if(ClientSize.Width<320||ClientSize.Height<150)return;Bitmap next=Scene(ClientSize,rightCount);if(scene!=null)scene.Dispose();scene=next;changeVersion++;Invalidate();}
            public void SetCount(int count){rightCount=count;Rebuild();}
            protected override void OnResize(EventArgs e){base.OnResize(e);Rebuild();}
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);if(scene!=null)e.Graphics.DrawImageUnscaled(scene,0,0);
                using(Brush brush=new SolidBrush(Color.FromArgb(50+frame%140,75,165)))e.Graphics.FillRectangle(brush,8+frame%Math.Max(1,ClientSize.Width-80),ClientSize.Height*.70f,24,9);
                if(paintedVersion!=changeVersion){paintedVersion=changeVersion;Interlocked.Exchange(ref changePaint,Stopwatch.GetTimestamp());}
            }
            protected override void Dispose(bool disposing){if(disposing){tick.Dispose();if(scene!=null){scene.Dispose();scene=null;}}base.Dispose(disposing);}
        }
    }
}

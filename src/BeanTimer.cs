using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Threading;
using Timer = System.Windows.Forms.Timer;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MuMuBeans
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length>0 && args[0]=="--sound-test"){SoundTests.Run(args);return;}
            if(args.Length>0 && args[0]=="--sound-media-test"){SoundTests.Media(args);return;}
            if(args.Length>0 && args[0]=="--sound-engine-test"){SoundTests.EngineAudio(args);return;}
            if(args.Length>0 && args[0]=="--render-sounds"){SoundPanel.Render(args[1]);return;}
            if(args.Length>0 && args[0]=="--sound-ui-test"){SoundPanel.Verify(args[1]);return;}
            if(args.Length>0 && args[0]=="--empty-bean-test"){EmptyBeanTests.Run(args);return;}
            if(args.Length>0 && args[0]=="--bean-relocation-test"){BeanRelocationTests.Run(args);return;}
            if(args.Length>0 && args[0]=="--pause-test"){try{PauseTests.Run(args[1]);}catch(Exception ex){Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"pause-error.txt"),ex.ToString());Environment.ExitCode=1;}return;}
            if (args.Length > 0 && args[0] == "--self-test")
            {
                try { Tests.Run(args[1], args[2], args[3]); }
                catch (Exception ex) { File.WriteAllText(Path.Combine(args[3], "test-error.txt"), ex.ToString()); Environment.ExitCode = 1; }
                return;
            }
            if(args.Length>0 && args[0]=="--dark-screen-test"){DarkScreenTests.Run(args);return;}
            if(args.Length>0 && args[0]=="--dark-test"){DarkTests.Run(args);return;}
            if(args.Length>0 && args[0]=="--auto-test"){AutoTests.Run(args);return;}
            if(args.Length>0 && args[0]=="--latest-screen-test"){AutoTests.RunLatest(args[1],args[2]);return;}
            if(args.Length>0 && args[0]=="--render")
            {try{using(Bitmap image=new Bitmap(args[1]))using(MainForm form=new MainForm(false))form.RenderDemo(image,args[2]);}catch(Exception ex){File.WriteAllText(args[2]+".error.txt",ex.ToString());Environment.ExitCode=1;}return;}
            if(args.Length>0 && args[0]=="--render-states"){try{using(Bitmap image=new Bitmap(args[1]))using(MainForm form=new MainForm(false))form.RenderStates(image,args[2]);}catch(Exception ex){Directory.CreateDirectory(args[2]);File.WriteAllText(Path.Combine(args[2],"render-error.txt"),ex.ToString());Environment.ExitCode=1;}return;}
            if(args.Length>0 && args[0]=="--ui-test")
            {try{Directory.CreateDirectory(args[1]);using(MainForm form=new MainForm(false))form.VerifyInteractions(args[1]);}catch(Exception ex){File.WriteAllText(Path.Combine(args[1],"UI-error.txt"),ex.ToString());Environment.ExitCode=1;}return;}
            if(args.Length>0 && args[0]=="--clock-seed"){try{ClockReader.Seed(new string[]{args[1],args[2],args[3],args[4],args[5]},args[6]);}catch(Exception ex){File.WriteAllText(args[6]+".error.txt",ex.ToString());Environment.ExitCode=1;}return;}
            if(args.Length>0 && args[0]=="--clock57-test"){try{Clock57Bench.Run(new string[]{args[1],args[2]});}catch(Exception ex){File.WriteAllText(args[2]+".error.txt",ex.ToString());Environment.ExitCode=1;}return;}
            if(args.Length>0 && args[0]=="--clock57-live"){try{Clock57Live.Run(args);}catch(Exception ex){Directory.CreateDirectory(args[2]);File.WriteAllText(Path.Combine(args[2],"链路错误.txt"),ex.ToString());Environment.ExitCode=1;}return;}
            if(args.Length>0 && args[0]=="--v56-test"){Version56Tests.Run(args);return;}
            if(args.Length>0 && args[0]=="--v56-live"){Version56Tests.Live(args);return;}
            if(args.Length>0 && args[0]=="--clock55-live"){Clock55Live.Run(args);return;}
            if(args.Length>0 && args[0]=="--clock55-test"){Clock55Tests.Run(args);return;}
            if(args.Length>0 && args[0]=="--clock-test"){ClockTests.Run(args);return;}
            if(args.Length>0 && args[0]=="--clock-only"){ClockScreenTest.ClockOnly(args);return;}
            if(args.Length>0 && args[0]=="--clock-screen-test"){ClockScreenTest.Run(args);return;}
            try{Application.Run(new MainForm());}catch(Exception ex){string log=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"启动错误.txt");File.WriteAllText(log,ex.ToString());MessageBox.Show("程序启动失败，错误详情已保存到："+log,"启动错误");}
        }
    }

    static class Native
    {
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);
        public delegate bool EnumProc(IntPtr hwnd, IntPtr param);
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct Pt { public int X, Y; public Pt(int x, int y) { X = x; Y = y; } }
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc, IntPtr param);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd, ref Pt point);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Pt point);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint id);

        public static Rectangle ClientBounds(IntPtr hwnd)
        {
            Rect r; Pt p = new Pt(0, 0);
            if (!IsWindow(hwnd) || !GetClientRect(hwnd, out r) || !ClientToScreen(hwnd, ref p)) return Rectangle.Empty;
            return new Rectangle(p.X, p.Y, r.Right - r.Left, r.Bottom - r.Top);
        }

        public static bool Uncovered(IntPtr hwnd, Rectangle r)
        {
            if (!IsWindow(hwnd) || !IsWindowVisible(hwnd) || IsIconic(hwnd) || r.Width < 8 || r.Height < 8) return false;
            if (!SystemInformation.VirtualScreen.Contains(r)) return false;
            // Check a dense grid across the small detection area, including all four beans.
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 9; x++)
                {
                    Pt p = new Pt(r.Left + 1 + (r.Width - 3) * x / 8, r.Top + 1 + (r.Height - 3) * y / 2);
                    if (GetAncestor(WindowFromPoint(p), 2) != GetAncestor(hwnd, 2)) return false;
                }
            return true;
        }

        public static Bitmap Capture(Rectangle r)
        {
            Bitmap b = new Bitmap(r.Width, r.Height);
            try { using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size); return b; }
            catch { b.Dispose(); throw; }
        }
    }

    sealed class WindowItem
    {
        public IntPtr Handle; public string Title;
        public override string ToString() { return Title; }
    }

    sealed class Reading
    {
        public bool Valid = true;
        public bool[] Lit = new bool[4];
        public double[] Signal = new double[4], Bright = new double[4], Contrast = new double[4];
        public string Reason = "OK";
        public int Count { get { int n = 0; foreach (bool v in Lit) if (v) n++; return n; } }
    }

    // Copy the small ROI once. Avoid thousands of GDI+ GetPixel calls per sample.
    sealed class PixelMap
    {
        readonly byte[] data; readonly int width;
        public PixelMap(Bitmap image, Rectangle area)
        {
            width = area.Width; data = new byte[area.Width * area.Height * 4];
            BitmapData bits = image.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try { for (int y = 0; y < area.Height; y++) Marshal.Copy(IntPtr.Add(bits.Scan0, y * bits.Stride), data, y * width * 4, width * 4); }
            finally { image.UnlockBits(bits); }
        }
        public void Read(int x, int y, out int r, out int g, out int b)
        { int n = (y * width + x) * 4; b = data[n]; g = data[n + 1]; r = data[n + 2]; }
        public int Peak(int x, int y) { int r,g,b; Read(x,y,out r,out g,out b); return Math.Max(r,Math.Max(g,b)); }
    }

    static class Detector
    {
        sealed class Slot
        { public bool Valid, Lit; public double Signal, Bright, Contrast, Score; public string Reason; }
        public static Reading Analyze(Bitmap image, Rectangle area, int threshold)
        {
            Reading result = new Reading();
            if (area.Width < 24 || area.Height < 8 || !new Rectangle(Point.Empty, image.Size).Contains(area))
            { result.Valid = false; result.Reason = "识别框太小或超出画面"; return result; }
            PixelMap pixels = new PixelMap(image, area);
            double cell = area.Width / 4.0;
            for (int i = 0; i < 4; i++)
            {
                Slot best = null;
                // Search only inside each slot, up to 8% of its size. Never jump to a neighbor.
                foreach (double dy in new double[] { 0, -.08, .08 })
                    foreach (double dx in new double[] { 0, -.08, .08 })
                    {
                        Slot sample = Sample(pixels, area.Width, area.Height, cell * (i + .5 + dx), area.Height * (.5 + dy), cell, threshold);
                        sample.Score -= (Math.Abs(dx) + Math.Abs(dy)) * .3;
                        if (best == null || sample.Score > best.Score) best = sample;
                    }
                result.Signal[i] = best.Signal; result.Bright[i] = best.Bright; result.Contrast[i] = best.Contrast; result.Lit[i] = best.Lit;
                if (!best.Valid) { result.Valid = false; result.Reason = "第 " + (i + 1) + " 颗：" + best.Reason; }
            }
            return result;
        }
        // Purple empty slots have a low green core surrounded by a brighter violet rim.
        // Require the cavity across five points: a passing flare is not a dark bean.
        static bool PurpleCavity(PixelMap m,int w,int h,double cx,double cy,double pitch,double height)
        {
            double core=0,rim=0;int n=0;
            foreach(PointF d in new PointF[]{PointF.Empty,new PointF(-.10f,0),new PointF(.10f,0),new PointF(0,-.15f),new PointF(0,.15f)}){
                int r,g,b;m.Read(Math.Max(0,Math.Min(w-1,(int)(cx+d.X*pitch))),Math.Max(0,Math.Min(h-1,(int)(cy+d.Y*height))),out r,out g,out b);
                if(g>30||r<15||r>105||b<40||b>145||r-g<10||b-r<15)return false;core+=b;
            }
            foreach(double dx in new double[]{-.34,.34})foreach(double dy in new double[]{-.08,0,.08}){
                int r,g,b;m.Read(Math.Max(0,Math.Min(w-1,(int)(cx+dx*pitch))),Math.Max(0,Math.Min(h-1,(int)(cy+dy*height))),out r,out g,out b);
                if(b>110&&r-g>30&&b-g>45)n++;rim+=b;
            }
            return n>=4&&rim/6-core/5>=35;
        }
        static Slot Sample(PixelMap pixels, int width, int height, double cx, double cy, double cell, int threshold)
        {
            int total=0, colored=0, bright=0, darkInterior=0, violet=0; double sum=0,magenta=0;
            for (int y=Math.Max(0,(int)(cy-height*.23)); y<=Math.Min(height-1,(int)(cy+height*.23)); y++)
                for (int x=Math.Max(0,(int)(cx-cell*.18)); x<=Math.Min(width-1,(int)(cx+cell*.18)); x++)
                {
                    int r,g,b; pixels.Read(x,y,out r,out g,out b); total++;
                    int peak=Math.Max(r,Math.Max(g,b)), low=Math.Min(r,Math.Min(g,b));
                    bool cyan=b>=40 && b-r>=14 && g-r>=5 && b>=g*.82;
                    bool gold=r>=60 && r-b>=18 && g>=r*.18 && r>=g*.85;
                    bool purple=b>=40 && r>=15 && b-g>=20 && r-g>=10;
                    bool white=low>=180 && peak-low<55;
                    if((b>=45&&b<=155&&g<=b*.82&&r<=b*.80)||(b>=50&&b<=170&&r>=40&&r<=160&&g<=Math.Min(r,b)*.7))darkInterior++;
                    if (cyan||gold||purple||white) { colored++; if (peak>=threshold) bright++; }
                    sum+=peak;magenta+=r-g;if(purple&&r>=110&&b>=180)violet++;
                }
            double[] corners=new double[4],magentaCorners=new double[4]; int k=0;
            foreach(double dx in new double[]{-.38,.38}) foreach(double dy in new double[]{-.36,.36})
            {
                int x=Math.Max(0,Math.Min(width-1,(int)(cx+cell*dx))), y=Math.Max(0,Math.Min(height-1,(int)(cy+height*dy)));
                int cr,cg,cb;pixels.Read(x,y,out cr,out cg,out cb);magentaCorners[k]=cr-cg;corners[k++]=Math.Max(cr,Math.Max(cg,cb));
            }
            Array.Sort(corners);
            double mean=sum/total, contrast=mean-(corners[1]+corners[2])/2;
            Slot a=new Slot { Signal=colored/(double)total, Bright=bright/(double)total, Contrast=contrast };
            // A bright and a dark acceptance band, with a deliberate unknown band between them.
            Array.Sort(magentaCorners);
            bool purpleFlare=violet/(double)total>=.65&&a.Bright>=.9&&mean>=190&&magenta/total-(magentaCorners[1]+magentaCorners[2])/2>=20;
            bool lit=(a.Bright>=.48 && contrast>=20)||purpleFlare;
            double darkShape=DarkGemShape.Score(pixels,width,height,cx,cy,cell,height);
            bool shapedDark=darkShape>=40 || PurpleCavity(pixels,width,height,cx,cy,cell,height);
            bool dim=shapedDark || (a.Bright<=.10 && darkInterior/(double)total>=.65 && mean<=threshold-14 && (contrast>=9 || contrast<=-18));
            if(shapedDark)lit=false;
            a.Lit=lit; a.Valid=a.Signal>=.44 && (lit||dim);
            a.Reason=a.Signal<.44 ? "颜色不足 / 遮挡" : "明暗或轮廓不确定";
            a.Score=(a.Valid?10:0)+(shapedDark?2:0)+a.Signal+Math.Min(Math.Max(contrast,0),100)/200+(lit?a.Bright:1-a.Bright)*.25;
            return a;
        }
    }

    sealed class Counter
    {
        public int? Baseline, Candidate;
        public double Deadline, LastConfirmationMs;
        public int Triggers;
        
        double candidateSince, invalidSince=-1;
        int candidateFrames;
        public double Remaining(double now) { return Math.Max(0,Deadline-now); }
        public void Forget() { Baseline=null; Candidate=null; candidateFrames=0; invalidSince=-1; }
        public void Reset() { Forget(); Deadline=0; }
        public bool Observe(int? count, double now)
        {
            if (!count.HasValue)
            {
                Candidate=null; candidateFrames=0;
                if(invalidSince<0) invalidSince=now;
                if(now-invalidSince>=.7) Baseline=null; // Uncertain frames never change the active deadline.
                return false;
            }
            invalidSince=-1;
            if(Candidate!=count) { Candidate=count; candidateSince=now; candidateFrames=1; return false; }
            candidateFrames++;
            // Reject isolated frames; no fixed 250 ms delay. Typically 3-4 samples at 20 ms.
            double confirmation=Baseline.HasValue&&count.Value>Baseline.Value?.120:.040;
            if(candidateFrames<3 || now-candidateSince<confirmation-1e-9) return false;
            bool dropped=Baseline.HasValue && count.Value<Baseline.Value;
            Baseline=count;
           
            if(dropped)
            {
                LastConfirmationMs=(now-candidateSince)*1000;
                Deadline=now+15.000; Triggers++; return true;
            }
            return false;
        }
    }

    sealed class Snapshot
    {
        public Reading Reading,ConfirmedReading;
        public RectangleF LeftRegion, RightRegion;
        public Size CaptureSize;
        public int Side;
        
        public double LocateMs;
        public double Remaining, ProcessMs, GapMs, MaxGapMs, ConfirmationMs;
        public double ClockNow,CooldownDeadline;
        public int ResetVersion;
        public int Triggers, SlowSamples;
        public long TriggerStamp;
        public string Status;
        public string Recovery;
        public bool Enabled,Paused;
        public int? Count;
        public bool NoBeans
        {
            get
            {
                Reading display=Enabled?ConfirmedReading:Reading;
                return !Paused && display!=null && display.Valid && display.Count==0;
            }
        }
    }

    sealed class Engine : IDisposable
    {
        readonly object gate=new object();
        readonly Stopwatch time=Stopwatch.StartNew();
        readonly Counter counter=new Counter();
        readonly GameClockWorker gameClock=new GameClockWorker();
        string recovery="等待少豆 · 自动读取游戏时间";
        double recoveryPendingUntil=-1;long recoveryTrigger;
        readonly AutoResetEvent wake=new AutoResetEvent(false);
        readonly Thread worker;
        bool preciseTimer;
        readonly Queue<string> logs=new Queue<string>();
        readonly Queue<Bitmap> frames=new Queue<Bitmap>();
        readonly Queue<double> frameTimes=new Queue<double>();
        IntPtr window;
        RectangleF region;
        int threshold=150, version;
        bool enabled, stopped, automatic,paused;
        long samples;int idle;
        int selectedSide=1;
        RectangleF leftRegion,rightRegion;
        Size lockedSize;
        double locateMs,nextLocate,invalidSince=-1;
        Reading reading,confirmedReading;
        string status="选择 MuMu 窗口后校准";
        double processMs, gapMs, maxGapMs, previousSample=-1, nextFrame;
        int slowSamples;
        long triggerStamp;
        int resetVersion;
        public Engine()
        { WindowCapture.SetPaused(false);preciseTimer=Native.timeBeginPeriod(1)==0;worker=new Thread(Loop) { IsBackground=true, Priority=ThreadPriority.AboveNormal, Name="MuMu ROI capture" }; worker.Start(); }
        public long Samples{get{return Interlocked.Read(ref samples);}}
        public bool PauseCompleted{get{lock(gate)return paused&&Interlocked.CompareExchange(ref idle,0,0)==1&&gameClock.PausedIdle&&WindowCapture.CaptureStopped;}}
        public void SetPaused(bool value)
        {
            lock(gate){if(paused==value)return;paused=value;Interlocked.Exchange(ref idle,0);version++;counter.Forget();previousSample=-1;if(value){enabled=false;status="监测已暂停 · 正在停止捕获";}}
            WindowCapture.SetPaused(value);gameClock.SetPaused(value);wake.Set();
        }
        public void Configure(IntPtr h, RectangleF r, int t, bool run)
        {
            lock(gate) { window=h; automatic=false; region=r; threshold=t; enabled=run&&!paused; version++; counter.Forget(); previousSample=-1; }
            gameClock.Configure(h);wake.Set();
        }
        public void ConfigureAuto(IntPtr h,int side,bool run,bool relocate=false)
        {
            lock(gate)
            {
                bool changed=window!=h||selectedSide!=side||!automatic||relocate;
                window=h;selectedSide=side;automatic=true;enabled=run&&!paused;version++;counter.Forget();previousSample=-1;
                if(changed){region=RectangleF.Empty;leftRegion=rightRegion=RectangleF.Empty;lockedSize=Size.Empty;nextLocate=0;invalidSince=-1;reading=null;}
            }
            gameClock.Configure(h);wake.Set();
        }
        public GameClockWorker Clock { get { return gameClock; } }
        public void Reset() { lock(gate) {counter.Reset();resetVersion++;recoveryPendingUntil=-1;recovery="等待少豆 · 自动读取游戏时间";} }
        static RectangleF Normalize(Rectangle r,Size size)
        {return r.IsEmpty?RectangleF.Empty:new RectangleF(r.X/(float)size.Width,r.Y/(float)size.Height,r.Width/(float)size.Width,r.Height/(float)size.Height);}

        public Snapshot GetSnapshot()
        {
            lock(gate){double now=time.Elapsed.TotalSeconds;return new Snapshot { ClockNow=now,CooldownDeadline=counter.Deadline,ResetVersion=resetVersion,Recovery=recovery,ConfirmedReading=counter.Baseline.HasValue?confirmedReading:null,Side=selectedSide,CaptureSize=lockedSize,LeftRegion=leftRegion,RightRegion=rightRegion,LocateMs=locateMs,Reading=reading, Remaining=counter.Remaining(now), ProcessMs=processMs, GapMs=gapMs, MaxGapMs=maxGapMs, ConfirmationMs=counter.LastConfirmationMs, Triggers=counter.Triggers, SlowSamples=slowSamples, Status=paused?(PauseCompleted?"监测已暂停 · 捕获与识别已停止":WindowCapture.PauseCompleted.IsFaulted?WindowCapture.Status:"正在暂停 · 等待在途采集与识别结束"):status, Enabled=enabled, Paused=paused, Count=counter.Baseline, TriggerStamp=triggerStamp };}
        }
        public Bitmap GetFrame()
        { lock(gate) { if(frames.Count==0) return null; Bitmap last=null; foreach(Bitmap b in frames) last=b; return (Bitmap)last.Clone(); } }
        public string Export(string root)
        {
            string folder=Path.Combine(root,"诊断_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")); Directory.CreateDirectory(folder);
            List<Bitmap> copy=new List<Bitmap>(); string[] rows; double[] times;
            lock(gate) { rows=logs.ToArray(); times=frameTimes.ToArray(); foreach(Bitmap b in frames) copy.Add((Bitmap)b.Clone()); }
            try
            {
                File.WriteAllLines(Path.Combine(folder,"采样记录.csv"), rows, new UTF8Encoding(true));
                for(int i=0;i<copy.Count;i++) copy[i].Save(Path.Combine(folder,string.Format(CultureInfo.InvariantCulture,"roi_{0:000}_{1:0.000}s.png",i,times[i])));
                File.WriteAllText(Path.Combine(folder,"说明.txt"),"仅包含最近约 12 秒识别区域截图和最近约 30 秒的采样记录。未上传。ms 是程序内部计时；不包含游戏到屏幕呈现的延迟。截图约每 100 ms 一张，不适合单独证明 200 ms 端到端延迟。\r\nCSV: t_s,raw_count,valid,process_ms,gap_ms,trigger,remaining_s,reason\r\n",new UTF8Encoding(true));
                gameClock.Export(folder);
                return folder;
            }
            finally { foreach(Bitmap b in copy) b.Dispose(); }
        }
        void Loop()
        {
            while(true)
            {
                IntPtr h; RectangleF r; int t,v,side; bool run,auto,pause; Size size;double invalid,nextSearch;
                lock(gate) { if(stopped) return; h=window;r=region;t=threshold;v=version;run=enabled;auto=automatic;side=selectedSide;size=lockedSize;pause=paused;invalid=invalidSince;nextSearch=nextLocate;Interlocked.Exchange(ref idle,pause?1:0); }
                if(pause){wake.WaitOne();continue;}
                double start=time.Elapsed.TotalSeconds;
                Reading current=null; Bitmap bitmap=null; string state;
                try
                {
                    Rectangle client=Native.ClientBounds(h);
                    if(auto && h!=IntPtr.Zero && client.Width>320 && client.Height>150 && !Native.IsIconic(h))
                    {
                        bool revalidate=!r.IsEmpty&&client.Size==size&&invalid>=0&&start-invalid>=.45;
                        bool search=r.IsEmpty||client.Size!=size||revalidate;
                        if(search && start>=nextSearch && SystemInformation.VirtualScreen.Contains(client))
                        {
                            LocatedPair pair;double began=time.Elapsed.TotalSeconds;
                            using(Bitmap full=WindowCapture.Capture(h,client))pair=AutoLocator.Find(full,t);
                            if(!pair.Left.IsEmpty&&!WindowCapture.Available(h,new Rectangle(client.X+pair.Left.X,client.Y+pair.Left.Y,pair.Left.Width,pair.Left.Height)))pair.Left=Rectangle.Empty;
                            if(!pair.Right.IsEmpty&&!WindowCapture.Available(h,new Rectangle(client.X+pair.Right.X,client.Y+pair.Right.Y,pair.Right.Width,pair.Right.Height)))pair.Right=Rectangle.Empty;
                            RectangleF lr=Normalize(pair.Left,client.Size),rr=Normalize(pair.Right,client.Size);
                            RectangleF found=side==0?lr:rr;
                            Reading located=side==0?pair.LeftReading:pair.RightReading;
                            // A stale locked HUD may move between characters without resizing the
                            // window. Revalidate it, but a black/occluded frame never erases a lock.
                            bool accept=!revalidate||(!found.IsEmpty&&located!=null&&located.Valid);
                            lock(gate)if(v==version)
                            {
                                locateMs=(time.Elapsed.TotalSeconds-began)*1000;nextLocate=time.Elapsed.TotalSeconds+1;
                                if(accept)
                                {
                                    if(client.Size!=size||r!=found)counter.Forget();
                                    if(!revalidate||!lr.IsEmpty)leftRegion=lr;
                                    if(!revalidate||!rr.IsEmpty)rightRegion=rr;
                                    region=r=found;lockedSize=client.Size;invalidSince=-1;
                                }
                            }
                        }
                        else if(client.Size!=size)r=RectangleF.Empty;
                    }
                    Rectangle area=new Rectangle(client.X+(int)(client.Width*r.X),client.Y+(int)(client.Height*r.Y),(int)(client.Width*r.Width),(int)(client.Height*r.Height));
                    if(h==IntPtr.Zero) state="请选择 MuMu 窗口";
                    else if(auto && r.IsEmpty) state="正在寻找"+(side==0?"左侧":"右侧")+"四颗豆子 · 请露出顶部血条和豆子";
                    else if(area.Width>1024 || area.Height>256) state="框选范围过大，请只框住四颗豆子";
                    else if(!WindowCapture.Available(h,area)) state="识别区暂不可见 · 原倒计时继续";
                    else
                    {
                        bitmap=WindowCapture.Capture(h,area);
                        Interlocked.Increment(ref samples);
                        current=Detector.Analyze(bitmap,new Rectangle(Point.Empty,bitmap.Size),t);
                        state=current.Valid ? (run?"监测中 · 少豆立即重置 15 秒":"定位完成 · 点击开始监测") : current.Reason+" · 暂停判定";
                    }
                }
                catch(Exception ex) { state="截图异常："+ex.Message; }
                double now=time.Elapsed.TotalSeconds;
                lock(gate)
                {
                    if(v==version)
                    {
                        processMs=(now-start)*1000;
                        gapMs=previousSample<0?0:(start-previousSample)*1000; previousSample=start;
                        if(run) { maxGapMs=Math.Max(maxGapMs,gapMs); if(gapMs>100) slowSamples++; }
                        bool trigger=run && counter.Observe(current!=null && current.Valid?(int?)current.Count:null,now);
                        if(run&&current!=null&&current.Valid&&counter.Baseline==current.Count)confirmedReading=current;
                        if(run&&current!=null&&current.Valid&&counter.Baseline==0)state="对方已无豆 · 等待豆子恢复";
                        if(trigger) {triggerStamp=Stopwatch.GetTimestamp();recoveryTrigger=triggerStamp;recovery=gameClock.RecoveryAt(triggerStamp);recoveryPendingUntil=recovery.Contains("未确认")?now+.25:-1;if(recoveryPendingUntil>0){recovery="恢复点：读取中";gameClock.Refresh();}}
                        if(recoveryPendingUntil>0){string result=gameClock.RecoveryAt(recoveryTrigger);if(!result.Contains("未确认")){recovery=result;recoveryPendingUntil=-1;}else if(now>=recoveryPendingUntil){recovery="恢复点：时间未确认（见时间校准）";recoveryPendingUntil=-1;}}
                        if(recovery.Contains("未确认") && counter.Remaining(now)>0){
                            ClockState cs=gameClock.State();
                            if(cs.Value.HasValue&&cs.Age>=0&&cs.Age<=.2){recovery=GameClockWorker.LateRecovery(cs.Value,counter.Remaining(now));}
                        }
                        reading=current;status=state;
                        if(auto && bitmap!=null && current!=null && !current.Valid)
                        {
                            if(invalidSince<0)invalidSince=now;
                            // A failed rescan preserves both the old ROI and the active deadline.
                            if(now-invalidSince>.45)status="豆子暂不可确认 · 自动校准位置，原倒计时继续";
                        }
                        else invalidSince=-1;
                        logs.Enqueue(string.Format(CultureInfo.InvariantCulture,"{0:F6},{1},{2},{3:F3},{4:F3},{5},{6:F6},\"{7}\"",now,current==null?"":current.Count.ToString(),current!=null&&current.Valid,processMs,gapMs,trigger,counter.Remaining(now),state.Replace("\"","\"\"")));
                        while(logs.Count>1500) logs.Dequeue();
                        if(bitmap!=null && now>=nextFrame)
                        {
                            frames.Enqueue(bitmap); frameTimes.Enqueue(now); bitmap=null; nextFrame=now+.1;
                            while(frames.Count>120) { frames.Dequeue().Dispose();frameTimes.Dequeue(); }
                        }
                    }
                }
                if(bitmap!=null) bitmap.Dispose();
                // Capture and classification never depend on the WinForms message loop.
                int delay=Math.Max(1,(run?8:100)-(int)((time.Elapsed.TotalSeconds-start)*1000));
                wake.WaitOne(delay);
            }
        }
        public void Dispose()
        {
            gameClock.Dispose();
            lock(gate) stopped=true;
            if(preciseTimer){Native.timeEndPeriod(1);preciseTimer=false;}
            wake.Set();
            if(worker.Join(1500))ReleaseBuffers();else System.Threading.Tasks.Task.Run(()=>{worker.Join();ReleaseBuffers();});
        }
        void ReleaseBuffers(){wake.Dispose();lock(gate){foreach(Bitmap b in frames)b.Dispose();frames.Clear();}WindowCapture.Shutdown();}
    }

    // The HUD owns a message loop, so the settings window cannot stall its clock.
    sealed class HudHost : IDisposable
    {
        readonly Thread thread;
        readonly ManualResetEvent ready=new ManualResetEvent(false);
        Overlay form;Exception startupError;
        volatile bool visible;
        long paintedStamp;int paintedTrigger;
        public bool Visible{get{return visible;}}
        public long PaintedStamp{get{return Interlocked.Read(ref paintedStamp);}}
        public int PaintedTrigger{get{return Interlocked.CompareExchange(ref paintedTrigger,0,0);}}
        public HudHost(Engine engine,Action restore,Action<int> chooseSide=null)
        {
            thread=new Thread(delegate()
            {
                try
                {
                    using(Overlay hud=new Overlay(restore,chooseSide))
                    using(Timer timer=new Timer{Interval=16})
                    {
                        form=hud;int displayedTrigger=0;
                        hud.SurfacePresented+=delegate{if(displayedTrigger>paintedTrigger){Interlocked.Exchange(ref paintedStamp,Stopwatch.GetTimestamp());Interlocked.Exchange(ref paintedTrigger,displayedTrigger);}};
                        timer.Tick+=delegate
                        {
                            Snapshot s=engine.GetSnapshot();displayedTrigger=s.Triggers;timer.Interval=s.Paused?100:16;
                            UpdateHud(hud,s);
                        };
                        IntPtr handle=hud.Handle;timer.Start();ready.Set();Application.Run();timer.Stop();
                    }
                }
                catch(Exception ex){startupError=ex;ready.Set();}
            }){IsBackground=true,Name="Independent countdown HUD"};
            thread.SetApartmentState(ApartmentState.STA);thread.Start();ready.WaitOne();
            if(startupError!=null)throw new InvalidOperationException("悬浮窗启动失败",startupError);
        }
        public void ShowAt(Point p){form.BeginInvoke((Action)delegate{form.Location=p;form.Show();visible=true;});}
        internal static void UpdateHud(Overlay hud,Snapshot s)
        {
            hud.NoBeans=s.NoBeans;hud.ClockLabel.Text=Theme.Clock(s.Remaining);hud.SetSide(s.Side);hud.ClockLabel.ForeColor=Theme.Urgency(s.Remaining);hud.Target.Text=s.NoBeans?"0 / 4 颗 · 等待豆子恢复":s.Recovery;
            Reading display=s.Enabled?s.ConfirmedReading:s.Reading;
            hud.Detail.Text=(!s.Enabled?"已暂停 · ":"")+(display!=null&&display.Valid?display.Count+" / 4 颗":"画面待确认");hud.RefreshSurface();
        }
        public void Hide(){form.BeginInvoke((Action)delegate{form.Hide();visible=false;});}
        internal void SelectSideForTest(int side){form.BeginInvoke((Action)delegate{form.ClickSideForTest(side);});}
        internal bool TestTopMost{get{return (bool)form.Invoke(new Func<bool>(delegate{return form.TopMost;}));}}
        public void Dispose(){if(form!=null&&!form.IsDisposed)form.BeginInvoke((Action)delegate{form.Hide();Application.ExitThread();});if(thread.Join(1500))ready.Dispose();}
    }
    static class Tests
    {
        static readonly List<string> lines = new List<string>();
        static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); lines.Add("PASS  " + label); }
        static void Stable(Counter c, int? n, double start)
        {int samples=c.Baseline.HasValue&&n.HasValue&&n.Value>c.Baseline.Value?7:3;for(int i=0;i<samples;i++)c.Observe(n,start+i*.02);}
        public static void Run(string reference, string goldReference, string folder)
        {
            Directory.CreateDirectory(folder);
            using (Bitmap image = new Bitmap(reference))
            {
                Rectangle r = new Rectangle(1576, 98, 122, 38);
                Reading actual = Detector.Analyze(image, r, 150);
                File.WriteAllText(Path.Combine(folder, "detector-debug.txt"), string.Format("blue valid={0} count={1} signal={2} bright={3} contrast={4}", actual.Valid, actual.Count, string.Join(",", actual.Signal), string.Join(",", actual.Bright), string.Join(",", actual.Contrast)));
                Check(actual.Valid && actual.Count == 3 && !actual.Lit[0] && actual.Lit[1] && actual.Lit[2] && actual.Lit[3], "用户蓝色参考图片：准确识别 1 暗 + 3 亮");
                lines.Add(string.Format("参考图颜色信号：{0:P0}, {1:P0}, {2:P0}, {3:P0}", actual.Signal[0], actual.Signal[1], actual.Signal[2], actual.Signal[3]));
                foreach(int dx in new int[]{-2,0,2})foreach(int dy in new int[]{-2,0,2})
                {Reading shifted=Detector.Analyze(image,new Rectangle(r.X+dx,r.Y+dy,r.Width,r.Height),150);Check(shifted.Valid&&shifted.Count==3,"框选偏移 "+dx+","+dy+"px 仍识别 3 颗");}
                foreach(double gain in new double[]{.80,.9,1.1})
                    using(Bitmap perturbed=image.Clone(r,PixelFormat.Format32bppArgb))
                    {
                        for(int y=0;y<perturbed.Height;y++)for(int x=0;x<perturbed.Width;x++){Color q=perturbed.GetPixel(x,y);perturbed.SetPixel(x,y,Color.FromArgb(Math.Min(255,(int)(q.R*gain)),Math.Min(255,(int)(q.G*gain)),Math.Min(255,(int)(q.B*gain))));}
                        Reading a=Detector.Analyze(perturbed,new Rectangle(Point.Empty,perturbed.Size),150);Check(a.Valid&&a.Count==3,"合成亮度变化 "+gain+" 倍仍识别 3 颗");
                    }
                using(Bitmap blocked=image.Clone(r,PixelFormat.Format32bppArgb))
                {using(Graphics g=Graphics.FromImage(blocked))g.FillRectangle(Brushes.Black,new Rectangle(30,0,31,38));Check(!Detector.Analyze(blocked,new Rectangle(Point.Empty,blocked.Size),150).Valid,"单颗被黑色遮挡不猜成消耗");}
                foreach (double scale in new double[] { .5, .75, 1.5 })
                    using (Bitmap resized = new Bitmap(image, (int)(image.Width * scale), (int)(image.Height * scale)))
                    {
                        Reading scaled = Detector.Analyze(resized, new Rectangle((int)(r.X * scale), (int)(r.Y * scale), (int)(r.Width * scale), (int)(r.Height * scale)), 150);
                        Check(scaled.Valid && scaled.Count == 3, "蓝色参考图缩放 " + scale + " 倍仍识别 3 颗");
                    }
                // Derive all 16 lit/dark combinations from the actual screenshot's bean patches.
                for (int mask = 0; mask < 16; mask++)
                    using (Bitmap fixture = new Bitmap(120, 38))
                    {
                        using (Graphics g = Graphics.FromImage(fixture))
                            for (int i = 0; i < 4; i++)
                                g.DrawImage(image, new Rectangle(i * 30, 0, 30, 38), new Rectangle((mask & (1 << i)) != 0 ? 1607 : 1576, 98, 30, 38), GraphicsUnit.Pixel);
                        Reading a = Detector.Analyze(fixture, new Rectangle(0, 0, 120, 38), 150);
                        int expected = 0; for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) expected++;
                        Check(a.Valid && a.Count == expected, "截图豆子组合 " + mask + " → " + expected + " 亮");
                    }
                using (Bitmap blank = new Bitmap(120, 38))
                    Check(!Detector.Analyze(blank, new Rectangle(0, 0, 120, 38), 150).Valid, "黑屏不当作 0 豆");
                foreach (Color flat in new Color[] { Color.FromArgb(50, 150, 220), Color.FromArgb(255, 190, 20), Color.FromArgb(170, 50, 240), Color.FromArgb(26, 52, 85) })
                    using (Bitmap b = new Bitmap(120, 38))
                    {
                        using (Graphics g = Graphics.FromImage(b)) g.Clear(flat);
                        Check(!Detector.Analyze(b, new Rectangle(0, 0, 120, 38), 150).Valid, "纯色块不当作豆子 " + flat.ToString());
                    }
                using (MainForm form = new MainForm(false)) form.RenderDemo(image, Path.Combine(folder, "界面预览.png"));
            }
            using (Bitmap gold = new Bitmap(goldReference))
            {
                Rectangle region = new Rectangle(1666, 109, 130, 40);
                Reading a = Detector.Analyze(gold, region, 150);
                File.AppendAllText(Path.Combine(folder, "detector-debug.txt"), string.Format("\ngold valid={0} count={1} signal={2} bright={3} contrast={4}", a.Valid, a.Count, string.Join(",", a.Signal), string.Join(",", a.Bright), string.Join(",", a.Contrast)));
                Check(a.Valid && a.Count == 4, "用户金色参考图片：准确识别 4 亮");
                foreach (double scale in new double[] { .5, .75, 1.5 })
                    using (Bitmap resized = new Bitmap(gold, (int)(gold.Width * scale), (int)(gold.Height * scale)))
                    {
                        Reading scaled = Detector.Analyze(resized, new Rectangle((int)(region.X * scale), (int)(region.Y * scale), (int)(region.Width * scale), (int)(region.Height * scale)), 150);
                        Check(scaled.Valid && scaled.Count == 4, "金色参考图缩放 " + scale + " 倍仍识别 4 颗");
                    }
                // Synthetic purple only validates channel independence; it is not a real purple sample.
                using (Bitmap purple = gold.Clone(region, gold.PixelFormat))
                {
                    for (int y = 0; y < purple.Height; y++) for (int x = 0; x < purple.Width; x++)
                    { Color p = purple.GetPixel(x, y); purple.SetPixel(x, y, Color.FromArgb(p.G, p.B, p.R)); }
                    Reading pResult = Detector.Analyze(purple, new Rectangle(Point.Empty, purple.Size), 150);
                    Check(pResult.Valid && pResult.Count == 4, "合成紫色 4 亮验证（非真实紫色样本）");
                }
            }
            Counter c=new Counter(); Stable(c,4,0);Check(c.Triggers==0,"初次建立基准不触发");
            Stable(c,3,1);double first=c.Deadline;
            Check(c.Triggers==1 && Math.Abs(first-16.04)<.000001,"4→3，从确认时刻起精确 15.000 秒");
            Stable(c,2,3);Check(c.Triggers==2 && Math.Abs(c.Deadline-18.04)<.000001 && c.Deadline>first,"计时中 3→2，立即重置完整 15 秒");
            Stable(c,1,4);Check(c.Triggers==3&&Math.Abs(c.Deadline-19.04)<.000001,"连续消耗再次重置");
            Stable(c,3,5);Check(c.Triggers==3&&Math.Abs(c.Deadline-19.04)<.000001,"长豆只更新数量，不重置");
            Stable(c,2,6);Check(c.Triggers==4&&Math.Abs(c.Deadline-21.04)<.000001,"增长后再消耗正常重置");
            Stable(c,2,22);Check(c.Triggers==4&&c.Remaining(22)==0,"保持低豆数不重复触发");
            Stable(c,0,23);Check(c.Triggers==5,"一次减少多颗只重置一次");
            Check(c.Remaining(38.039)>0&&c.Remaining(38.040)==0,"截止时间前 1ms 未结束，截止时归零");
            Counter flicker=new Counter();Stable(flicker,4,0);flicker.Observe(3,1);Stable(flicker,4,1.02);Check(flicker.Triggers==0,"单帧闪烁不触发");
            flicker.Observe(3,2);flicker.Observe(null,2.02);Stable(flicker,4,2.04);Check(flicker.Triggers==0,"模糊帧打断确认，不拿旧帧充数");
            flicker.Observe(null,3);flicker.Observe(null,3.8);Stable(flicker,2,4);Check(flicker.Triggers==0,"长期失去画面后重新建基准，不补算");
            Stable(flicker,1,5);double held=flicker.Deadline;flicker.Observe(null,6);flicker.Observe(null,6.1);Check(flicker.Deadline==held,"短暂识别不清不立即作废计时");flicker.Observe(null,6.18);Check(flicker.Deadline==held,"区域消失180ms仍继续计时，不清零");
            flicker.Reset();Check(flicker.Remaining(9)==0&&!flicker.Baseline.HasValue,"清除计时与基准");
            for(int n=4;n>=1;n--){Counter t=new Counter();Stable(t,n,0);Stable(t,n-1,1);Check(t.Triggers==1,n+"→"+(n-1)+" 触发验证");}
            foreach(double interval in new double[]{.010,.020,.033,.050})
            {
                double worst=0;
                for(int phase=0;phase<10;phase++)
                {
                    Counter t=new Counter();Stable(t,4,0);double onset=1+interval*phase/10;
                    for(double at=1;at<1.6;at+=interval)if(t.Observe(at>=onset?3:4,at)){worst=Math.Max(worst,at-onset);break;}
                    Check(t.Triggers==1,"采样间隔 "+(interval*1000)+"ms 相位 "+phase+" 能触发");
                }
                Check(worst<=.2,"模拟采样 "+(interval*1000)+"ms：最坏确认 "+(worst*1000).ToString("0.0")+"ms ≤200ms（不含采集/显示）");
            }
            using(Bitmap referenceImage=new Bitmap(reference))
            using(Bitmap small=referenceImage.Clone(new Rectangle(1576,98,122,38),PixelFormat.Format32bppArgb))
            {
                for(int i=0;i<50;i++)Detector.Analyze(small,new Rectangle(Point.Empty,small.Size),150);
                List<double> timings=new List<double>();
                for(int i=0;i<500;i++){Stopwatch sw=Stopwatch.StartNew();Detector.Analyze(small,new Rectangle(Point.Empty,small.Size),150);timings.Add(sw.Elapsed.TotalMilliseconds);}
                timings.Sort();lines.Add(string.Format("BENCH  122x38 局部图分类 500 次：P50={0:F3}ms P95={1:F3}ms MAX={2:F3}ms（仅分类，不代表游戏端到端延迟）",timings[250],timings[475],timings[499]));
            }
            if(Environment.GetEnvironmentVariable("BEAN_SCREEN_TEST")=="1") ScreenTest(reference,folder);
            lines.Add("范围：两张真实截图、合成扰动/紫色、状态机、程序内测试窗口。未验证十局失败画面或真实 MuMu 游戏端到端延迟。");
            File.WriteAllLines(Path.Combine(folder,"验证结果.txt"),lines.ToArray(),new UTF8Encoding(true));
        }
        sealed class Fixture : Form
        {
            public Bitmap Frame;
            public Fixture(){ClientSize=new Size(240,76);FormBorderStyle=FormBorderStyle.None;TopMost=true;StartPosition=FormStartPosition.Manual;Location=new Point(40,40);DoubleBuffered=true;Text="豆子识别验证窗口";}
            protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Frame!=null)e.Graphics.DrawImage(Frame,ClientRectangle);}
        }
        static Bitmap MakeFrame(Bitmap image,int count)
        {
            Bitmap b=new Bitmap(120,38);using(Graphics g=Graphics.FromImage(b))for(int i=0;i<4;i++)g.DrawImage(image,new Rectangle(i*30,0,30,38),new Rectangle(i<4-count?1576:1607,98,30,38),GraphicsUnit.Pixel);return b;
        }
        static void ScreenTest(string reference,string folder)
        {
            using(Bitmap image=new Bitmap(reference))
            using(Fixture form=new Fixture())
            using(Engine engine=new Engine())
            using(Bitmap four=MakeFrame(image,4))
            using(Bitmap three=MakeFrame(image,3))
            using(Bitmap two=MakeFrame(image,2))
            using(HudHost hud=new HudHost(engine,delegate{}))
            {
                hud.ShowAt(new Point(300,40));
                form.Frame=four;form.Show();form.Refresh();Application.DoEvents();
                engine.Configure(form.Handle,new RectangleF(0,0,1,1),150,true);
                Stopwatch timeout=Stopwatch.StartNew();
                while(engine.GetSnapshot().Count!=4&&timeout.ElapsedMilliseconds<2000){Application.DoEvents();Thread.Sleep(5);}
                if(engine.GetSnapshot().Count!=4){Snapshot error=engine.GetSnapshot();engine.Export(folder);File.WriteAllText(Path.Combine(folder,"screen-error.txt"),error.Status+" count="+error.Count+" raw="+(error.Reading==null?"null":error.Reading.Count.ToString()));}
                Check(engine.GetSnapshot().Count==4,"本机可见测试窗口：真实屏幕采集建立 4 豆基准");
                List<double> latencies=new List<double>();
                for(int i=0;i<12;i++)
                {
                    // Restore four beans, then alternate 4→3 / 4→2. Includes cooldown resets.
                    form.Frame=four;form.Refresh();Application.DoEvents();timeout.Restart();
                    while(engine.GetSnapshot().Count!=4&&timeout.ElapsedMilliseconds<1000){Application.DoEvents();Thread.Sleep(5);}
                    Check(engine.GetSnapshot().Count==4,"屏幕试验 "+i+" 长豆基准更新");
                    int before=engine.GetSnapshot().Triggers;
                    long changed=Stopwatch.GetTimestamp();form.Frame=i%2==0?three:two;form.Refresh();
                    if(i==6)Thread.Sleep(300); // Simulate a busy UI. Capture worker must still detect independently.
                    timeout.Restart();while(engine.GetSnapshot().Triggers==before&&timeout.ElapsedMilliseconds<1000){Application.DoEvents();Thread.Sleep(5);}
                    Snapshot snap=engine.GetSnapshot();
                    Check(snap.Triggers==before+1,"真实屏幕试验 "+i+" 消耗触发（包括倒计时中重置）");
                    double ms=(snap.TriggerStamp-changed)*1000.0/Stopwatch.Frequency;latencies.Add(ms);
                    timeout.Restart();while(hud.PaintedTrigger<snap.Triggers&&timeout.ElapsedMilliseconds<500){Application.DoEvents();Thread.Sleep(2);}
                    Check(hud.PaintedTrigger==snap.Triggers,"悬浮窗试验 "+i+" 已绘制新计时");
                    double uiMs=(hud.PaintedStamp-changed)*1000.0/Stopwatch.Frequency;
                    lines.Add("DISPLAY  "+i+"：请求绘制变化→计时文字绘制 = "+uiMs.ToString("F2")+"ms"+(i==6?"，包含故意阻塞 UI 300ms":""));
                    lines.Add("SCREEN  "+i+"：请求绘制变化→后台触发 = "+ms.ToString("F2")+"ms"+(i==6?"，UI 阻塞 300ms":""));
                    // Report misses against the user's budget honestly, rather than hiding a slow trial.
                    lines.Add((ms<=200?"BUDGET OK  ":"BUDGET MISS  ")+i+" / 200ms");
                }
                engine.Export(folder);form.Hide();hud.Hide();latencies.Sort();
                lines.Add(string.Format("SCREEN SUMMARY  12 次本机测试窗口：P50={0:F2}ms MAX={1:F2}ms。包含 GDI 屏幕采集和识别，不包含 MuMu 渲染和计时文本下一次刷新。",latencies[6],latencies[11]));
            }
        }
    }

}







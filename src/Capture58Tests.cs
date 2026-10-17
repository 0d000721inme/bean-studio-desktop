using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MuMuBeans
{
    // Headless regression checks. No game, desktop/window capture or visible
    // test window is created; all images below are synthetic bitmaps.
    static class Capture58Tests
    {
        static readonly List<string> rows=new List<string>();
        static int failures;
        static void Check(bool value,string name){rows.Add((value?"PASS ":"FAIL ")+name);if(!value)failures++;}
        static bool SameReading(Reading a,Reading b)
        {
            if(a.Valid!=b.Valid||a.Reason!=b.Reason||a.Count!=b.Count)return false;
            for(int i=0;i<4;i++)if(a.Lit[i]!=b.Lit[i]||a.Signal[i]!=b.Signal[i]||a.Bright[i]!=b.Bright[i]||a.Contrast[i]!=b.Contrast[i])return false;
            return true;
        }
        static Bitmap Gems(Color fill)
        {
            Bitmap image=new Bitmap(128,40,PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(image))
            using(Brush brush=new SolidBrush(fill))
            {
                g.Clear(Color.FromArgb(24,32,43));
                for(int i=0;i<4;i++){int x=i*32+16;g.FillPolygon(brush,new Point[]{new Point(x,2),new Point(x+12,20),new Point(x,38),new Point(x-12,20)});}
            }
            return image;
        }
        public static void Run(string[] args)
        {
            rows.Clear();failures=0;
            using(Bitmap image=Gems(Color.Cyan))
            using(Bitmap copy=(Bitmap)image.Clone())
            {
                Rectangle area=new Rectangle(Point.Empty,image.Size);
                PixelMap original=new PixelMap(image,area),duplicate=new PixelMap(copy,area);
                Check(original.SamePixels(duplicate),"identical fresh ROI may reuse classification");
                copy.SetPixel(112,20,Color.FromArgb(16,41,94));
                Check(!original.SamePixels(new PixelMap(copy,area)),"one changed bean pixel invalidates exact cache");
                copy.SetPixel(112,20,image.GetPixel(112,20));copy.SetPixel(0,0,Color.FromArgb(23,32,43));
                Check(!original.SamePixels(new PixelMap(copy,area)),"even an edge pixel invalidates exact cache");
                Check(!original.SamePixels(new PixelMap(image,new Rectangle(0,0,64,40))),"different ROI dimensions cannot reuse cache");
            }
            foreach(Color color in new Color[]{Color.Cyan,Color.Gold,Color.FromArgb(209,44,245),Color.FromArgb(34,72,113)})
            using(Bitmap image=Gems(color))
            {
                Rectangle area=new Rectangle(Point.Empty,image.Size);
                Check(SameReading(Detector.Analyze(image,area,150),Detector.Analyze(new PixelMap(image,area),image.Width,image.Height,150)),"bitmap and cached-pixel detector agree for "+color.ToArgb());
            }
            Counter counter=new Counter();
            counter.Observe(4,0);counter.Observe(4,.02);counter.Observe(4,.04);
            Check(counter.Baseline==4&&counter.Triggers==0,"initial three fresh observations establish baseline only");
            counter.Observe(3,.06);counter.Observe(3,.08);
            Check(counter.Triggers==0,"two fresh observations do not prematurely confirm decrease");
            Check(counter.Observe(3,.10)&&Math.Abs(counter.Deadline-15.10)<1e-9,"third fresh observation after 40 ms starts complete 15 seconds");
            counter.Observe(4,.12);counter.Observe(4,.14);counter.Observe(4,.24);
            Check(counter.Baseline==4&&counter.Triggers==1&&Math.Abs(counter.Deadline-15.10)<1e-9,"confirmed growth does not restart deadline");
            counter.Observe(3,.26);counter.Observe(3,.28);counter.Observe(3,.30);
            Check(counter.Triggers==2&&Math.Abs(counter.Deadline-15.30)<1e-9,"later real decrease restarts full 15 seconds");
            counter.Observe(null,.40);counter.Observe(null,.80);counter.Observe(null,1.11);
            Check(!counter.Baseline.HasValue&&Math.Abs(counter.Deadline-15.30)<1e-9,"unknown/black frames forget baseline without clearing active deadline");
            Snapshot empty=new Snapshot{Enabled=true,ConfirmedReading=new Reading(),Paused=false};
            Check(empty.NoBeans,"four confirmed empty slots retain explicit no-beans state");
            empty.Paused=true;Check(!empty.NoBeans,"pause does not present a live no-beans confirmation");

            bool wasPaused=WindowCapture.Paused;int oldRate=WindowCapture.MaximumReadbacks;
            try
            {
                WindowCapture.SetReadbackRate(300);Check(WindowCapture.MaximumReadbacks==60,"GPU readback budget has a 60 Hz ceiling");
                WindowCapture.SetReadbackRate(20);Check(WindowCapture.MaximumReadbacks==20,"preview uses reduced readback budget");
                WindowCapture.SetPaused(true);WindowCapture.PauseCompleted.Wait();long before=WindowCapture.Readbacks;bool rejected=false;
                try{using(Bitmap unused=WindowCapture.CaptureOnce(IntPtr.Zero,Rectangle.Empty)){}}catch(InvalidOperationException){rejected=true;}
                Check(rejected&&WindowCapture.Paused&&WindowCapture.CaptureStopped&&WindowCapture.Readbacks==before,"failed paused one-shot never opens WGC or resumes monitoring");
            }
            finally{WindowCapture.SetReadbackRate(oldRate);if(!wasPaused)WindowCapture.SetPaused(false);}
            rows.Add("FAILURES="+failures);
            string report=args.Length>1?args[1]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"capture58-tests.txt");
            string folder=Path.GetDirectoryName(Path.GetFullPath(report));Directory.CreateDirectory(folder);File.WriteAllLines(report,rows.ToArray());
            Environment.ExitCode=failures==0?0:1;
        }
    }
}

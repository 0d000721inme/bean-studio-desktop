using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace MuMuBeans
{
    sealed class PerformancePanel : Control
    {
        PerformanceSnapshot snapshot;
        int inspected = -1;
        readonly Font captionFont = new Font("Microsoft YaHei UI", 9);
        readonly Font valueFont = new Font("Bahnschrift", 18);
        readonly Font noteFont = new Font("Microsoft YaHei UI", 8);
        readonly ToolTip tip = new ToolTip();
        public PerformancePanel()
        {
            DoubleBuffered = true; TabStop = true; BackColor = Color.White;
            AccessibleName = "本程序 CPU、GPU 与内存占用，最近 60 秒";
            AccessibleDescription = "方向键查看样本；百分比是本程序占用，不是整机。GPU 无数据时显示未提供。";
            tip.SetToolTip(this, "每秒采样一次，保存最近 60 点。CPU 按整机逻辑核心归一化；内存为工作集；GPU 为当前程序最忙的引擎。方向键查看历史。 ");
        }
        public void SetSnapshot(PerformanceSnapshot value)
        {
            snapshot = value;
            if (inspected >= 0 && value != null && inspected >= value.Samples.Length) inspected = value.Samples.Length - 1;
            PerformanceSample sample = value == null ? null : value.Latest;
            AccessibleDescription = sample == null ? "等待本程序资源采样" : Describe(sample);
            Invalidate();
        }
        static string Number(double? value, string format, string unit)
        { return value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) + unit : "—"; }
        static string Describe(PerformanceSample s)
        { return "CPU " + Number(s.CpuPercent,"0.0","%") + "，GPU " + Number(s.GpuPercent,"0.0","%") + "，内存 " + Number(s.WorkingSetMb,"0.0"," MB"); }
        protected override bool IsInputKey(Keys keyData)
        { return (keyData & Keys.KeyCode) == Keys.Left || (keyData & Keys.KeyCode) == Keys.Right || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e); if (snapshot == null || snapshot.Samples.Length == 0) return;
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
            { inspected = Math.Max(0, Math.Min(snapshot.Samples.Length-1, (inspected < 0 ? snapshot.Samples.Length-1 : inspected) + (e.KeyCode == Keys.Left ? -1 : 1))); e.Handled = true; Invalidate(); }
            if (e.KeyCode == Keys.Escape) { inspected = -1; Invalidate(); }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e); if (snapshot == null || snapshot.Samples.Length == 0) return;
            int width = Math.Max(1, Width / 3), localX = e.X % width;
            inspected = Math.Max(0, Math.Min(snapshot.Samples.Length-1,(int)Math.Round((localX-16) / (double)Math.Max(1,width-32) * (snapshot.Samples.Length-1)))); Invalidate();
        }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); inspected = -1; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); inspected = -1; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            int columnWidth = Width / 3;
            PerformanceSample latest = snapshot == null ? null : snapshot.Latest;
            PerformanceSample[] samples = snapshot == null ? new PerformanceSample[0] : snapshot.Samples;
            if (inspected >= 0 && inspected < samples.Length) latest = samples[inspected];
            for (int metric=0;metric<3;metric++)
            {
                int x = metric*columnWidth;
                if (metric>0) using(Pen divider = new Pen(Color.FromArgb(231,235,232))) g.DrawLine(divider,x,13,x,Height-13);
                string title = metric==0 ? "CPU" : metric==1 ? "GPU" : "内存工作集";
                string value = latest==null ? "—" : metric==0 ? Number(latest.CpuPercent,"0.0","%") : metric==1 ? Number(latest.GpuPercent,"0.0","%") : Number(latest.WorkingSetMb,"0.0"," MB");
                TextRenderer.DrawText(g,title,captionFont,new Rectangle(x+16,10,columnWidth-32,20),Theme.Muted,TextFormatFlags.Left);
                TextRenderer.DrawText(g,value,valueFont,new Rectangle(x+14,30,columnWidth-28,30),Theme.Ink,TextFormatFlags.Left);
                string note = latest==null ? "等待采样" : metric==0 ? (latest.CpuPercent.HasValue?"本程序 / 整机核心":latest.CpuStatus) : metric==1 ? (latest.GpuPercent.HasValue ? "本程序最忙 GPU 引擎" : latest.GpuStatus) : (latest.MemoryPercent.HasValue ? "占物理内存 " + Number(latest.MemoryPercent,"0.00","%") : latest.MemoryStatus);
                TextRenderer.DrawText(g,note,noteFont,new Rectangle(x+16,62,columnWidth-32,18),Theme.Muted,TextFormatFlags.EndEllipsis);
                Rectangle plot = new Rectangle(x+16,87,Math.Max(1,columnWidth-32),Math.Max(8,Height-112));
                using(Pen baseline=new Pen(Color.FromArgb(223,229,225))) g.DrawLine(baseline,plot.Left,plot.Bottom,plot.Right,plot.Bottom);
                double ceiling = metric==0?5:100;
                if(metric==0)foreach(PerformanceSample s in samples)if(s.CpuPercent.HasValue)ceiling=Math.Max(ceiling,Math.Min(100,Math.Ceiling(s.CpuPercent.Value/5)*5));
                if(metric==2) { ceiling=32;foreach(PerformanceSample s in samples) if(s.WorkingSetMb.HasValue) ceiling=Math.Max(ceiling,Math.Ceiling(s.WorkingSetMb.Value/32+1)*32); }
                PointF? previous=null;
                using(Pen curve=new Pen(metric==0 ? Theme.Blue : metric==1 ? Color.FromArgb(98,113,132) : Color.FromArgb(113,126,100),1.6f))
                {
                    for(int i=0;i<samples.Length;i++)
                    {
                        double? raw=metric==0?samples[i].CpuPercent:metric==1?samples[i].GpuPercent:samples[i].WorkingSetMb;
                        if(!raw.HasValue){previous=null;continue;}
                        float px=plot.Left+(samples.Length==1?plot.Width: i/(float)(samples.Length-1)*plot.Width);
                        float py=plot.Bottom-(float)Math.Min(1,Math.Max(0,raw.Value/ceiling))*plot.Height;
                        PointF point=new PointF(px,py);if(previous.HasValue)g.DrawLine(curve,previous.Value,point);previous=point;
                        if(i==inspected)using(Brush b=new SolidBrush(curve.Color))g.FillEllipse(b,px-3,py-3,6,6);
                    }
                }
                string legend=inspected>=0 && latest!=null ? "历史 " + latest.ElapsedSeconds.ToString("0",CultureInfo.InvariantCulture)+" 秒" : "最近 60 点  ·  0–"+ceiling.ToString("0",CultureInfo.InvariantCulture)+(metric==2?" MB":"%");
                TextRenderer.DrawText(g,legend,noteFont,new Rectangle(x+16,Height-21,columnWidth-32,18),Theme.Muted,TextFormatFlags.Left);
            }
            if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle(2,2,Width-5,Height-5),Theme.Blue,BackColor);
        }
        protected override void Dispose(bool disposing)
        { if(disposing){captionFont.Dispose();valueFont.Dispose();noteFont.Dispose();tip.Dispose();}base.Dispose(disposing); }
    }
}

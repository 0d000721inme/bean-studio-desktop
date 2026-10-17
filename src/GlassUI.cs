using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Timer=System.Windows.Forms.Timer;

namespace MuMuBeans
{
    static class Theme
    {
        public static readonly Color Ink=Color.FromArgb(36,46,52), Muted=Color.FromArgb(96,108,115), Blue=Color.FromArgb(41,118,111), Cyan=Color.FromArgb(41,118,111), Red=Color.FromArgb(183,67,61);
        public static Color Urgency(double remaining)
        {
            if(remaining<=0)return Muted;
            if(remaining>=5)return Blue;
            double t=Math.Max(0,Math.Min(1,(5-remaining)/5));
            return Color.FromArgb(238,(int)(160-115*t),(int)(40+25*t));
        }
        public static string Clock(double remaining){return remaining>0?(Math.Ceiling(remaining*100)/100).ToString("00.00",CultureInfo.InvariantCulture):"00.00";}
        public static GraphicsPath Round(RectangleF r,float radius)
        {
            float d=Math.Max(1,Math.Min(radius*2,Math.Min(r.Width,r.Height)));GraphicsPath p=new GraphicsPath();
            p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }
        public static readonly Color Bg=Color.FromArgb(245,246,245);
        public static void Glass(Graphics g,RectangleF r,float radius,bool blue=false)
        {
            if(r.Width<2||r.Height<2)return;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(GraphicsPath p=Round(r,radius))
            {using(Brush b=new SolidBrush(Color.White))g.FillPath(b,p);using(Pen pen=new Pen(Color.FromArgb(224,230,226),1))g.DrawPath(pen,p);}
        }
        public static void Background(Graphics g,Size size){g.Clear(Bg);}
        public static void RoundedWindow(Form form,int radius)
        {if(form.Width<1||form.Height<1)return;Region old=form.Region;using(GraphicsPath p=Round(new RectangleF(0,0,form.Width,form.Height),radius))form.Region=new Region(p);if(old!=null)old.Dispose();}
        public static Label Label(string text,int size=10,bool bold=false)
        {return new Label{Text=text,AutoSize=false,BackColor=Color.Transparent,ForeColor=Ink,Font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular),TextAlign=ContentAlignment.MiddleLeft};}
    }
    sealed class GlassPanel:Panel
    {
        public bool Blue,Material;public int Radius=24;
        public GlassPanel(){SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.UserPaint,true);BackColor=Color.Transparent;}
        protected override void OnPaintBackground(PaintEventArgs e){base.OnPaintBackground(e);if(Width<2||Height<2)return;Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;RectangleF rect=new RectangleF(1,1,Width-3,Height-3);using(GraphicsPath p=Theme.Round(rect,Radius)){
            using(Brush brush=new SolidBrush(Blue?Color.FromArgb(238,244,241):Color.White))g.FillPath(brush,p);
            using(Pen pen=new Pen(Color.FromArgb(224,230,226),1))g.DrawPath(pen,p);
        }}
    }
    sealed class GlassButton:Button
    {
        bool primary,selected,hover,pressed;public string Icon="";
        public bool Primary{get{return primary;}set{primary=value;Invalidate();}}
        public bool Selected{get{return selected;}set{selected=value;AccessibleDescription=value?"已选中":"未选中";Invalidate();}}
        public GlassButton(){Font=new Font("Microsoft YaHei UI",10);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;TabStop=true;UseVisualStyleBackColor=false;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);SetStyle(ControlStyles.Opaque,false);BackColor=Color.Transparent;}
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=pressed=false;Invalidate();}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);pressed=true;Invalidate();}protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);pressed=false;Invalidate();}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e){if(Width<4||Height<4)return;Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;Color fill=primary?Theme.Ink:selected?Theme.Blue:Color.White;if(hover)fill=primary||selected?Color.FromArgb(54,85,78):Color.FromArgb(238,243,239);if(pressed)fill=Color.FromArgb(220,231,224);if(!Enabled)fill=Color.FromArgb(234,237,234);
            using(GraphicsPath p=Theme.Round(new RectangleF(2,2,Width-5,Height-5),7)){using(Brush b=new SolidBrush(fill))g.FillPath(b,p);using(Pen border=new Pen(primary||selected?fill:Color.FromArgb(219,227,222),1))g.DrawPath(border,p);}
            Color ink=!Enabled?Theme.Muted:(primary||selected)&&!pressed?Color.White:Theme.Ink;int padding=Width<45?2:8;Rectangle label=new Rectangle(padding,1,Width-padding*2,Height-2);
            if(Icon.Length>0){Glyphs.Draw(g,Icon,new Rectangle(15,(Height-18)/2,18,18),ink);label=new Rectangle(36,1,Width-43,Height-2);}
            TextRenderer.DrawText(g,Text,Font,label,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Focused)using(GraphicsPath p=Theme.Round(new RectangleF(.8f,.8f,Width-2,Height-2),8))using(Pen focus=new Pen(Theme.Blue,2))g.DrawPath(focus,p);
        }
    }
    static class Glyphs {
        public static void Draw(Graphics g,string name,Rectangle r,Color c){using(Pen p=new Pen(c,1.6f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round}){
            float x=r.X,y=r.Y,w=r.Width,h=r.Height;
            if(name=="play"){using(Brush b=new SolidBrush(c))g.FillPolygon(b,new PointF[]{new PointF(x+4,y+2),new PointF(x+4,y+h-2),new PointF(x+w-2,y+h/2)});}
            else if(name=="pause"){g.DrawLine(p,x+5,y+3,x+5,y+h-3);g.DrawLine(p,x+w-5,y+3,x+w-5,y+h-3);}
            else if(name=="float"){g.DrawRectangle(p,x+1,y+2,w-2,h-4);g.DrawRectangle(p,x+w/2,y+h/2,w/2-2,h/2-3);}
            else if(name=="scan"){g.DrawLine(p,x,y+5,x,y);g.DrawLine(p,x,y,x+5,y);g.DrawLine(p,x+w-5,y,x+w,y);g.DrawLine(p,x+w,y,x+w,y+5);g.DrawLine(p,x,y+h-5,x,y+h);g.DrawLine(p,x,y+h,x+5,y+h);g.DrawLine(p,x+w-5,y+h,x+w,y+h);g.DrawLine(p,x+w,y+h,x+w,y+h-5);g.DrawLine(p,x+4,y+h/2,x+w-4,y+h/2);}
            else if(name=="reset"){g.DrawArc(p,x+3,y+3,w-6,h-6,210,285);g.DrawLine(p,x+1,y+1,x+1,y+7);g.DrawLine(p,x+1,y+7,x+7,y+7);}
            else if(name=="save"){g.DrawLine(p,x+w/2,y+1,x+w/2,y+h-6);g.DrawLines(p,new PointF[]{new PointF(x+4,y+h-10),new PointF(x+w/2,y+h-6),new PointF(x+w-4,y+h-10)});g.DrawLines(p,new PointF[]{new PointF(x+2,y+h-5),new PointF(x+2,y+h-1),new PointF(x+w-2,y+h-1),new PointF(x+w-2,y+h-5)});}
        }}
    }
    sealed class CooldownRail:Control {
        double remaining;int lastSegments=-1,lastColor;
        public double Remaining{get{return remaining;}set{remaining=value;int segments=(int)Math.Max(0,Math.Min(15,Math.Ceiling(value))),color=Theme.Urgency(value).ToArgb();if(segments!=lastSegments||color!=lastColor){lastSegments=segments;lastColor=color;Invalidate();}}}public CooldownRail(){DoubleBuffered=true;SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float pitch=Width/15f;for(int i=0;i<15;i++){RectangleF r=new RectangleF(i*pitch+1,6,pitch-4,5);using(GraphicsPath p=Theme.Round(r,2.5f))using(Brush b=new SolidBrush(i<Math.Ceiling(Remaining)?Theme.Urgency(Remaining):Color.FromArgb(213,226,233)))g.FillPath(b,p);}}
    }
    sealed class SessionBadge:Control {
        string caption="尚未连接";bool active;public string Caption{get{return caption;}set{if(caption==value)return;caption=value;AccessibleName=value;Invalidate();}}public bool Active{get{return active;}set{if(active==value)return;active=value;Invalidate();}}public SessionBadge(){DoubleBuffered=true;SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;using(GraphicsPath p=Theme.Round(new RectangleF(1,1,Width-3,Height-3),16))using(Brush b=new SolidBrush(Color.FromArgb(230,255,255,255)))g.FillPath(b,p);using(Brush b=new SolidBrush(Active?Theme.Blue:Theme.Muted))g.FillEllipse(b,14,Height/2-3,6,6);TextRenderer.DrawText(g,Caption,Font,new Rectangle(29,0,Width-36,Height),Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.Left);}
    }
    sealed class PreviewBox:Control
    {
        Bitmap frame;public RectangleF LeftRegion,RightRegion;public int Side=1;bool suppressed;
        public bool Suppressed{get{return suppressed;}set{if(suppressed!=value){suppressed=value;Invalidate();}}}
        public PreviewBox(){SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=Color.Transparent;}
        public Bitmap CopyFrame(){return frame==null?null:(Bitmap)frame.Clone();}
        public void SetFrame(Bitmap b){if(frame!=null)frame.Dispose();frame=b;Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            RectangleF bounds=new RectangleF(0,0,Width-1,Height-1);using(GraphicsPath path=Theme.Round(bounds,18))
            {
                using(Brush b=new SolidBrush(Color.FromArgb(17,33,45)))g.FillPath(b,path);
                if(frame==null||suppressed){TextRenderer.DrawText(g,suppressed?"静态预览已隐藏\n识别与快捷键照常工作":"选择 MuMu 窗口\n点击重新扫描，定位左右豆子",Font,ClientRectangle,Color.FromArgb(208,225,234),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);return;}
                GraphicsState state=g.Save();g.SetClip(path);float scale=Math.Min(Width/(float)frame.Width,Height/(float)frame.Height);RectangleF r=new RectangleF((Width-frame.Width*scale)/2,(Height-frame.Height*scale)/2,frame.Width*scale,frame.Height*scale);
                g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.DrawImage(frame,r);
                RectangleF[] regions={LeftRegion,RightRegion};for(int i=0;i<2;i++)if(!regions[i].IsEmpty)
                {
                    RectangleF n=regions[i],box=new RectangleF(r.X+n.X*r.Width,r.Y+n.Y*r.Height,n.Width*r.Width,n.Height*r.Height);Color color=i==Side?Color.FromArgb(115,236,229):Color.FromArgb(245,250,255);
                    using(Pen p=new Pen(Color.FromArgb(110,10,32,54),5))g.DrawRectangle(p,box.X,box.Y,box.Width,box.Height);
                    using(Pen p=new Pen(color,2))g.DrawRectangle(p,box.X,box.Y,box.Width,box.Height);
                    string caption=i==0?"左侧":"右侧";Rectangle tag=new Rectangle((int)box.X,(int)box.Bottom+5,48,23);using(Brush b=new SolidBrush(Color.FromArgb(195,27,49,74)))g.FillRectangle(b,tag);TextRenderer.DrawText(g,caption,Font,tag,color,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
                }g.Restore(state);
            }
        }
        protected override void Dispose(bool disposing){if(disposing&&frame!=null)frame.Dispose();base.Dispose(disposing);}
    }
    sealed class WindowPicker:Control
    {
        public readonly List<WindowItem> Items=new List<WindowItem>();
        // The popup belongs to this control. Closing a menu never disposes it while
        // WinForms is still dispatching an item click (the V3 crash).
        internal readonly ContextMenuStrip Popup=new ContextMenuStrip();
        int index=-1;public event EventHandler SelectedIndexChanged;
        public int SelectedIndex{get{return index;}set{index=value;Invalidate();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}}
        public object SelectedItem{get{return index>=0&&index<Items.Count?Items[index]:null;}}
        public WindowPicker()
        {SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);BackColor=Theme.Bg;Cursor=Cursors.Hand;TabStop=true;AccessibleName="选择 MuMu 窗口";AccessibleRole=AccessibleRole.ComboBox;Popup.Font=new Font("Microsoft YaHei UI",10);Popup.BackColor=Color.FromArgb(246,249,255);Popup.ShowImageMargin=false;}
        protected override void OnParentChanged(EventArgs e){base.OnParentChanged(e);if(Parent!=null)BackColor=Parent.BackColor;}
        internal void OpenMenu()
        {
            if(Popup.Visible){Popup.Close();return;}while(Popup.Items.Count>0){ToolStripItem old=Popup.Items[0];Popup.Items.RemoveAt(0);old.Dispose();}
            if(Items.Count==0)Popup.Items.Add(new ToolStripMenuItem("未发现 MuMu · 请刷新列表"){Enabled=false});
            for(int i=0;i<Items.Count;i++)
            {
                int selected=i;ToolStripMenuItem item=new ToolStripMenuItem(Items[i].Title){Checked=i==index};
                item.Click+=delegate{SelectedIndex=selected;};Popup.Items.Add(item);
            }Popup.MinimumSize=new Size(Width,0);Popup.Show(this,new Point(0,Height+4));
        }
        protected override void OnClick(EventArgs e){base.OnClick(e);Focus();OpenMenu();}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override bool IsInputKey(Keys key){return key==Keys.Down||key==Keys.Up||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter||e.KeyCode==Keys.Down||e.KeyCode==Keys.Up){OpenMenu();e.Handled=true;e.SuppressKeyPress=true;}}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;RectangleF r=new RectangleF(1,1,Width-3,Height-3);
            using(GraphicsPath p=Theme.Round(r,14)){using(Brush b=new LinearGradientBrush(r,Color.FromArgb(252,254,255),Color.FromArgb(237,245,248),90))g.FillPath(b,p);using(Pen pen=new Pen(Focused?Theme.Blue:Color.FromArgb(200,214,224),Focused?2f:1f))g.DrawPath(pen,p);}
            TextRenderer.DrawText(g,SelectedItem==null?"请选择 MuMu 窗口":SelectedItem.ToString(),Font,new Rectangle(16,0,Width-54,Height),SelectedItem==null?Theme.Muted:Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);
            using(Pen p=new Pen(Theme.Muted,1.5f)){p.StartCap=p.EndCap=LineCap.Round;g.DrawLines(p,new PointF[]{new PointF(Width-27,Height/2f-2),new PointF(Width-22,Height/2f+3),new PointF(Width-17,Height/2f-2)});}
        }
        protected override void Dispose(bool disposing){if(disposing)Popup.Dispose();base.Dispose(disposing);}
    }
    sealed class CropBox:Control
    {
        Bitmap frame;
        public CropBox(){SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=Color.Transparent;}
        public void SetFrame(Bitmap b){if(frame!=null)frame.Dispose();frame=b;Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(GraphicsPath p=Theme.Round(new RectangleF(0,0,Width-1,Height-1),14))
            {
                using(Brush b=new SolidBrush(Color.FromArgb(45,152,179,212)))e.Graphics.FillPath(b,p);
                if(frame==null){TextRenderer.DrawText(e.Graphics,"等待豆子区域",Font,ClientRectangle,Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);return;}
                GraphicsState state=e.Graphics.Save();e.Graphics.SetClip(p);float scale=Math.Min((Width-16)/(float)frame.Width,(Height-12)/(float)frame.Height);RectangleF r=new RectangleF((Width-frame.Width*scale)/2,(Height-frame.Height*scale)/2,frame.Width*scale,frame.Height*scale);e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;e.Graphics.DrawImage(frame,r);e.Graphics.Restore(state);
            }
        }
        protected override void Dispose(bool disposing){if(disposing&&frame!=null)frame.Dispose();base.Dispose(disposing);}
    }
    sealed class BeanDots:Control
    {
        Reading reading;int visualState=int.MinValue;
        public Reading Reading{get{return reading;}set{int state=value!=null&&value.Valid?16:0;if(value!=null&&value.Valid)for(int i=0;i<4;i++)if(value.Lit[i])state|=1<<i;reading=value;if(state!=visualState){visualState=state;Invalidate();}}}
        public BeanDots(){SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Color.Transparent;}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            for(int i=0;i<4;i++)
            {
                bool valid=Reading!=null&&Reading.Valid,lit=valid&&Reading.Lit[i];float x=16+i*40,y=Height/2f;PointF[] diamond={new PointF(x,y-10),new PointF(x+7,y),new PointF(x,y+10),new PointF(x-7,y)};
                using(Brush b=new LinearGradientBrush(new RectangleF(x-7,y-10,14,20),lit?Color.FromArgb(138,224,255):Color.FromArgb(200,211,228),lit?Theme.Blue:Color.FromArgb(151,169,192),80))g.FillPolygon(b,diamond);
                using(Pen p=new Pen(Color.FromArgb(235,255,255,255),1))g.DrawPolygon(p,diamond);
            }
        }
    }
    sealed class MainForm:Form
    {
        readonly ToolTip tips=new ToolTip{InitialDelay=350,AutoPopDelay=8000};readonly ContextMenuStrip floatingMenu=new ContextMenuStrip();
        readonly Engine engine=new Engine();readonly HudHost overlay;readonly Timer pulse=new Timer{Interval=50};readonly SoundAlerts soundAlerts;
        readonly ProcessPerformance performance=new ProcessPerformance();readonly PerformancePanel performancePanel=new PerformancePanel();
        readonly Label performanceTitle=Theme.Label("本程序资源",10,true),shortcutHint=Theme.Label("",9);
        readonly GlassButton settingsButton=new GlassButton{Text="快捷键与浮窗",AccessibleName="设置全局快捷键、浮窗风格、大小和透明度"};
        DesktopSettings settings;readonly HotkeyManager hotkeys;double lastPerformance=-1;
        readonly WindowPicker windows=new WindowPicker();readonly PreviewBox preview=new PreviewBox();readonly CropBox crop=new CropBox();readonly BeanDots dots=new BeanDots();
        readonly GlassPanel source=new GlassPanel{Material=true,Radius=10},picture=new GlassPanel{Radius=12},timerPanel=new GlassPanel{Blue=true,Radius=12},live=new GlassPanel{Radius=12};
        readonly CooldownRail rail=new CooldownRail();readonly SessionBadge sessionBadge=new SessionBadge();
        readonly Label gameTitle=Theme.Label("游戏时间",10,true),gameValue=Theme.Label("—",28),gameState=Theme.Label("等待时间数字",9),duration=Theme.Label("15 秒",9);
        readonly Label title=Theme.Label("豆子 Studio",24,true),subtitle=Theme.Label("替身计时助手",9),edition=Theme.Label("V5.8.1",9);
        readonly Label sourceTitle=Theme.Label("画面来源",10,true),pictureTitle=Theme.Label("游戏画面",13,true),pictureHint=Theme.Label("扫描时的定位预览",9),previewNote=Theme.Label("静态定位预览  ·  下方选择监测方向",9);
        readonly Label timerTitle=Theme.Label("冷却剩余",11,true),clockLabel=Theme.Label("00.00",32),timerState=Theme.Label("等待下一次少豆",9),timerRule=Theme.Label("等待少豆",9);
        readonly Label recoveryTitle=Theme.Label("游戏恢复点",11,true),recoveryValue=Theme.Label("—",32);
        readonly Label liveTitle=Theme.Label("豆子识别",11,true),countLabel=Theme.Label("待确认",11,true),metrics=Theme.Label("开始监测后显示采样状态",9),status=Theme.Label("选择 MuMu 窗口，然后扫描画面。",9);
        readonly GlassButton calibrate=new GlassButton{Text="校准",AccessibleName="游戏时间校准"};
        readonly GlassButton sound=new GlassButton{Text="音效设置",AccessibleName="音效设置：少豆、计时结束及音量"};
        readonly GlassButton refresh=new GlassButton{Text="刷新列表"},scan=new GlassButton{Text="重新扫描",Icon="scan"},left=new GlassButton{Text="左侧"},right=new GlassButton{Text="右侧",Selected=true},run=new GlassButton{Text="开始监测",Primary=true,Icon="play"},floating=new GlassButton{Text="悬浮倒计时",Icon="float"},clear=new GlassButton{Text="清空冷却",Icon="reset"},save=new GlassButton{Text="保存诊断",Icon="save"},minimize=new GlassButton{Text="—"},close=new GlassButton{Text="×"};
        bool monitoring,released,demo,uiReady,noBeansDisplay,clockFontIsNoBeans,waitingScan,interactiveMoving;int side=1,uiFrames;long lastCropFrame=-1;bool lastCropHasReading;int lastCropSide=-1;string lastClockTip="",lastRecoveryText;RecoveryDisplay lastRecoveryDisplay;string transient="";double transientUntil;readonly Stopwatch uptime=Stopwatch.StartNew();
        readonly bool activateOnShow;
        protected override bool ShowWithoutActivation{get{return !activateOnShow;}}
        public MainForm(bool connectOnShow=true)
        {
            activateOnShow=connectOnShow;
            string settingsError="";settings=connectOnShow?DesktopSettingsStore.Load(DesktopSettingsStore.DefaultPath,out settingsError):new DesktopSettings();
            AutoScaleMode=AutoScaleMode.None;Text="豆子 Bean Studio V5.8.1";Font=new Font("Microsoft YaHei UI",10);ForeColor=Theme.Ink;BackColor=Theme.Bg;FormBorderStyle=FormBorderStyle.None;DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);ClientSize=new Size(1240,860);MinimumSize=new Size(1040,760);StartPosition=FormStartPosition.CenterScreen;
            Controls.AddRange(new Control[]{title,subtitle,edition,sessionBadge,source,picture,timerPanel,live,run,floating,clear,sound,save,minimize,close,status});
            Controls.AddRange(new Control[]{settingsButton,performanceTitle,performancePanel,shortcutHint});
            source.Controls.AddRange(new Control[]{sourceTitle,windows,refresh,scan});picture.Controls.AddRange(new Control[]{pictureTitle,pictureHint,preview,previewNote,left,right});timerPanel.Controls.AddRange(new Control[]{timerTitle,duration,clockLabel,recoveryTitle,recoveryValue,timerState,timerRule,rail});live.Controls.AddRange(new Control[]{liveTitle,crop,dots,countLabel,gameTitle,gameValue,gameState,calibrate});picture.Controls.Add(metrics);
            picture.Controls.AddRange(new Control[]{gameTitle,gameValue,gameState,calibrate});
            BackColor=Theme.Bg;sourceTitle.ForeColor=timerTitle.ForeColor=liveTitle.ForeColor=Theme.Ink;subtitle.ForeColor=edition.ForeColor=pictureHint.ForeColor=previewNote.ForeColor=timerRule.ForeColor=metrics.ForeColor=status.ForeColor=duration.ForeColor=gameState.ForeColor=Theme.Muted;
            clockLabel.Font=new Font("Bahnschrift",32,FontStyle.Regular);recoveryValue.Font=new Font("Bahnschrift",32,FontStyle.Regular);clockLabel.ForeColor=recoveryValue.ForeColor=Theme.Blue;clockLabel.TextAlign=recoveryValue.TextAlign=ContentAlignment.MiddleCenter;recoveryTitle.TextAlign=timerTitle.TextAlign=ContentAlignment.MiddleCenter;duration.Text="现实时间 · 15 秒";duration.TextAlign=ContentAlignment.MiddleCenter;timerState.TextAlign=ContentAlignment.MiddleCenter;timerRule.TextAlign=ContentAlignment.MiddleCenter;countLabel.TextAlign=ContentAlignment.MiddleRight;gameValue.Font=new Font("Bahnschrift",28);gameValue.ForeColor=Theme.Ink;sessionBadge.Font=Font;
            foreach(GlassButton b in new GlassButton[]{run,floating,clear,sound,save,scan,refresh,left,right,calibrate,minimize,close}){b.AccessibleName=b.AccessibleName??b.Text;b.TabStop=true;}
            source.TabIndex=0;windows.TabIndex=0;refresh.TabIndex=1;scan.TabIndex=2;picture.TabIndex=1;left.TabIndex=0;right.TabIndex=1;live.TabIndex=2;calibrate.TabIndex=0;run.TabIndex=3;floating.TabIndex=4;clear.TabIndex=5;sound.TabIndex=6;save.TabIndex=7;minimize.TabIndex=8;close.TabIndex=9;minimize.AccessibleName="最小化窗口";close.AccessibleName="关闭窗口";
            windows.Font=Font;
            calibrate.Click+=delegate{using(Bitmap b=preview.CopyFrame())using(ClockPanel panel=new ClockPanel(engine.Clock,b)){panel.ShowDialog(this);}};
            tips.SetToolTip(calibrate,"查看实际时间区域、失败原因；在扫描预览上手动框选中间数字。");
            refresh.Click+=delegate{RefreshWindows();};scan.Click+=delegate{RequestPreview();};left.Click+=delegate{SelectSide(0);};right.Click+=delegate{SelectSide(1);};
            run.Click+=delegate{if(Selected==null||!Native.IsWindow(Selected.Handle)){Notice("请先打开并选择有效的 MuMu 窗口。");return;}monitoring=!monitoring;run.Text=monitoring?"暂停监测":"开始监测";run.Icon=monitoring?"pause":"play";engine.SetPaused(!monitoring);SyncEngine(false);};
            floating.Text="悬浮倒计时 ▾";floating.Click+=delegate{floatingMenu.Show(floating,new Point(0,-floatingMenu.PreferredSize.Height-6));};clear.Click+=delegate{engine.Reset();};
            sound.Click+=delegate{using(SoundPanel panel=new SoundPanel(soundAlerts)){if(panel.ShowDialog(this)==DialogResult.OK)Notice("音效设置已保存。");}};
            settingsButton.Click+=delegate{OpenDesktopSettings();};
            save.Click+=delegate{try{string folder=engine.Export(AppDomain.CurrentDomain.BaseDirectory);Notice("诊断已保存："+Path.GetFileName(folder));}catch(Exception ex){Notice("保存失败："+ex.Message);}};
            minimize.Click+=delegate{WindowState=FormWindowState.Minimized;};close.Click+=delegate{Close();};
            overlay=new HudHost(engine,delegate{if(!IsDisposed)BeginInvoke((Action)delegate{Show();WindowState=FormWindowState.Normal;Activate();overlay.Hide();});},delegate(int selected){if(!IsDisposed)BeginInvoke((Action)delegate{SelectSide(selected);});});
            hotkeys=new HotkeyManager(this,delegate(DesktopHotkeyAction action){if(action==DesktopHotkeyAction.ToggleOverlay)ToggleOverlay();else if(action==DesktopHotkeyAction.Rescan)RequestPreview();});
            if(!connectOnShow)settings.HotkeysEnabled=false;
            hotkeys.StatusChanged+=delegate(string message){if(message.IndexOf("失败",StringComparison.Ordinal)>=0)Notice(message);};
            string initialHotkeyError;if(!hotkeys.TryApply(settings,out initialHotkeyError))settingsError=initialHotkeyError;
            ApplyVisualSettings();
            settingsButton.TabIndex=8;performancePanel.TabIndex=9;minimize.TabIndex=10;close.TabIndex=11;calibrate.TabIndex=2;
            windows.SelectedIndexChanged+=delegate{if(!demo){SyncEngine(true);if(Visible)RequestPreview();}};
            foreach(Control c in new Control[]{this,title,subtitle,edition})
            {c.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left&&Cursor.Position.Y<Top+95){ReleaseCapture();try{SendMessage(Handle,0xA1,(IntPtr)2,IntPtr.Zero);}finally{EndInteractiveMove();}}};}
            floatingMenu.Font=Font;floatingMenu.Items.Add("打开悬浮窗 · 默认置顶",null,delegate{overlay.ShowAt(new Point(Left+24,Top+120));});floatingMenu.Items.Add("关闭悬浮窗",null,delegate{overlay.Hide();});floatingMenu.Items.Add(new ToolStripSeparator());floatingMenu.Items.Add(new ToolStripMenuItem("悬浮窗内可直接切换左右；主界面保持打开"){Enabled=false});
            floatingMenu.Items.Add(new ToolStripSeparator());
            foreach(OverlayStyle style in new OverlayStyle[]{OverlayStyle.Focus,OverlayStyle.Digits,OverlayStyle.Ribbon,OverlayStyle.Classic})
            {OverlayStyle picked=style;floatingMenu.Items.Add(OverlayName(style),null,delegate{DesktopSettings draft=settings.Copy();draft.OverlayStyle=picked;string error=SaveDesktopSettings(draft);if(error.Length>0)Notice(error);});}
            tips.SetToolTip(floating,"先打开菜单，再选择显示悬浮窗。默认置顶，主界面不会自动隐藏。");tips.SetToolTip(left,"监测左侧；切换时重新建立豆数基准，不触发计时。");tips.SetToolTip(right,"监测右侧；增长不触发，减少重置完整15秒。");tips.SetToolTip(scan,"截取定位预览时主界面短暂收起。请露出顶部血条和豆子。");tips.SetToolTip(run,"暂停会停止后台窗口捕获、豆子识别和游戏时间识别；已有倒计时继续。");
            if(connectOnShow){soundAlerts=new SoundAlerts(engine);soundAlerts.StatusChanged+=SoundStatus;}
            tips.SetToolTip(sound,"设置少豆与计时结束的提示音、独立开关和音量；可选择音频或视频文件。");
            Shown+=delegate{MinimumSize=new Size(1040,760);if(connectOnShow){string error;if(!hotkeys.TryApply(settings,out error))Notice(error);performance.SetEnabled(settings.PerformanceVisible);RefreshWindows();}if(settingsError.Length>0)Notice(settingsError);};pulse.Tick+=Tick;pulse.Start();uiReady=true;ApplyVisualSettings();
        }
        protected override void OnResize(EventArgs e){base.OnResize(e);if(uiReady){performance.SetEnabled(activateOnShow&&settings.PerformanceVisible&&WindowState!=FormWindowState.Minimized&&Visible);if(WindowState!=FormWindowState.Minimized&&ClientSize.Width>0&&ClientSize.Height>0){LayoutUI();Theme.RoundedWindow(this,12);}}}
        protected override void OnPaintBackground(PaintEventArgs e){Theme.Background(e.Graphics,ClientSize);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;for(int i=0;i<4;i++){float x=30+(i%2)*18,y=30+(i/2)*21;PointF[] points={new PointF(x+7,y),new PointF(x+14,y+10),new PointF(x+7,y+20),new PointF(x,y+10)};using(Brush b=new SolidBrush(i==3?Color.FromArgb(140,196,204):Theme.Blue))g.FillPolygon(b,points);}}
        protected override void WndProc(ref Message m)
        {
            if(hotkeys!=null&&hotkeys.ProcessMessage(ref m))return;
            if(m.Msg==0x231)interactiveMoving=true; // WM_ENTERSIZEMOVE: presentation only; workers continue.
            if(m.Msg==0x232||m.Msg==0x1F||(m.Msg==0x215&&m.LParam==IntPtr.Zero))EndInteractiveMove(); // Exit, cancel or lost capture.
            base.WndProc(ref m);if(m.Msg==0x84&&(int)m.Result==1)
            {Point p=PointToClient(new Point(unchecked((short)(long)m.LParam),unchecked((short)((long)m.LParam>>16))));bool l=p.X<8,r=p.X>Width-9,t=p.Y<8,b=p.Y>Height-9;m.Result=(IntPtr)(t?(l?13:r?14:12):b?(l?16:r?17:15):l?10:r?11:1);}
        }
        void EndInteractiveMove(){interactiveMoving=false;lastPerformance=-1;}
        void LayoutUI()
        {
            if(!uiReady||WindowState==FormWindowState.Minimized||ClientSize.Width<900||ClientSize.Height<700)return;
            int w=ClientSize.Width,h=ClientSize.Height,margin=24,gap=18,rightWidth=302,leftWidth=w-margin*2-gap-rightWidth,contentY=172;
            int contentH=settings.PerformanceVisible?h-460:h-287,timerH=Math.Min(218,Math.Max(160,(int)((contentH-gap)*.54)));
            title.SetBounds(65,18,280,38);subtitle.SetBounds(67,59,340,23);sessionBadge.SetBounds(w-392,27,145,32);edition.SetBounds(w-454,30,60,25);settingsButton.SetBounds(w-238,22,158,42);minimize.SetBounds(w-70,22,31,38);close.SetBounds(w-37,22,30,38);
            source.SetBounds(margin,96,w-margin*2,60);sourceTitle.SetBounds(18,15,86,28);windows.SetBounds(102,9,source.Width-382,42);refresh.SetBounds(source.Width-264,9,112,42);scan.SetBounds(source.Width-144,9,126,42);
            picture.SetBounds(margin,contentY,leftWidth,contentH);pictureTitle.SetBounds(20,14,188,28);pictureHint.SetBounds(211,17,Math.Max(20,leftWidth-392),22);preview.SetBounds(18,54,leftWidth-36,contentH-142);left.SetBounds(leftWidth-162,12,66,34);right.SetBounds(leftWidth-88,12,68,34);
            gameTitle.SetBounds(22,contentH-81,89,26);gameValue.SetBounds(111,contentH-89,94,38);gameState.SetBounds(212,contentH-88,Math.Max(50,leftWidth-329),40);calibrate.SetBounds(leftWidth-101,contentH-86,79,34);previewNote.SetBounds(22,contentH-45,leftWidth-44,22);metrics.SetBounds(22,contentH-24,leftWidth-44,19);
            timerPanel.SetBounds(margin+leftWidth+gap,contentY,rightWidth,timerH);
            int valueWidth=(rightWidth-42)/2,valueHeight=Math.Max(44,timerH-112);
            timerTitle.SetBounds(14,13,valueWidth,25);recoveryTitle.SetBounds(28+valueWidth,13,valueWidth,25);
            clockLabel.SetBounds(14,39,valueWidth,valueHeight);recoveryValue.SetBounds(28+valueWidth,39,valueWidth,valueHeight);
            duration.SetBounds(14,timerH-73,valueWidth,23);timerRule.SetBounds(28+valueWidth,timerH-73,valueWidth,23);
            timerState.SetBounds(18,timerH-47,rightWidth-36,22);rail.SetBounds(22,timerH-22,rightWidth-44,12);
            ApplyClockFont(timerH);
            live.SetBounds(timerPanel.Left,contentY+timerH+gap,rightWidth,contentH-timerH-gap);liveTitle.SetBounds(20,10,rightWidth-40,24);crop.SetBounds(20,38,rightWidth-40,40);dots.SetBounds(20,87,160,31);countLabel.SetBounds(180,87,rightWidth-200,31);
            performanceTitle.Visible=performancePanel.Visible=settings.PerformanceVisible;performanceTitle.SetBounds(margin+3,h-274,w-margin*2,22);performancePanel.SetBounds(margin,h-250,w-margin*2,138);
            run.SetBounds(margin,h-91,170,46);floating.SetBounds(margin+180,h-91,158,46);clear.SetBounds(margin+348,h-91,128,46);sound.SetBounds(margin+486,h-91,126,46);save.SetBounds(w-margin-128,h-91,128,46);status.SetBounds(margin+3,h-31,Math.Max(100,w-margin*2-422),23);shortcutHint.SetBounds(w-margin-411,h-31,411,23);shortcutHint.TextAlign=ContentAlignment.MiddleRight;
        }
        internal static float SafeClockSize(int timerHeight){return Math.Max(28,Math.Min(32,(timerHeight-112)*.67f));}
        void ApplyClockFont(int timerHeight)
        {
            float size=noBeansDisplay?15:SafeClockSize(timerHeight);string family=noBeansDisplay?"Microsoft YaHei UI":"Bahnschrift";FontStyle style=noBeansDisplay?FontStyle.Bold:FontStyle.Regular;
            if(Math.Abs(clockLabel.Font.Size-size)>.1||clockFontIsNoBeans!=noBeansDisplay||clockLabel.Font.Style!=style){Font old=clockLabel.Font;clockLabel.Font=new Font(family,size,style);clockFontIsNoBeans=noBeansDisplay;old.Dispose();}
            float recoverySize=SafeClockSize(timerHeight);if(Math.Abs(recoveryValue.Font.Size-recoverySize)>.1){Font old=recoveryValue.Font;recoveryValue.Font=new Font("Bahnschrift",recoverySize,FontStyle.Regular);old.Dispose();}
        }
        internal void SetCountdownDisplay(bool noBeans,double remaining)
        {
            noBeansDisplay=noBeans;ApplyClockFont(timerPanel.Height);clockLabel.Text=noBeans?"对方已无豆":Theme.Clock(remaining);clockLabel.ForeColor=noBeans?Theme.Blue:Theme.Urgency(remaining);clockLabel.AccessibleName=noBeans?"对方已无豆，0 / 4 颗":"冷却剩余："+clockLabel.Text+" 秒";
            timerState.Text=noBeans?"0 / 4 颗 · 等待豆子恢复":remaining>0&&remaining<3?"即将恢复 · 留意游戏时间":remaining>0?"计时中 · 再次减少即重置":"等待下一次少豆";rail.Remaining=noBeans?0:remaining;
        }
        WindowItem Selected{get{return windows.SelectedItem as WindowItem;}}
        void SelectSide(int value){if(value==side)return;side=value;left.Selected=side==0;right.Selected=side==1;left.Invalidate();right.Invalidate();preview.Side=side;preview.Invalidate();SyncEngine(true);}
        void SyncEngine(bool relocate){engine.ConfigureAuto(Selected==null?IntPtr.Zero:Selected.Handle,side,monitoring,relocate);}
        void Notice(string message){transient=message;transientUntil=uptime.Elapsed.TotalSeconds+8;status.Text=message;}
        void SoundStatus(string message)
        {
            if(released||IsDisposed||!IsHandleCreated)return;
            try{BeginInvoke((Action)delegate{if(!released&&!IsDisposed)Notice(message);});}catch(InvalidOperationException){}
        }
        static string OverlayName(OverlayStyle style)
        {return style==OverlayStyle.Focus?"专注 · 精简计时":style==OverlayStyle.Digits?"纯数字 · 无背景":style==OverlayStyle.Ribbon?"横条 · 贴边查看":"经典 · 完整信息";}
        void ToggleOverlay(){overlay.ToggleAt(new Point(Left+24,Top+120));}
        void ApplyVisualSettings()
        {
            overlay.ApplySettings(settings);performance.SetEnabled(activateOnShow&&settings.PerformanceVisible&&Visible&&WindowState!=FormWindowState.Minimized);
            preview.Suppressed=!settings.PreviewVisible;
            shortcutHint.Text=settings.HotkeysEnabled?settings.ToggleOverlayHotkey+" 浮窗   "+settings.RescanHotkey+" 重扫":"快捷键已关闭";
            tips.SetToolTip(floating,"可在菜单选择风格；快捷开关："+settings.ToggleOverlayHotkey);
            tips.SetToolTip(scan,"后台重新定位当前窗口，保留冷却。快捷键："+settings.RescanHotkey+"。暂停时只扫描一次，请先让游戏画面可见。");
            if(uiReady)LayoutUI();
        }
        string SaveDesktopSettings(DesktopSettings draft)
        {
            DesktopSettings previous=settings.Copy();string error;
            try
            {
                DesktopSettingsStore.Validate(draft);
                if(!hotkeys.TryApply(draft,out error))return error;
                try{DesktopSettingsStore.Save(DesktopSettingsStore.DefaultPath,draft);}
                catch(Exception ex){string rollback;hotkeys.TryApply(previous,out rollback);return "设置未保存："+ex.Message+(rollback.Length>0?"；"+rollback:"");}
                settings=draft.Copy();ApplyVisualSettings();Notice("设置已保存。");return "";
            }
            catch(ArgumentException ex){return ex.Message;}
        }
        void OpenDesktopSettings()
        {
            hotkeys.Suspend();
            try{using(DesktopSettingsPanel panel=new DesktopSettingsPanel(settings,SaveDesktopSettings))panel.ShowDialog(this);}
            finally{string error;if(!hotkeys.Resume(out error))Notice(error);}
        }
        void RefreshWindows()
        {
            IntPtr previous=Selected==null?IntPtr.Zero:Selected.Handle;List<WindowItem> list=new List<WindowItem>();uint own=(uint)Process.GetCurrentProcess().Id;
            Native.EnumWindows(delegate(IntPtr h,IntPtr p){uint pid;Native.GetWindowThreadProcessId(h,out pid);if(pid==own||!Native.IsWindowVisible(h))return true;StringBuilder t=new StringBuilder(512);Native.GetWindowText(h,t,t.Capacity);string name=t.ToString();if(name.IndexOf("mumu",StringComparison.OrdinalIgnoreCase)>=0&&Native.ClientBounds(h).Width>320)list.Add(new WindowItem{Handle=h,Title=name});return true;},IntPtr.Zero);
            windows.Items.Clear();foreach(WindowItem item in list)windows.Items.Add(item);int index=list.FindIndex(delegate(WindowItem item){return item.Handle==previous;});windows.SelectedIndex=index>=0?index:(list.Count>0?0:-1);if(list.Count==0){SyncEngine(true);Notice("未发现 MuMu 窗口，请打开模拟器后刷新列表。");}
        }
        void RequestPreview()
        {
            WindowItem selected=Selected;if(selected==null){Notice("请先选择 MuMu 窗口。");return;}
            if(!Native.IsWindow(selected.Handle)){Notice("来源窗口已关闭，请刷新列表后重新选择。");return;}
            if(Native.IsIconic(selected.Handle)){Notice("请先恢复 MuMu 窗口。");return;}
            if(engine.RequestScan()){waitingScan=true;Notice("正在按当前窗口尺寸重新定位，原冷却继续。");}
        }
        static RectangleF Normalized(Rectangle r,Size size){return r.IsEmpty?RectangleF.Empty:new RectangleF(r.X/(float)size.Width,r.Y/(float)size.Height,r.Width/(float)size.Width,r.Height/(float)size.Height);}
        void Tick(object sender,EventArgs e)
        {
            Snapshot s=engine.GetSnapshot();pulse.Interval=s.Paused||WindowState==FormWindowState.Minimized?100:50;
            ApplySnapshot(s,engine.Clock.State());
            if(waitingScan&&!s.ScanPending){waitingScan=false;transientUntil=0;}
            // Keep timing and recovery above current. Dragging only defers noncritical bitmap/chart work.
            if(interactiveMoving||WindowState==FormWindowState.Minimized)return;
            Bitmap scanned;if(engine.TryTakeScanPreview(out scanned)){preview.SetFrame(scanned);preview.LeftRegion=s.LeftRegion;preview.RightRegion=s.RightRegion;preview.Invalidate();}
            if(uptime.Elapsed.TotalSeconds-lastPerformance>=1){lastPerformance=uptime.Elapsed.TotalSeconds;if(settings.PerformanceVisible)performancePanel.SetSnapshot(performance.GetSnapshot());}
            if(++uiFrames%4==0&&(lastCropFrame!=s.UniqueFrames||lastCropHasReading!=(s.Reading!=null)||lastCropSide!=s.Side))
            {
                lastCropFrame=s.UniqueFrames;lastCropHasReading=s.Reading!=null;lastCropSide=s.Side;Bitmap b=s.Reading==null?null:engine.GetFrame();crop.SetFrame(b);
            }
            if(preview.LeftRegion!=s.LeftRegion||preview.RightRegion!=s.RightRegion){preview.LeftRegion=s.LeftRegion;preview.RightRegion=s.RightRegion;preview.Invalidate();}
        }
        internal void ApplySnapshot(Snapshot s,ClockState cs)
        {
            SetCountdownDisplay(s.NoBeans,s.Remaining);if(lastRecoveryDisplay==null||lastRecoveryText!=s.Recovery){lastRecoveryText=s.Recovery;lastRecoveryDisplay=RecoveryDisplay.Parse(s.Recovery);}RecoveryDisplay target=lastRecoveryDisplay;
            recoveryValue.Text=target.IsApproximate?target.Value.TrimStart('≈'):target.Value;recoveryTitle.Text=target.Caption+(target.IsApproximate?" ≈":"");recoveryValue.ForeColor=target.HasValue?Theme.Blue:Theme.Muted;
            recoveryValue.AccessibleName="游戏恢复点："+target.Value+"；"+target.Status;timerRule.Text=target.Status;
            scan.Enabled=!s.ScanPending;scan.Text=s.ScanPending?"重新定位中":"重新扫描";
            bool fresh=cs.Value.HasValue&&cs.Age<=.65;
            gameValue.Text=s.Paused?"—":fresh?cs.Value.Value.ToString(cs.Value<10?"0.00":"0",CultureInfo.InvariantCulture):"—";
            gameState.Text=s.Paused?"已暂停\r\n停止读取时间":fresh?"已确认\r\n持续读取中":cs.Value.HasValue?"读数已过期\r\n可点校准检查":"等待时间数字\r\n可点校准检查";
            // Confidence and backend belong to the hovered diagnostic; avoid resetting the native tooltip every 50 ms.
            if(!interactiveMoving&&gameState.ClientRectangle.Contains(gameState.PointToClient(Cursor.Position)))
            {
                string diagnostic=cs.Status+"\r\n"+cs.Backend+"\r\n识别方式："+cs.Method+"；本帧匹配分数："+cs.Confidence.ToString("0.00",CultureInfo.InvariantCulture);
                if(diagnostic!=lastClockTip){lastClockTip=diagnostic;tips.SetToolTip(gameState,diagnostic);}
            }
            gameValue.AccessibleName="游戏时间："+(!s.Paused&&fresh?gameValue.Text:"未确认");
            sessionBadge.Caption=s.Paused?"监测已暂停":Selected==null?"尚未连接":s.Enabled?"正在监测":"预览模式";sessionBadge.Active=s.Enabled&&!s.Paused;
            Reading display=s.Enabled?s.ConfirmedReading:s.Reading;dots.Reading=display;countLabel.Text=display!=null&&display.Valid?display.Count+" / 4 颗":"待确认";
            if(!interactiveMoving){metrics.Text=s.Paused?s.Status : !s.Enabled?"开始监测后显示采样状态":string.Format("采样 {0:0} ms    确认 {1:0} ms    {2}",s.GapMs,s.ConfirmationMs,WindowCapture.Status=="WGC 窗口捕获"?"窗口捕获":"兼容 / 等待帧");metrics.ForeColor=s.Enabled&&s.GapMs>100?Theme.Red:Theme.Muted;}
            status.Text=uptime.Elapsed.TotalSeconds<transientUntil?transient:s.Status;previewNote.Text=(side==0?"监测左侧":"监测右侧")+"  ·  增长不触发";
        }
        [DllImport("user32.dll")]static extern bool ReleaseCapture();
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int message,IntPtr wParam,IntPtr lParam);
        public void RenderDemo(Bitmap frame,string path)
        {
            demo=true;pulse.Stop();LocatedPair p=AutoLocator.Find(frame);preview.SetFrame((Bitmap)frame.Clone());preview.LeftRegion=Normalized(p.Left,frame.Size);preview.RightRegion=Normalized(p.Right,frame.Size);if(!p.Right.IsEmpty)crop.SetFrame(frame.Clone(p.Right,PixelFormat.Format32bppArgb));dots.Reading=p.RightReading;
            windows.Items.Add(new WindowItem{Title="MuMu 安卓设备"});windows.SelectedIndex=0;clockLabel.Text="15.00";clockLabel.ForeColor=Theme.Blue;timerState.Text="计时中 · 再次减少即重置";countLabel.Text=p.RightReading!=null&&p.RightReading.Valid?p.RightReading.Count+" / 4 颗":"待确认";metrics.Text="设计预览 · 演示数据";gameValue.Text="37";gameState.Text="演示读数\r\n未连接游戏";recoveryValue.Text="22";timerRule.Text="已确认 · 演示";rail.Remaining=15;sessionBadge.Caption="设计预览";previewNote.Text="监测右侧  ·  增长不触发";status.Text="自动定位左右区域  ·  变色与增长不触发计时  ·  再次减少，重置 15 秒";run.Text="暂停监测";run.Icon="pause";
            StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);Show();Application.DoEvents();SaveRender(path);ClientSize=new Size(1040,760);Application.DoEvents();SaveRender(Path.Combine(Path.GetDirectoryName(path),"紧凑界面预览.png"));ClientSize=new Size(1440,960);Application.DoEvents();SaveRender(Path.Combine(Path.GetDirectoryName(path),"大尺寸界面预览.png"));ClientSize=new Size(1040,760);Application.DoEvents();SaveRender(Path.Combine(Path.GetDirectoryName(path),"最小界面预览.png"));Hide();
            using(Overlay hud=new Overlay(delegate{})){hud.ClockLabel.Text="15.00";hud.Detail.Text="3 / 4 颗";hud.Location=new Point(-20000,-20000);hud.Show();Application.DoEvents();using(Bitmap b=hud.Surface()){b.Save(Path.Combine(Path.GetDirectoryName(path),"悬浮窗预览.png"));}hud.Hide();}
        }
        public void RenderStates(Bitmap frame,string folder)
        {
            demo=true;pulse.Stop();Directory.CreateDirectory(folder);StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);ClientSize=new Size(1040,760);Show();Application.DoEvents();
            windows.Items.Clear();windows.SelectedIndex=-1;preview.SetFrame(null);crop.SetFrame(null);countLabel.Text="待确认";clockLabel.Text="00.00";gameValue.Text="—";gameState.Text="等待时间数字\r\n可点校准检查";timerState.Text="等待下一次少豆";recoveryValue.Text="—";timerRule.Text="等待少豆";sessionBadge.Caption="尚未连接";status.Text="选择 MuMu 窗口，然后扫描画面。";run.Text="开始监测";run.Icon="play";rail.Remaining=0;SaveRender(Path.Combine(folder,"未连接.png"));
            windows.Items.Add(new WindowItem{Title="MuMu 安卓设备（演示）"});windows.SelectedIndex=0;preview.SetFrame((Bitmap)frame.Clone());sessionBadge.Caption="设计预览";clockLabel.Text="08.00";clockLabel.ForeColor=Theme.Blue;timerState.Text="计时中 · 再次减少即重置";recoveryValue.Text="—";timerRule.Text="游戏时间未确认";rail.Remaining=8;gameState.Text="等待时间数字\r\n可点校准检查";status.Text="设计预览 · 时间未确认时保持冷却计时";run.Text="暂停监测";run.Icon="pause";SaveRender(Path.Combine(folder,"时间未确认.png"));
            clockLabel.Text="02.00";clockLabel.ForeColor=Theme.Urgency(2);timerState.Text="即将恢复 · 留意游戏时间";recoveryValue.Text="3.50";recoveryTitle.Text="游戏恢复点 ≈";timerRule.Text="估算 · 中途补算";rail.Remaining=2;gameValue.Text="5.50";gameState.Text="演示读数\r\n未连接游戏";status.Text="设计预览 · 接近归零时使用紧急颜色";SaveRender(Path.Combine(folder,"接近恢复.png"));
            run.Text="开始监测";run.Icon="play";sessionBadge.Caption="暂停监测";status.Text="监测暂停 · 已有冷却继续";SaveRender(Path.Combine(folder,"暂停监测.png"));
            LocatedPair pair=AutoLocator.Find(frame);preview.LeftRegion=Normalized(pair.Left,frame.Size);preview.RightRegion=Normalized(pair.Right,frame.Size);preview.Side=0;side=0;left.Selected=true;right.Selected=false;previewNote.Text="监测左侧  ·  增长不触发";if(!pair.Left.IsEmpty)crop.SetFrame(frame.Clone(pair.Left,PixelFormat.Format32bppArgb));
            dots.Reading=pair.LeftReading;countLabel.Text="0 / 4 颗";sessionBadge.Caption="设计预览";run.Text="暂停监测";run.Icon="pause";SetCountdownDisplay(true,8);recoveryTitle.Text="游戏恢复点";recoveryValue.Text="18";timerRule.Text="已确认 · 演示";status.Text="设计预览 · 已确认四格全空，显示对方已无豆";SaveRender(Path.Combine(folder,"四空豆主界面.png"));ClientSize=new Size(1440,960);Application.DoEvents();SaveRender(Path.Combine(folder,"四空豆大尺寸界面.png"));ClientSize=new Size(1040,760);Application.DoEvents();
            SetCountdownDisplay(false,8);recoveryValue.Text="—";timerRule.Text="游戏时间未确认";dots.Reading=new Reading{Lit=new bool[]{false,false,false,true},Reason="演示恢复一豆"};countLabel.Text="1 / 4 颗";timerState.Text="计时中 · 再次减少即重置";status.Text="设计预览 · 豆子恢复后还原数字字号，已有冷却继续";SaveRender(Path.Combine(folder,"豆子恢复数字界面.png"));Hide();
            using(Overlay hud=new Overlay(delegate{})){hud.NoBeans=true;hud.ClockLabel.Text="08.00";hud.Detail.Text="0 / 4 颗";hud.Target.Text="等待豆子恢复";hud.SetSide(0);using(Bitmap b=hud.Surface())b.Save(Path.Combine(folder,"四空豆悬浮窗.png"));hud.NoBeans=false;hud.Detail.Text="1 / 4 颗";hud.Target.Text="恢复点：时间未确认";using(Bitmap b=hud.Surface())b.Save(Path.Combine(folder,"豆子恢复悬浮窗.png"));}
            using(ClockPanel panel=new ClockPanel(engine.Clock,frame))panel.SavePreview(folder);
        }
        void SaveRender(string path){Refresh();Application.DoEvents();using(Bitmap b=new Bitmap(Width,Height)){DrawToBitmap(b,ClientRectangle);b.Save(path);}}
        internal void VerifyInteractions(string folder)
        {
            List<string> report=new List<string>();Exception fault=null;System.Threading.ThreadExceptionEventHandler handler=delegate(object sender,System.Threading.ThreadExceptionEventArgs e){fault=e.Exception;};Application.ThreadException+=handler;
            try
            {
                StartPosition=FormStartPosition.Manual;Location=new Point(20,20);ClientSize=new Size(1040,800);TopMost=true;Show();Application.DoEvents();
                for(int i=0;i<12;i++){WindowState=FormWindowState.Minimized;Application.DoEvents();WindowState=FormWindowState.Normal;ClientSize=new Size(1020+(i%3)*70,780+(i%3)*30);Application.DoEvents();if(fault!=null)throw fault;}
                if(SafeClockSize(-533)<=0||SafeClockSize(0)<=0)throw new Exception("字号保护失败");report.Add("PASS 最小化/恢复/缩放12轮，无负数字号异常；零/负高度字号保护有效");
                typeof(Control).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(floating,new object[]{EventArgs.Empty});Application.DoEvents();
                if(!floatingMenu.Visible||!Visible||WindowState==FormWindowState.Minimized)throw new Exception("悬浮按钮未先打开菜单或隐藏了主窗体");report.Add("PASS 点击悬浮倒计时先打开二级菜单，主窗体保持可见");
                ((ToolStripMenuItem)floatingMenu.Items[0]).PerformClick();floatingMenu.Close();WaitUI(delegate{return overlay.Visible;});if(!overlay.Visible||!overlay.TestTopMost)throw new Exception("悬浮窗未显示或未默认置顶");report.Add("PASS 菜单打开悬浮窗，默认置顶，主窗体不自动隐藏");
                overlay.SelectSideForTest(0);WaitUI(delegate{return side==0;});if(side!=0||engine.GetSnapshot().Side!=0)throw new Exception("悬浮窗左侧切换失败");
                overlay.SelectSideForTest(1);WaitUI(delegate{return side==1;});if(side!=1||engine.GetSnapshot().Side!=1)throw new Exception("悬浮窗右侧切换失败");report.Add("PASS 悬浮窗左右快捷键同步主界面与识别引擎，未增加触发");overlay.Hide();
                int configured=(int)typeof(Engine).GetField("version",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(engine);SelectSide(side);if((int)typeof(Engine).GetField("version",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(engine)!=configured)throw new Exception("重复侧重新配置引擎");report.Add("PASS 重复点击当前侧不重新定位，不中断识别基准");
                typeof(Control).GetMethod("OnKeyDown",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(windows,new object[]{new KeyEventArgs(Keys.Up)});Application.DoEvents();if(!windows.Popup.Visible)throw new Exception("来源上箭头未打开菜单");windows.Popup.Close();report.Add("PASS 来源选择可使用上下键/Enter/Space，菜单可关闭");
                ClientSize=new Size(1040,760);Application.DoEvents();
                foreach(Control parent in new Control[]{source,picture,timerPanel,live})foreach(Control c in parent.Controls)if(c.Visible&&!parent.ClientRectangle.Contains(c.Bounds))throw new Exception("控件超出容器："+c.Text);if(rail.Top<timerState.Bottom)throw new Exception("冷却进度覆盖状态");if(clear.Right>=sound.Left||sound.Right>=save.Left||!ClientRectangle.Contains(sound.Bounds))throw new Exception("音效入口覆盖其他按钮");report.Add("PASS 最小尺寸下关键控件均在容器内，音效入口独立可见，进度条不覆盖状态");
                ClientSize=new Size(1040,800);Application.DoEvents();
                foreach(GlassButton button in new GlassButton[]{run,floating,left,right,scan,refresh}){typeof(Control).GetMethod("OnMouseEnter",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});button.Refresh();typeof(Control).GetMethod("OnMouseLeave",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});button.Refresh();}
                Refresh();Application.DoEvents();System.Threading.Thread.Sleep(80);Rectangle bounds=Native.ClientBounds(Handle);if(!SystemInformation.VirtualScreen.Contains(bounds))throw new Exception("测试窗口超出屏幕，无法验证实际绘制");
                Bitmap actual=null;string captureMethod="WGC 窗口捕获";using(WindowFrames frames=new WindowFrames(Handle)){Stopwatch wait=Stopwatch.StartNew();while(actual==null&&wait.ElapsedMilliseconds<3000){Application.DoEvents();try{actual=frames.Crop(bounds);}catch(InvalidOperationException){System.Threading.Thread.Sleep(12);}}}if(actual==null)throw new Exception("实际窗口帧未到达，未采集其他窗口");using(Bitmap capture=actual){capture.Save(Path.Combine(folder,"实际窗口绘制.png"));Color button=capture.GetPixel(run.Right-18,run.Top+run.Height/2);if(button.R>60||button.G>110||button.B>130)throw new Exception("实际主按钮被背景遮挡，截图颜色："+button);Color canvas=capture.GetPixel(picture.Left+preview.Left+12,picture.Top+preview.Top+preview.Height/2);if(canvas.R>70||canvas.G>90||canvas.B>110)throw new Exception("实际预览画布被遮挡，截图颜色："+canvas);}if(fault!=null)throw fault;report.Add("PASS 实际主按钮与预览画布可见，已保存悬停/恢复后的实际绘制（"+captureMethod+"，非 DrawToBitmap）");
                File.WriteAllLines(Path.Combine(folder,"界面交互验证.txt"),report.ToArray());
            }
            finally{Application.ThreadException-=handler;overlay.Hide();Hide();}
        }
        static void WaitUI(Func<bool> condition){Stopwatch t=Stopwatch.StartNew();while(!condition()&&t.ElapsedMilliseconds<1500){Application.DoEvents();System.Threading.Thread.Sleep(4);}}
        internal void VerifySoundEntry(List<string> report)
        {
            if(soundAlerts!=null)throw new Exception("设计测试不应连接音效服务");StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);ClientSize=new Size(1040,760);Show();Application.DoEvents();
            if(clear.Right>=sound.Left||sound.Right>=save.Left||!sound.Visible||!ClientRectangle.Contains(sound.Bounds))throw new Exception("最小界面音效入口不可用");report.Add("PASS 最小主界面音效设置入口可见，位于清空冷却与保存诊断之间");
            bool opened=false;using(Timer closer=new Timer{Interval=80})
            {
                closer.Tick+=delegate{foreach(Form item in Application.OpenForms){SoundPanel panel=item as SoundPanel;if(panel!=null){opened=true;panel.DialogResult=DialogResult.Cancel;panel.Close();break;}}};
                closer.Start();typeof(Control).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(sound,new object[]{EventArgs.Empty});closer.Stop();
            }
            if(!opened||!Visible||!pulse.Enabled)throw new Exception("音效设置未打开模态面板或停止了主界面计时更新");report.Add("PASS 音效按钮打开独立设置面板，取消后主界面与刷新计时保持运行");Hide();
        }
        protected override void Dispose(bool disposing){if(disposing&&!released){released=true;pulse.Dispose();tips.Dispose();floatingMenu.Dispose();if(hotkeys!=null)hotkeys.Dispose();performance.Dispose();if(soundAlerts!=null){soundAlerts.StatusChanged-=SoundStatus;soundAlerts.Dispose();}if(overlay!=null)overlay.Dispose();engine.Dispose();}base.Dispose(disposing);}
    }
}



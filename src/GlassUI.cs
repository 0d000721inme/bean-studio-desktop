using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Timer=System.Windows.Forms.Timer;

namespace MuMuBeans
{
    static class Theme
    {
        public static readonly Color Ink=Color.FromArgb(22,36,52), Muted=Color.FromArgb(79,98,116), Blue=Color.FromArgb(0,105,117), Cyan=Color.FromArgb(0,118,127), Red=Color.FromArgb(185,53,69);
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
        public static readonly Color Bg=Color.FromArgb(238,243,247);
        public static void Glass(Graphics g,RectangleF r,float radius,bool blue=false)
        {
            if(r.Width<2||r.Height<2)return;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(GraphicsPath p=Round(r,radius))
            {using(Brush b=new LinearGradientBrush(r,Color.White,blue?Color.FromArgb(230,235,255):Color.FromArgb(243,249,252),65))g.FillPath(b,p);using(Pen pen=new Pen(Color.FromArgb(213,225,241),1))g.DrawPath(pen,p);}
        }
        public static void Background(Graphics g,Size size){g.Clear(Bg);if(size.Width>0&&size.Height>0){Glow(g,new RectangleF(size.Width*.56f,-size.Height*.28f,size.Width*.66f,size.Height*.7f),Color.FromArgb(52,170,207,217));Glow(g,new RectangleF(-size.Width*.18f,size.Height*.42f,size.Width*.55f,size.Height*.75f),Color.FromArgb(30,167,187,214));}}
        static void Glow(Graphics g,RectangleF r,Color color)
        {using(GraphicsPath p=new GraphicsPath()){p.AddEllipse(r);using(PathGradientBrush b=new PathGradientBrush(p)){b.CenterColor=color;b.SurroundColors=new Color[]{Color.FromArgb(0,color)};g.FillPath(b,p);}}}
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
            Color a=Material?Color.FromArgb(245,255,255,255):Color.White,b=Blue?Color.FromArgb(230,243,245):Color.FromArgb(249,251,253);
            using(Brush brush=new LinearGradientBrush(rect,a,b,100))g.FillPath(brush,p);
            using(Pen pen=new Pen(Color.FromArgb(205,216,226),1))g.DrawPath(pen,p);
            using(GraphicsPath rim=Theme.Round(new RectangleF(2,2,Width-5,Height-5),Radius-1))using(Pen pen=new Pen(Color.FromArgb(230,255,255,255),1))g.DrawPath(pen,rim);
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
        protected override void OnPaint(PaintEventArgs e){if(Width<4||Height<4)return;Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;Color fill=primary?Color.FromArgb(22,61,76):selected?Theme.Blue:Color.FromArgb(249,252,254);if(hover)fill=primary||selected?Color.FromArgb(11,83,96):Color.FromArgb(228,240,244);if(pressed)fill=Color.FromArgb(200,224,231);if(!Enabled)fill=Color.FromArgb(229,235,239);
            using(GraphicsPath p=Theme.Round(new RectangleF(2,2,Width-5,Height-5),Math.Min(21,Height/2f-2))){using(Brush b=new SolidBrush(fill))g.FillPath(b,p);using(Pen border=new Pen(primary||selected?Color.FromArgb(22,85,100):Color.FromArgb(202,216,225),1))g.DrawPath(border,p);if(!primary&&!selected)using(Pen rim=new Pen(Color.White,1))g.DrawArc(rim,3,3,Height-7,Height-7,180,95);}
            Color ink=!Enabled?Theme.Muted:(primary||selected)&&!pressed?Color.White:Theme.Ink;Rectangle label=new Rectangle(8,1,Width-16,Height-2);
            if(Icon.Length>0){Glyphs.Draw(g,Icon,new Rectangle(15,(Height-18)/2,18,18),ink);label=new Rectangle(36,1,Width-43,Height-2);}
            TextRenderer.DrawText(g,Text,Font,label,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Focused)using(GraphicsPath p=Theme.Round(new RectangleF(.8f,.8f,Width-2,Height-2),Math.Min(22,Height/2f)))using(Pen focus=new Pen(Theme.Blue,2))g.DrawPath(focus,p);
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
        public double Remaining;public CooldownRail(){DoubleBuffered=true;SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float pitch=Width/15f;for(int i=0;i<15;i++){RectangleF r=new RectangleF(i*pitch+1,6,pitch-4,5);using(GraphicsPath p=Theme.Round(r,2.5f))using(Brush b=new SolidBrush(i<Math.Ceiling(Remaining)?Theme.Urgency(Remaining):Color.FromArgb(213,226,233)))g.FillPath(b,p);}}
    }
    sealed class SessionBadge:Control {
        string caption="尚未连接";public string Caption{get{return caption;}set{caption=value;AccessibleName=value;}}public bool Active;public SessionBadge(){DoubleBuffered=true;SetStyle(ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;using(GraphicsPath p=Theme.Round(new RectangleF(1,1,Width-3,Height-3),16))using(Brush b=new SolidBrush(Color.FromArgb(230,255,255,255)))g.FillPath(b,p);using(Brush b=new SolidBrush(Active?Theme.Blue:Theme.Muted))g.FillEllipse(b,14,Height/2-3,6,6);TextRenderer.DrawText(g,Caption,Font,new Rectangle(29,0,Width-36,Height),Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.Left);}
    }
    sealed class PreviewBox:Control
    {
        Bitmap frame;public RectangleF LeftRegion,RightRegion;public int Side=1;
        public PreviewBox(){SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=Color.Transparent;}
        public Bitmap CopyFrame(){return frame==null?null:(Bitmap)frame.Clone();}
        public void SetFrame(Bitmap b){if(frame!=null)frame.Dispose();frame=b;Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            RectangleF bounds=new RectangleF(0,0,Width-1,Height-1);using(GraphicsPath path=Theme.Round(bounds,18))
            {
                using(Brush b=new SolidBrush(Color.FromArgb(17,33,45)))g.FillPath(b,path);
                if(frame==null){TextRenderer.DrawText(g,"画面会在这里出现\n选择 MuMu 后，点击「扫描画面」",Font,ClientRectangle,Color.FromArgb(208,225,234),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);return;}
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
        public Reading Reading;
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
        readonly Engine engine=new Engine();readonly HudHost overlay;readonly Timer pulse=new Timer{Interval=33};
        readonly WindowPicker windows=new WindowPicker();readonly PreviewBox preview=new PreviewBox();readonly CropBox crop=new CropBox();readonly BeanDots dots=new BeanDots();
        readonly GlassPanel source=new GlassPanel{Material=true,Radius=20},picture=new GlassPanel{Radius=28},timerPanel=new GlassPanel{Blue=true,Radius=28},live=new GlassPanel{Radius=24};
        readonly CooldownRail rail=new CooldownRail();readonly SessionBadge sessionBadge=new SessionBadge();
        readonly Label gameTitle=Theme.Label("游戏时间",10,true),gameValue=Theme.Label("—",28),gameState=Theme.Label("等待时间数字",9),duration=Theme.Label("15 秒",9);
        readonly Label title=Theme.Label("豆子",26,true),subtitle=Theme.Label("Bean Studio   /   MuMu 对局助手",9),edition=Theme.Label("V5.7",9);
        readonly Label sourceTitle=Theme.Label("画面来源",10,true),pictureTitle=Theme.Label("游戏画面",13,true),pictureHint=Theme.Label("扫描时的定位预览",9),previewNote=Theme.Label("静态定位预览  ·  下方选择监测方向",9);
        readonly Label timerTitle=Theme.Label("冷却剩余",11,true),clockLabel=Theme.Label("00.00",60,true),timerState=Theme.Label("等待下一次少豆",10),timerRule=Theme.Label("每次减少，重新开始完整 15 秒",9);
        readonly Label liveTitle=Theme.Label("豆子识别",11,true),countLabel=Theme.Label("待确认",11,true),metrics=Theme.Label("开始监测后显示采样状态",9),status=Theme.Label("选择 MuMu 窗口，然后扫描画面。",9);
        readonly GlassButton calibrate=new GlassButton{Text="校准",AccessibleName="游戏时间校准"};
        readonly GlassButton refresh=new GlassButton{Text="刷新列表"},scan=new GlassButton{Text="扫描画面",Icon="scan"},left=new GlassButton{Text="左侧"},right=new GlassButton{Text="右侧",Selected=true},run=new GlassButton{Text="开始监测",Primary=true,Icon="play"},floating=new GlassButton{Text="悬浮倒计时",Icon="float"},clear=new GlassButton{Text="清空冷却",Icon="reset"},save=new GlassButton{Text="保存诊断",Icon="save"},minimize=new GlassButton{Text="—"},close=new GlassButton{Text="×"};
        bool monitoring,refreshing,released,demo,uiReady;int side=1,uiFrames;string transient="";double transientUntil;readonly Stopwatch uptime=Stopwatch.StartNew();
        public MainForm(bool connectOnShow=true)
        {
            AutoScaleMode=AutoScaleMode.None;Text="豆子 Bean Studio V5.7";Font=new Font("Microsoft YaHei UI",10);ForeColor=Theme.Ink;BackColor=Color.FromArgb(230,237,250);FormBorderStyle=FormBorderStyle.None;DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);ClientSize=new Size(1240,860);MinimumSize=new Size(1040,760);StartPosition=FormStartPosition.CenterScreen;
            Controls.AddRange(new Control[]{title,subtitle,edition,sessionBadge,source,picture,timerPanel,live,run,floating,clear,save,minimize,close,status});
            source.Controls.AddRange(new Control[]{sourceTitle,windows,refresh,scan});picture.Controls.AddRange(new Control[]{pictureTitle,pictureHint,preview,previewNote,left,right});timerPanel.Controls.AddRange(new Control[]{timerTitle,duration,clockLabel,timerState,timerRule,rail});live.Controls.AddRange(new Control[]{liveTitle,crop,dots,countLabel,gameTitle,gameValue,gameState,calibrate});picture.Controls.Add(metrics);
            BackColor=Theme.Bg;sourceTitle.ForeColor=timerTitle.ForeColor=liveTitle.ForeColor=Theme.Ink;subtitle.ForeColor=edition.ForeColor=pictureHint.ForeColor=previewNote.ForeColor=timerRule.ForeColor=metrics.ForeColor=status.ForeColor=duration.ForeColor=gameState.ForeColor=Theme.Muted;
            clockLabel.Font=new Font("Bahnschrift Light",62,FontStyle.Regular);clockLabel.ForeColor=Theme.Blue;clockLabel.TextAlign=ContentAlignment.MiddleCenter;timerState.TextAlign=ContentAlignment.MiddleCenter;timerRule.TextAlign=ContentAlignment.MiddleLeft;countLabel.TextAlign=ContentAlignment.MiddleRight;gameValue.Font=new Font("Bahnschrift",28);gameValue.ForeColor=Theme.Ink;sessionBadge.Font=Font;
            foreach(GlassButton b in new GlassButton[]{run,floating,clear,save,scan,refresh,left,right,calibrate,minimize,close}){b.AccessibleName=b.AccessibleName??b.Text;b.TabStop=true;}
            source.TabIndex=0;windows.TabIndex=0;refresh.TabIndex=1;scan.TabIndex=2;picture.TabIndex=1;left.TabIndex=0;right.TabIndex=1;live.TabIndex=2;calibrate.TabIndex=0;run.TabIndex=3;floating.TabIndex=4;clear.TabIndex=5;save.TabIndex=6;minimize.TabIndex=7;close.TabIndex=8;minimize.AccessibleName="最小化窗口";close.AccessibleName="关闭窗口";
            windows.Font=Font;
            calibrate.Click+=delegate{using(Bitmap b=preview.CopyFrame())using(ClockPanel panel=new ClockPanel(engine.Clock,b)){panel.ShowDialog(this);}};
            tips.SetToolTip(calibrate,"查看实际时间区域、失败原因；在扫描预览上手动框选中间数字。");
            refresh.Click+=delegate{RefreshWindows();};scan.Click+=delegate{RequestPreview();};left.Click+=delegate{SelectSide(0);};right.Click+=delegate{SelectSide(1);};
            run.Click+=delegate{if(Selected==null){Notice("请先打开并选择 MuMu 窗口。");return;}monitoring=!monitoring;run.Text=monitoring?"暂停监测":"开始监测";run.Icon=monitoring?"pause":"play";SyncEngine(false);};
            floating.Text="悬浮倒计时 ▾";floating.Click+=delegate{floatingMenu.Show(floating,new Point(0,-floatingMenu.PreferredSize.Height-6));};clear.Click+=delegate{engine.Reset();};
            save.Click+=delegate{try{string folder=engine.Export(AppDomain.CurrentDomain.BaseDirectory);Notice("诊断已保存："+Path.GetFileName(folder));}catch(Exception ex){Notice("保存失败："+ex.Message);}};
            minimize.Click+=delegate{WindowState=FormWindowState.Minimized;};close.Click+=delegate{Close();};
            overlay=new HudHost(engine,delegate{if(!IsDisposed)BeginInvoke((Action)delegate{Show();WindowState=FormWindowState.Normal;Activate();overlay.Hide();});},delegate(int selected){if(!IsDisposed)BeginInvoke((Action)delegate{SelectSide(selected);});});
            windows.SelectedIndexChanged+=delegate{if(!demo){SyncEngine(true);if(Visible)RequestPreview();}};
            Point offset=Point.Empty;bool dragging=false;foreach(Control c in new Control[]{this,title,subtitle,edition})
            {c.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left&&Cursor.Position.Y<Top+95){dragging=true;offset=new Point(Cursor.Position.X-Left,Cursor.Position.Y-Top);((Control)sender).Capture=true;}};c.MouseMove+=delegate{if(dragging)Location=new Point(Cursor.Position.X-offset.X,Cursor.Position.Y-offset.Y);};c.MouseUp+=delegate(object sender,MouseEventArgs e){dragging=false;((Control)sender).Capture=false;};}
            floatingMenu.Font=Font;floatingMenu.Items.Add("打开悬浮窗 · 默认置顶",null,delegate{overlay.ShowAt(new Point(Left+24,Top+120));});floatingMenu.Items.Add("关闭悬浮窗",null,delegate{overlay.Hide();});floatingMenu.Items.Add(new ToolStripSeparator());floatingMenu.Items.Add(new ToolStripMenuItem("悬浮窗内可直接切换左右；主界面保持打开"){Enabled=false});
            tips.SetToolTip(floating,"先打开菜单，再选择显示悬浮窗。默认置顶，主界面不会自动隐藏。");tips.SetToolTip(left,"监测左侧；切换时重新建立豆数基准，不触发计时。");tips.SetToolTip(right,"监测右侧；增长不触发，减少重置完整15秒。");tips.SetToolTip(scan,"截取定位预览时主界面短暂收起。请露出顶部血条和豆子。");tips.SetToolTip(run,"开始后启用高速采样；暂停不会清除已有倒计时。");
            Shown+=delegate{MinimumSize=new Size(1040,760);if(connectOnShow)RefreshWindows();};pulse.Tick+=Tick;pulse.Start();uiReady=true;LayoutUI();
        }
        protected override void OnResize(EventArgs e){base.OnResize(e);if(uiReady&&WindowState!=FormWindowState.Minimized&&ClientSize.Width>0&&ClientSize.Height>0){LayoutUI();Theme.RoundedWindow(this,16);}}
        protected override void OnPaintBackground(PaintEventArgs e){Theme.Background(e.Graphics,ClientSize);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;for(int i=0;i<4;i++){float x=30+(i%2)*18,y=30+(i/2)*21;PointF[] points={new PointF(x+7,y),new PointF(x+14,y+10),new PointF(x+7,y+20),new PointF(x,y+10)};using(Brush b=new SolidBrush(i==3?Color.FromArgb(140,196,204):Theme.Blue))g.FillPolygon(b,points);}}
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);if(m.Msg==0x84&&(int)m.Result==1)
            {Point p=PointToClient(new Point(unchecked((short)(long)m.LParam),unchecked((short)((long)m.LParam>>16))));bool l=p.X<8,r=p.X>Width-9,t=p.Y<8,b=p.Y>Height-9;m.Result=(IntPtr)(t?(l?13:r?14:12):b?(l?16:r?17:15):l?10:r?11:1);}
        }
        void LayoutUI()
        {
            if(!uiReady||WindowState==FormWindowState.Minimized||ClientSize.Width<900||ClientSize.Height<700)return;
            int w=ClientSize.Width,h=ClientSize.Height,margin=24,gap=18,rightWidth=334,leftWidth=w-margin*2-gap-rightWidth,contentY=184,contentH=h-292,timerH=Math.Min(244,contentH-246);
            title.SetBounds(82,21,220,42);subtitle.SetBounds(84,62,360,25);sessionBadge.SetBounds(w-336,31,146,34);edition.SetBounds(w-178,34,48,27);minimize.SetBounds(w-120,26,44,44);close.SetBounds(w-69,26,44,44);
            source.SetBounds(margin,102,w-margin*2,64);sourceTitle.SetBounds(22,17,86,30);windows.SetBounds(110,10,source.Width-390,44);refresh.SetBounds(source.Width-264,10,114,44);scan.SetBounds(source.Width-140,10,120,44);
            picture.SetBounds(margin,contentY,leftWidth,contentH);pictureTitle.SetBounds(22,18,leftWidth-225,28);pictureHint.SetBounds(leftWidth-198,21,174,24);preview.SetBounds(20,58,leftWidth-40,contentH-139);previewNote.SetBounds(24,contentH-72,leftWidth-225,27);metrics.SetBounds(24,contentH-40,leftWidth-48,23);left.SetBounds(leftWidth-185,contentH-76,74,39);right.SetBounds(leftWidth-103,contentH-76,79,39);
            timerPanel.SetBounds(margin+leftWidth+gap,contentY,rightWidth,timerH);timerTitle.SetBounds(22,17,200,27);duration.SetBounds(rightWidth-75,19,54,24);clockLabel.SetBounds(16,47,rightWidth-32,timerH-148);timerState.SetBounds(18,timerH-101,rightWidth-36,23);rail.SetBounds(22,timerH-71,rightWidth-44,17);timerRule.SetBounds(22,timerH-48,rightWidth-44,38);
            float clockSize=SafeClockSize(timerH);if(Math.Abs(clockLabel.Font.Size-clockSize)>.1){Font old=clockLabel.Font;clockLabel.Font=new Font("Bahnschrift Light",clockSize,FontStyle.Regular);old.Dispose();}
            live.SetBounds(timerPanel.Left,contentY+timerH+gap,rightWidth,contentH-timerH-gap);liveTitle.SetBounds(22,14,rightWidth-44,25);crop.SetBounds(22,45,rightWidth-44,46);dots.SetBounds(22,96,160,31);countLabel.SetBounds(180,96,rightWidth-204,31);gameTitle.SetBounds(22,139,140,24);calibrate.SetBounds(rightWidth-100,134,78,34);gameValue.SetBounds(22,170,132,40);gameState.SetBounds(159,170,rightWidth-181,47);
            run.SetBounds(margin,h-94,174,50);floating.SetBounds(margin+186,h-94,160,50);clear.SetBounds(margin+358,h-94,134,50);save.SetBounds(w-margin-134,h-94,134,50);status.SetBounds(margin+4,h-34,w-margin*2-8,25);
        }
        internal static float SafeClockSize(int timerHeight){return Math.Max(32,Math.Min(64,(timerHeight-144)*.60f));}
        WindowItem Selected{get{return windows.SelectedItem as WindowItem;}}
        void SelectSide(int value){if(value==side)return;side=value;left.Selected=side==0;right.Selected=side==1;left.Invalidate();right.Invalidate();preview.Side=side;preview.Invalidate();SyncEngine(true);}
        void SyncEngine(bool relocate){engine.ConfigureAuto(Selected==null?IntPtr.Zero:Selected.Handle,side,monitoring,relocate);}
        void Notice(string message){transient=message;transientUntil=uptime.Elapsed.TotalSeconds+8;status.Text=message;}
        void RefreshWindows()
        {
            IntPtr previous=Selected==null?IntPtr.Zero:Selected.Handle;List<WindowItem> list=new List<WindowItem>();uint own=(uint)Process.GetCurrentProcess().Id;
            Native.EnumWindows(delegate(IntPtr h,IntPtr p){uint pid;Native.GetWindowThreadProcessId(h,out pid);if(pid==own||!Native.IsWindowVisible(h))return true;StringBuilder t=new StringBuilder(512);Native.GetWindowText(h,t,t.Capacity);string name=t.ToString();if(name.IndexOf("mumu",StringComparison.OrdinalIgnoreCase)>=0&&Native.ClientBounds(h).Width>320)list.Add(new WindowItem{Handle=h,Title=name});return true;},IntPtr.Zero);
            windows.Items.Clear();foreach(WindowItem item in list)windows.Items.Add(item);int index=list.FindIndex(delegate(WindowItem item){return item.Handle==previous;});windows.SelectedIndex=index>=0?index:(list.Count>0?0:-1);if(list.Count==0){SyncEngine(true);Notice("未发现 MuMu 窗口，请打开模拟器后刷新列表。");}
        }
        void RequestPreview()
        {
            WindowItem selected=Selected;if(selected==null||refreshing)return;if(Native.IsIconic(selected.Handle)){Notice("请先恢复 MuMu 窗口。");return;}
            refreshing=true;bool wasOverlay=overlay.Visible;overlay.Hide();Hide();Native.SetForegroundWindow(selected.Handle);Timer shot=new Timer{Interval=250};
            shot.Tick+=delegate
            {
                shot.Stop();shot.Dispose();
                try
                {
                    Rectangle bounds=Native.ClientBounds(selected.Handle);if(bounds.Width<320||!SystemInformation.VirtualScreen.Contains(bounds))throw new InvalidOperationException("请把 MuMu 窗口完整移到屏幕内。");
                    using(Bitmap image=WindowCapture.Capture(selected.Handle,bounds)){LocatedPair pair=AutoLocator.Find(image);preview.LeftRegion=Normalized(pair.Left,image.Size);preview.RightRegion=Normalized(pair.Right,image.Size);preview.SetFrame((Bitmap)image.Clone());}
                    SyncEngine(true);
                }
                catch(Exception ex){Notice("扫描失败："+ex.Message);}
                finally{refreshing=false;Show();Activate();if(wasOverlay)overlay.ShowAt(new Point(Left+24,Top+24));}
            };shot.Start();
        }
        static RectangleF Normalized(Rectangle r,Size size){return r.IsEmpty?RectangleF.Empty:new RectangleF(r.X/(float)size.Width,r.Y/(float)size.Height,r.Width/(float)size.Width,r.Height/(float)size.Height);}
        void Tick(object sender,EventArgs e)
        {
            Snapshot s=engine.GetSnapshot();clockLabel.Text=Theme.Clock(s.Remaining);timerState.Text=s.Remaining>0?"计时中 · 再次减少即重置":"等待下一次少豆";clockLabel.ForeColor=Theme.Urgency(s.Remaining);timerState.Text=s.Remaining>0&&s.Remaining<3?"即将恢复 · 留意游戏时间":timerState.Text;timerRule.Text=s.Recovery;rail.Remaining=s.Remaining;rail.Invalidate();
            ClockState cs=engine.Clock.State();bool fresh=cs.Value.HasValue&&cs.Age<=.65;gameValue.Text=fresh?cs.Value.Value.ToString(cs.Value<10?"0.00":"0",CultureInfo.InvariantCulture):"—";gameState.Text=fresh?"已确认\r\n持续读取中":cs.Value.HasValue?"读数已过期\r\n可点校准检查":"等待时间数字\r\n可点校准检查";tips.SetToolTip(gameState,cs.Status+"\r\n"+cs.Backend+"\r\n识别方式："+cs.Method+"；本帧匹配分数："+cs.Confidence.ToString("0.00",CultureInfo.InvariantCulture));gameValue.AccessibleName="游戏时间："+(fresh?gameValue.Text:"未确认");sessionBadge.Caption=Selected==null?"尚未连接":s.Enabled?"正在监测":"预览模式";sessionBadge.Active=s.Enabled;sessionBadge.Invalidate();
            Reading display=s.Enabled?s.ConfirmedReading:s.Reading;
            dots.Reading=display;dots.Invalidate();countLabel.Text=display!=null&&display.Valid?display.Count+" / 4 颗":"待确认";
            metrics.Text=!s.Enabled?"开始监测后显示采样状态":string.Format("采样 {0:0} ms    确认 {1:0} ms    {2}",s.GapMs,s.ConfirmationMs,WindowCapture.Status=="WGC 窗口捕获"?"窗口捕获":"兼容 / 等待帧");metrics.ForeColor=s.Enabled&&s.GapMs>100?Theme.Red:Theme.Muted;
            status.Text=uptime.Elapsed.TotalSeconds<transientUntil?transient:s.Status;previewNote.Text=(side==0?"监测左侧":"监测右侧")+"  ·  增长不触发";
            if(++uiFrames%4==0&&WindowState!=FormWindowState.Minimized){Bitmap b=engine.GetFrame();crop.SetFrame(s.Reading==null?null:b);if(s.Reading==null&&b!=null)b.Dispose();preview.LeftRegion=s.LeftRegion;preview.RightRegion=s.RightRegion;preview.Invalidate();}
        }
        public void RenderDemo(Bitmap frame,string path)
        {
            demo=true;pulse.Stop();LocatedPair p=AutoLocator.Find(frame);preview.SetFrame((Bitmap)frame.Clone());preview.LeftRegion=Normalized(p.Left,frame.Size);preview.RightRegion=Normalized(p.Right,frame.Size);if(!p.Right.IsEmpty)crop.SetFrame(frame.Clone(p.Right,PixelFormat.Format32bppArgb));dots.Reading=p.RightReading;
            windows.Items.Add(new WindowItem{Title="MuMu 安卓设备"});windows.SelectedIndex=0;clockLabel.Text="15.00";clockLabel.ForeColor=Theme.Blue;timerState.Text="计时中 · 再次减少即重置";countLabel.Text=p.RightReading!=null&&p.RightReading.Valid?p.RightReading.Count+" / 4 颗":"待确认";metrics.Text="设计预览 · 演示数据";gameValue.Text="37";gameState.Text="演示读数\r\n未连接游戏";timerRule.Text="恢复点：22 秒（演示）";rail.Remaining=15;sessionBadge.Caption="设计预览";previewNote.Text="监测右侧  ·  增长不触发";status.Text="自动定位左右区域  ·  变色与增长不触发计时  ·  再次减少，重置 15 秒";run.Text="暂停监测";run.Icon="pause";
            StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);Show();Application.DoEvents();SaveRender(path);ClientSize=new Size(1040,760);Application.DoEvents();SaveRender(Path.Combine(Path.GetDirectoryName(path),"紧凑界面预览.png"));ClientSize=new Size(1440,960);Application.DoEvents();SaveRender(Path.Combine(Path.GetDirectoryName(path),"大尺寸界面预览.png"));ClientSize=new Size(1040,760);Application.DoEvents();SaveRender(Path.Combine(Path.GetDirectoryName(path),"最小界面预览.png"));Hide();
            using(Overlay hud=new Overlay(delegate{})){hud.ClockLabel.Text="15.00";hud.Detail.Text="3 / 4 颗";hud.Location=new Point(-20000,-20000);hud.Show();Application.DoEvents();using(Bitmap b=hud.Surface()){b.Save(Path.Combine(Path.GetDirectoryName(path),"悬浮窗预览.png"));}hud.Hide();}
        }
        public void RenderStates(Bitmap frame,string folder)
        {
            demo=true;pulse.Stop();Directory.CreateDirectory(folder);StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);ClientSize=new Size(1040,760);Show();Application.DoEvents();
            windows.Items.Clear();windows.SelectedIndex=-1;preview.SetFrame(null);crop.SetFrame(null);countLabel.Text="待确认";clockLabel.Text="00.00";gameValue.Text="—";gameState.Text="等待时间数字\r\n可点校准检查";timerState.Text="等待下一次少豆";timerRule.Text="少豆时计算恢复点";sessionBadge.Caption="尚未连接";status.Text="选择 MuMu 窗口，然后扫描画面。";run.Text="开始监测";run.Icon="play";rail.Remaining=0;SaveRender(Path.Combine(folder,"未连接.png"));
            windows.Items.Add(new WindowItem{Title="MuMu 安卓设备（演示）"});windows.SelectedIndex=0;preview.SetFrame((Bitmap)frame.Clone());sessionBadge.Caption="设计预览";clockLabel.Text="08.00";clockLabel.ForeColor=Theme.Blue;timerState.Text="计时中 · 再次减少即重置";timerRule.Text="恢复点：时间未确认（可点校准检查）";rail.Remaining=8;gameState.Text="等待时间数字\r\n可点校准检查";status.Text="设计预览 · 时间未确认时保持冷却计时";run.Text="暂停监测";run.Icon="pause";SaveRender(Path.Combine(folder,"时间未确认.png"));
            clockLabel.Text="02.00";clockLabel.ForeColor=Theme.Urgency(2);timerState.Text="即将恢复 · 留意游戏时间";timerRule.Text="恢复点≈3.50 秒（中途补算）";rail.Remaining=2;gameValue.Text="5.50";gameState.Text="演示读数\r\n未连接游戏";status.Text="设计预览 · 接近归零时使用紧急颜色";SaveRender(Path.Combine(folder,"接近恢复.png"));
            run.Text="开始监测";run.Icon="play";sessionBadge.Caption="暂停监测";status.Text="监测暂停 · 已有冷却继续";SaveRender(Path.Combine(folder,"暂停监测.png"));Hide();using(ClockPanel panel=new ClockPanel(engine.Clock,frame))panel.SavePreview(folder);
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
                foreach(Control parent in new Control[]{source,picture,timerPanel,live})foreach(Control c in parent.Controls)if(c.Visible&&!parent.ClientRectangle.Contains(c.Bounds))throw new Exception("控件超出容器："+c.Text);if(rail.Top<timerState.Bottom)throw new Exception("冷却进度覆盖状态");report.Add("PASS 最小尺寸下关键控件均在容器内，进度条不覆盖状态");
                ClientSize=new Size(1040,800);Application.DoEvents();
                foreach(GlassButton button in new GlassButton[]{run,floating,left,right,scan,refresh}){typeof(Control).GetMethod("OnMouseEnter",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});button.Refresh();typeof(Control).GetMethod("OnMouseLeave",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});button.Refresh();}
                Refresh();Application.DoEvents();System.Threading.Thread.Sleep(80);Rectangle bounds=Native.ClientBounds(Handle);if(!SystemInformation.VirtualScreen.Contains(bounds))throw new Exception("测试窗口超出屏幕，无法验证实际绘制");
                Bitmap actual=null;string captureMethod="WGC 窗口捕获";using(WindowFrames frames=new WindowFrames(Handle)){Stopwatch wait=Stopwatch.StartNew();while(actual==null&&wait.ElapsedMilliseconds<3000){Application.DoEvents();try{actual=frames.Crop(bounds);}catch(InvalidOperationException){System.Threading.Thread.Sleep(12);}}}if(actual==null)throw new Exception("实际窗口帧未到达，未采集其他窗口");using(Bitmap capture=actual){capture.Save(Path.Combine(folder,"实际窗口绘制.png"));Color button=capture.GetPixel(run.Right-18,run.Top+run.Height/2);if(button.R>60||button.G>110||button.B>130)throw new Exception("实际主按钮被背景遮挡，截图颜色："+button);Color canvas=capture.GetPixel(picture.Left+preview.Left+12,picture.Top+preview.Top+preview.Height/2);if(canvas.R>70||canvas.G>90||canvas.B>110)throw new Exception("实际预览画布被遮挡，截图颜色："+canvas);}if(fault!=null)throw fault;report.Add("PASS 实际主按钮与预览画布可见，已保存悬停/恢复后的实际绘制（"+captureMethod+"，非 DrawToBitmap）");
                File.WriteAllLines(Path.Combine(folder,"界面交互验证.txt"),report.ToArray());
            }
            finally{Application.ThreadException-=handler;overlay.Hide();Hide();}
        }
        static void WaitUI(Func<bool> condition){Stopwatch t=Stopwatch.StartNew();while(!condition()&&t.ElapsedMilliseconds<1500){Application.DoEvents();System.Threading.Thread.Sleep(4);}}
        protected override void Dispose(bool disposing){if(disposing&&!released){released=true;pulse.Dispose();tips.Dispose();floatingMenu.Dispose();if(overlay!=null)overlay.Dispose();engine.Dispose();}base.Dispose(disposing);}
    }
}



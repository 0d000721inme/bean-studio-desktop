using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Drawing.Imaging;using System.Runtime.InteropServices;using System.Windows.Forms;
namespace MuMuBeans {
sealed class Overlay:Form {
 public Label ClockLabel=Theme.Label("00.00",30,true),Detail=Theme.Label("等待画面",8),Target=Theme.Label("等待少豆",8);
 public bool NoBeans;
 public event EventHandler SurfacePresented;
 readonly Action restore;readonly Action<int> choose;int side=1;bool dragging;Point offset;
 readonly Rectangle leftButton=new Rectangle(122,8,38,21),rightButton=new Rectangle(165,8,38,21);
 public Overlay(Action restore,Action<int> chooseSide=null){this.restore=restore;choose=chooseSide;Text="豆子 · 轻透悬浮窗";ClientSize=new Size(216,112);FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;AutoScaleMode=AutoScaleMode.None;ClockLabel.ForeColor=Color.FromArgb(74,196,255);}
 protected override CreateParams CreateParams{get{var cp=base.CreateParams;cp.ExStyle|=0x80000|0x08000000;return cp;}}
 protected override bool ShowWithoutActivation{get{return true;}}
 public void SetSide(int value){side=value;}
 internal void ClickSideForTest(int value){side=value;if(choose!=null)choose(value);RefreshSurface();}
 protected override void OnShown(EventArgs e){base.OnShown(e);RefreshSurface();}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;if(leftButton.Contains(e.Location)){ClickSideForTest(0);return;}if(rightButton.Contains(e.Location)){ClickSideForTest(1);return;}dragging=true;offset=e.Location;Capture=true;}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)Location=new Point(Cursor.Position.X-offset.X,Cursor.Position.Y-offset.Y);}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;}
 protected override void OnDoubleClick(EventArgs e){base.OnDoubleClick(e);if(restore!=null)restore();}
 static void TextPath(Graphics g,string text,float size,RectangleF area,Color color,bool bold,bool outline){using(FontFamily family=new FontFamily("Microsoft YaHei UI"))using(GraphicsPath p=new GraphicsPath())using(StringFormat fmt=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center}){p.AddString(text??"",family,(int)(bold?FontStyle.Bold:FontStyle.Regular),size,area,fmt);if(outline)using(Pen pen=new Pen(Color.FromArgb(230,12,22,36),size>25?3:2){LineJoin=LineJoin.Round})g.DrawPath(pen,p);using(Brush b=new SolidBrush(color))g.FillPath(b,p);}}
 public Bitmap Surface(){Bitmap b=new Bitmap(Width,Height,PixelFormat.Format32bppPArgb);using(Graphics g=Graphics.FromImage(b)){g.SmoothingMode=SmoothingMode.AntiAlias;using(GraphicsPath p=Theme.Round(new RectangleF(1,1,Width-2,Height-2),17)){using(Brush brush=new SolidBrush(Color.FromArgb(45,15,27,42)))g.FillPath(brush,p);using(Pen pen=new Pen(Color.FromArgb(55,220,241,255)))g.DrawPath(pen,p);}
 TextPath(g,Detail.Text,10,new RectangleF(8,9,107,18),Color.White,false,true);
 Color accent=NoBeans?Color.FromArgb(214,229,242):ClockLabel.ForeColor;if(accent==Theme.Blue)accent=Color.FromArgb(89,211,255);if(accent==Theme.Muted)accent=Color.FromArgb(214,229,242);
 TextPath(g,NoBeans?"对方已无豆":ClockLabel.Text,NoBeans?26:42,new RectangleF(6,28,204,53),accent,true,true);
 TextPath(g,Target.Text,10.5f,new RectangleF(5,83,206,21),Color.White,false,true);
 for(int i=0;i<2;i++){Rectangle rect=i==0?leftButton:rightButton;using(GraphicsPath p=Theme.Round(rect,8))using(Brush brush=new SolidBrush(i==side?Color.FromArgb(185,14,119,136):Color.FromArgb(65,18,35,49)))g.FillPath(brush,p);TextPath(g,i==0?"左":"右",11,rect,Color.White,i==side,false);}}
 return b;}
 public void RefreshSurface(){if(!IsHandleCreated||!Visible)return;using(Bitmap bitmap=Surface()){IntPtr screen=GetDC(IntPtr.Zero),dc=CreateCompatibleDC(screen),dib=bitmap.GetHbitmap(Color.FromArgb(0)),old=SelectObject(dc,dib);try{P point=new P{X=Left,Y=Top},origin=new P();S size=new S{W=Width,H=Height};B blend=new B{Alpha=255,Format=1};if(!UpdateLayeredWindow(Handle,screen,ref point,ref size,dc,ref origin,0,ref blend,2))throw new System.ComponentModel.Win32Exception();}finally{SelectObject(dc,old);DeleteObject(dib);DeleteDC(dc);ReleaseDC(IntPtr.Zero,screen);}}if(SurfacePresented!=null)SurfacePresented(this,EventArgs.Empty);}
 protected override void Dispose(bool disposing){if(disposing){ClockLabel.Dispose();Detail.Dispose();Target.Dispose();}base.Dispose(disposing);}
 [StructLayout(LayoutKind.Sequential)]struct P{public int X,Y;}[StructLayout(LayoutKind.Sequential)]struct S{public int W,H;}[StructLayout(LayoutKind.Sequential,Pack=1)]struct B{public byte Op,Flags,Alpha,Format;}
 [DllImport("user32.dll",SetLastError=true)]static extern bool UpdateLayeredWindow(IntPtr h,IntPtr d,ref P p,ref S s,IntPtr source,ref P origin,int key,ref B blend,int flags);
 [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr h);[DllImport("user32.dll")]static extern int ReleaseDC(IntPtr h,IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);[DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);[DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);[DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
}}

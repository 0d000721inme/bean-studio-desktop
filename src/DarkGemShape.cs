using System;using System.Drawing;
namespace MuMuBeans {
// Blue-minus-green retains the dark cavity under red exit effects.
// The rim and vertical symmetry checks reject flat fields and displaced glow.
static class DarkGemShape {
 static readonly PointF[] CoreOffsets={PointF.Empty,new PointF(-.13f,0),new PointF(.13f,0),new PointF(0,-.225f),new PointF(0,.225f)};
 static readonly double[] RimX={-.40,.40},RimY={-.10,0,.10};
 static void Read(PixelMap m,int w,int h,double x,double y,out int r,out int g,out int b){m.Read(Math.Max(0,Math.Min(w-1,(int)Math.Round(x))),Math.Max(0,Math.Min(h-1,(int)Math.Round(y))),out r,out g,out b);}
 public static double Score(PixelMap m,int w,int h,double x,double y,double pitch,double height){
 double core=0,bmean=0,left=0,right=0,top=0,bottom=0;int i=0;
 // Keep the original point order and arithmetic, without temporary bg/blue arrays.
 foreach(PointF d in CoreOffsets){
 int r,g,b;Read(m,w,h,x+pitch*d.X,y+height*d.Y,out r,out g,out b);double bg=b-g;core+=bg;bmean+=b;if(b>170||b<40)return -1;
 if(i==1)left=bg;else if(i==2)right=bg;else if(i==3)top=bg;else if(i==4)bottom=bg;i++;}core/=5;bmean/=5;
 if(core<18||bmean>150)return -1;
 double rim=0;foreach(double dx in RimX)foreach(double dy in RimY){int r,g,b;Read(m,w,h,x+pitch*dx,y+height*dy,out r,out g,out b);rim+=b-g;}
 double contrast=core-rim/6;if(contrast<8)return -1;
 double asym=Math.Abs(top-bottom);if(asym>23)return -1;
 return 55+Math.Min(contrast,60)-asym*1.5-Math.Abs(left-right)*.5;
 }
}}

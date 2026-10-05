using System;using System.Drawing;
namespace MuMuBeans {
// Blue-minus-green retains the dark cavity under red exit effects.
// The rim and vertical symmetry checks reject flat fields and displaced glow.
static class DarkGemShape {
 static void Read(PixelMap m,int w,int h,double x,double y,out int r,out int g,out int b){m.Read(Math.Max(0,Math.Min(w-1,(int)Math.Round(x))),Math.Max(0,Math.Min(h-1,(int)Math.Round(y))),out r,out g,out b);}
 public static double Score(PixelMap m,int w,int h,double x,double y,double pitch,double height){
 double[] bg=new double[5],blue=new double[5];int i=0;
 foreach(PointF d in new PointF[]{PointF.Empty,new PointF(-.13f,0),new PointF(.13f,0),new PointF(0,-.225f),new PointF(0,.225f)}){
 int r,g,b;Read(m,w,h,x+pitch*d.X,y+height*d.Y,out r,out g,out b);bg[i]=b-g;blue[i]=b;i++;}
 double core=0,bmean=0;for(i=0;i<5;i++){core+=bg[i];bmean+=blue[i];if(blue[i]>170||blue[i]<40)return -1;}core/=5;bmean/=5;
 if(core<18||bmean>150)return -1;
 double rim=0;foreach(double dx in new double[]{-.40,.40})foreach(double dy in new double[]{-.10,0,.10}){int r,g,b;Read(m,w,h,x+pitch*dx,y+height*dy,out r,out g,out b);rim+=b-g;}
 double contrast=core-rim/6;if(contrast<8)return -1;
 double asym=Math.Abs(bg[3]-bg[4]);if(asym>23)return -1;
 return 55+Math.Min(contrast,60)-asym*1.5-Math.Abs(bg[1]-bg[2])*.5;
 }
}}

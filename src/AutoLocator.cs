using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MuMuBeans
{
    sealed class LocatedPair
    {
        public Rectangle Left,Right;
        public Reading LeftReading,RightReading;
        public double LeftScore,RightScore;public bool LeftPurple,RightPurple;
    }
    static class AutoLocator
    {
        sealed class Candidate{public int X,Y;public double Pitch,Score;public bool Purple;}
        sealed class Map
        {
            public int W,H;public double[] Energy;public bool[] Color,Red,Dark,Purple;
            public double E(int x,int y){return Energy[Math.Max(0,Math.Min(H-1,y))*W+Math.Max(0,Math.Min(W-1,x))];}
            public bool C(int x,int y){return Color[Math.Max(0,Math.Min(H-1,y))*W+Math.Max(0,Math.Min(W-1,x))];}
            public bool R(int x,int y){return Red[Math.Max(0,Math.Min(H-1,y))*W+Math.Max(0,Math.Min(W-1,x))];}
            public bool P(int x,int y){return Purple[Math.Max(0,Math.Min(H-1,y))*W+Math.Max(0,Math.Min(W-1,x))];}
            public bool D(int x,int y){return Dark[Math.Max(0,Math.Min(H-1,y))*W+Math.Max(0,Math.Min(W-1,x))];}
        }
        public static LocatedPair Find(Bitmap frame,int threshold=150)
        {
            LocatedPair pair=new LocatedPair();if(frame.Width<320||frame.Height<150)return pair;
            double scale=Math.Min(1,1024.0/frame.Width);int w=(int)Math.Round(frame.Width*scale),h=(int)Math.Round(frame.Height*.28*scale);
            using(Bitmap top=new Bitmap(w,h))
            {
                using(Graphics g=Graphics.FromImage(top)){g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.DrawImage(frame,new Rectangle(0,0,w,h),new Rectangle(0,0,frame.Width,(int)(frame.Height*.28)),GraphicsUnit.Pixel);}
                PixelMap pixels=new PixelMap(top,new Rectangle(Point.Empty,top.Size));Map m=new Map{W=w,H=h,Energy=new double[w*h],Color=new bool[w*h],Red=new bool[w*h],Dark=new bool[w*h],Purple=new bool[w*h]};
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                {
                    int r,g,b;pixels.Read(x,y,out r,out g,out b);int max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),i=y*w+x;
                    m.Energy[i]=max+(r+g+b-max-min)*.5;
                    m.Color[i]=(b>=40&&b-r>=14&&g-r>=5&&b>=g*.82)||(r>=60&&r-b>=18&&g>=r*.18&&r>=g*.85)||(b>=40&&r>=15&&b-g>=20&&r-g>=10)||(min>=180&&max-min<55);
                    m.Purple[i]=b>110&&r>65&&r-g>30&&b-g>40;
                    m.Red[i]=r>150&&r>g*1.6&&b<r*.48&&g>=15;
                    m.Dark[i]=(b>=45&&b<=155&&g<=b*.82&&r<=b*.80)||(b>=50&&b<=170&&r>=40&&r<=160&&g<=Math.Min(r,b)*.7);
                }
                // Fall back to four strong gem shapes when the health bar is empty.
                // This pass permits no missing slot; uncertainty is never a zero count.
                for(int pass=0;pass<2;pass++)for(int side=0;side<2;side++)
                {
                    if(pass==1&&!(side==0?pair.Left:pair.Right).IsEmpty)continue;
                    List<Candidate> candidates=new List<Candidate>();double minimum=0;
                    for(double p=w*.0145;p<=w*.0180;p+=.5)
                    {
                        if(side==1 && !pair.Left.IsEmpty && Math.Abs(p-pair.Left.Width*.25*scale)>pair.Left.Width*.25*scale*.08)continue;
                        int xmin=(int)(w*(side==0?.07:.78)),xmax=Math.Min((int)(w*(side==0?.20:.89)),w-(int)(p*3.7));
                        for(int y=(int)(frame.Height*scale*.035);y<h-p*.8;y++)for(int x=xmin;x<xmax;x++)
                        {
                            bool purpleBar=PurpleBar(m,x,y,p,side);
                            if(side==1 && !pair.Left.IsEmpty && !purpleBar && (y/scale<pair.Left.Top+pair.Left.Height*.25 || y/scale>pair.Left.Top+pair.Left.Height*.75))continue;
                            if(pass==0&&!purpleBar&&!BloodBar(m,x,y,p,side))continue;
                            double sum=0,min=10000;bool valid=true;int weak=0;
                            // Coarse downsampling can weaken one dark outline. All four
                            // cells must still pass the full-resolution detector below.
                            for(int i=0;i<4;i++){double score=pass==0?Diamond(m,x+(int)Math.Round(i*p),y,p):Math.Max(DarkGemShape.Score(pixels,w,h,x+i*p,y,p,p*1.25),Diamond(m,x+(int)Math.Round(i*p),y,p));if(score<0){weak++;score=0;if(weak>(pass==0?1:0)){valid=false;break;}}sum+=score;min=Math.Min(min,score);}
                            if(!valid)continue;double quality=min*.55+sum/4*.45-Math.Abs(p-w*.0159)*2;
                            if(quality<20||quality<minimum)continue;
                            candidates.Add(new Candidate{X=x,Y=y,Pitch=p,Score=quality,Purple=purpleBar});
                            if(candidates.Count>400){candidates.Sort(delegate(Candidate a,Candidate b){return b.Score.CompareTo(a.Score);});candidates.RemoveRange(200,candidates.Count-200);minimum=candidates[candidates.Count-1].Score;}
                        }
                    }
                    candidates.Sort(delegate(Candidate a,Candidate b){return b.Score.CompareTo(a.Score);});
                    foreach(Candidate c in candidates)
                    {
                        Rectangle r=new Rectangle((int)Math.Round((c.X-c.Pitch*.5)/scale),(int)Math.Round((c.Y-c.Pitch*.625)/scale),(int)Math.Round(c.Pitch*4/scale),(int)Math.Round(c.Pitch*1.25/scale));
                        Reading reading=Detector.Analyze(frame,r,threshold);
                        if(!reading.Valid)continue;
                        if(side==0){pair.LeftPurple=c.Purple;pair.Left=r;pair.LeftReading=reading;pair.LeftScore=c.Score;}else{pair.RightPurple=c.Purple;pair.Right=r;pair.RightReading=reading;pair.RightScore=c.Score;}break;
                    }
                }
            }
            // Both HUDs share a row and scale. Refuse a clearly inconsistent extra match.
            if(!pair.Left.IsEmpty&&!pair.Right.IsEmpty)
            {
                double delta=Math.Abs((pair.Left.Top+pair.Left.Height*.5)-(pair.Right.Top+pair.Right.Height*.5));
                if(delta>Math.Max(pair.Left.Height,pair.Right.Height)*(pair.LeftPurple||pair.RightPurple?1.0:.45)){if(pair.LeftScore<pair.RightScore){pair.Left=Rectangle.Empty;pair.LeftReading=null;}else{pair.Right=Rectangle.Empty;pair.RightReading=null;}}
            }
            return pair;
        }
        static bool PurpleBar(Map m,int x,int y,double p,int side){
            // Require a long violet bar above the gems, including outside the four slots.
            for(double up=.65;up<1.65;up+=.12){int yy=y-(int)(up*p),n=0;
                foreach(double dx in new double[]{0,1,2,3,side==0?5:-2})if(m.P(x+(int)(dx*p),yy))n++;
                if(n==5)return true;
            }return false;
        }
        static bool BloodBar(Map m,int x,int y,double p,int side)
        {
            for(double up=.65;up<1.85;up+=.12)
            {
                int yy=y-(int)Math.Round(up*p),count=0;
                foreach(double dx in new double[]{.4,1,1.7,2.4,3.3})if(m.R(x+(int)Math.Round(dx*p),yy))count++;
                // Probe outside the four gems so their gold glow cannot impersonate the bar.
                if(count>=4&&m.R(x+(int)Math.Round((side==0?6:-1.2)*p),yy)&&(side==0||m.R(x+(int)Math.Round(3.3*p),yy)))return true;
            }
            return false;
        }
        static double Diamond(Map m,int x,int y,double p)
        {
            int inner=(int)Math.Round(p*.13),cx=(int)Math.Round(p*.40),cy=(int)Math.Round(p*.47),gap=(int)Math.Round(p*.49),vertical=(int)Math.Round(p*.25);
            int colored=0,dark=0;double core=0;
            foreach(Point d in new Point[]{Point.Empty,new Point(-inner,0),new Point(inner,0),new Point(0,-inner),new Point(0,inner)}){core+=m.E(x+d.X,y+d.Y);if(m.C(x+d.X,y+d.Y))colored++;if(m.D(x+d.X,y+d.Y))dark++;}
            if(colored<4)return -1;core/=5;
            double[] corner={m.E(x-cx,y-cy),m.E(x+cx,y-cy),m.E(x-cx,y+cy),m.E(x+cx,y+cy)};Array.Sort(corner);
            double contrast=core-(corner[1]+corner[2])/2,valley=core-(m.E(x-gap,y)+m.E(x+gap,y))/2;
            if(dark>=4 && core>=55 && core<150 && Math.Abs(contrast)>=8 && valley < -3) return 100-core*.3+Math.Min(Math.Abs(contrast),70)*.1+Math.Min(-valley,60)*.1;
            if(contrast<12||valley<5)return -1;
            return Math.Min(contrast,150)*.5+Math.Min(valley,100)*.5-Math.Abs(m.E(x,y-vertical)-m.E(x,y+vertical))*.15;
        }
    }
}



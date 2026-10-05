using System;using System.Drawing;using System.Drawing.Imaging;using System.Drawing.Drawing2D;using System.IO;using System.Collections.Generic;using System.Diagnostics;
namespace MuMuBeans {
static class DarkTests {
 static List<string> report;static int failed;static void Check(bool pass,string name){report.Add((pass?"PASS ":"FAIL ")+name);if(!pass)failed++;}
 static Bitmap Scale(Bitmap b,double scale){Bitmap r=new Bitmap((int)(b.Width*scale),(int)(b.Height*scale));using(Graphics g=Graphics.FromImage(r)){g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.DrawImage(b,new Rectangle(Point.Empty,r.Size));}return r;}
 static Bitmap Mask(Bitmap image,Bitmap blue,int mask){Bitmap b=(Bitmap)image.Clone();using(Graphics g=Graphics.FromImage(b))for(int i=0;i<4;i++)if((mask&(1<<i))!=0)g.DrawImage(blue,new Rectangle(183+30*i,101,30,38),new Rectangle(1607,98,30,38),GraphicsUnit.Pixel);return b;}
 static int Count(int mask){int n=0;for(int i=0;i<4;i++)if((mask&(1<<i))!=0)n++;return n;}
 public static void Run(string[] args){report=new List<string>();failed=0;Directory.CreateDirectory(args[3]);try{using(Bitmap original=new Bitmap(args[1]))using(Bitmap blue=new Bitmap(args[2])){
 foreach(double scale in new double[]{.5,.75,1,1.25})using(Bitmap zero=Scale(original,scale))using(Bitmap four=Mask(original,blue,15))using(Bitmap fourScaled=Scale(four,scale))using(Bitmap gold=(Bitmap)original.Clone()){
  using(Graphics g=Graphics.FromImage(gold))for(int i=0;i<4;i++)g.DrawImage(blue,new Rectangle(183+i*30,101,30,38),new Rectangle(180,94,30,38),GraphicsUnit.Pixel);
  LocatedPair goldPair;using(Bitmap goldScaled=Scale(gold,scale))goldPair=AutoLocator.Find(goldScaled);Check(goldPair.LeftReading!=null&&goldPair.LeftReading.Count==4,"空血条金色满豆 "+scale);
  LocatedPair zeroPair=AutoLocator.Find(zero),fourPair=AutoLocator.Find(fourScaled);Check(zeroPair.LeftReading!=null&&zeroPair.LeftReading.Count==0,"原始空血条四暗豆 "+scale+" ROI="+zeroPair.Left);Check(fourPair.LeftReading!=null&&fourPair.LeftReading.Count==4,"空血条四亮豆 "+scale);
  for(int mask=0;mask<16;mask++)using(Bitmap full=Mask(original,blue,mask))using(Bitmap b=Scale(full,scale)){
   LocatedPair pair=AutoLocator.Find(b);int expected=Count(mask);Check(pair.LeftReading!=null&&pair.LeftReading.Count==expected,"重新定位 scale="+scale+" mask="+mask+" got="+(pair.LeftReading==null?"?":pair.LeftReading.Count.ToString()));
   foreach(Rectangle locked in new Rectangle[]{zeroPair.Left,fourPair.Left,goldPair.Left}){Reading r=Detector.Analyze(b,locked,150);Check(r.Valid&&r.Count==expected,"锁定位置 scale="+scale+" mask="+mask+" ROI="+locked+" got="+r.Count+" valid="+r.Valid);}
  }
  for(int i=0;i<4;i++)using(Bitmap blocked=(Bitmap)zero.Clone()){Rectangle cover=new Rectangle((int)((183+i*30)*scale),(int)(98*scale),Math.Max(1,(int)(30*scale)),Math.Max(1,(int)(43*scale)));using(Graphics g=Graphics.FromImage(blocked))g.FillRectangle(Brushes.Black,cover);Reading r=Detector.Analyze(blocked,zeroPair.Left,150);Check(!r.Valid,"锁定位置单格遮挡不猜数 "+scale+" / "+i);Check(AutoLocator.Find(blocked).LeftReading==null,"单格遮挡不重新误定位 "+scale+" / "+i);}
 }

 foreach(double gain in new double[]{.8,1.15,1.4})using(Bitmap shifted=(Bitmap)original.Clone()){
   for(int y=90;y<146;y++)for(int x=175;x<315;x++){Color c=shifted.GetPixel(x,y);shifted.SetPixel(x,y,Color.FromArgb(Math.Min(255,(int)(c.R*gain)),Math.Min(255,(int)(c.G*gain)),Math.Min(255,(int)(c.B*gain))));}
   LocatedPair pair=AutoLocator.Find(shifted);Check(pair.LeftReading!=null&&pair.LeftReading.Count==0,"暗豆局部亮度变化仍为0："+gain);
 }
 using(Bitmap noBars=(Bitmap)original.Clone()){using(Graphics g=Graphics.FromImage(noBars)){g.FillRectangle(Brushes.Black,175,75,380,25);g.FillRectangle(Brushes.Black,1300,75,370,25);}LocatedPair pair=AutoLocator.Find(noBars);Check(pair.LeftReading!=null&&pair.LeftReading.Count==0&&pair.RightReading!=null&&pair.RightReading.Count==4,"双侧都没有血条颜色，仍依靠四格几何定位");}
 using(Bitmap mirrored=(Bitmap)original.Clone()){mirrored.RotateFlip(RotateFlipType.RotateNoneFlipX);LocatedPair pair=AutoLocator.Find(mirrored);Check(pair.RightReading!=null&&pair.RightReading.Count==0,"左右镜像：右侧暗豆也能确认0");}
 foreach(Color color in new Color[]{Color.Black,Color.FromArgb(26,54,97),Color.FromArgb(80,55,94),Color.FromArgb(25,120,145),Color.FromArgb(210,160,55)})using(Bitmap flat=new Bitmap(original.Width,original.Height)){using(Graphics g=Graphics.FromImage(flat))g.Clear(color);LocatedPair pair=AutoLocator.Find(flat);Check(pair.LeftReading==null&&pair.RightReading==null,"平坦颜色不可伪装成四颗暗豆 "+color);}
 LocatedPair initial=AutoLocator.Find(original);using(Bitmap roi=original.Clone(initial.Left,PixelFormat.Format32bppArgb)){Stopwatch sw=Stopwatch.StartNew();for(int i=0;i<500;i++)Detector.Analyze(roi,new Rectangle(Point.Empty,roi.Size),150);sw.Stop();report.Add("MEASURE 暗豆小区域判定平均 "+(sw.Elapsed.TotalMilliseconds/500).ToString("F3")+" ms（不含采集）");}
 Counter counter=new Counter();using(Bitmap f=Mask(original,blue,15)){for(int n=4;n>=0;n--){using(Bitmap frame=Mask(original,blue,(1<<n)-1)){Reading r=Detector.Analyze(frame,initial.Left,150);for(int j=0;j<3;j++)counter.Observe(r.Valid?(int?)r.Count:null,5-n+j*.02);}}}Check(counter.Baseline==0&&counter.Triggers==4,"锁定位置连续4→3→2→1→0，恰好触发4次");double deadline=counter.Deadline;for(int i=0;i<100;i++)counter.Observe(0,6+i*.02);Check(counter.Deadline==deadline&&counter.Triggers==4,"0颗保持稳定，不重复触发");counter.Observe(null,9);counter.Observe(null,10);Check(counter.Deadline==deadline,"画面消失不清零");
 using(Bitmap proof=(Bitmap)original.Clone()){using(Graphics g=Graphics.FromImage(proof))using(Pen pen=new Pen(Color.Lime,3))g.DrawRectangle(pen,initial.Left);proof.Save(Path.Combine(args[3],"暗豆定位修正.png"));}
 }}catch(Exception ex){failed++;report.Add(ex.ToString());}report.Add("TOTAL pass="+(report.FindAll(s=>s.StartsWith("PASS")).Count)+" fail="+failed);File.WriteAllLines(Path.Combine(args[3],"暗豆专项验证.txt"),report);Environment.ExitCode=failed==0?0:1;}
}}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Tesseract;
namespace MuMuBeans
{
    // Samples are manually labelled silhouettes. OCR is local and never on the bean thread.
    sealed class ClockReader : IDisposable
    {
        sealed class Glyph { public Rectangle Box; public List<Point> Pixels=new List<Point>(); public char Digit; public double Confidence; public bool Classified,Tolerant,UsedOcr; }
        sealed class Template {public char Digit;public bool[] Bits,Expanded;}
        readonly List<Template> templates=new List<Template>();
        readonly TesseractEngine ocr;
        public string LastReason="等待数字",BackendStatus="模板 + OCR",LastMethod="未确认";
        public double LastConfidence;
        int ocrBudget;
        Stopwatch readClock;
        public ClockReader()
        {
            string root=AppDomain.CurrentDomain.BaseDirectory;
            string file=Path.Combine(root,"clock-templates.txt");
            if(File.Exists(file))foreach(string line in File.ReadAllLines(file))
                if(line.Length==962){bool[] bits=new bool[960];for(int i=0;i<960;i++)bits[i]=line[i+2]=='1';templates.Add(new Template{Digit=line[0],Bits=bits,Expanded=Expand(bits)});}
            try{ocr=new TesseractEngine(Path.Combine(root,"tessdata"),"eng",EngineMode.LstmOnly);ocr.SetVariable("tessedit_char_whitelist","0123456789");}catch(Exception ex){if(ocr!=null)ocr.Dispose();ocr=null;BackendStatus="仅模板：OCR 初始化失败 · "+ex.Message;}
        }
        public static Rectangle Area(Size size)
        {return new Rectangle((int)(size.Width*.40),(int)(size.Height*.055),(int)(size.Width*.20),(int)(size.Height*.16));}
        static List<Glyph> Parts(Bitmap b,bool fill)
        {return Parts(b,fill?1:0);}
        static List<Glyph> Parts(Bitmap b,int mode)
        {
            int w=b.Width,h=b.Height;bool[] mask=new bool[w*h],seen=new bool[w*h];PixelMap map=new PixelMap(b,new Rectangle(Point.Empty,b.Size));
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                int r,g,blue;map.Read(x,y,out r,out g,out blue);
                bool red=mode<2?r>70&&r>g*1.25&&r>blue*1.25:r>45&&r-g>=18&&r-blue>=18&&r>g*1.20&&r>blue*1.20;
                bool light=mode<2?r>185&&g>170&&blue>150:Math.Min(r,Math.Min(g,blue))>125&&Math.Max(r,Math.Max(g,blue))-Math.Min(r,Math.Min(g,blue))<65;
                mask[y*w+x]=mode%2==1?!red:((r>(mode<2?110:80)&&red)||light);
            }
            List<Glyph> all=new List<Glyph>();int[] queue=new int[w*h];
            for(int index=0;index<mask.Length;index++)
            {
                if(seen[index]||!mask[index])continue;
                int head=0,tail=1;queue[0]=index;seen[index]=true;int l=w,t=h,r=0,bot=0;
                Glyph glyph=new Glyph();
                while(head<tail)
                {
                    int p=queue[head++],x=p%w,y=p/w;l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);bot=Math.Max(bot,y);
                    if(x>0)Enqueue(p-1,mask,seen,queue,ref tail);if(x<w-1)Enqueue(p+1,mask,seen,queue,ref tail);
                    if(y>0)Enqueue(p-w,mask,seen,queue,ref tail);if(y<h-1)Enqueue(p+w,mask,seen,queue,ref tail);
                    if(mode>=2){if(x>0&&y>0)Enqueue(p-w-1,mask,seen,queue,ref tail);if(x<w-1&&y>0)Enqueue(p-w+1,mask,seen,queue,ref tail);if(x>0&&y<h-1)Enqueue(p+w-1,mask,seen,queue,ref tail);if(x<w-1&&y<h-1)Enqueue(p+w+1,mask,seen,queue,ref tail);}
                }
                glyph.Box=Rectangle.FromLTRB(l,t,r+1,bot+1);
                if(l>0&&t>0&&r<w-1&&bot<h-1&&tail>2){for(int p=0;p<tail;p++)glyph.Pixels.Add(new Point(queue[p]%w,queue[p]/w));all.Add(glyph);}
            }
            return all;
        }
        static void Enqueue(int p,bool[] mask,bool[] seen,int[] q,ref int tail){if(mask[p]&&!seen[p]){seen[p]=true;q[tail++]=p;}}
        static List<Glyph> Digits(List<Glyph> all,int height)
        {
            int max=0;foreach(Glyph p in all)if(p.Box.Width<p.Box.Height*1.4&&p.Box.Height>height*.27)max=Math.Max(max,p.Box.Height);
            List<Glyph> result=new List<Glyph>();
            foreach(Glyph p in all)if(p.Box.Height>=max*.40&&p.Box.Height>height*.20&&p.Box.Width>p.Box.Height*.17&&p.Box.Width<p.Box.Height*1.4&&p.Pixels.Count>p.Box.Width*p.Box.Height*.16)result.Add(p);
            result.RemoveAll(a=>result.Exists(b=>!object.ReferenceEquals(a,b)&&a.Box.Contains(b.Box)&&b.Box.Height>a.Box.Height*.4));result.Sort(delegate(Glyph a,Glyph b){return a.Box.X.CompareTo(b.Box.X);});return result;
        }
        static List<Glyph> LayoutDigits(List<Glyph> all,int height)
        {
            List<Glyph> result=new List<Glyph>();
            foreach(Glyph p in all)if(p.Box.Height>height*.10&&p.Box.Height>=7&&p.Box.Width>p.Box.Height*.17&&p.Box.Width<p.Box.Height*1.4&&p.Pixels.Count>p.Box.Width*p.Box.Height*.16)result.Add(p);
            result.RemoveAll(a=>result.Exists(b=>!object.ReferenceEquals(a,b)&&a.Box.Contains(b.Box)&&b.Box.Height>a.Box.Height*.4));result.Sort(delegate(Glyph a,Glyph b){return a.Box.X.CompareTo(b.Box.X);});return result;
        }
        static void MergeSmallFragments(List<Glyph> all)
        {
            int max=0;foreach(Glyph g in all)max=Math.Max(max,g.Box.Height);
            for(int i=0;i<all.Count;i++)for(int j=i+1;j<all.Count;j++)
            {
                Glyph a=all[i],b=all[j];Rectangle union=Rectangle.Union(a.Box,b.Box);
                int overlap=Math.Min(a.Box.Right,b.Box.Right)-Math.Max(a.Box.Left,b.Box.Left);
                int gap=Math.Max(a.Box.Top,b.Box.Top)-Math.Min(a.Box.Bottom,b.Box.Bottom);
                if(a.Box.Height<4||b.Box.Height<4||Math.Max(a.Box.Height,b.Box.Height)>max*.75||union.Height>max*.80||overlap<Math.Min(a.Box.Width,b.Box.Width)*.50||gap>2||union.Width>union.Height*1.3)continue;
                a.Pixels.AddRange(b.Pixels);a.Box=union;all.RemoveAt(j);j--;a.Classified=false;
            }
        }
        static bool[] Bits(Glyph g)
        {
            bool[] source=new bool[g.Box.Width*g.Box.Height];foreach(Point p in g.Pixels)source[(p.Y-g.Box.Y)*g.Box.Width+p.X-g.Box.X]=true;
            bool[] result=new bool[960];for(int y=0;y<40;y++)for(int x=0;x<24;x++)result[y*24+x]=source[Math.Min(g.Box.Height-1,(int)((y+.5)*g.Box.Height/40))*g.Box.Width+Math.Min(g.Box.Width-1,(int)((x+.5)*g.Box.Width/24))];return result;
        }
        static double Similarity(bool[] a,bool[] b)
        {int union=0,intersection=0;for(int i=0;i<a.Length;i++){if(a[i]||b[i])union++;if(a[i]&&b[i])intersection++;}return union==0?0:intersection/(double)union;}
        static bool[] Expand(bool[] bits)
        {bool[] result=new bool[960];for(int y=0;y<40;y++)for(int x=0;x<24;x++)if(bits[y*24+x])for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(x+dx>=0&&x+dx<24&&y+dy>=0&&y+dy<40)result[(y+dy)*24+x+dx]=true;return result;}
        static double EdgeSimilarity(bool[] a,bool[] expandedA,Template t)
        {int n=0,m=0,hitA=0,hitB=0;for(int i=0;i<960;i++){if(a[i]){n++;if(t.Expanded[i])hitA++;}if(t.Bits[i]){m++;if(expandedA[i])hitB++;}}if(n==0||m==0)return 0;double precision=hitA/(double)n,recall=hitB/(double)m;return precision+recall==0?0:2*precision*recall/(precision+recall);}
        char Classify(Glyph glyph,out double confidence)
        {
            if(glyph.Classified){confidence=glyph.Confidence;return glyph.Digit;}
            bool[] bits=Bits(glyph);double best=0,second=0;char digit='?';
            double[] scores=new double[10];foreach(Template t in templates){double score=Similarity(bits,t.Bits);int d=t.Digit-'0';if(d>=0&&d<10)scores[d]=Math.Max(scores[d],score);}
            for(int d=0;d<10;d++)if(scores[d]>best){second=best;best=scores[d];digit=(char)('0'+d);}else second=Math.Max(second,scores[d]);
            if(best>=.78&&best-second>=.07){confidence=best;glyph.Classified=true;glyph.Digit=digit;glyph.Confidence=best;return digit;}
            // A one-pixel edge allowance handles tiny antialiased hundredths; require stronger class separation.
            if(glyph.Box.Height<=18&&best>=.65){bool[] expanded=Expand(bits);double[] edge=new double[10];foreach(Template t in templates){int d=t.Digit-'0';if(d>=0&&d<10)edge[d]=Math.Max(edge[d],EdgeSimilarity(bits,expanded,t));}double first=0,next=0;int selected=-1;for(int d=0;d<10;d++)if(edge[d]>first){next=first;first=edge[d];selected=d;}else next=Math.Max(next,edge[d]);if(selected>=0&&first>=.94&&first-next>=.10&&scores[selected]>=.65){confidence=scores[selected]*.4+first*.6;glyph.Classified=glyph.Tolerant=true;glyph.Digit=(char)('0'+selected);glyph.Confidence=confidence;return glyph.Digit;}}
            if(ocr==null||ocrBudget<=0||(readClock!=null&&readClock.ElapsedMilliseconds>=60)){confidence=best;glyph.Classified=true;glyph.Digit='?';glyph.Confidence=best;return '?';}
            ocrBudget--;
            using(Bitmap b=new Bitmap(72,104))
            {
                using(Graphics gr=Graphics.FromImage(b))gr.Clear(Color.White);
                for(int y=0;y<80;y++)for(int x=0;x<48;x++)if(bits[(y/2)*24+x/2])b.SetPixel(x+12,y+12,Color.Black);
                using(MemoryStream stream=new MemoryStream()){b.Save(stream,System.Drawing.Imaging.ImageFormat.Png);using(Pix p=Pix.LoadFromMemory(stream.ToArray()))using(Page page=ocr.Process(p,PageSegMode.SingleChar))
                {string text=page.GetText().Trim();confidence=page.GetMeanConfidence();glyph.Classified=true;glyph.Confidence=confidence;glyph.Digit=text.Length==1&&char.IsDigit(text[0])&&confidence>=.75?text[0]:'?';glyph.UsedOcr=glyph.Digit!='?';return glyph.Digit;}}
            }
        }
        sealed class Candidate {public double Value,Confidence;public bool Decimal;public string Method;}
        static bool Aligned(Glyph a,Glyph b,double ratio)
        {int h=Math.Max(a.Box.Height,b.Box.Height);return Math.Min(a.Box.Height,b.Box.Height)>=h*ratio&&Math.Abs(a.Box.Bottom-b.Box.Bottom)<=h*.24&&b.Box.X>=a.Box.Right-h*.14&&b.Box.X-a.Box.Right<=h*.75;}
        static bool DecimalDot(List<Glyph> all,Glyph a,Glyph b)
        {foreach(Glyph p in all)if(p.Box.Height>=2&&p.Box.Height<a.Box.Height*.25&&p.Box.Width<a.Box.Height*.30&&p.Box.X>=a.Box.Right-a.Box.Height*.08&&p.Box.Right<=b.Box.X+a.Box.Height*.08&&p.Box.Y>a.Box.Y+a.Box.Height*.58&&p.Box.Bottom<=a.Box.Bottom+a.Box.Height*.12)return true;return false;}
        void Consider(List<Candidate> results,Glyph[] digits,bool dec,string method)
        {
            int value=0;double minimum=1;bool tolerant=false,usedOcr=false;foreach(Glyph g in digits){double confidence;char ch=Classify(g,out confidence);if(ch=='?')return;value=value*10+ch-'0';minimum=Math.Min(minimum,confidence);tolerant|=g.Tolerant;usedOcr|=g.UsedOcr;}
            double number=dec?value/100.0:value;
            if(number<0||number>60||(!dec&&(number<10||digits[0].Digit=='0')))return;
            results.Add(new Candidate{Value=number,Confidence=minimum,Decimal=dec,Method=method+(tolerant?" + 小字边缘":"")+(usedOcr?" + 本地OCR":" + 模板")});
        }
        public double? Read(Bitmap crop)
        {
            LastConfidence=0;LastMethod="未确认";LastReason="未找到完整数字：检查区域或画面遮挡";ocrBudget=6;readClock=Stopwatch.StartNew();
            List<Candidate> results=new List<Candidate>();
            for(int mode=0;mode<4;mode++)
            {
                List<Glyph> all=Parts(crop,mode);if(mode>=2)MergeSmallFragments(all);List<Glyph> digits=LayoutDigits(all,crop.Height);if(digits.Count>16)continue;
                string method=mode<2?"标准轮廓":"弱光轮廓";
                for(int i=0;i<digits.Count;i++)for(int j=i+1;j<digits.Count;j++)
                {
                    Glyph a=digits[i],b=digits[j];
                    if(a.Box.Height<crop.Height*.20)continue;
                    if(Aligned(a,b,.70))Consider(results,new Glyph[]{a,b},false,method);
                    if(b.Box.Height<a.Box.Height*.38||b.Box.Height>a.Box.Height*.78||Math.Abs(a.Box.Bottom-b.Box.Bottom)>a.Box.Height*.24||b.Box.X<a.Box.Right-a.Box.Height*.10||b.Box.X-a.Box.Right>a.Box.Height*.85||!DecimalDot(all,a,b))continue;
                    for(int k=j+1;k<digits.Count;k++)if(Aligned(b,digits[k],.74))Consider(results,new Glyph[]{a,b,digits[k]},true,method);
                }
                // The low-light masks only run when the standard masks have no strong, unambiguous reading.
                if(mode==1&&results.Count>0){double maximum=0;double? strong=null;bool conflict=false;foreach(Candidate c in results)if(c.Confidence>=.90){if(strong.HasValue&&Math.Abs(strong.Value-c.Value)>.001)conflict=true;strong=c.Value;maximum=Math.Max(maximum,c.Confidence);}if(strong.HasValue&&!conflict){foreach(Candidate c in results)if(Math.Abs(c.Value-strong.Value)>.001&&maximum-c.Confidence<.06)conflict=true;if(!conflict)break;}}
            }
            Candidate best=null;double alternative=0;
            foreach(Candidate c in results)if(best==null||c.Confidence>best.Confidence)best=c;
            if(best==null){LastReason="完整数字未确认：轮廓、布局或匹配分数不足";return null;}
            foreach(Candidate c in results)if(Math.Abs(c.Value-best.Value)>.001)alternative=Math.Max(alternative,c.Confidence);
            if(alternative>0&&best.Confidence-alternative<.06){LastReason="多种数字读法冲突 · 等待下一帧";return null;}
            LastConfidence=best.Confidence;LastMethod=best.Method;LastReason=(best.Decimal?"小数时间":"整数时间")+" · "+best.Value.ToString("0.##",CultureInfo.InvariantCulture)+" · "+LastMethod;
            return best.Value;
        }
        public static void Seed(string[] paths,string destination)
        {
            List<string> lines=new List<string>();string[] labels={"37","18","797","643","49"};bool[] modes={false,true,false,false,false};
            for(int n=0;n<paths.Length;n++)using(Bitmap full=new Bitmap(paths[n]))using(Bitmap crop=full.Clone(Area(full.Size),PixelFormat.Format32bppArgb))
            {List<Glyph> digits=Digits(Parts(crop,modes[n]),crop.Height);if(digits.Count!=labels[n].Length)throw new Exception("Sample segmentation failed: "+labels[n]+" got "+digits.Count+" "+string.Join(";",digits.ConvertAll(d=>d.Box.ToString()).ToArray()));
             for(int i=0;i<digits.Count;i++){bool[] bits=Bits(digits[i]);string line=labels[n][i]+" ";foreach(bool b in bits)line+=b?'1':'0';lines.Add(line);}}
            File.WriteAllLines(destination,lines);
        }
        public static void AddLabeledSample(string path,string label,string destination){
            List<string> lines=new List<string>(File.ReadAllLines(destination));
            using(Bitmap original=new Bitmap(path))foreach(double scale in new double[]{1,.75,.5})using(Bitmap full=new Bitmap(original,new Size((int)(original.Width*scale),(int)(original.Height*scale))))using(Bitmap crop=full.Clone(Area(full.Size),PixelFormat.Format32bppArgb)){
                List<Glyph> digits=Digits(Parts(crop,false),crop.Height);if(digits.Count!=label.Length)throw new InvalidOperationException("标注样本分割失败");
                for(int i=0;i<digits.Count;i++){string line=label[i]+" ";foreach(bool b in Bits(digits[i]))line+=b?'1':'0';if(!lines.Contains(line))lines.Add(line);}
            }File.WriteAllLines(destination,lines);
        }
        public void Dispose(){if(ocr!=null)ocr.Dispose();}
    }
}

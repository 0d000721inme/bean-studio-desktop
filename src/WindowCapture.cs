using System;using System.Drawing;using System.Threading.Tasks;
namespace MuMuBeans {
static class WindowCapture {
 static readonly object gate=new object();static WindowFrames frames;static IntPtr window;static int generation;static bool requested;static string state="屏幕兼容捕获";
 public static string Status{get{lock(gate)return state;}}
 static void Prepare(IntPtr h){lock(gate){if(h==IntPtr.Zero)return;if(window!=h){WindowFrames old=frames;frames=null;window=h;requested=false;generation++;if(old!=null)Task.Run(()=>old.Dispose());}if(requested)return;requested=true;int v=generation;state="WGC 初始化中";Task.Run(()=>{WindowFrames next=null;try{next=new WindowFrames(h);lock(gate){if(v==generation){frames=next;next=null;state="WGC 窗口捕获";}}}catch(Exception ex){lock(gate)if(v==generation)state="屏幕兼容捕获："+ex.Message;}finally{if(next!=null)next.Dispose();}});}}
 public static bool Available(IntPtr h,Rectangle r){Prepare(h);lock(gate)if(window==h&&frames!=null)return Native.IsWindow(h)&&Native.IsWindowVisible(h)&&!Native.IsIconic(h);return Native.Uncovered(h,r);}
 public static Bitmap Capture(IntPtr h,Rectangle r){Prepare(h);WindowFrames current;lock(gate)current=window==h?frames:null;if(current!=null){try{Bitmap b=current.Crop(r);lock(gate)state="WGC 窗口捕获";return b;}catch(Exception ex){lock(gate)state="WGC 等待帧："+ex.Message;}}if(!Native.Uncovered(h,r))throw new InvalidOperationException(Status+" · 兼容截图不可用（遮挡或最小化）");return Native.Capture(r);}
 public static void Shutdown(){WindowFrames old;lock(gate){old=frames;frames=null;window=IntPtr.Zero;generation++;requested=false;}if(old!=null)Task.Run(()=>{try{old.Dispose();}catch{}});}
}
}

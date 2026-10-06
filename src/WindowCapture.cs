using System;using System.Drawing;using System.Threading.Tasks;
namespace MuMuBeans {
static class WindowCapture {
 static readonly object gate=new object();static WindowFrames frames;static IntPtr window;static int generation;static bool requested,paused;static string state="屏幕兼容捕获";
 static Task preparing=Task.FromResult(0),stopping=Task.FromResult(0);
 public static string Status{get{lock(gate)return state;}}
 public static bool Paused{get{lock(gate)return paused;}}
 public static Task PauseCompleted{get{lock(gate)return stopping;}}
 public static bool CaptureStopped{get{lock(gate)return paused&&stopping.IsCompleted&&!stopping.IsFaulted&&!stopping.IsCanceled;}}
 public static long Readbacks{get{return WindowFrames.Readbacks;}}
 public static void SetPaused(bool value){lock(gate){if(paused==value)return;if(!value){paused=false;state="等待重新捕获窗口";return;}paused=true;WindowFrames old=frames;frames=null;window=IntPtr.Zero;requested=false;int v=++generation;Task opening=preparing,previous=stopping;state="正在停止窗口捕获";stopping=Task.Run(async delegate{try{await previous;await opening;if(old!=null)old.Dispose();lock(gate)if(paused&&v==generation)state="窗口捕获已暂停";}catch(Exception ex){lock(gate)if(paused&&v==generation)state="停止捕获异常："+ex.Message;throw;}});}}
 static void Prepare(IntPtr h){lock(gate){if(paused||h==IntPtr.Zero)return;if(window!=h){WindowFrames old=frames;frames=null;window=h;requested=false;generation++;if(old!=null){Task prior=stopping;stopping=Task.Run(async delegate{await prior;old.Dispose();});}}if(requested)return;requested=true;int v=generation;Task previous=stopping,earlier=preparing;state="WGC 初始化中";preparing=Task.Run(async delegate{WindowFrames next=null;try{await previous;await earlier;lock(gate)if(paused||v!=generation)return;next=new WindowFrames(h);lock(gate){if(!paused&&v==generation){frames=next;next=null;state="WGC 窗口捕获";}}}catch(Exception ex){lock(gate)if(!paused&&v==generation)state="屏幕兼容捕获："+ex.Message;}finally{if(next!=null)next.Dispose();}});}}
 public static bool Available(IntPtr h,Rectangle r){Prepare(h);lock(gate){if(paused)return false;if(window==h&&frames!=null)return Native.IsWindow(h)&&Native.IsWindowVisible(h)&&!Native.IsIconic(h);return Native.Uncovered(h,r);}}
 public static Bitmap Capture(IntPtr h,Rectangle r){Prepare(h);WindowFrames current;lock(gate){if(paused)throw new InvalidOperationException("窗口捕获已暂停");current=window==h?frames:null;}if(current!=null){try{Bitmap b=current.Crop(r);lock(gate){if(paused){b.Dispose();throw new InvalidOperationException("窗口捕获已暂停");}state="WGC 窗口捕获";}return b;}catch(Exception ex){lock(gate)if(!paused)state="WGC 等待帧："+ex.Message;}}lock(gate){if(paused)throw new InvalidOperationException("窗口捕获已暂停");if(!Native.Uncovered(h,r))throw new InvalidOperationException(state+" · 兼容截图不可用（遮挡或最小化）");return Native.Capture(r);}}
 public static void Shutdown(){SetPaused(true);}
}
}

using System;using System.Drawing;using System.Diagnostics;using System.Threading;using System.Threading.Tasks;
namespace MuMuBeans {
static class WindowCapture {
 static readonly object gate=new object();static WindowFrames frames;static IntPtr window;static int generation,maximumFrames=60;static long compatibilitySequence;static bool requested,paused;static volatile string state="屏幕兼容捕获";
 static Task preparing=Task.FromResult(0),stopping=Task.FromResult(0);
 // Presentation reads only this published immutable text. It must never wait
 // behind a compatibility CopyFromScreen or a capture lifecycle transition.
 public static string Status{get{return state;}}
 public static bool Paused{get{lock(gate)return paused;}}
 public static Task PauseCompleted{get{lock(gate)return stopping;}}
 public static bool CaptureStopped{get{lock(gate)return paused&&stopping.IsCompleted&&!stopping.IsFaulted&&!stopping.IsCanceled;}}
 public static long Readbacks{get{return WindowFrames.Readbacks;}}
 public static int MaximumReadbacks{get{lock(gate)return maximumFrames;}}
 public static void SetReadbackRate(int value){lock(gate){maximumFrames=Math.Max(1,Math.Min(60,value));if(frames!=null)frames.SetReadbackRate(maximumFrames);}}
 public static void SetPaused(bool value){lock(gate){if(paused==value)return;if(!value){paused=false;state="等待重新捕获窗口";return;}paused=true;WindowFrames old=frames;frames=null;window=IntPtr.Zero;requested=false;int v=++generation;Task opening=preparing,previous=stopping;state="正在停止窗口捕获";stopping=Task.Run(async delegate{try{await previous;await opening;if(old!=null)old.Dispose();lock(gate)if(paused&&v==generation)state="窗口捕获已暂停";}catch(Exception ex){lock(gate)if(paused&&v==generation)state="停止捕获异常："+ex.Message;throw;}});}}
 static void Prepare(IntPtr h){lock(gate){if(paused||h==IntPtr.Zero)return;if(window!=h){WindowFrames old=frames;frames=null;window=h;requested=false;generation++;if(old!=null){Task prior=stopping;stopping=Task.Run(async delegate{await prior;old.Dispose();});}}if(requested)return;requested=true;int v=generation;Task previous=stopping,earlier=preparing;state="WGC 初始化中";preparing=Task.Run(async delegate{WindowFrames next=null;try{await previous;await earlier;lock(gate)if(paused||v!=generation)return;next=new WindowFrames(h);lock(gate){if(!paused&&v==generation){next.SetReadbackRate(maximumFrames);frames=next;next=null;state="WGC 窗口捕获";}}}catch(Exception ex){lock(gate)if(!paused&&v==generation)state="屏幕兼容捕获："+ex.Message;}finally{if(next!=null)next.Dispose();}});}}
 public static bool Available(IntPtr h,Rectangle r){Prepare(h);lock(gate){if(paused)return false;if(window==h&&frames!=null)return Native.IsWindow(h)&&Native.IsWindowVisible(h)&&!Native.IsIconic(h);return Native.Uncovered(h,r);}}
 public static Bitmap Capture(IntPtr h,Rectangle r){long sequence=0,stamp;return CaptureSince(h,r,ref sequence,out stamp);}
 // Consumers keep separate cursors. A null result means that no new WGC frame
 // is available; it is not an unknown/invalid observation and must not count as
 // a second confirmation or refresh the age of an old game-time reading.
 public static Bitmap CaptureSince(IntPtr h,Rectangle r,ref long sequence){long stamp;return CaptureSince(h,r,ref sequence,out stamp);}
 public static Bitmap CaptureSince(IntPtr h,Rectangle r,ref long sequence,out long stamp){Prepare(h);stamp=0;WindowFrames current;lock(gate){if(paused)throw new InvalidOperationException("窗口捕获已暂停");current=window==h?frames:null;}if(current!=null){try{long id;Bitmap b=current.CropSince(r,sequence,out id,out stamp);lock(gate){if(paused){if(b!=null)b.Dispose();throw new InvalidOperationException("窗口捕获已暂停");}state="WGC 窗口捕获";}sequence=id;return b;}catch(Exception ex){lock(gate)if(!paused)state="WGC 等待帧："+ex.Message;}}lock(gate){if(paused)throw new InvalidOperationException("窗口捕获已暂停");if(!Native.Uncovered(h,r))throw new InvalidOperationException(state+" · 兼容截图不可用（遮挡或最小化）");Bitmap b=Native.Capture(r);stamp=Stopwatch.GetTimestamp();sequence=-Interlocked.Increment(ref compatibilitySequence);return b;}}
 // An explicit scan while paused takes one desktop snapshot only. It does not
 // create a WGC session, resume monitoring/OCR, or change the capture pause flag.
 public static Bitmap CaptureOnce(IntPtr h,Rectangle r){if(!Native.Uncovered(h,r))throw new InvalidOperationException("单次扫描需要露出游戏窗口 · 请移开遮挡窗口");return Native.Capture(r);}
 public static void Shutdown(){SetPaused(true);}
}
}

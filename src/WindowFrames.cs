using System;using System.Drawing;using System.Drawing.Imaging;using System.Diagnostics;using System.Runtime.InteropServices;using System.Runtime.InteropServices.WindowsRuntime;using Windows.Graphics.Capture;using Windows.Graphics.DirectX;using Windows.Graphics.DirectX.Direct3D11;using Windows.Graphics.Imaging;
namespace MuMuBeans {
// Optional WGC backend. A failed or stale frame is never treated as a valid observation.
sealed class WindowFrames:IDisposable {
 [ComImport,Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ItemInterop{[PreserveSig]int CreateForWindow(IntPtr h,ref Guid iid,out IntPtr p);[PreserveSig]int CreateForMonitor(IntPtr h,ref Guid iid,out IntPtr p);}
 [DllImport("combase.dll")]static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)]string s,int n,out IntPtr p);
 [DllImport("combase.dll")]static extern int WindowsDeleteString(IntPtr p);
 [DllImport("combase.dll")]static extern int RoGetActivationFactory(IntPtr s,ref Guid iid,out IntPtr p);
 [DllImport("d3d11.dll")]static extern int D3D11CreateDevice(IntPtr a,int type,IntPtr software,uint flags,IntPtr levels,int count,uint sdk,out IntPtr device,out int level,out IntPtr context);
 [DllImport("d3d11.dll")]static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr d,out IntPtr p);
 [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr h,int attr,out Native.Rect r,int size);
 readonly object gate=new object();IDirect3DDevice device;Direct3D11CaptureFramePool pool;GraphicsCaptureSession session;Bitmap latest;byte[] pixels;Windows.Storage.Streams.Buffer buffer;long stamp;bool stopped;readonly IntPtr hwnd;public string Error="等待窗口帧";
 public WindowFrames(IntPtr h){hwnd=h;try{Initialize();}catch{Dispose();throw;}}
 void Initialize(){
  if(!GraphicsCaptureSession.IsSupported())throw new NotSupportedException("系统不支持 WGC");
  IntPtr hs=IntPtr.Zero,factory=IntPtr.Zero,item=IntPtr.Zero;GraphicsCaptureItem capture;
  try{string name="Windows.Graphics.Capture.GraphicsCaptureItem";Marshal.ThrowExceptionForHR(WindowsCreateString(name,name.Length,out hs));Guid iid=typeof(ItemInterop).GUID;Marshal.ThrowExceptionForHR(RoGetActivationFactory(hs,ref iid,out factory));var interop=(ItemInterop)Marshal.GetObjectForIUnknown(factory);Guid itemId=new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");try{Marshal.ThrowExceptionForHR(interop.CreateForWindow(hwnd,ref itemId,out item));capture=(GraphicsCaptureItem)Marshal.GetObjectForIUnknown(item);}finally{Marshal.ReleaseComObject(interop);}}
  finally{if(item!=IntPtr.Zero)Marshal.Release(item);if(factory!=IntPtr.Zero)Marshal.Release(factory);if(hs!=IntPtr.Zero)WindowsDeleteString(hs);}
  IntPtr d=IntPtr.Zero,context=IntPtr.Zero,dxgi=IntPtr.Zero,inspectable=IntPtr.Zero;int level;
  try{Marshal.ThrowExceptionForHR(D3D11CreateDevice(IntPtr.Zero,1,IntPtr.Zero,0x20,IntPtr.Zero,0,7,out d,out level,out context));Guid iid=new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d,ref iid,out dxgi));Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi,out inspectable));device=(IDirect3DDevice)Marshal.GetObjectForIUnknown(inspectable);}
  finally{if(inspectable!=IntPtr.Zero)Marshal.Release(inspectable);if(dxgi!=IntPtr.Zero)Marshal.Release(dxgi);if(context!=IntPtr.Zero)Marshal.Release(context);if(d!=IntPtr.Zero)Marshal.Release(d);}
  pool=Direct3D11CaptureFramePool.CreateFreeThreaded(device,DirectXPixelFormat.B8G8R8A8UIntNormalized,2,capture.Size);pool.FrameArrived+=Arrived;session=pool.CreateCaptureSession(capture);session.StartCapture();
 }
 int busy;
 void Arrived(Direct3D11CaptureFramePool source,object args){if(System.Threading.Interlocked.Exchange(ref busy,1)!=0)return;System.Threading.Tasks.Task.Run(()=>{try{ReadFrame(source);}finally{System.Threading.Interlocked.Exchange(ref busy,0);}});}
 void ReadFrame(Direct3D11CaptureFramePool source){lock(gate){if(stopped)return;try{using(var f=source.TryGetNextFrame()){if(f==null)return;var size=f.ContentSize;if(size.Width<1||size.Height<1)return;
   using(SoftwareBitmap sb=ReadSurface(f.Surface)){
    if(sb.PixelWidth!=size.Width||sb.PixelHeight!=size.Height){source.Recreate(device,DirectXPixelFormat.B8G8R8A8UIntNormalized,2,size);return;}
    int length=sb.PixelWidth*sb.PixelHeight*4;if(pixels==null||pixels.Length!=length){pixels=new byte[length];buffer=new Windows.Storage.Streams.Buffer((uint)length);}byte[] bytes=pixels;sb.CopyToBuffer(buffer);using(var dataReader=Windows.Storage.Streams.DataReader.FromBuffer(buffer))dataReader.ReadBytes(bytes);if(latest==null||latest.Width!=sb.PixelWidth||latest.Height!=sb.PixelHeight){if(latest!=null)latest.Dispose();latest=new Bitmap(sb.PixelWidth,sb.PixelHeight,PixelFormat.Format32bppArgb);}Bitmap b=latest;var data=b.LockBits(new Rectangle(Point.Empty,b.Size),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{for(int y=0;y<b.Height;y++)Marshal.Copy(bytes,y*b.Width*4,IntPtr.Add(data.Scan0,y*data.Stride),b.Width*4);}finally{b.UnlockBits(data);}stamp=Stopwatch.GetTimestamp();Error="WGC 窗口捕获";
   }
  }}catch(Exception e){Error="WGC: "+e.Message;}}}
 static SoftwareBitmap ReadSurface(IDirect3DSurface surface){var op=SoftwareBitmap.CreateCopyFromSurfaceAsync(surface);try{Stopwatch sw=Stopwatch.StartNew();while(op.Status==Windows.Foundation.AsyncStatus.Started){if(sw.ElapsedMilliseconds>1000){op.Cancel();throw new TimeoutException("GPU readback timeout");}System.Threading.Thread.Sleep(1);}return op.GetResults();}finally{op.Close();}}
 public Bitmap Crop(Rectangle screenArea){lock(gate){if(stopped||latest==null||(Stopwatch.GetTimestamp()-stamp)/(double)Stopwatch.Frequency>.3)throw new InvalidOperationException(Error+" · 等待新帧");Native.Rect r;Marshal.ThrowExceptionForHR(DwmGetWindowAttribute(hwnd,9,out r,Marshal.SizeOf(typeof(Native.Rect))));Rectangle crop=new Rectangle(screenArea.X-r.Left,screenArea.Y-r.Top,screenArea.Width,screenArea.Height);if(!new Rectangle(Point.Empty,latest.Size).Contains(crop))throw new InvalidOperationException("窗口尺寸变化，等待新帧");return latest.Clone(crop,PixelFormat.Format32bppArgb);}}
 public void Dispose(){lock(gate){if(stopped)return;stopped=true;if(pool!=null)pool.FrameArrived-=Arrived;if(latest!=null){latest.Dispose();latest=null;}pixels=null;buffer=null;}try{if(session!=null)session.Dispose();}finally{try{if(pool!=null)pool.Dispose();}finally{if(device!=null)device.Dispose();}}}
}
}



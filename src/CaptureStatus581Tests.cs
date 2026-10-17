using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace MuMuBeans
{
    // Simulate a slow compatibility capture by holding only its lock. No actual
    // screen, emulator, WGC session or user window is touched by this check.
    static class CaptureStatus581Tests
    {
        static readonly List<string> rows=new List<string>();
        static int failures;
        static void Check(bool value,string message){rows.Add((value?"PASS ":"FAIL ")+message);if(!value)failures++;}
        static double ReadWhileHeld(object captureGate,bool legacy,out string value)
        {
            string observed=null;
            using(ManualResetEvent held=new ManualResetEvent(false))
            {
                Thread busy=new Thread(delegate(){lock(captureGate){held.Set();Thread.Sleep(250);}}){IsBackground=true};
                busy.Start();held.WaitOne();Stopwatch elapsed=Stopwatch.StartNew();
                if(legacy){lock(captureGate)observed=WindowCapture.Status;}
                else for(int i=0;i<1000;i++)observed=WindowCapture.Status;
                double milliseconds=elapsed.Elapsed.TotalMilliseconds;busy.Join();value=observed;return milliseconds;
            }
        }
        public static void Run(string[] args)
        {
            string folder=args[1];Directory.CreateDirectory(folder);rows.Clear();failures=0;
            try
            {
                object captureGate=typeof(WindowCapture).GetField("gate",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                string initial=WindowCapture.Status,oldValue,newValue;
                double legacy=ReadWhileHeld(captureGate,true,out oldValue);
                double revised=ReadWhileHeld(captureGate,false,out newValue);
                Check(legacy>=150,"旧锁式状态读取确实等候模拟的慢截图锁");
                Check(revised<100,"新状态getter连续1000次读取不等待截图锁，低于100ms预算");
                Check(initial==oldValue&&initial==newValue,"无锁快照保持原状态文字，不伪造捕获就绪");
                Check(WindowCapture.Readbacks==0,"状态检查没有启动WGC或获取任何画面");
                rows.Add("BENCH legacy_locked_one_read_ms="+legacy.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" revised_1000_reads_ms="+revised.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
                rows.Add("SCOPE Simulated 250 ms lock contention only, not measured MuMu drag responsiveness.");
            }
            catch(Exception ex){Check(false,ex.ToString());}
            rows.Add("FAILURES="+failures);File.WriteAllLines(Path.Combine(folder,"capture-status581.txt"),rows.ToArray(),new UTF8Encoding(true));Environment.ExitCode=failures==0?0:1;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MuMuBeans
{
    static class Drag581Tests
    {
        static readonly List<string> rows=new List<string>();static int failed;
        static void Check(bool ok,string title){rows.Add((ok?"PASS ":"FAIL ")+title);if(!ok)failed++;}
        static T Field<T>(object item,string name){return (T)item.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(item);}
        static void Save(Form form,string path){using(Bitmap image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,form.ClientRectangle);image.Save(path);}}
        static void Pump(int milliseconds){Stopwatch watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(3);}}
        public static void Run(string[] args)
        {
            string folder=args[1];Directory.CreateDirectory(folder);rows.Clear();failed=0;
            try
            {
                using(MainForm form=new MainForm(false))
                {
                    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-18000,-18000);form.Show();Application.DoEvents();
                    Field<System.Windows.Forms.Timer>(form,"pulse").Stop();
                    MethodInfo tick=typeof(MainForm).GetMethod("Tick",BindingFlags.Instance|BindingFlags.NonPublic);
                    Control badge=Field<Control>(form,"sessionBadge"),dots=Field<Control>(form,"dots"),rail=Field<Control>(form,"rail"),crop=Field<Control>(form,"crop"),perf=Field<Control>(form,"performancePanel");
                    for(int i=0;i<8;i++)tick.Invoke(form,new object[]{null,EventArgs.Empty});Application.DoEvents();
                    int badges=0,beans=0,rails=0,crops=0;
                    badge.Invalidated+=delegate{badges++;};dots.Invalidated+=delegate{beans++;};rail.Invalidated+=delegate{rails++;};crop.Invalidated+=delegate{crops++;};
                    double total=0,maximum=0;
                    for(int i=0;i<100;i++)
                    {
                        Stopwatch watch=Stopwatch.StartNew();tick.Invoke(form,new object[]{null,EventArgs.Empty});watch.Stop();total+=watch.Elapsed.TotalMilliseconds;maximum=Math.Max(maximum,watch.Elapsed.TotalMilliseconds);Application.DoEvents();
                    }
                    rows.Add(string.Format(CultureInfo.InvariantCulture,"MEASURE 100 unchanged idle ticks: badge={0}, beans={1}, rail={2}, crop={3}; UI call mean={4:0.000} ms, max={5:0.000} ms",badges,beans,rails,crops,total/100,maximum));
                    MethodInfo apply=typeof(MainForm).GetMethod("ApplySnapshot",BindingFlags.Instance|BindingFlags.NonPublic);
                    if(apply==null){rows.Add("BASELINE V5.8 unchanged-idle-tick measurements only; no V5.8.1 behavior asserted.");}
                    else
                    {
                        Check(badges==0&&beans==0&&rails==0&&crops==0,"unchanged samples do not redraw badge, bean dots, rail or clone/repaint crop");
                        Snapshot snap=new Snapshot{Enabled=true,Remaining=12.34,Recovery="恢复点：18 秒（游戏）",Status="合成 UI 检查",Reading=new Reading{Lit=new bool[]{false,true,true,true}},ConfirmedReading=new Reading{Lit=new bool[]{false,true,true,true}}};
                        ClockState clock=new ClockState{Value=33,Age=.01,Status="合成读数",Method="fixture",Backend="fixture"};
                        apply.Invoke(form,new object[]{snap,clock});
                        Label cooldown=Field<Label>(form,"clockLabel"),recovery=Field<Label>(form,"recoveryValue"),recoveryState=Field<Label>(form,"timerRule");
                        Check(cooldown.Text=="12.34"&&recovery.Text=="18","decimal cooldown and fixed game recovery point appear together");
                        foreach(Size size in new Size[]{new Size(1040,760),new Size(1240,860),new Size(1440,960)})
                        {
                            form.ClientSize=size;Application.DoEvents();
                            Check(Math.Abs(cooldown.Font.Size-recovery.Font.Size)<.01&&cooldown.Size==recovery.Size,"equal priority, font and number area at "+size.Width+"x"+size.Height);
                            Check(cooldown.Parent.ClientRectangle.Contains(cooldown.Bounds)&&cooldown.Parent.ClientRectangle.Contains(recovery.Bounds)&&!cooldown.Bounds.IntersectsWith(recovery.Bounds),"both numbers remain separate and inside timer at "+size.Width+"x"+size.Height);
                            Check(recoveryState.Width>=TextRenderer.MeasureText("本回合不足冷却时间",recoveryState.Font).Width,"full insufficient-round explanation fits at "+size.Width+"x"+size.Height);
                            Save(form,Path.Combine(folder,"timer-"+size.Width+"x"+size.Height+".png"));
                        }
                        form.ClientSize=new Size(1040,760);Application.DoEvents();
                        snap.Recovery="恢复点≈3.50 秒（中途补算）";snap.Remaining=2.38;apply.Invoke(form,new object[]{snap,clock});
                        Check(recovery.Text=="3.50"&&Field<Label>(form,"recoveryTitle").Text.Contains("≈")&&recoveryState.Text.Contains("估算"),"estimated recovery retains approximation and its explanation");
                        Check(recovery.Width>=TextRenderer.MeasureText(recovery.Text,recovery.Font).Width,"approximate decimal recovery is not clipped in minimum window");
                        Save(form,Path.Combine(folder,"timer-approximate.png"));
                        snap.Recovery="恢复点≈59.99 秒（中途补算）";apply.Invoke(form,new object[]{snap,clock});
                        Check(recovery.Text=="59.99"&&recovery.Width>=TextRenderer.MeasureText(recovery.Text,recovery.Font).Width,"largest two-decimal recovery fits; approximation is explicit in caption");
                        snap.Recovery="恢复点：时间未确认（见时间校准）";apply.Invoke(form,new object[]{snap,clock});
                        Check(recovery.Text=="—"&&recoveryState.Text.Contains("未确认"),"unknown recovery stays explicitly unknown without fabricated numeric point");
                        snap.Recovery="恢复点：本回合不足冷却时间";apply.Invoke(form,new object[]{snap,clock});
                        Check(recovery.Text=="—"&&recoveryState.Text.Contains("不足"),"insufficient round time remains an explicit status");
                        snap.Recovery="恢复点：18 秒（游戏）";snap.ConfirmedReading=new Reading();apply.Invoke(form,new object[]{snap,clock});
                        Check(cooldown.Text=="对方已无豆"&&recovery.Text=="18","last bean consumption preserves its recovery point beside no-beans state");
                        snap.ConfirmedReading=new Reading{Lit=new bool[]{false,true,true,true}};snap.Remaining=10.42;apply.Invoke(form,new object[]{snap,clock});
                        Engine engine=Field<Engine>(form,"engine");object gate=Field<object>(engine,"gate");Counter counter=Field<Counter>(engine,"counter");Stopwatch time=Field<Stopwatch>(engine,"time");
                        lock(gate){counter.Deadline=time.Elapsed.TotalSeconds+15;counter.Triggers=7;}
                        Snapshot before=engine.GetSnapshot();int cropDuring=0,perfDuring=0;crop.Invalidated+=delegate{cropDuring++;};perf.Invalidated+=delegate{perfDuring++;};
                        Field<System.Windows.Forms.Timer>(form,"pulse").Start();SendMessage(form.Handle,0x231,IntPtr.Zero,IntPtr.Zero);
                        Check(Field<bool>(form,"interactiveMoving"),"native enter-size-move activates only presentation throttling");
                        Stopwatch drag=Stopwatch.StartNew();double maxMove=0;bool decimalChanged=false;string initial=Field<Label>(form,"clockLabel").Text;
                        for(int i=0;i<40;i++)
                        {
                            Stopwatch move=Stopwatch.StartNew();SetWindowPos(form.Handle,IntPtr.Zero,-18000+i*3,-18000+i,0,0,0x15);move.Stop();maxMove=Math.Max(maxMove,move.Elapsed.TotalMilliseconds);Pump(8);
                            decimalChanged|=Field<Label>(form,"clockLabel").Text!=initial;
                        }
                        Snapshot after=engine.GetSnapshot();
                        Check(decimalChanged,"real WinForms timer messages keep updating decimal cooldown during movement");
                        Check(cropDuring==0&&perfDuring==0,"moving window defers noncritical crop and resource chart redraws");
                        Check(after.CooldownDeadline==before.CooldownDeadline&&after.Triggers==before.Triggers,"moving window neither resets deadline nor adds a trigger");
                        Check(!after.Paused&&!before.Paused,"drag path never pauses the engine");
                        Check(after.Remaining<before.Remaining-.15,"authoritative countdown continues over elapsed movement time");
                        SendMessage(form.Handle,0x232,IntPtr.Zero,IntPtr.Zero);Pump(150);
                        Check(!Field<bool>(form,"interactiveMoving")&&perfDuring>0,"deferred chart refresh resumes on first post-drag tick");
                        SendMessage(form.Handle,0x231,IntPtr.Zero,IntPtr.Zero);SendMessage(form.Handle,0x1F,IntPtr.Zero,IntPtr.Zero);
                        Check(!Field<bool>(form,"interactiveMoving"),"canceling native drag clears presentation throttle");
                        SendMessage(form.Handle,0x231,IntPtr.Zero,IntPtr.Zero);SendMessage(form.Handle,0x215,IntPtr.Zero,IntPtr.Zero);
                        Check(!Field<bool>(form,"interactiveMoving"),"losing native mouse capture clears presentation throttle");
                        rows.Add(string.Format(CultureInfo.InvariantCulture,"MEASURE 40 actual own-window SetWindowPos messages over {0:0.0} ms; maximum native move call={1:0.000} ms; deadline difference={2:0.000000}; monitoring source deliberately absent",drag.Elapsed.TotalMilliseconds,maxMove,after.CooldownDeadline-before.CooldownDeadline));
                        form.Hide();
                    }
                }
            }
            catch(Exception ex){failed++;rows.Add("FAIL "+ex);}
            rows.Add("FAILURES="+failed);rows.Add("Own-window synthetic UI/counter verification only. No MuMu launch, game accuracy or hardware gameplay latency claim.");
            File.WriteAllLines(Path.Combine(folder,"drag581-tests.txt"),rows.ToArray());Environment.ExitCode=failed==0?0:1;
        }
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int message,IntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int width,int height,uint flags);
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace MuMuBeans
{
    static class ProcessPerformanceTests
    {
        static readonly List<string> rows = new List<string>();
        static int failures;
        static void Check(bool pass, string title) { rows.Add((pass ? "PASS " : "FAIL ") + title); if (!pass) failures++; }
        static bool Wait(Func<bool> condition, int timeout)
        {
            Stopwatch clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < timeout) { if (condition()) return true; Thread.Sleep(20); }
            return condition();
        }
        static GpuEngineValue Gpu(string suffix, double value) { return new GpuEngineValue("pid_123_luid_0x00000000_0x00001234_phys_0_" + suffix, value); }
        static bool Near(double? value, double expected) { return value.HasValue && Math.Abs(value.Value - expected) < .001; }

        public static void Run(string[] args)
        {
            string folder = args[1]; Directory.CreateDirectory(folder); rows.Clear(); failures = 0;
            try
            {
                Check(Near(ProcessPerformance.CpuUsage(2, 1, 8), 25), "CPU 使用累计处理器时间增量，按 8 个逻辑核心归一化");
                Check(Near(ProcessPerformance.CpuUsage(0, 1, 8), 0), "确实无 CPU 工作可显示真实 0%");
                Check(!ProcessPerformance.CpuUsage(1, 0, 8).HasValue && !ProcessPerformance.CpuUsage(-1, 1, 8).HasValue &&
                    !ProcessPerformance.CpuUsage(1, 1, 0).HasValue, "无效间隔/时间回退/核心数不伪装成 0");
                Check(!ProcessPerformance.CpuUsage(Double.NaN, 1, 8).HasValue && !ProcessPerformance.CpuUsage(1, Double.PositiveInfinity, 8).HasValue,
                    "拒绝非有限 CPU 读数");
                Check(Near(ProcessPerformance.CpuUsage(20, 1, 8), 100), "CPU 结果限制为 0–100%");

                List<GpuEngineValue> values = new List<GpuEngineValue>();
                values.Add(Gpu("eng_0_engtype_3D", 20)); values.Add(Gpu("eng_0_engtype_3D#1", 15));
                values.Add(Gpu("eng_1_engtype_Copy", 80));
                Check(Near(GpuProcessCounter.Aggregate(123, values), 80), "同一物理引擎聚合后取最忙引擎，20+15 与 80 不错误求和");
                values.RemoveAt(2);
                Check(Near(GpuProcessCounter.Aggregate(123, values), 35), "同物理引擎的独立计数器实例正确相加");
                values.Add(new GpuEngineValue(values[0].Instance, 20));
                Check(Near(GpuProcessCounter.Aggregate(123, values), 35), "重复同一实例不重复计入");
                values.Add(new GpuEngineValue("pid_1234_luid_0x0_0x1_phys_0_eng_0_engtype_3D", 99));
                values.Add(new GpuEngineValue("pid_12_luid_0x0_0x1_phys_0_eng_0_engtype_3D", 99));
                Check(Near(GpuProcessCounter.Aggregate(123, values), 35), "精确 PID 边界排除 1234 与 12");
                values.Add(new GpuEngineValue("pid_123_luid_0x0_0x5678_phys_0_eng_0_engtype_3D", 60));
                Check(Near(GpuProcessCounter.Aggregate(123, values), 60), "不同适配器的同编号引擎不混合相加");
                values.Clear(); values.Add(Gpu("eng_0_engtype_3D", Double.NaN)); values.Add(Gpu("eng_0_engtype_3D#1", -1));
                values.Add(new GpuEngineValue("pid_123_unknown", 50));
                Check(!GpuProcessCounter.Aggregate(123, values).HasValue, "无效/不支持 GPU 数据返回 unavailable 而非 0%");
                values.Clear(); values.Add(Gpu("eng_0_engtype_3D", 80)); values.Add(Gpu("eng_0_engtype_3D#1", 50));
                Check(Near(GpuProcessCounter.Aggregate(123, values), 100), "GPU 聚合上限 100% 不产生超过整卡的读数");
                values.Clear(); values.Add(Gpu("eng_0_engtype_3D", 0));
                Check(Near(GpuProcessCounter.Aggregate(123, values), 0), "只有真实有效 GPU 样本才显示 0%");

                PerformanceHistory history = new PerformanceHistory();
                for (int i = 0; i < 120; i++) history.Add(new PerformanceSample(i, null, null, null, null, "", "", ""));
                PerformanceSample[] points = history.Copy();
                Check(points.Length == 60 && points[0].ElapsedSeconds == 60 && points[59].ElapsedSeconds == 119,
                    "历史窗口恒定最多 60 点，保留最近点且时间正序");
                points[0] = null;
                Check(history.Copy()[0] != null, "图表快照数组与内部有界队列隔离");

                ProcessPerformance sampler = new ProcessPerformance();
                try
                {
                    Check(!sampler.WorkerAlive && !sampler.GetSnapshot().Enabled && sampler.GetSnapshot().Latest == null,
                        "构造不启线程/PDH，不给静态 UI 预览增加开销");
                    sampler.SetEnabled(true);
                    bool received = Wait(delegate { return sampler.GetSnapshot().Samples.Length >= 3; }, 8000);
                    PerformanceSnapshot snapshot = sampler.GetSnapshot();
                    Check(received && snapshot.Latest.WorkingSetMb.HasValue && snapshot.Latest.WorkingSetMb.Value > 0 &&
                        snapshot.Latest.MemoryPercent.HasValue && snapshot.Latest.MemoryPercent.Value > 0,
                        "真实当前进程采样：工作集 MB 和物理 RAM 百分比可用");
                    Check(received && !snapshot.Samples[0].CpuPercent.HasValue && snapshot.Latest.CpuPercent.HasValue &&
                        snapshot.Latest.CpuPercent.Value >= 0 && snapshot.Latest.CpuPercent.Value <= 100,
                        "CPU 首帧等待增量，后续真实采样位于 0–100%");
                    Check(snapshot.Latest != null && !String.IsNullOrEmpty(snapshot.Latest.GpuStatus) &&
                        (!snapshot.Latest.GpuPercent.HasValue || snapshot.Latest.GpuPercent.Value >= 0 && snapshot.Latest.GpuPercent.Value <= 100),
                        "GPU 实机返回真实有效值或明确 unavailable 原因");
                    if (snapshot.Latest != null)
                        rows.Add("LIVE CPU=" + Format(snapshot.Latest.CpuPercent) + "% WORKING_SET=" + Format(snapshot.Latest.WorkingSetMb) +
                            "MB RAM=" + Format(snapshot.Latest.MemoryPercent) + "% GPU=" + Format(snapshot.Latest.GpuPercent) + "% " + snapshot.Latest.GpuStatus);
                    int loadStartCount = snapshot.Samples.Length;
                    Thread load = new Thread(delegate()
                    {
                        Stopwatch burn = Stopwatch.StartNew();
                        while (burn.ElapsedMilliseconds < 2200) Thread.SpinWait(10000);
                    });
                    load.IsBackground = true; load.Start();
                    bool loadSeen = Wait(delegate { return sampler.GetSnapshot().Samples.Length >= loadStartCount + 2; }, 5000);
                    PerformanceSnapshot busy = sampler.GetSnapshot(); load.Join(4000);
                    Check(loadSeen && busy.Latest.CpuPercent.HasValue && busy.Latest.CpuPercent.Value > 0,
                        "受控单线程真实 CPU 负载被检测到，非固定或随机图表数据");
                    if (busy.Latest != null) rows.Add("LIVE_BUSY_CPU=" + Format(busy.Latest.CpuPercent) + "%");
                    int repeatedStart=sampler.GetSnapshot().Samples.Length;Stopwatch repeated=Stopwatch.StartNew();
                    for(int i=0;i<20;i++){sampler.SetEnabled(true);Thread.Sleep(20);}
                    Check(sampler.GetSnapshot().Samples.Length-repeatedStart<=1+(int)Math.Ceiling(repeated.Elapsed.TotalSeconds),
                        "重复启用/窗口缩放不唤醒额外高频性能采样，仍保持约1 Hz");
                    Check(busy.Samples.Length < 2 || busy.Samples[busy.Samples.Length - 1].ElapsedSeconds -
                        busy.Samples[busy.Samples.Length - 2].ElapsedSeconds >= .9,
                        "性能读取约 1 Hz，不因启动信号产生紧邻重复样本");
                    sampler.SetEnabled(false);
                    Check(Wait(delegate { return !sampler.IsSampling; }, 4000), "隐藏性能面板及时停止采样并释放 GPU 查询");
                    int before = sampler.GetSnapshot().Samples.Length; Thread.Sleep(1150);
                    Check(sampler.GetSnapshot().Samples.Length == before && !sampler.GetSnapshot().Enabled,
                        "隐藏后没有周期轮询/新增历史点");
                    sampler.SetEnabled(true);
                    Check(Wait(delegate { return sampler.GetSnapshot().Samples.Length > before; }, 4000), "显示面板恢复后台采样");
                    PerformanceSnapshot resumed = sampler.GetSnapshot();
                    Check(!resumed.Latest.CpuPercent.HasValue, "恢复时重新建立 CPU 基准，不把隐藏期间混进短时占用");
                }
                finally { sampler.Dispose(); }
                Check(Wait(delegate { return !sampler.WorkerAlive; }, 4000) && !sampler.GetSnapshot().Enabled,
                    "Dispose 退出采样线程并释放原生计数器/缓冲区");
                sampler.SetEnabled(true); sampler.Dispose();
                Check(!sampler.WorkerAlive, "重复 Dispose/关闭后启用均安全，不重启线程");
            }
            catch (Exception ex) { Check(false, "性能测试异常：" + ex); }
            rows.Add("PASS_COUNT=" + rows.FindAll(delegate(string s) { return s.StartsWith("PASS "); }).Count);
            rows.Add("FAILURES=" + failures);
            File.WriteAllLines(Path.Combine(folder, "performance-tests.txt"), rows.ToArray(), new UTF8Encoding(true));
            Environment.ExitCode = failures == 0 ? 0 : 1;
        }
        static string Format(double? value) { return value.HasValue ? value.Value.ToString("F2", CultureInfo.InvariantCulture) : "unavailable"; }
    }
}

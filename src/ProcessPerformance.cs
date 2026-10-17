using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace MuMuBeans
{
    // These are this application's costs, not the emulator's or the whole PC's load.
    sealed class PerformanceSample
    {
        public readonly double ElapsedSeconds;
        public readonly double? CpuPercent, WorkingSetMb, MemoryPercent, GpuPercent;
        public readonly string CpuStatus, MemoryStatus, GpuStatus;

        public PerformanceSample(double elapsedSeconds, double? cpu, double? memoryMb,
            double? memoryPercent, double? gpu, string cpuStatus, string memoryStatus, string gpuStatus)
        {
            ElapsedSeconds = elapsedSeconds; CpuPercent = cpu; WorkingSetMb = memoryMb;
            MemoryPercent = memoryPercent; GpuPercent = gpu;
            CpuStatus = cpuStatus; MemoryStatus = memoryStatus; GpuStatus = gpuStatus;
        }
    }

    sealed class PerformanceSnapshot
    {
        public readonly bool Enabled;
        public readonly PerformanceSample Latest;
        public readonly PerformanceSample[] Samples;
        public PerformanceSnapshot(bool enabled, PerformanceSample[] samples)
        {
            Enabled = enabled; Samples = samples;
            Latest = samples.Length == 0 ? null : samples[samples.Length - 1];
        }
    }

    sealed class PerformanceHistory
    {
        readonly Queue<PerformanceSample> samples = new Queue<PerformanceSample>();
        public void Add(PerformanceSample sample)
        {
            samples.Enqueue(sample);
            while (samples.Count > 60) samples.Dequeue();
        }
        public PerformanceSample[] Copy() { return samples.ToArray(); }
    }

    sealed class ProcessPerformance : IDisposable
    {
        readonly object sync = new object();
        readonly PerformanceHistory history = new PerformanceHistory();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly Stopwatch lifetime = Stopwatch.StartNew();
        Thread worker;
        volatile bool enabled, disposed, sampling;

        public bool IsSampling { get { return sampling; } }
        internal bool WorkerAlive { get { lock (sync) return worker != null && worker.IsAlive; } }

        // Construction is inert so rendered previews/tests don't start performance counters.
        public void SetEnabled(bool value)
        {
            lock (sync)
            {
                if (disposed) return;
                if (enabled == value && (worker != null || !value)) return;
                enabled = value;
                if (value && worker == null)
                {
                    worker = new Thread(Work); worker.IsBackground = true;
                    worker.Name = "Bean Studio performance (1 Hz)"; worker.Start();
                }
                else if (worker != null) wake.Set();
            }
        }

        public PerformanceSnapshot GetSnapshot()
        {
            lock (sync) return new PerformanceSnapshot(enabled && !disposed, history.Copy());
        }

        void Work()
        {
            GpuProcessCounter gpu = null;
            using (Process process = Process.GetCurrentProcess())
            {
                bool baseline = false;
                double previousWall = 0, previousCpu = 0;
                ulong totalRam = ReadTotalPhysicalMemory();
                try
                {
                    while (!disposed)
                    {
                        if (!enabled)
                        {
                            if (gpu != null) { gpu.Dispose(); gpu = null; }
                            baseline = false; sampling = false;
                            wake.WaitOne(); continue;
                        }
                        sampling = true;
                        if (gpu == null) gpu = new GpuProcessCounter(process.Id);
                        double now = lifetime.Elapsed.TotalSeconds;
                        double? cpu = null, memoryMb = null, memoryPercent = null;
                        string cpuStatus = "CPU 等待第二次采样", memoryStatus = "本程序工作集";
                        try
                        {
                            process.Refresh();
                            double cpuSeconds = process.TotalProcessorTime.TotalSeconds;
                            if (baseline)
                            {
                                cpu = CpuUsage(cpuSeconds - previousCpu, now - previousWall, Environment.ProcessorCount);
                                cpuStatus = cpu.HasValue ? "本程序 CPU，按逻辑核心归一化" : "CPU 采样间隔无效";
                            }
                            previousCpu = cpuSeconds; previousWall = now; baseline = true;
                            long workingSet = process.WorkingSet64;
                            memoryMb = workingSet / (1024.0 * 1024.0);
                            memoryPercent = totalRam > 0 ? (double?)ClampPercent(workingSet * 100.0 / totalRam) : null;
                            if (!memoryPercent.HasValue) memoryStatus = "工作集可读；物理内存总量不可用";
                        }
                        catch (Exception ex)
                        {
                            if (!(ex is System.ComponentModel.Win32Exception) && !(ex is InvalidOperationException)) throw;
                            baseline = false; cpuStatus = "CPU 读取不可用"; memoryStatus = "工作集读取不可用";
                        }
                        string gpuStatus;
                        double? gpuValue = gpu.Read(out gpuStatus);
                        PerformanceSample sample = new PerformanceSample(now, cpu, memoryMb, memoryPercent,
                            gpuValue, cpuStatus, memoryStatus, gpuStatus);
                        lock (sync) if (enabled && !disposed) history.Add(sample);
                        wake.WaitOne(1000);
                    }
                }
                catch (Exception ex)
                {
                    // Background exceptions must not terminate the monitoring application.
                    string error = "性能采样已停止：" + ex.GetType().Name;
                    lock (sync) if (!disposed) history.Add(new PerformanceSample(lifetime.Elapsed.TotalSeconds,
                        null, null, null, null, error, error, error));
                }
                finally
                {
                    if (gpu != null) gpu.Dispose();
                    sampling = false;
                    // Dispose may return before a slow OS counter call. The worker owns cleanup.
                    lock (sync) { enabled = false; disposed = true; wake.Dispose(); }
                }
            }
        }

        internal static double? CpuUsage(double cpuSeconds, double wallSeconds, int logicalProcessors)
        {
            if (wallSeconds <= 0 || cpuSeconds < 0 || logicalProcessors < 1 ||
                Double.IsNaN(cpuSeconds) || Double.IsInfinity(cpuSeconds) ||
                Double.IsNaN(wallSeconds) || Double.IsInfinity(wallSeconds)) return null;
            return ClampPercent(cpuSeconds * 100.0 / wallSeconds / logicalProcessors);
        }
        internal static double ClampPercent(double value) { return Math.Max(0, Math.Min(100, value)); }

        [StructLayout(LayoutKind.Sequential)]
        struct MemoryStatus
        {
            public uint Length, Load;
            public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile;
            public ulong TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        static ulong ReadTotalPhysicalMemory()
        {
            MemoryStatus status = new MemoryStatus(); status.Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            return GlobalMemoryStatusEx(ref status) ? status.TotalPhysical : 0;
        }

        public void Dispose()
        {
            Thread thread;
            lock (sync)
            {
                if (disposed) return;
                disposed = true; enabled = false; thread = worker;
                if (thread == null) wake.Dispose(); else wake.Set();
            }
            if (thread != null && thread != Thread.CurrentThread) thread.Join(2000);
        }
    }

    sealed class GpuEngineValue
    {
        public readonly string Instance;
        public readonly double Value;
        public GpuEngineValue(string instance, double value) { Instance = instance; Value = value; }
    }

    // PDH is native, language-neutral and queried only once a second. No WMI polling.
    // PdhGetFormattedCounterArrayW is the documented API for wildcard instance paths:
    // https://learn.microsoft.com/windows/win32/api/pdh/nf-pdh-pdhgetformattedcounterarrayw
    // Busiest-engine aggregation matches Microsoft's Task Manager convention:
    // https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/
    sealed class GpuProcessCounter : IDisposable
    {
        const uint MoreData = 0x800007D2, FormatDouble = 0x200, NoCap100 = 0x8000;
        readonly int processId;
        IntPtr query, counter, buffer;
        uint capacity;
        bool primed, unavailable;
        string unavailableReason;

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        struct CounterValue
        {
            [FieldOffset(0)] public uint Status;
            [FieldOffset(8)] public double Value;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct CounterItem { public IntPtr Name; public CounterValue Value; }
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        static extern uint PdhOpenQueryW(string dataSource, UIntPtr userData, out IntPtr query);
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        static extern uint PdhAddEnglishCounterW(IntPtr query, string path, UIntPtr userData, out IntPtr counter);
        [DllImport("pdh.dll")] static extern uint PdhCollectQueryData(IntPtr query);
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint size, out uint count, IntPtr values);
        [DllImport("pdh.dll")] static extern uint PdhCloseQuery(IntPtr query);

        public GpuProcessCounter(int pid)
        {
            processId = pid;
            try
            {
                uint result = PdhOpenQueryW(null, UIntPtr.Zero, out query);
                if (result == 0) result = PdhAddEnglishCounterW(query,
                    "\\GPU Engine(pid_" + pid.ToString(CultureInfo.InvariantCulture) + "_*)\\Utilization Percentage",
                    UIntPtr.Zero, out counter);
                if (result != 0) Unavailable("GPU 计数器不可用 (" + Hex(result) + ")");
            }
            catch (DllNotFoundException) { Unavailable("系统不支持 GPU PDH 计数器"); }
            catch (EntryPointNotFoundException) { Unavailable("系统不支持英文 GPU 计数器"); }
        }

        void Unavailable(string reason) { unavailable = true; unavailableReason = reason; CloseQuery(); }
        static string Hex(uint result) { return "0x" + result.ToString("X8", CultureInfo.InvariantCulture); }

        public double? Read(out string status)
        {
            if (unavailable) { status = unavailableReason; return null; }
            uint result = PdhCollectQueryData(query);
            if (result != 0)
            {
                primed = false;
                status = "GPU 暂无本程序计数器数据 (" + Hex(result) + ")";
                return null;
            }
            if (!primed) { primed = true; status = "GPU 等待第二次采样"; return null; }
            uint bytes = 0, count;
            result = PdhGetFormattedCounterArrayW(counter, FormatDouble | NoCap100, ref bytes, out count, IntPtr.Zero);
            if (result != MoreData || bytes == 0 || bytes > 4 * 1024 * 1024)
            {
                status = "GPU 样本不可用 (" + Hex(result) + ")"; return null;
            }
            // Retry only once when instances appear between the size/data calls.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (bytes > capacity)
                {
                    if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                    buffer = Marshal.AllocHGlobal((int)bytes); capacity = bytes;
                }
                uint size = capacity;
                result = PdhGetFormattedCounterArrayW(counter, FormatDouble | NoCap100, ref size, out count, buffer);
                if (result != MoreData) break;
                bytes = 0;
                result = PdhGetFormattedCounterArrayW(counter, FormatDouble | NoCap100, ref bytes, out count, IntPtr.Zero);
                if (result != MoreData || bytes == 0 || bytes > 4 * 1024 * 1024) break;
            }
            if (result != 0) { status = "GPU 样本读取失败 (" + Hex(result) + ")"; return null; }
            int stride = Marshal.SizeOf(typeof(CounterItem));
            if (count > capacity / stride) { status = "GPU 样本长度异常"; return null; }
            List<GpuEngineValue> values = new List<GpuEngineValue>((int)count);
            for (int i = 0; i < count; i++)
            {
                CounterItem item = (CounterItem)Marshal.PtrToStructure(IntPtr.Add(buffer, i * stride), typeof(CounterItem));
                // PDH_CSTATUS_VALID_DATA (0) and NEW_DATA (1) are the only readable statuses.
                if (item.Value.Status > 1 || item.Name == IntPtr.Zero) continue;
                values.Add(new GpuEngineValue(Marshal.PtrToStringUni(item.Name), item.Value.Value));
            }
            double? value = Aggregate(processId, values);
            status = value.HasValue ? "本程序 GPU 最忙引擎" : "暂无本程序 GPU 引擎；需 WDDM 2.0+ 驱动";
            return value;
        }

        internal static double? Aggregate(int pid, IList<GpuEngineValue> values)
        {
            string prefix = "pid_" + pid.ToString(CultureInfo.InvariantCulture) + "_";
            Dictionary<string, double> engines = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> instances = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GpuEngineValue item in values)
            {
                if (item == null || String.IsNullOrEmpty(item.Instance) ||
                    !item.Instance.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    Double.IsNaN(item.Value) || Double.IsInfinity(item.Value) || item.Value < 0) continue;
                string engine = PhysicalEngine(item.Instance.Substring(prefix.Length));
                if (engine == null || !instances.Add(item.Instance)) continue;
                double sum; engines.TryGetValue(engine, out sum);
                engines[engine] = sum + item.Value;
            }
            if (engines.Count == 0) return null;
            double busiest = 0;
            foreach (double value in engines.Values) busiest = Math.Max(busiest, value);
            return ProcessPerformance.ClampPercent(busiest);
        }

        static string PhysicalEngine(string instance)
        {
            string[] parts = instance.Split('_');
            string luid = null, physical = null, engine = null;
            for (int i = 0; i + 1 < parts.Length; i++)
            {
                if (parts[i].Equals("luid", StringComparison.OrdinalIgnoreCase) && i + 2 < parts.Length)
                    luid = parts[i + 1] + "_" + parts[i + 2];
                else if (parts[i].Equals("phys", StringComparison.OrdinalIgnoreCase)) physical = parts[i + 1];
                else if (parts[i].Equals("eng", StringComparison.OrdinalIgnoreCase)) engine = parts[i + 1];
            }
            int physicalId, engineId;
            if (luid == null || !Int32.TryParse(physical, out physicalId) || !Int32.TryParse(engine, out engineId)) return null;
            return luid + "_phys_" + physicalId + "_eng_" + engineId;
        }

        void CloseQuery()
        {
            if (query != IntPtr.Zero) { PdhCloseQuery(query); query = IntPtr.Zero; counter = IntPtr.Zero; }
        }
        public void Dispose()
        {
            CloseQuery();
            if (buffer != IntPtr.Zero) { Marshal.FreeHGlobal(buffer); buffer = IntPtr.Zero; capacity = 0; }
        }
    }
}

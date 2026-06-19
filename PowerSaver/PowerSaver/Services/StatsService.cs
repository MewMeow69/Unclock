using System.Diagnostics;
using Microsoft.Win32;

namespace Unclock.Services;

internal class CpuStat
{
    public int Usage { get; set; }
    public int FreqMHz { get; set; }
    public int TempC { get; set; }
    public bool HasTemp { get; set; }
    public double PowerW { get; set; }
    public bool HasPower { get; set; }
}

internal class GpuStat
{
    public int Usage { get; set; }
    public int TempC { get; set; }
    public int MemUsedMB { get; set; }
    public int MemTotalMB { get; set; }
    public int FreqMHz { get; set; }
    public double PowerW { get; set; }
    public bool Available { get; set; }
}

internal record CpuSnapshot(CpuStat Cpu, bool IsFresh);

internal record GpuSnapshot(GpuStat Gpu, bool IsFresh);

internal static class StatsService
{
    static ulong _prevIdle, _prevKernel, _prevUser;
    static bool _firstCpuSample = true;
    static int _cpuMaxFreq;
    static int _cpuTemp = -1;
    static bool _cpuTempDone;
    static double _cpuTdp;
    static bool _cpuTdpDone;
    static CpuStat _lastCpu = new();
    static GpuStat _lastGpu = new();
    static readonly object _lock = new();

    static StatsService()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            _cpuMaxFreq = (int?)(key?.GetValue("~MHz")) ?? 0;
        }
        catch { }
    }

    public static CpuSnapshot RefreshCpu()
    {
        lock (_lock)
        {
            NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user);
            var idleNow = idle.ToUlong();
            var kernelNow = kernel.ToUlong();
            var userNow = user.ToUlong();

            int usage = 0;
            if (!_firstCpuSample)
            {
                var idleDelta = idleNow - _prevIdle;
                var totalDelta = (kernelNow - _prevKernel) + (userNow - _prevUser);
                if (totalDelta > 0)
                    usage = (int)((1.0 - (double)idleDelta / totalDelta) * 100);
            }
            _firstCpuSample = false;
            _prevIdle = idleNow;
            _prevKernel = kernelNow;
            _prevUser = userNow;

            if (!_cpuTempDone) { ReadCpuTemp(); _cpuTempDone = true; }
            if (!_cpuTdpDone) { ReadCpuTdp(); _cpuTdpDone = true; }

            var hasPower = _cpuTdp > 0;
            var isFresh = !_firstCpuSample;
            _lastCpu = new CpuStat
            {
                Usage = Math.Clamp(usage, 0, 100),
                FreqMHz = _cpuMaxFreq,
                TempC = Math.Max(0, _cpuTemp),
                HasTemp = _cpuTemp >= 0,
                PowerW = hasPower ? Math.Round(_cpuTdp * usage / 100.0, 1) : 0,
                HasPower = hasPower
            };
            return new CpuSnapshot(_lastCpu, true);
        }
    }

    public static CpuStat GetCachedCpu() { lock (_lock) { return _lastCpu; } }

    static void ReadCpuTemp()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("powershell",
                "-NoProfile -Command \"(Get-CimInstance -Namespace root/WMI -ClassName MSAcpu_ThermalZoneTemperature | Select-Object -ExpandProperty CurrentTemperature)\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
            if (p == null) return;
            var line = p.StandardOutput.ReadLine();
            p.WaitForExit(2000);
            if (p.ExitCode == 0 && line != null && int.TryParse(line.Trim(), out var raw))
                _cpuTemp = raw / 10 - 273;
        }
        catch { }
    }

    static void ReadCpuTdp()
    {
        // ponytail: fallback TDP if WMI doesn't report it
        try
        {
            using var p = Process.Start(new ProcessStartInfo("powershell",
                "-NoProfile -Command \"(Get-CimInstance Win32_Processor | Select-Object -First 1).ThermalDesignPower\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
            if (p == null) { _cpuTdp = 65; return; }
            var line = p.StandardOutput.ReadLine();
            p.WaitForExit(2000);
            if (p.ExitCode == 0 && line != null && double.TryParse(line.Trim(), out var tdp) && tdp > 0)
                _cpuTdp = tdp;
            else
                _cpuTdp = 65;
        }
        catch { _cpuTdp = 65; }
    }

    public static GpuSnapshot RefreshGpu(bool isNvidia)
    {
        var stat = new GpuStat();
        if (isNvidia)
        {
            var err = GpuControl.QueryStats(out var u, out var t, out var mu, out var mt, out var f, out var w);
            if (err == "")
            {
                stat.Usage = u; stat.TempC = t; stat.MemUsedMB = mu;
                stat.MemTotalMB = mt; stat.FreqMHz = f; stat.PowerW = w;
                stat.Available = true;
            }
        }
        _lastGpu = stat;
        return new GpuSnapshot(stat, true);
    }

    public static GpuStat GetCachedGpu() { return _lastGpu; }
}

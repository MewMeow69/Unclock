using System.Diagnostics;

namespace Unclock.Services;

internal static class GpuControl
{
    public static string NvidiaSave()
    {
        return Elevated.Run("nvidia-smi -lgc 0 && nvidia-smi -lmc 0");
    }

    public static string NvidiaReset()
    {
        return Elevated.Run("nvidia-smi --reset-gpu-clocks && nvidia-smi --reset-memory-clocks");
    }

    public static string AmdSave()
    {
        return Elevated.Run("amd_bridge.exe --power-save");
    }

    public static string AmdMediumSave()
    {
        return Elevated.Run("amd_bridge.exe --medium-save");
    }

    public static string AmdReset()
    {
        return Elevated.Run("amd_bridge.exe --reset");
    }

    public static string NvidiaMediumSave()
    {
        try
        {
            var maxGpu = RunCaptured("nvidia-smi --query-gpu=clocks.max.graphics --format=csv,noheader,nounits");
            var maxMem = RunCaptured("nvidia-smi --query-gpu=clocks.max.memory --format=csv,noheader,nounits");
            if (!int.TryParse(maxGpu?.Trim(), out var gpuMax)) gpuMax = 1500;
            if (!int.TryParse(maxMem?.Trim(), out var memMax)) memMax = 5000;
            var gpuTarget = gpuMax / 2;
            var memTarget = memMax / 2;
            return Elevated.Run($"nvidia-smi -lgc 0,{gpuTarget} && nvidia-smi -lmc 0,{memTarget}");
        }
        catch { return "medium save failed"; }
    }

    static string? RunCaptured(string command)
    {
        try
        {
            var parts = command.Split(' ', 2);
            using var p = Process.Start(new ProcessStartInfo(parts[0], parts.Length > 1 ? parts[1] : "")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            });
            if (p == null) return null;
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(5000);
            return p.ExitCode == 0 ? output : null;
        }
        catch { return null; }
    }

    // returns detected GpuPowerMode (0=Performance, 1=Medium, 2=PowerSaver, -1=unknown)
    public static int NvidiaDetectMode()
    {
        try
        {
            var cur = RunCaptured("nvidia-smi --query-gpu=clocks.applications.graphics --format=csv,noheader,nounits");
            var max = RunCaptured("nvidia-smi --query-gpu=clocks.max.graphics --format=csv,noheader,nounits");
            if (!int.TryParse(cur?.Trim(), out var curMhz)) return -1;
            if (!int.TryParse(max?.Trim(), out var maxMhz) || maxMhz <= 0) return -1;
            double ratio = (double)curMhz / maxMhz;
            if (ratio < 0.25) return 2; // PowerSaver
            if (ratio < 0.60) return 1; // Medium
            return 0; // Performance
        }
        catch { return -1; }
    }

    public static int AmdDetectMode()
    {
        try
        {
            var json = RunCaptured("amd_bridge.exe --detect");
            if (json == null) return -1;
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("mode", out var modeProp) && modeProp.GetString() == "auto")
                return 0; // auto = Performance

            int hwMin = root.GetProperty("hwMin").GetInt32();
            int hwMax = root.GetProperty("hwMax").GetInt32();
            int curMax = root.GetProperty("curMax").GetInt32();
            int range = hwMax - hwMin;
            if (range <= 0) return -1;
            double ratio = (double)(curMax - hwMin) / range;
            if (ratio < 0.25) return 2; // PowerSaver
            if (ratio < 0.65) return 1; // Medium
            return 0; // Performance
        }
        catch { return -1; }
    }

    public static string QueryStats(out int usage, out int temp, out int memUsed, out int memTotal, out int freq, out double powerW)
    {
        usage = temp = memUsed = memTotal = freq = 0;
        powerW = 0;
        try
        {
            using var p = Process.Start(new ProcessStartInfo("nvidia-smi",
                "--query-gpu=utilization.gpu,temperature.gpu,memory.used,memory.total,clocks.current.graphics,power.draw --format=csv,noheader,nounits")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            });
            if (p == null) return "no nvidia-smi";
            var line = p.StandardOutput.ReadLine();
            p.WaitForExit(5000);
            if (p.ExitCode != 0 || line == null) return $"exit {p.ExitCode}";
            var parts = line.Split(',');
            if (parts.Length < 6) return "unexpected output";
            usage = int.Parse(parts[0].Trim());
            temp = int.Parse(parts[1].Trim());
            memUsed = int.Parse(parts[2].Trim());
            memTotal = int.Parse(parts[3].Trim());
            freq = int.Parse(parts[4].Trim());
            powerW = double.Parse(parts[5].Trim());
            return "";
        }
        catch (Exception ex) { return ex.Message; }
    }
}

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Unclock.Services;

internal class HardwareInfo
{
    public bool HasNvidiaGpu { get; set; }
    public bool HasAmdGpu { get; set; }
    public bool HasIntelArcUnsupported { get; set; }
    public string GpuName { get; set; } = "";
    public bool HasCpu { get; set; } = true;
}

internal static class HardwareDetector
{
    public static HardwareInfo Detect()
    {
        var info = new HardwareInfo();

        var nvidiaName = GetNvidiaGpuName();
        if (nvidiaName != null)
        {
            info.HasNvidiaGpu = true;
            info.GpuName = nvidiaName;
        }

        var amdName = GetAmdGpuName();
        if (amdName != null)
        {
            info.HasAmdGpu = true;
            if (!info.HasNvidiaGpu) info.GpuName = amdName;
        }

        if (!info.HasNvidiaGpu && !info.HasAmdGpu)
        {
            var arcName = GetIntelArcName();
            if (arcName != null)
            {
                info.HasIntelArcUnsupported = true;
                info.GpuName = arcName;
            }
            else
            {
                info.GpuName = "none detected";
            }
        }

        return info;
    }

    static string? GetNvidiaGpuName()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("nvidia-smi", "--query-gpu=name --format=csv,noheader")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            });
            if (p == null) return null;
            var name = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(3000);
            return p.ExitCode == 0 && name != "" ? Regex.Replace(name, @"\s+", " ") : null;
        }
        catch { return null; }
    }

    static string? GetAmdGpuName()
    {
        // WMI: detect AMD/ATI/Radeon GPU — works without amd_bridge.exe
        try
        {
            using var p = Process.Start(new ProcessStartInfo("powershell",
                "-NoProfile -Command \"Get-CimInstance Win32_VideoController | Where-Object { $_.AdapterCompatibility -like '*AMD*' -or $_.AdapterCompatibility -like '*ATI*' -or $_.Name -like '*Radeon*' -or $_.Name -like '*AMD*' } | Select-Object -First 1 -ExpandProperty Name\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
            if (p == null) return null;
            var name = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(5000);
            if (p.ExitCode == 0 && name != "") return name;
        }
        catch { }

        // fallback: try amd_bridge --info for richer naming
        try
        {
            using var p = Process.Start(new ProcessStartInfo("amd_bridge.exe", "--info")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            });
            if (p == null) return null;
            var json = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(3000);
            if (p.ExitCode != 0 || json == "") return null;
            var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement[0].GetProperty("name").GetString();
        }
        catch { return null; }
    }

    // detect dedicated Intel Arc GPUs only — integrated iGPUs are ignored
    static string? GetIntelArcName()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("powershell",
                "-NoProfile -Command \"Get-CimInstance Win32_VideoController | Where-Object { $_.AdapterCompatibility -like '*Intel*' -and $_.Name -like '*Arc*' } | Select-Object -First 1 -ExpandProperty Name\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
            if (p == null) return null;
            var name = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(5000);
            return p.ExitCode == 0 && name != "" ? name : null;
        }
        catch { return null; }
    }
}

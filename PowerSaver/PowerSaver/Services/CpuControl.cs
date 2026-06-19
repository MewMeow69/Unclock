using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Unclock.Services;

internal static class CpuControl
{
    static readonly string PlanHighPerf = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    static readonly string PlanBalanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    static readonly string PlanPowerSaver = "a1841308-3541-4fab-bc81-f71556f20b4a";

    static string CreateCustomScheme(int maxPercent)
    {
        var output = Elevated.RunCapture("powercfg -duplicatescheme SCHEME_BALANCED");
        var m = Regex.Match(output, @"\{([^}]+)\}");
        var guid = m.Success ? m.Groups[1].Value : "";
        if (string.IsNullOrEmpty(guid)) return "failed to create custom power scheme";

        var script = $@"
powercfg /setacvalueindex {guid} SUB_PROCESSOR PROCTHROTTLEMAX {maxPercent}
powercfg /setdcvalueindex {guid} SUB_PROCESSOR PROCTHROTTLEMAX {maxPercent}
powercfg /setacvalueindex {guid} SUB_PROCESSOR PROCTHROTTLEMIN 5
powercfg /setdcvalueindex {guid} SUB_PROCESSOR PROCTHROTTLEMIN 5
powercfg /setacvalueindex {guid} SUB_PROCESSOR SYSCOOLPOL 1
powercfg /setdcvalueindex {guid} SUB_PROCESSOR SYSCOOLPOL 1
powercfg /setactive {guid}
";
        return Elevated.Run(script);
    }

    public static string CpuSetMaxSave() => CreateCustomScheme(50);
    public static string CpuSetMidSave() => CreateCustomScheme(70);
    public static string CpuSetHighPerf() => Elevated.Run($"powercfg /setactive {PlanBalanced}");
    public static string CpuSetMaxPerf()
    {
        // set minimum processor state to 100% so CPU stays at max frequency
        var script = $@"
powercfg /setacvalueindex {PlanHighPerf} SUB_PROCESSOR PROCTHROTTLEMIN 100
powercfg /setdcvalueindex {PlanHighPerf} SUB_PROCESSOR PROCTHROTTLEMIN 100
powercfg /setactive {PlanHighPerf}
";
        return Elevated.Run(script);
    }

    // legacy — called by Reset All
    public static string Save() => CpuSetMidSave();
    public static string Reset() => CpuSetHighPerf();

    public static string SetPowerPlan(string plan)
    {
        var guid = plan switch
        {
            "high" => PlanHighPerf,
            "balanced" => PlanBalanced,
            "saver" => PlanPowerSaver,
            _ => PlanBalanced
        };
        return Elevated.Run($"powercfg /setactive {guid}");
    }

    public static string DetectActivePlan()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("powercfg", "-getactivescheme")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            });
            if (p == null) return "balanced";
            var text = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            if (p.ExitCode != 0) return "balanced";

            var m = Regex.Match(text, @"\{([0-9a-fA-F\-]+)\}");
            if (!m.Success) return "balanced";
            var guid = m.Groups[1].Value.ToLowerInvariant();

            if (guid == PlanHighPerf.ToLowerInvariant()) return "high";
            if (guid == PlanBalanced.ToLowerInvariant()) return "balanced";
            if (guid == PlanPowerSaver.ToLowerInvariant()) return "saver";
            return "custom"; // our modified scheme or other custom plan
        }
        catch { return "balanced"; }
    }
}

using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Unclock.Services;

internal static class Elevated
{
    static string Exec(string scriptLine, bool returnOutput)
    {
        var tmpOut = Path.GetTempFileName();
        var tmpBat = Path.ChangeExtension(Path.GetTempFileName(), ".bat");
        try
        {
            File.WriteAllText(tmpBat, $"@echo off\r\n{scriptLine} > \"{tmpOut}\" 2>&1\r\nexit /b %ERRORLEVEL%\r\n");
            using var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{tmpBat}\"")
            {
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            if (p == null) return returnOutput ? "" : "elevation cancelled";
            p.WaitForExit(60000);
            var output = File.Exists(tmpOut) ? File.ReadAllText(tmpOut).Trim() : "";
            return returnOutput ? output : (p.ExitCode == 0 ? "" : $"exit {p.ExitCode}: {output}");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return returnOutput ? "" : "elevation cancelled"; }
        catch (Exception ex) { return returnOutput ? "" : ex.Message; }
        finally { try { File.Delete(tmpBat); File.Delete(tmpOut); } catch { } }
    }

    public static string Run(string scriptLine) => Exec(scriptLine, false);
    public static string RunCapture(string scriptLine) => Exec(scriptLine, true);
}

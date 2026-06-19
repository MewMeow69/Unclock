using System.Runtime.InteropServices;

namespace Unclock;

internal static class NativeMethods
{
    const string DwmApi = "dwmapi.dll";
    const string User32 = "user32.dll";
    const string Kernel32 = "kernel32.dll";

    [DllImport(DwmApi)]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport(User32)]
    static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport(Kernel32)]
    static extern bool GetPhysicallyInstalledSystemMemory(out long totalKilobytes);

    [DllImport(Kernel32, SetLastError = true)]
    public static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential)]
    public struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
        public ulong ToUlong() => ((ulong)dwHighDateTime << 32) | dwLowDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    const int DWMSBT_MAINWINDOW = 2;
    const int DWMSBT_ACRYLIC = 3;

    const int WCA_ACCENT_POLICY = 19;
    const int ACCENT_ENABLE_BLURBEHIND = 3;
    const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

    static int? _winBuild;

    static int WinBuild
    {
        get
        {
            if (_winBuild == null)
            {
                var os = Environment.OSVersion;
                _winBuild = os.Version.Build;
            }
            return _winBuild.Value;
        }
    }

    public static void ApplyBackdrop(IntPtr hwnd)
    {
        if (WinBuild >= 22000)
        {
            int backdrop = DWMSBT_ACRYLIC;
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }
        else if (WinBuild >= 17134)
        {
            var accent = new AccentPolicy
            {
                AccentState = ACCENT_ENABLE_ACRYLICBLURBEHIND,
                GradientColor = unchecked((int)0xE00D1117)
            };
            var accentStruct = Marshal.AllocHGlobal(Marshal.SizeOf(accent));
            try
            {
                Marshal.StructureToPtr(accent, accentStruct, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WCA_ACCENT_POLICY,
                    Data = accentStruct,
                    SizeOfData = Marshal.SizeOf(accent)
                };
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(accentStruct);
            }
        }
    }
}

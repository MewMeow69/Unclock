using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Unclock.Services;
using SD = System.Drawing;

namespace Unclock;

public partial class MainWindow : Window
{
    enum Theme { Nvidia, Amd, Intel }
    enum GpuPowerMode { Performance, Medium, PowerSaver }
    enum CpuPowerMode { MaxPerf, HighPerf, MidSave, MaxSave }
    enum WinPowerMode { HighPerformance, Balanced, PowerSaver }

    bool _forceClose;
    bool _isNvidia;
    bool _isAmd;
    Theme _currentTheme = Theme.Nvidia;
    bool _savedOriginals;
    GpuPowerMode _gpuMode = GpuPowerMode.Performance;
    CpuPowerMode _cpuMode = CpuPowerMode.HighPerf;
    WinPowerMode _winMode = WinPowerMode.Balanced;

    SolidColorBrush _origAccent = null!, _origAccentDim = null!, _origAccentDimHover = null!, _origAccentDimPressed = null!, _origBorderDim = null!, _origBorderHover = null!, _origBrushAccent = null!;
    Color _origAccentColor;

    readonly DispatcherTimer _glowTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    double _glowPhase;
    readonly List<Button> _glowBtns = new();
    Button[] _gpuBtns = null!;
    Button[] _cpuBtns = null!;
    Button[] _winBtns = null!;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;

        _glowTimer.Tick += (_, _) =>
        {
            _glowPhase += 0.2;
            if (_glowPhase > Math.PI * 2) _glowPhase -= Math.PI * 2;
            var blur = 6 + 6 * Math.Sin(_glowPhase);
            var alpha = 0.35 + 0.25 * Math.Sin(_glowPhase);
            var c = _currentTheme switch
            {
                Theme.Amd => Color.FromArgb((byte)(alpha * 255), 237, 28, 36),
                Theme.Intel => Color.FromArgb((byte)(alpha * 255), 0, 194, 255),
                _ => Color.FromArgb((byte)(alpha * 255), 118, 185, 0)
            };
            foreach (var b in _glowBtns)
                if (b.Effect is DropShadowEffect d) { d.BlurRadius = blur; d.Color = c; }
        };
    }

    async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            _gpuBtns = new[] { GpuPerfBtn, GpuMedBtn, GpuSaverBtn };
            _cpuBtns = new[] { CpuMaxPerfBtn, CpuHighPerfBtn, CpuMidSaveBtn, CpuMaxSaveBtn };
            _winBtns = new[] { WinHighBtn, WinBalBtn, WinSaverBtn };

            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.ApplyBackdrop(hwnd);

            var hw = await Task.Run(() => HardwareDetector.Detect());
            _isNvidia = hw.HasNvidiaGpu;
            _isAmd = hw.HasAmdGpu;
            bool hasGpu = _isNvidia || _isAmd;

            if (hw.HasNvidiaGpu || hw.HasAmdGpu)
                GpuLabel.Text = $"\u25CF {hw.GpuName}";
            else if (hw.HasIntelArcUnsupported)
                GpuLabel.Text = $"\u26A0 {hw.GpuName} \u2014 unsupported";
            else
                GpuLabel.Text = "\u2716 no compatible GPU detected";

            foreach (var b in _gpuBtns) b.IsEnabled = hasGpu;

            // ── detect current system state ──

            var plan = await Task.Run(() => CpuControl.DetectActivePlan());

            // detect Windows power plan
            _winMode = plan switch { "high" => WinPowerMode.HighPerformance, "saver" => WinPowerMode.PowerSaver, _ => WinPowerMode.Balanced };

            // detect CPU mode from plan
            _cpuMode = plan switch
            {
                "high" => CpuPowerMode.MaxPerf,
                "saver" => CpuPowerMode.MaxSave,
                "custom" => CpuPowerMode.MidSave,
                _ => CpuPowerMode.HighPerf
            };

            // detect GPU mode from hardware
            if (_isNvidia)
            {
                var detectedGpu = await Task.Run(() => GpuControl.NvidiaDetectMode());
                if (detectedGpu >= 0) _gpuMode = (GpuPowerMode)detectedGpu;
            }
            else if (_isAmd)
            {
                var detectedGpu = await Task.Run(() => GpuControl.AmdDetectMode());
                if (detectedGpu >= 0) _gpuMode = (GpuPowerMode)detectedGpu;
            }

            var detected = hw.HasNvidiaGpu ? Theme.Nvidia : hw.HasAmdGpu ? Theme.Amd : Theme.Intel;
            ApplyTheme(detected);

            // apply detected state (UI only, no commands)
            SetGpuMode(_gpuMode, apply: false);
            SetCpuMode(_cpuMode, apply: false);
            SetWinMode(_winMode, apply: false);

            BuildTrayIcon();
            StartStatsTimer();
        }
        catch
        {
            _gpuBtns ??= new[] { GpuPerfBtn, GpuMedBtn, GpuSaverBtn };
            _cpuBtns ??= new[] { CpuMaxSaveBtn, CpuMidSaveBtn, CpuHighPerfBtn, CpuMaxPerfBtn };
            _winBtns ??= new[] { WinHighBtn, WinBalBtn, WinSaverBtn };
            SetGpuMode(GpuPowerMode.Performance, apply: false);
            SetCpuMode(CpuPowerMode.HighPerf, apply: false);
            SetWinMode(WinPowerMode.Balanced, apply: false);
            BuildTrayIcon();
        }
    }

    void StartStatsTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            try
            {
                var cpu = await Task.Run(() => StatsService.RefreshCpu());
                var cpuText = cpu.Cpu.FreqMHz > 0
                    ? $"CPU  {cpu.Cpu.Usage}%  \u00B7  {cpu.Cpu.FreqMHz} MHz"
                    : $"CPU  {cpu.Cpu.Usage}%";
                cpuText += cpu.Cpu.HasTemp ? $"  \u00B7  {cpu.Cpu.TempC}\u00B0C" : "";
                CpuStatsText.Text = cpuText;
                CpuPowerTxt.Text = cpu.Cpu.HasPower ? $"{cpu.Cpu.PowerW:F1} W" : "-- W";

                var gpuStats = "";
                if (_isNvidia)
                {
                    var gpu = await Task.Run(() => StatsService.RefreshGpu(true));
                    if (gpu.Gpu.Available)
                    {
                        GpuStatsText.Text = $"GPU  {gpu.Gpu.Usage}%  \u00B7  {gpu.Gpu.FreqMHz} MHz  \u00B7  {gpu.Gpu.MemUsedMB}/{gpu.Gpu.MemTotalMB} MB  \u00B7  {gpu.Gpu.TempC}\u00B0C";
                        GpuPowerTxt.Text = $"{gpu.Gpu.PowerW:F1} W";
                        SysPowerTxt.Text = $"{(gpu.Gpu.PowerW + cpu.Cpu.PowerW):F1} W";
                        gpuStats = $"GPU {gpu.Gpu.Usage}% {gpu.Gpu.FreqMHz}MHz {gpu.Gpu.TempC}\u00B0C  {gpu.Gpu.PowerW:F1}W";
                    }
                    else
                    {
                        GpuStatsText.Text = "GPU: stats unavailable";
                        GpuPowerTxt.Text = "-- W";
                        SysPowerTxt.Text = $"{cpu.Cpu.PowerW:F1} W";
                        gpuStats = "GPU: unavailable";
                    }
                }
                else if (_isAmd)
                {
                    GpuStatsText.Text = "GPU: query via amd_bridge.exe";
                    GpuPowerTxt.Text = "-- W";
                    SysPowerTxt.Text = $"{cpu.Cpu.PowerW:F1} W";
                    gpuStats = "GPU: AMD";
                }

                TrayIcon.ToolTipText = $"Unclock\n{cpuText}\n{gpuStats}";
            }
            finally { timer.Start(); }
        };
        timer.Start();
    }

    void BuildTrayIcon()
    {
        var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
        if (System.IO.File.Exists(path))
            TrayIcon.Icon = new SD.Icon(path);
        else
        {
            var c = _currentTheme switch
            {
                Theme.Amd => SD.Color.FromArgb(237, 28, 36),
                Theme.Intel => SD.Color.FromArgb(0, 194, 255),
                _ => SD.Color.FromArgb(118, 185, 0),
            };
            using var bmp = new SD.Bitmap(32, 32);
            using var g = SD.Graphics.FromImage(bmp);
            g.Clear(SD.Color.Transparent);
            using var brush = new SD.SolidBrush(c);
            g.FillEllipse(brush, 2, 2, 28, 28);
            g.DrawString("U", new SD.Font("Segoe UI", 14, SD.FontStyle.Bold),
                SD.Brushes.White, 9, 6);
            TrayIcon.Icon = SD.Icon.FromHandle(bmp.GetHicon());
        }
    }

    // ─── GPU Power Mode ────────────────────────────────────

    void GpuPerfBtn_Click(object? s, RoutedEventArgs e)  => SetGpuMode(GpuPowerMode.Performance);
    void GpuMedBtn_Click(object? s, RoutedEventArgs e)   => SetGpuMode(GpuPowerMode.Medium);
    void GpuSaverBtn_Click(object? s, RoutedEventArgs e) => SetGpuMode(GpuPowerMode.PowerSaver);

    void SetGpuMode(GpuPowerMode mode, bool apply = true)
    {
        _gpuMode = mode;
        foreach (var b in _gpuBtns) SetButtonActive(b, false);
        SetButtonActive(_gpuBtns[(int)mode], true);

        if (!apply) return;

        if (_isNvidia)
        {
            var m = mode;
            _ = Task.Run(() =>
            {
                _ = m switch
                {
                    GpuPowerMode.Performance => GpuControl.NvidiaReset(),
                    GpuPowerMode.Medium => GpuControl.NvidiaMediumSave(),
                    GpuPowerMode.PowerSaver => GpuControl.NvidiaSave(),
                    _ => null
                };
            });
        }
        else if (_isAmd)
        {
            var m = mode;
            _ = Task.Run(() =>
            {
                _ = m switch
                {
                    GpuPowerMode.Performance => GpuControl.AmdReset(),
                    GpuPowerMode.Medium => GpuControl.AmdMediumSave(),
                    GpuPowerMode.PowerSaver => GpuControl.AmdSave(),
                    _ => null
                };
            });
        }
    }

    // ─── CPU Power Mode ────────────────────────────────────

    void CpuMaxSaveBtn_Click(object? s, RoutedEventArgs e)  => SetCpuMode(CpuPowerMode.MaxSave);
    void CpuMidSaveBtn_Click(object? s, RoutedEventArgs e)  => SetCpuMode(CpuPowerMode.MidSave);
    void CpuHighPerfBtn_Click(object? s, RoutedEventArgs e) => SetCpuMode(CpuPowerMode.HighPerf);
    void CpuMaxPerfBtn_Click(object? s, RoutedEventArgs e)  => SetCpuMode(CpuPowerMode.MaxPerf);

    void SetCpuMode(CpuPowerMode mode, bool apply = true)
    {
        _cpuMode = mode;
        foreach (var b in _cpuBtns) SetButtonActive(b, false);
        SetButtonActive(_cpuBtns[(int)mode], true);

        if (!apply) return;

        var m = mode;
        _ = Task.Run(() =>
        {
            _ = m switch
            {
                CpuPowerMode.MaxSave => CpuControl.CpuSetMaxSave(),
                CpuPowerMode.MidSave => CpuControl.CpuSetMidSave(),
                CpuPowerMode.HighPerf => CpuControl.CpuSetHighPerf(),
                CpuPowerMode.MaxPerf => CpuControl.CpuSetMaxPerf(),
                _ => null
            };
        });
    }

    // ─── Windows Power Plan ───────────────────────────────

    void WinHighBtn_Click(object? s, RoutedEventArgs e)  => SetWinMode(WinPowerMode.HighPerformance);
    void WinBalBtn_Click(object? s, RoutedEventArgs e)   => SetWinMode(WinPowerMode.Balanced);
    void WinSaverBtn_Click(object? s, RoutedEventArgs e) => SetWinMode(WinPowerMode.PowerSaver);

    void SetWinMode(WinPowerMode mode, bool apply = true)
    {
        _winMode = mode;
        foreach (var b in _winBtns) SetButtonActive(b, false);
        SetButtonActive(_winBtns[(int)mode], true);

        if (!apply) return;

        SetGpuMode(mode switch
        {
            WinPowerMode.HighPerformance => GpuPowerMode.Performance,
            WinPowerMode.Balanced => GpuPowerMode.Medium,
            WinPowerMode.PowerSaver => GpuPowerMode.PowerSaver,
            _ => GpuPowerMode.Performance
        }, apply: true);

        SetCpuMode(mode switch
        {
            WinPowerMode.HighPerformance => CpuPowerMode.MaxPerf,
            WinPowerMode.Balanced => CpuPowerMode.HighPerf,
            WinPowerMode.PowerSaver => CpuPowerMode.MaxSave,
            _ => CpuPowerMode.HighPerf
        }, apply: true);
    }

    // ─── Button active state + glow ───────────────────────

    void SetButtonActive(Button btn, bool active)
    {
        if (active)
        {
            btn.Background = (Brush)FindResource("AccentDim");
            btn.BorderBrush = (Brush)FindResource("BrushAccent");
            btn.Foreground = (Brush)FindResource("BrushAccent");
            btn.Effect = new DropShadowEffect { ShadowDepth = 0, BlurRadius = 10 };
            _glowBtns.Add(btn);
            _glowTimer.Start();
        }
        else
        {
            btn.Background = (Brush)FindResource("AccentDim");
            btn.BorderBrush = Brushes.Transparent;
            btn.Foreground = (Brush)FindResource("BrushTextSecondary");
            btn.Effect = null;
            _glowBtns.Remove(btn);
            if (_glowBtns.Count == 0) _glowTimer.Stop();
        }
    }

    // ─── Reset All ────────────────────────────────────────

    void BtnResetAll_Click(object? s, RoutedEventArgs e)
    {
        SetGpuMode(GpuPowerMode.Performance, apply: true);
        SetCpuMode(CpuPowerMode.HighPerf, apply: true);
    }

    // ─── Tray ─────────────────────────────────────────────

    void TrayShow_Click(object? s, RoutedEventArgs e) => ShowWindow();
    void TrayIcon_TrayLeftMouseUp(object? s, RoutedEventArgs e) => ShowWindow();
    void ShowWindow() { Show(); WindowState = WindowState.Normal; Activate(); }

    void TrayExit_Click(object? s, RoutedEventArgs e)
    {
        _forceClose = true;
        TrayIcon.Dispose();
        Application.Current.Shutdown();
    }

    // ─── Theme ────────────────────────────────────────────

    void ApplyTheme(Theme t)
    {
        _currentTheme = t;
        if (!_savedOriginals)
        {
            _origAccent = (SolidColorBrush)Application.Current.Resources["Accent"];
            _origAccentDim = (SolidColorBrush)Application.Current.Resources["AccentDim"];
            _origAccentDimHover = (SolidColorBrush)Application.Current.Resources["AccentDimHover"];
            _origAccentDimPressed = (SolidColorBrush)Application.Current.Resources["AccentDimPressed"];
            _origBorderDim = (SolidColorBrush)Application.Current.Resources["BorderDim"];
            _origBorderHover = (SolidColorBrush)Application.Current.Resources["BorderHover"];
            _origBrushAccent = (SolidColorBrush)Application.Current.Resources["BrushAccent"];
            _origAccentColor = (Color)Application.Current.Resources["AccentColor"];
            _savedOriginals = true;
        }
        if (t == Theme.Nvidia)
        {
            Application.Current.Resources["Accent"] = _origAccent;
            Application.Current.Resources["AccentDim"] = _origAccentDim;
            Application.Current.Resources["AccentDimHover"] = _origAccentDimHover;
            Application.Current.Resources["AccentDimPressed"] = _origAccentDimPressed;
            Application.Current.Resources["BorderDim"] = _origBorderDim;
            Application.Current.Resources["BorderHover"] = _origBorderHover;
            Application.Current.Resources["BrushAccent"] = _origBrushAccent;
            Application.Current.Resources["AccentColor"] = _origAccentColor;
            RefreshActiveButtons();
            return;
        }
        Application.Current.Resources["Accent"] = Application.Current.Resources[t == Theme.Amd ? "AccentAmd" : "AccentIntel"];
        Application.Current.Resources["AccentDim"] = Application.Current.Resources[t == Theme.Amd ? "AccentAmdDim" : "AccentIntelDim"];
        Application.Current.Resources["AccentDimHover"] = Application.Current.Resources[t == Theme.Amd ? "AccentAmdDimHover" : "AccentIntelDimHover"];
        Application.Current.Resources["AccentDimPressed"] = Application.Current.Resources[t == Theme.Amd ? "AccentAmdDimPressed" : "AccentIntelDimPressed"];
        Application.Current.Resources["BorderDim"] = Application.Current.Resources[t == Theme.Amd ? "BorderAmdDim" : "BorderIntelDim"];
        Application.Current.Resources["BorderHover"] = Application.Current.Resources[t == Theme.Amd ? "BorderAmdHover" : "BorderIntelHover"];
        Application.Current.Resources["BrushAccent"] = Application.Current.Resources[t == Theme.Amd ? "BrushAccentAmd" : "BrushAccentIntel"];
        Application.Current.Resources["AccentColor"] = Application.Current.Resources[t == Theme.Amd ? "AccentAmdColor" : "AccentIntelColor"];
        RefreshActiveButtons();
    }

    void RefreshActiveButtons()
    {
        foreach (var b in _gpuBtns) SetButtonActive(b, false);
        foreach (var b in _cpuBtns) SetButtonActive(b, false);
        foreach (var b in _winBtns) SetButtonActive(b, false);
        SetButtonActive(_gpuBtns[(int)_gpuMode], true);
        SetButtonActive(_cpuBtns[(int)_cpuMode], true);
        SetButtonActive(_winBtns[(int)_winMode], true);
    }

    void ThemeNvidia_Click(object? s, RoutedEventArgs e) { ApplyTheme(Theme.Nvidia); BuildTrayIcon(); }
    void ThemeAmd_Click(object? s, RoutedEventArgs e) { ApplyTheme(Theme.Amd); BuildTrayIcon(); }
    void ThemeIntel_Click(object? s, RoutedEventArgs e) { ApplyTheme(Theme.Intel); BuildTrayIcon(); }

    // ─── Window chrome ────────────────────────────────────

    void TitleBar_OnMouseDown(object? s, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    void MinBtn_Click(object? s, RoutedEventArgs e) => HideToTray();
    void CloseBtn_Click(object? s, RoutedEventArgs e) => HideToTray();

    void HideToTray() { Hide(); TrayIcon.Visibility = Visibility.Visible; }

    void OnClosing(object? s, CancelEventArgs e)
    {
        if (!_forceClose) { e.Cancel = true; HideToTray(); }
    }
}

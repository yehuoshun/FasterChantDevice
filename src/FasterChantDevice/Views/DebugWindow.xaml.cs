using System;
using System.Diagnostics;
using System.Windows;
using FasterChantDevice.Services;

namespace FasterChantDevice.Views;

/// <summary>
/// Diagnostic debug window showing real-time OCR readings, event triggers,
/// game window status, and scrollable log.
/// </summary>
public partial class DebugWindow : Window
{
    private readonly DebugService _debug;
    private readonly System.Windows.Threading.DispatcherTimer _refreshTimer;

    public DebugWindow(DebugService debug)
    {
        InitializeComponent();
        _debug = debug;
        DataContext = debug;

        // Auto-refresh status every 1s
        _refreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += (_, _) => _debug.Refresh();
        _refreshTimer.Start();

        Closed += (_, _) => _refreshTimer.Stop();
    }

    private void ScreenshotKda_Click(object sender, RoutedEventArgs e)
    {
        var path = _debug.SaveOcrScreenshot("kda");
        if (path != null)
            DebugLogger.Info($"KDA 截图已保存: {path}");
        else
            DebugLogger.Warning("KDA 截图失败（可能游戏窗口未找到）");
    }

    private void ScreenshotBroadcast_Click(object sender, RoutedEventArgs e)
    {
        var path = _debug.SaveOcrScreenshot("broadcast");
        if (path != null)
            DebugLogger.Info($"播报截图已保存: {path}");
        else
            DebugLogger.Warning("播报截图失败（可能游戏窗口未找到）");
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        _debug.LogEntries.Clear();
    }

    private void OpenLogDir_Click(object sender, RoutedEventArgs e)
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FasterChantDevice");
        Process.Start("explorer.exe", dir);
    }
}

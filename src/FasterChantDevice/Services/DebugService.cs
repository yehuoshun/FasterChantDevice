using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using FasterChantDevice.Models;

namespace FasterChantDevice.Services;

/// <summary>
/// Gathers runtime diagnostic state for the debug window.
/// Subscribes to DebugLogger.OnLog to keep a rolling log buffer.
/// </summary>
public class DebugService : INotifyPropertyChanged
{
    private readonly OcrEngineService _ocr;
    private readonly GameEventService? _gameEvent;
    private readonly KeyboardHookService? _hook;
    private readonly OverlayService? _overlay;
    private readonly SchemeManager _schemes;

    // Rolling log buffer
    public ObservableCollection<LogEntry> LogEntries { get; } = new();
    private const int MaxLogEntries = 200;

    // State tracking
    private string _gameWindowStatus = "未检测";
    public string GameWindowStatus
    {
        get => _gameWindowStatus;
        set { _gameWindowStatus = value; OnPropertyChanged(); }
    }

    private string _hookStatus = "未知";
    public string HookStatus
    {
        get => _hookStatus;
        set { _hookStatus = value; OnPropertyChanged(); }
    }

    private string _overlayStatus = "隐藏";
    public string OverlayStatus
    {
        get => _overlayStatus;
        set { _overlayStatus = value; OnPropertyChanged(); }
    }

    private string _kdaRaw = "-";
    public string KdaRaw
    {
        get => _kdaRaw;
        set { _kdaRaw = value; OnPropertyChanged(); }
    }

    private string _kdaParsed = "-";
    public string KdaParsed
    {
        get => _kdaParsed;
        set { _kdaParsed = value; OnPropertyChanged(); }
    }

    private string _broadcastText = "-";
    public string BroadcastText
    {
        get => _broadcastText;
        set { _broadcastText = value; OnPropertyChanged(); }
    }

    private string _lastEvent = "-";
    public string LastEvent
    {
        get => _lastEvent;
        set { _lastEvent = value; OnPropertyChanged(); }
    }

    private string _pixelChange = "-";
    public string PixelChange
    {
        get => _pixelChange;
        set { _pixelChange = value; OnPropertyChanged(); }
    }

    private string _ocrFps = "0";
    public string OcrFps
    {
        get => _ocrFps;
        set { _ocrFps = value; OnPropertyChanged(); }
    }

    public DebugService(
        OcrEngineService ocr,
        SchemeManager schemes,
        GameEventService? gameEvent = null,
        KeyboardHookService? hook = null,
        OverlayService? overlay = null)
    {
        _ocr = ocr;
        _schemes = schemes;
        _gameEvent = gameEvent;
        _hook = hook;
        _overlay = overlay;

        DebugLogger.OnLog += OnDebugLog;
    }

    private void OnDebugLog(LogEntry entry)
    {
        // Thread-safe add to observable collection
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            LogEntries.Add(entry);
            while (LogEntries.Count > MaxLogEntries)
                LogEntries.RemoveAt(0);
        });
    }

    /// <summary>
    /// Refresh all diagnostic state. Called by debug window timer.
    /// </summary>
    public void Refresh()
    {
        if (!DebugLogger.IsEnabled) return;

        try
        {
            // Game window
            var hwnd = _ocr.FindGameWindow();
            OcrEngineService.RECT rect = default;
            bool hasRect = false;
            if (hwnd != IntPtr.Zero)
            {
                OcrEngineService.GetWindowRectStatic(hwnd, out rect);
                hasRect = true;
            }
            var isFg = _ocr.IsGameForeground();

            GameWindowStatus = hwnd != IntPtr.Zero
                ? $"HWND=0x{hwnd:X8} FG={isFg} Rect=({rect.Left},{rect.Top})-{rect.Width}x{rect.Height}"
                : "未找到游戏窗口";

            // Hook status
            HookStatus = _hook != null ? "已启动" : "未初始化";

            // Overlay status
            OverlayStatus = _overlay?.IsVisible == true ? "可见" : "隐藏";
        }
        catch (Exception ex)
        {
            DebugLogger.Error(ex, "DebugService.Refresh");
        }
    }

    /// <summary>
    /// Update KDA readings from OCR result.
    /// </summary>
    public void UpdateKda(string rawOcr, int kills, int deaths, int assists)
    {
        KdaRaw = string.IsNullOrWhiteSpace(rawOcr) ? "(空)" : rawOcr;
        KdaParsed = $"K={kills} D={deaths} A={assists}";
    }

    /// <summary>
    /// Update broadcast OCR text.
    /// </summary>
    public void UpdateBroadcast(string text)
    {
        BroadcastText = string.IsNullOrWhiteSpace(text) ? "(空)" : text;
    }

    /// <summary>
    /// Update last event trigger.
    /// </summary>
    public void UpdateLastEvent(string eventType, string detail = "")
    {
        LastEvent = $"[{DateTime.Now:HH:mm:ss}] {eventType} {detail}";
    }

    /// <summary>
    /// Update pixel change detection status.
    /// </summary>
    public void UpdatePixelChange(bool changed, double pct)
    {
        PixelChange = changed ? $"变化 {pct:F1}%" : $"无变化 ({pct:F1}%)";
    }

    /// <summary>
    /// Save OCR screenshot to disk for debugging.
    /// </summary>
    public string? SaveOcrScreenshot(string regionName)
    {
        try
        {
            var hwnd = _ocr.FindGameWindow();
            if (hwnd == IntPtr.Zero) return null;

            OcrEngineService.GetWindowRectStatic(hwnd, out var windowRect);
            OcrRegion region;
            if (regionName == "kda")
                region = _schemes.Settings.KdaRegion;
            else if (regionName == "broadcast")
                region = _schemes.Settings.BroadcastRegion;
            else
                return null;

            int x = windowRect.Left + (int)(windowRect.Width * region.XRatio);
            int y = windowRect.Top + (int)(windowRect.Height * region.YRatio);
            int w = (int)(windowRect.Width * region.WRatio);
            int h = (int)(windowRect.Height * region.HRatio);

            if (w <= 0 || h <= 0) return null;

            using var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
                g.CopyFromScreen(x, y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FasterChantDevice", "screenshots");
            Directory.CreateDirectory(dir);

            var filename = $"{regionName}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var path = Path.Combine(dir, filename);
            bitmap.Save(path, ImageFormat.Png);

            DebugLogger.Info($"Screenshot saved: {path}", "Debug");
            return path;
        }
        catch (Exception ex)
        {
            DebugLogger.Error(ex, "SaveOcrScreenshot");
            return null;
        }
    }

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    #endregion
}

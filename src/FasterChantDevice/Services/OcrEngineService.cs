using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using FasterChantDevice.Models;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace FasterChantDevice.Services;

/// <summary>
/// OCR engine for reading 300 Heroes in-game HUD elements.
/// Uses Windows.Media.Ocr (built-in Windows 10+, no external dependencies).
/// </summary>
public class OcrEngineService : IDisposable
{
    private readonly Models.AppSettings _settings;
    private OcrEngine? _engine;
    private IntPtr _cachedGameHwnd;
    private DateTime _lastHwndCheck = DateTime.MinValue;
    private static readonly TimeSpan HwndCacheTtl = TimeSpan.FromSeconds(2);

    public OcrEngineService(Models.AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Initialize OCR engine with Chinese language support.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Try Chinese (simplified) first, fall back to user profile languages
        var chsEngine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("zh-Hans"));
        if (chsEngine != null)
        {
            _engine = chsEngine;
        }
        else
        {
            _engine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (_engine != null)
            {
                Debug.WriteLine("[OCR] Chinese (zh-Hans) OCR not available, " +
                    "falling back to system language. Chinese character recognition may be degraded.");
            }
            else
            {
                Debug.WriteLine("[OCR] No OCR engine available. Game event detection disabled.");
            }
        }
    }

    /// <summary>
    /// Get the game window handle. Cached for 2s to avoid repeated enumeration.
    /// </summary>
    public IntPtr FindGameWindow()
    {
        // Return cached handle if still valid
        if (_cachedGameHwnd != IntPtr.Zero && IsWindow(_cachedGameHwnd) &&
            DateTime.UtcNow - _lastHwndCheck < HwndCacheTtl)
            return _cachedGameHwnd;

        _lastHwndCheck = DateTime.UtcNow;

        // Enumerate windows looking for title containing "300英雄"
        var foundHwnd = IntPtr.Zero;
        EnumWindows((h, _) =>
        {
            var title = new char[256];
            GetWindowText(h, title, title.Length);
            var t = new string(title).TrimEnd('\0');
            if (t.Contains("300英雄") && !t.Contains("FasterChant") && IsWindowVisible(h))
            {
                GetWindowRect(h, out var rect);
                if (rect.Width > 800 && rect.Height > 600)
                    foundHwnd = h;
            }
            return foundHwnd == IntPtr.Zero;
        }, IntPtr.Zero);

        _cachedGameHwnd = foundHwnd;
        return foundHwnd;
    }

    public bool IsGameForeground()
    {
        var fg = GetForegroundWindow();
        var gameHwnd = FindGameWindow();
        return fg != IntPtr.Zero && fg == gameHwnd;
    }

    /// <summary>
    /// Read K/D/A counter from the top-right HUD area.
    /// Returns (kills, deaths, assists). Returns (-1,-1,-1) on failure.
    /// </summary>
    public async Task<(int kills, int deaths, int assists)> ReadKDACounter()
    {
        try
        {
            var hwnd = FindGameWindow();
            if (hwnd == IntPtr.Zero) return (-1, -1, -1);

            GetWindowRect(hwnd, out var windowRect);

            // Calculate KDA region based on configured ratios
            var region = _settings.KdaRegion;
            int x = windowRect.Left + (int)(windowRect.Width * region.XRatio);
            int y = windowRect.Top + (int)(windowRect.Height * region.YRatio);
            int w = (int)(windowRect.Width * region.WRatio);
            int h = (int)(windowRect.Height * region.HRatio);

            if (w <= 0 || h <= 0) return (-1, -1, -1);

            // Capture screen region
            using var bitmap = CaptureRegion(x, y, w, h);
            if (bitmap == null) return (-1, -1, -1);

            // Preprocess: convert to grayscale, increase contrast for better OCR
            using var processed = PreprocessForOcr(bitmap);

            // Run OCR
            var text = await RecognizeBitmapAsync(processed);
            Debug.WriteLine($"[KDA OCR] raw: '{text}'");

            return ParseKDAText(text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[KDA OCR] error: {ex.Message}");
            return (-1, -1, -1);
        }
    }

    /// <summary>
    /// Read kill broadcast area for event type confirmation.
    /// Returns text found in the broadcast region.
    /// </summary>
    public async Task<string> ReadBroadcastText()
    {
        try
        {
            var hwnd = FindGameWindow();
            if (hwnd == IntPtr.Zero) return "";

            GetWindowRect(hwnd, out var windowRect);
            var region = _settings.BroadcastRegion;
            int x = windowRect.Left + (int)(windowRect.Width * region.XRatio);
            int y = windowRect.Top + (int)(windowRect.Height * region.YRatio);
            int w = (int)(windowRect.Width * region.WRatio);
            int h = (int)(windowRect.Height * region.HRatio);

            if (w <= 0 || h <= 0) return "";

            using var bitmap = CaptureRegion(x, y, w, h);
            if (bitmap == null) return "";

            using var processed = PreprocessForOcr(bitmap);
            var text = await RecognizeBitmapAsync(processed);
            Debug.WriteLine($"[Broadcast OCR] raw: '{text}'");
            return text;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Broadcast OCR] error: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// Detect if there's been a significant pixel change in the broadcast area.
    /// Used as a fallback when text OCR fails.
    /// </summary>
    public bool HasPixelChange(byte[] previousFrame, int thresholdPercent = 5)
    {
        try
        {
            var hwnd = FindGameWindow();
            if (hwnd == IntPtr.Zero) return false;

            GetWindowRect(hwnd, out var windowRect);
            var region = _settings.BroadcastRegion;
            int x = windowRect.Left + (int)(windowRect.Width * region.XRatio);
            int y = windowRect.Top + (int)(windowRect.Height * region.YRatio);
            int w = (int)(windowRect.Width * region.WRatio);
            int h = (int)(windowRect.Height * region.HRatio);

            using var bitmap = CaptureRegion(x, y, w, h);
            if (bitmap == null) return false;

            // Convert to grayscale bytes for comparison
            var currentFrame = ToGrayscaleBytes(bitmap);
            if (previousFrame.Length != currentFrame.Length) return true;

            int changedPixels = 0;
            for (int i = 0; i < currentFrame.Length; i++)
            {
                if (Math.Abs(currentFrame[i] - previousFrame[i]) > 30)
                    changedPixels++;
            }

            double pct = (double)changedPixels / currentFrame.Length * 100;
            return pct >= thresholdPercent;
        }
        catch
        {
            return false;
        }
    }

    public byte[] CaptureBroadcastFrame()
    {
        var hwnd = FindGameWindow();
        if (hwnd == IntPtr.Zero) return Array.Empty<byte>();

        GetWindowRect(hwnd, out var windowRect);
        var region = _settings.BroadcastRegion;
        int x = windowRect.Left + (int)(windowRect.Width * region.XRatio);
        int y = windowRect.Top + (int)(windowRect.Height * region.YRatio);
        int w = (int)(windowRect.Width * region.WRatio);
        int h = (int)(windowRect.Height * region.HRatio);

        using var bitmap = CaptureRegion(x, y, w, h);
        return bitmap != null ? ToGrayscaleBytes(bitmap) : Array.Empty<byte>();
    }

    #region Internal OCR

    private async Task<string> RecognizeBitmapAsync(Bitmap bitmap)
    {
        if (_engine == null) return "";

        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;

        var randomAccessStream = stream.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync();
        // Convert to BGRA8 if needed
        if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
            softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
        {
            softwareBitmap = SoftwareBitmap.Convert(softwareBitmap,
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        var result = await _engine.RecognizeAsync(softwareBitmap);
        return result.Text;
    }

    private static Bitmap? CaptureRegion(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0) return null;
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    /// <summary>
    /// Preprocess bitmap for better OCR: grayscale + contrast boost.
    /// </summary>
    private static Bitmap PreprocessForOcr(Bitmap source)
    {
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(result);

        // Increase contrast
        using var attr = new ImageAttributes();
        var matrix = new ColorMatrix(new float[][]
        {
            new float[] { 1.5f, 0f, 0f, 0f, 0f },
            new float[] { 0f, 1.5f, 0f, 0f, 0f },
            new float[] { 0f, 0f, 1.5f, 0f, 0f },
            new float[] { 0f, 0f, 0f, 1f, 0f },
            new float[] { -0.1f, -0.1f, -0.1f, 0f, 1f }
        });
        attr.SetColorMatrix(matrix);
        g.DrawImage(source,
            new Rectangle(0, 0, source.Width, source.Height),
            0, 0, source.Width, source.Height,
            GraphicsUnit.Pixel, attr);

        return result;
    }

    private static byte[] ToGrayscaleBytes(Bitmap bitmap)
    {
        var bytes = new byte[bitmap.Width * bitmap.Height];
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                bytes[y * bitmap.Width + x] = (byte)((pixel.R + pixel.G + pixel.B) / 3);
            }
        }
        return bytes;
    }

    /// <summary>
    /// Parse KDA text like "5/2/8" or "击杀 5 死亡 2 助攻 8".
    /// Handles various OCR output formats.
    /// </summary>
    private static (int kills, int deaths, int assists) ParseKDAText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (-1, -1, -1);

        // Try format: "5/2/8" or "5 / 2 / 8"
        var slashParts = text.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (slashParts.Length == 3 &&
            int.TryParse(slashParts[0].Trim(), out var k1) &&
            int.TryParse(slashParts[1].Trim(), out var d1) &&
            int.TryParse(slashParts[2].Trim(), out var a1))
        {
            return (k1, d1, a1);
        }

        // Try to extract all numbers from text
        var numbers = new List<int>();
        foreach (var part in text.Split(new[] { ' ', '\n', '\r', '\t', '/', '|', ':', ';' },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part.Trim(), out var n) && n >= 0 && n <= 99)
                numbers.Add(n);
        }

        // KDA is typically 3 consecutive numbers
        if (numbers.Count >= 3)
            return (numbers[0], numbers[1], numbers[2]);

        return (-1, -1, -1);
    }

    public void Dispose()
    {
        // nothing to dispose
    }

    #endregion

    #region P/Invoke

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, char[] text, int count);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    #endregion
}

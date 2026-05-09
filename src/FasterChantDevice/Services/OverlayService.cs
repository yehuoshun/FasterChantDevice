using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using FasterChantDevice.Models;

namespace FasterChantDevice.Services;

/// <summary>
/// Manages the semi-transparent overlay window that appears on top of the game.
/// Uses WS_EX_TRANSPARENT for click-through (doesn't steal focus from game).
/// </summary>
public class OverlayService : IDisposable
{
    private OverlayWindow? _window;
    private readonly SchemeManager _schemeManager;
    private readonly InputSimulationService _input;
    private CancellationTokenSource? _burstCts;

    // Current state
    private bool _visible;
    private int _currentPanelIndex = -1; // -1 = main panel, 0-9 = secondary panel
    private int _currentGroupIndex = -1;

    public bool IsVisible => _visible;

    public OverlayService(SchemeManager schemeManager, InputSimulationService input)
    {
        _schemeManager = schemeManager;
        _input = input;
    }

    public void Show()
    {
        if (_visible) return;

        if (_window == null)
        {
            _window = new OverlayWindow();
            _window.KeyPressed += OnOverlayKeyPressed;
        }

        PositionOverWindow(GetGameWindowHandle());
        _window.Show();
        _visible = true;
        _currentPanelIndex = -1; // start at main panel

        UpdateOverlayContent();
    }

    public void Hide()
    {
        if (!_visible) return;
        _window?.Hide();
        _visible = false;
        _currentPanelIndex = -1;
        _burstCts?.Cancel();
    }

    public void Toggle()
    {
        if (_visible) Hide();
        else Show();
    }

    private void OnOverlayKeyPressed(int numberKey)
    {
        var hero = _schemeManager.Heroes.FirstOrDefault();
        if (hero == null) return;

        if (_currentPanelIndex == -1)
        {
            // Main panel: number selects group → show its lines (secondary view)
            if (numberKey >= 0 && numberKey < hero.Panels.Count)
            {
                _currentGroupIndex = numberKey;
                _currentPanelIndex = numberKey;
                var panel = hero.Panels[numberKey];
                if (panel.Lines.Count > 0)
                {
                    UpdateSecondaryContent(panel);
                }
            }
        }
        else
        {
            // Secondary panel: number selects which line to send
            if (_currentGroupIndex >= 0 && _currentGroupIndex < hero.Panels.Count)
            {
                var panel = hero.Panels[_currentGroupIndex];
                if (numberKey >= 0 && numberKey < panel.Lines.Count)
                {
                    if (_schemeManager.Settings.BurstMode)
                    {
                        var lines = _schemeManager.PickLines(panel.Lines);
                        _burstCts?.Cancel();
                        _burstCts = new CancellationTokenSource();
                        _ = Task.Run(() => _input.SendLinesSequentially(lines,
                            _schemeManager.Settings.BurstIntervalMs, _burstCts.Token));
                    }
                    else
                    {
                        _input.SendText(panel.Lines[numberKey]);
                        Hide();
                        return;
                    }
                }
            }
        }
    }



    private void UpdateOverlayContent()
    {
        if (_window == null) return;

        var hero = _schemeManager.Heroes.FirstOrDefault();
        if (hero == null)
        {
            _window.SetContent(new[] { "无英雄方案" }, Array.Empty<string>());
            return;
        }

        var names = hero.Panels.Select(p => p.Name).ToArray();
        _window.SetContent(names, Array.Empty<string>());
    }

    private void UpdateSecondaryContent(PhrasePanel panel)
    {
        if (_window == null) return;
        var lines = panel.Lines.Select((l, i) => $"{i}. {l}").ToArray();
        _window.SetContent(lines, Array.Empty<string>());
    }

    private static IntPtr GetGameWindowHandle()
    {
        // Find 300 Heroes window by class/title
        var hwnd = FindWindow(null, "300英雄");
        if (hwnd == IntPtr.Zero)
            hwnd = GetForegroundWindow();
        return hwnd;
    }

    private void PositionOverWindow(IntPtr targetHwnd)
    {
        if (targetHwnd == IntPtr.Zero || _window == null) return;

        GetWindowRect(targetHwnd, out var rect);
        var helper = new WindowInteropHelper(_window);

        // Convert physical pixels to WPF device-independent units for high-DPI
        var dpi = GetDpiForWindow(targetHwnd);
        if (dpi == 0) dpi = 96;
        var scale = dpi / 96.0;

        _window.Left = rect.Left / scale + 20;
        _window.Top = rect.Top / scale + 100;
        _window.Height = Math.Max(100, (rect.Bottom - rect.Top) / scale - 200);
    }

    public void Dispose()
    {
        _burstCts?.Cancel();
        _window?.Close();
    }

    #region P/Invoke
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
    #endregion
}

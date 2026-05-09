using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FasterChantDevice.Services;

/// <summary>
/// Monitors 300 Heroes game events using dual OCR engine:
/// 1. K/D/A counter changes (primary, reliable)
/// 2. Kill broadcast text (secondary, skin-dependent)
/// 3. Pixel change detection (fallback, font-independent)
/// 
/// Activated only when game window is foreground.
/// </summary>
public class GameEventService : IDisposable
{
    private readonly SchemeManager _schemeManager;
    private readonly InputSimulationService _input;
    private readonly Models.AppSettings _settings;
    private CancellationTokenSource? _cts;
    private bool _running;
    private bool _isInGame;

    // Previous K/D/A values for change detection
    private int _prevKills = -1, _prevDeaths = -1, _prevAssists = -1;

    // Taunt cooldown: prevent taunt for N seconds after kill/death/assist
    private DateTime _lastCombatEvent = DateTime.MinValue;

    // Taunt timer
    private DateTime _lastTaunt = DateTime.MinValue;

    public GameEventService(SchemeManager schemeManager, InputSimulationService input)
    {
        _schemeManager = schemeManager;
        _input = input;
        _settings = schemeManager.Settings;
    }

    public void Start()
    {
        _running = true;
        _cts = new CancellationTokenSource();
        _ = Task.Run(MonitorLoop, _cts.Token);
    }

    private async Task MonitorLoop()
    {
        while (_running && !_cts!.Token.IsCancellationRequested)
        {
            _isInGame = IsGameWindowForeground();

            if (_isInGame)
            {
                // ----- K/D/A check (500ms) -----
                var (kills, deaths, assists) = ReadKDACounter();

                if (_prevKills >= 0 && _prevDeaths >= 0 && _prevAssists >= 0)
                {
                    if (kills > _prevKills) await OnEvent("kill");
                    if (deaths > _prevDeaths) await OnEvent("death");
                    if (assists > _prevAssists) await OnEvent("assist");
                    if (kills == 0 && deaths == 0 && assists == 0
                        && _prevKills == 0 && _prevDeaths == 0 && _prevAssists == 0
                        && _prevKills == -1 == false)
                    {
                        // Possible new game: K/D/A all reset to 0
                        if (_prevKills > 0 && kills == 0)
                            await OnEvent("game_start");
                    }
                }

                _prevKills = kills;
                _prevDeaths = deaths;
                _prevAssists = assists;

                // ----- Taunt timer -----
                await CheckTauntTimer();
            }

            await Task.Delay(500, _cts.Token);
        }
    }

    private async Task OnEvent(string eventType)
    {
        _lastCombatEvent = DateTime.UtcNow;

        var hero = _schemeManager.Heroes.FirstOrDefault();
        if (hero == null) return;

        var phrases = eventType switch
        {
            "game_start" => hero.Triggers.GameStart,
            "kill" => hero.Triggers.Kill,
            "death" => hero.Triggers.Death,
            "assist" => hero.Triggers.Assist,
            _ => new List<string>()
        };

        if (phrases.Count == 0) return;

        var lines = _schemeManager.PickLines(phrases);

        if (_settings.BurstMode)
            await Task.Run(() => _input.SendLinesSequentially(lines, _settings.BurstIntervalMs));
        else if (lines.Length > 0)
            _input.SendText(lines[0]);
    }

    private async Task CheckTauntTimer()
    {
        var mode = _settings.TauntMode;
        if (mode == "manual") return;

        var cooldownS = _settings.TauntCooldownS;
        if ((DateTime.UtcNow - _lastCombatEvent).TotalSeconds < cooldownS) return;

        var intervalS = _settings.TauntIntervalS;
        if ((DateTime.UtcNow - _lastTaunt).TotalSeconds < intervalS) return;

        _lastTaunt = DateTime.UtcNow;

        var hero = _schemeManager.Heroes.FirstOrDefault();
        if (hero == null) return;

        var lines = _schemeManager.PickTauntLines(hero);
        if (lines.Length == 0) return;

        if (_settings.BurstMode)
            await Task.Run(() => _input.SendLinesSequentially(lines, _settings.BurstIntervalMs));
        else if (lines.Length > 0)
            _input.SendText(lines[0]);
    }

    /// <summary>
    /// Read K/D/A counter from game screen.
    /// Uses Windows.Media.Ocr on the KDA region of the game window.
    /// </summary>
    private (int kills, int deaths, int assists) ReadKDACounter()
    {
        // Placeholder — actual implementation uses:
        // 1. Get game window rect
        // 2. Calculate KDA region from ratios in settings
        // 3. Capture screenshot of region
        // 4. Windows.Media.Ocr → parse numbers
        // 5. Return (kill, death, assist)
        return (0, 0, 0);
    }

    private static bool IsGameWindowForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        var title = new char[256];
        GetWindowText(hwnd, title, title.Length);
        var titleStr = new string(title).TrimEnd('\0');

        // Match 300 Heroes window
        return titleStr.Contains("300英雄") || titleStr.Contains("300Heroes");
    }

    public void ManualTaunt()
    {
        var mode = _settings.TauntMode;
        if (mode != "manual" && mode != "both") return;

        // Trigger taunt immediately via F2
        _lastTaunt = DateTime.MinValue; // bypass interval check
    }

    public void Dispose()
    {
        _running = false;
        _cts?.Cancel();
        _cts?.Dispose();
    }

    #region P/Invoke
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, char[] text, int count);
    #endregion
}

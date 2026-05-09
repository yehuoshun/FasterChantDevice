using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FasterChantDevice.Services;

/// <summary>
/// Monitors 300 Heroes game events via OCR and triggers auto-send.
/// 
/// OCR strategy:
///   1. K/D/A counter (primary, top-right HUD, always visible) — detect change
///   2. Kill broadcast (secondary, center-top popup, skin-dependent) — confirm type
///   3. Pixel change (fallback) — detect "something appeared" with zero font dependency
/// 
/// Events: game_start, kill, death, assist, taunt (manual + timer)
/// Taunt has cooldown after any combat event to avoid awkward timing.
/// </summary>
public class GameEventService : IDisposable
{
    private readonly SchemeManager _schemeManager;
    private readonly InputSimulationService _input;
    private readonly OcrEngineService _ocr;
    private readonly Models.AppSettings _settings;
    private CancellationTokenSource? _cts;
    private volatile bool _running;

    // K/D/A tracking for change detection
    private int _prevKills = -1, _prevDeaths = -1, _prevAssists = -1;

    // Broadcast pixel change detection
    private byte[] _prevBroadcastFrame = Array.Empty<byte>();

    // Game start detection: K/D/A all = 0 means new game
    private bool _gameStarted;

    // Combat timestamps for taunt cooldown
    private DateTime _lastCombatEvent = DateTime.MinValue;
    private DateTime _lastTaunt = DateTime.MinValue;

    public GameEventService(SchemeManager schemeManager, InputSimulationService input)
    {
        _schemeManager = schemeManager;
        _input = input;
        _settings = schemeManager.Settings;
        _ocr = new OcrEngineService(_settings);
    }

    public async Task StartAsync()
    {
        await _ocr.InitializeAsync();
        _running = true;
        _cts = new CancellationTokenSource();
        _ = Task.Run(MonitorLoop, _cts.Token);
    }

    public void Start()
    {
        _ = StartAsync(); // fire-and-forget for backwards compat
    }

    private async Task MonitorLoop()
    {
        while (_running && !_cts!.Token.IsCancellationRequested)
        {
            try
            {
                if (!_ocr.IsGameForeground())
                {
                    // Game not active — reset game state
                    _gameStarted = false;
                    _prevKills = _prevDeaths = _prevAssists = -1;
                    await Task.Delay(1000, _cts.Token);
                    continue;
                }

                // ——— K/D/A check (every 500ms) ———
                var (kills, deaths, assists) = await _ocr.ReadKDACounter();

                // Detect new game: K/D/A reset from non-zero to 0/0/0
                if (kills == 0 && deaths == 0 && assists == 0 &&
                    (_prevKills > 0 || _prevDeaths > 0 || _prevAssists > 0) &&
                    !_gameStarted)
                {
                    _gameStarted = true;
                    Debug.WriteLine("[GameEvent] New game detected");
                    await TriggerEvent("game_start");
                }

                // Detect individual changes
                if (_prevKills >= 0 && _prevDeaths >= 0 && _prevAssists >= 0)
                {
                    if (kills > _prevKills)
                    {
                        Debug.WriteLine($"[GameEvent] Kill detected ({_prevKills} → {kills})");

                        // Try to confirm via broadcast OCR
                        var broadcast = await _ocr.ReadBroadcastText();
                        var isDeath = broadcast.Contains("你被") || broadcast.Contains("击杀") && broadcast.Contains("你");

                        await TriggerEvent("kill");
                    }
                    if (deaths > _prevDeaths)
                    {
                        Debug.WriteLine($"[GameEvent] Death detected ({_prevDeaths} → {deaths})");
                        await TriggerEvent("death");
                    }
                    if (assists > _prevAssists)
                    {
                        Debug.WriteLine($"[GameEvent] Assist detected ({_prevAssists} → {assists})");
                        await TriggerEvent("assist");
                    }
                }

                // Store current frame for pixel change comparison
                var newFrame = _ocr.CaptureBroadcastFrame();
                if (_prevBroadcastFrame.Length > 0 && newFrame.Length > 0)
                {
                    // If we missed a KDA change but broadcast changed, try to catch it
                    if (_ocr.HasPixelChange(_prevBroadcastFrame) &&
                        kills == _prevKills && deaths == _prevDeaths && assists == _prevAssists)
                    {
                        // Pixel changed but KDA didn't — could be a non-lethal event, ignore
                    }
                }
                _prevBroadcastFrame = newFrame;

                _prevKills = kills;
                _prevDeaths = deaths;
                _prevAssists = assists;

                // ——— Taunt timer ———
                await CheckTauntTimer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GameEvent] monitor error: {ex.Message}");
            }

            await Task.Delay(500, _cts.Token);
        }
    }

    /// <summary>
    /// Trigger a game event: pick matching phrases from current hero and send.
    /// </summary>
    private async Task TriggerEvent(string eventType)
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
        if (lines.Length == 0) return;

        if (_settings.BurstMode)
            await Task.Run(() => _input.SendLinesSequentially(lines, _settings.BurstIntervalMs));
        else
            _input.SendText(lines[0]);
    }

    /// <summary>
    /// Check if it's time to auto-taunt (timer mode).
    /// Suppressed during cooldown after combat events.
    /// </summary>
    private async Task CheckTauntTimer()
    {
        var mode = _settings.TauntMode;
        if (mode != "timer" && mode != "both") return;

        var cooldown = _settings.TauntCooldownS;
        if ((DateTime.UtcNow - _lastCombatEvent).TotalSeconds < cooldown) return;

        var interval = _settings.TauntIntervalS;
        if ((DateTime.UtcNow - _lastTaunt).TotalSeconds < interval) return;

        await TriggerTaunt();
    }

    /// <summary>
    /// Manual taunt via F2 hotkey.
    /// </summary>
    public async Task TriggerTaunt()
    {
        var mode = _settings.TauntMode;
        if (mode != "manual" && mode != "both" && mode != "timer") return;

        _lastTaunt = DateTime.UtcNow;

        var hero = _schemeManager.Heroes.FirstOrDefault();
        if (hero == null) return;

        var lines = _schemeManager.PickTauntLines(hero);
        if (lines.Length == 0) return;

        if (_settings.BurstMode)
            await Task.Run(() => _input.SendLinesSequentially(lines, _settings.BurstIntervalMs));
        else
            _input.SendText(lines[0]);
    }

    public void ManualTaunt()
    {
        _ = TriggerTaunt();
    }

    public void Dispose()
    {
        _running = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _ocr.Dispose();
    }
}

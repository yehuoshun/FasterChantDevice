using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FasterChantDevice.Services;

namespace FasterChantDevice;

public partial class App : Application
{
    private KeyboardHookService? _keyboardHook;
    private SchemeManager? _schemeManager;
    private InputSimulationService? _inputSim;
    private OverlayService? _overlay;
    private GameEventService? _gameEvent;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private DebugService? _debug;
    private Views.DebugWindow? _debugWindow;
    private bool _ctrlPressed;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FasterChantDevice");

        _schemeManager = new SchemeManager(dataDir);
        _schemeManager.LoadAll();

        // Debug mode: enable file logging
        if (_schemeManager.Settings.DebugMode)
        {
            DebugLogger.Enable(dataDir, _schemeManager.Settings.DebugLogLevel);
            DebugLogger.Info($"Debug mode enabled (level={_schemeManager.Settings.DebugLogLevel})");
            DebugLogger.Info($"Data dir: {dataDir}");
            DebugLogger.Info($"GameWindowClass: {_schemeManager.Settings.GameWindowClass}");
            DebugLogger.Info($"KDA region: x={_schemeManager.Settings.KdaRegion.XRatio:F2} " +
                $"y={_schemeManager.Settings.KdaRegion.YRatio:F2} " +
                $"w={_schemeManager.Settings.KdaRegion.WRatio:F2} " +
                $"h={_schemeManager.Settings.KdaRegion.HRatio:F2}");
        }

        var ocr = new OcrEngineService(_schemeManager.Settings);

        _inputSim = new InputSimulationService();
        _overlay = new OverlayService(_schemeManager, _inputSim);

        _debug = new DebugService(ocr, _schemeManager, null, null, _overlay);
        _gameEvent = new GameEventService(_schemeManager, _inputSim, _debug);

        // Start keyboard hook
        _keyboardHook = new KeyboardHookService();
        _keyboardHook.KeyDown += OnGlobalKeyDown;
        _keyboardHook.KeyUp += OnGlobalKeyUp;
        _keyboardHook.Start();

        // Update debug service refs after hook is started
        var debugField = typeof(DebugService).GetField("_hook",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        debugField?.SetValue(_debug, _keyboardHook);
        var gameEventField = typeof(DebugService).GetField("_gameEvent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        gameEventField?.SetValue(_debug, _gameEvent);

        // Start game event monitoring
        _gameEvent.Start();

        if (_schemeManager.Settings.DebugMode)
            DebugLogger.Info("Application startup complete, game event monitor started");

        // Setup tray icon
        SetupTray();
    }

    private void OnGlobalKeyDown(Key key)
    {
        // Debug hotkey: Ctrl+Shift+D
        if (key == Key.LeftCtrl || key == Key.RightCtrl)
        {
            _ctrlPressed = true;
        }
        if (key == Key.D && _ctrlPressed && (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
        {
            Dispatcher.Invoke(ToggleDebugWindow);
            return;
        }

        var triggerKey = ParseKey(_schemeManager!.Settings.TriggerKey);
        var tauntKey = ParseKey(_schemeManager.Settings.TauntKey);

        // Configured trigger key → toggle overlay
        if (key == triggerKey)
        {
            Dispatcher.Invoke(() => _overlay?.Toggle());
        }
        // Configured taunt key → manual taunt (cooldown enforced inside GameEventService)
        else if (key == tauntKey)
        {
            Dispatcher.Invoke(() => _gameEvent?.ManualTaunt());
        }
        // Escape → close overlay when visible
        else if (key == Key.Escape && _overlay?.IsVisible == true)
        {
            Dispatcher.Invoke(() => _overlay?.Hide());
        }
        // Number keys 0-9 → route to overlay when visible
        else if (_overlay?.IsVisible == true)
        {
            if (key >= Key.D0 && key <= Key.D9)
                Dispatcher.Invoke(() => _overlay.HandleNumberKey(key - Key.D0));
            else if (key >= Key.NumPad0 && key <= Key.NumPad9)
                Dispatcher.Invoke(() => _overlay.HandleNumberKey(key - Key.NumPad0));
        }
    }

    private void OnGlobalKeyUp(Key key)
    {
        if (key == Key.LeftCtrl || key == Key.RightCtrl)
            _ctrlPressed = false;
    }

    private void ToggleDebugWindow()
    {
        if (_debugWindow == null || !_debugWindow.IsVisible)
        {
            if (_debug == null) return;
            _debugWindow = new Views.DebugWindow(_debug);
            _debugWindow.Show();
            DebugLogger.Info("Debug window opened");
        }
        else
        {
            _debugWindow.Hide();
            DebugLogger.Info("Debug window closed");
        }
    }

    private static Key ParseKey(string keyName)
    {
        if (Enum.TryParse<Key>(keyName, ignoreCase: true, out var key))
            return key;
        // Fallback: try with "F" prefix removed for bare numbers like "1"
        if (!keyName.StartsWith('F') && Enum.TryParse<Key>($"D{keyName}", ignoreCase: true, out var dKey))
            return dKey;
        Debug.WriteLine($"[App] Unknown key '{keyName}', falling back to F1");
        return Key.F1;
    }

    private void SetupTray()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = _schemeManager!.Settings.DebugMode
                ? "300高速咏唱装置 [DEBUG]"
                : "300高速咏唱装置",
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true
        };

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("英雄编辑", null, (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                var editor = new Views.HeroEditorWindow(_schemeManager!);
                editor.Show();
            });
        });

        if (_schemeManager.Settings.DebugMode)
        {
            contextMenu.Items.Add("🔧 调试窗口", null, (_, _) =>
                Dispatcher.Invoke(ToggleDebugWindow));
        }

        contextMenu.Items.Add("设置", null, (_, _) => { /* TODO: settings window */ });
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (_, _) => Shutdown());

        _trayIcon.ContextMenuStrip = contextMenu;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DebugLogger.Info("Application shutting down");
        _keyboardHook?.Dispose();
        _overlay?.Dispose();
        _gameEvent?.Dispose();
        _debugWindow?.Close();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}

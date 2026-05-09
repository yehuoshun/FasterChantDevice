using System;
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FasterChantDevice");

        _schemeManager = new SchemeManager(dataDir);
        _schemeManager.LoadAll();

        _inputSim = new InputSimulationService();
        _overlay = new OverlayService(_schemeManager, _inputSim);
        _gameEvent = new GameEventService(_schemeManager, _inputSim);

        // Start keyboard hook
        _keyboardHook = new KeyboardHookService();
        _keyboardHook.KeyDown += OnGlobalKeyDown;
        _keyboardHook.Start();

        // Start game event monitoring
        _gameEvent.Start();

        // Setup tray icon
        SetupTray();
    }

    private void OnGlobalKeyDown(Key key)
    {
        // F1 → toggle overlay
        if (key == Key.F1)
        {
            Dispatcher.Invoke(() => _overlay?.Toggle());
        }
        // F2 → manual taunt
        else if (key == Key.F2)
        {
            _gameEvent?.ManualTaunt();
            // Trigger taunt via overlay or directly
            var hero = _schemeManager?.Heroes.FirstOrDefault();
            if (hero != null && _schemeManager != null)
            {
                var lines = _schemeManager.PickTauntLines(hero);
                if (lines.Length > 0)
                {
                    if (_schemeManager.Settings.BurstMode)
                        _ = Task.Run(() => _inputSim!.SendLinesSequentially(
                            lines, _schemeManager.Settings.BurstIntervalMs));
                    else
                        _inputSim!.SendText(lines[0]);
                }
            }
        }
    }

    private void SetupTray()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "300高速咏唱装置",
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
        contextMenu.Items.Add("设置", null, (_, _) => { /* TODO: settings window */ });
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (_, _) => Shutdown());

        _trayIcon.ContextMenuStrip = contextMenu;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _keyboardHook?.Dispose();
        _overlay?.Dispose();
        _gameEvent?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}

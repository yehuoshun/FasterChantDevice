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
            Text = "300高速咏唱装置",
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

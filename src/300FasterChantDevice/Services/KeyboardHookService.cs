using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace _300FasterChantDevice.Services;

/// <summary>
/// Low-level keyboard hook (WH_KEYBOARD_LL) for global key monitoring.
/// Runs on a dedicated thread with its own message pump.
/// Includes watchdog for automatic re-registration.
/// </summary>
public class KeyboardHookService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;

    private IntPtr _hookId = IntPtr.Zero;
    private readonly LowLevelKeyboardProc _proc;
    private Thread? _hookThread;
    private volatile bool _running;

    public event Action<Key>? KeyDown;
    public event Action<Key>? KeyUp;

    public KeyboardHookService()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        _running = true;
        _hookThread = new Thread(RunMessageLoop)
        {
            Name = "KeyboardHook",
            IsBackground = true
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();

        // Watchdog: check hook health every 500ms
        _ = Task.Run(WatchdogLoop);
    }

    private void RunMessageLoop()
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        var moduleHandle = GetModuleHandle(curModule!.ModuleName);
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, moduleHandle, 0);

        // Message pump
        while (_running)
        {
            // PeekMessage + minimal wait to avoid 100% CPU
            if (PeekMessage(out var msg, IntPtr.Zero, 0, 0, 1))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
            else
            {
                Thread.Sleep(1);
            }
        }

        if (_hookId != IntPtr.Zero)
            UnhookWindowsHookEx(_hookId);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var vkCode = Marshal.ReadInt32(lParam);
            var key = KeyInterop.KeyFromVirtualKey(vkCode);

            if (wParam == WM_KEYDOWN)
                KeyDown?.Invoke(key);
            else if (wParam == WM_KEYUP)
                KeyUp?.Invoke(key);
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private async Task WatchdogLoop()
    {
        while (_running)
        {
            await Task.Delay(500);
            if (_hookId == IntPtr.Zero && _running)
            {
                Debug.WriteLine("Hook lost, re-registering...");
                // Restart the hook thread
                _hookThread = new Thread(RunMessageLoop)
                {
                    Name = "KeyboardHook",
                    IsBackground = true
                };
                _hookThread.SetApartmentState(ApartmentState.STA);
                _hookThread.Start();
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    #region P/Invoke
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
        IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
        IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd,
        uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }
    #endregion
}

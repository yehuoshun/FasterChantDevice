using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace FasterChantDevice.Services;

/// <summary>
/// Simulates keyboard input via SendInput.
/// Uses KEYEVENTF_UNICODE to send characters directly — no clipboard involvement.
/// </summary>
public class InputSimulationService
{
    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    public void SendText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Open chat: Shift+Enter (300 Heroes chat key)
        SimulateShiftEnter();
        Thread.Sleep(50);

        // Send each character via Unicode input (no clipboard needed)
        foreach (char c in text)
        {
            SendUnicodeChar(c);
            Thread.Sleep(1);
        }

        Thread.Sleep(50);
        // Confirm send: Enter
        SimulateEnter();
    }

    public void SendLinesSequentially(string[] lines, int intervalMs, CancellationToken ct = default)
    {
        foreach (var line in lines)
        {
            if (ct.IsCancellationRequested) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            SendText(line);

            // Cancellable wait (Thread.Sleep blocks cancellation)
            if (intervalMs > 0)
                ct.WaitHandle.WaitOne(intervalMs);
        }
    }

    private static void SimulateShiftEnter()
    {
        // Shift down
        var shiftDown = CreateKeyboardInput(VK_SHIFT, false);
        SendInput(1, new[] { shiftDown }, Marshal.SizeOf<INPUT>());

        // Enter down
        var enterDown = CreateKeyboardInput(VK_RETURN, false);
        SendInput(1, new[] { enterDown }, Marshal.SizeOf<INPUT>());

        // Enter up
        var enterUp = CreateKeyboardInput(VK_RETURN, true);
        SendInput(1, new[] { enterUp }, Marshal.SizeOf<INPUT>());

        // Shift up
        var shiftUp = CreateKeyboardInput(VK_SHIFT, true);
        SendInput(1, new[] { shiftUp }, Marshal.SizeOf<INPUT>());
    }

    private const ushort VK_SHIFT = 0x10;
    private const ushort VK_RETURN = 0x0D;

    private static void SimulateEnter() =>
        SimulateKey(VK_RETURN);

    private static void SendUnicodeChar(char c)
    {
        var down = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        var up = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        var inputs = new[] { down, up };
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void SimulateKey(ushort vkCode)
    {
        var down = CreateKeyboardInput(vkCode, false);
        var up = CreateKeyboardInput(vkCode, true);
        var inputs = new[] { down, up };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT CreateKeyboardInput(ushort vkCode, bool keyUp)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vkCode,
                    wScan = 0,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    #region P/Invoke

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    #endregion
}

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace _300FasterChantDevice.Services;

/// <summary>
/// Simulates keyboard input via SendInput.
/// Sends text by copying to clipboard then simulating Ctrl+V + Enter.
/// </summary>
public class InputSimulationService
{
    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public void SendText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Save current clipboard
        var oldClipboard = Clipboard.GetText();

        // Set text to clipboard
        Clipboard.SetText(text);

        // Simulate: Enter → Ctrl+V → Enter
        SimulateEnter();
        Thread.Sleep(50);
        SimulateCtrlV();
        Thread.Sleep(50);
        SimulateEnter();

        // Restore clipboard after a short delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(oldClipboard))
                    Clipboard.SetText(oldClipboard);
            });
        });
    }

    public void SendLinesSequentially(string[] lines, int intervalMs, CancellationToken ct = default)
    {
        foreach (var line in lines)
        {
            if (ct.IsCancellationRequested) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            SendText(line);
            Thread.Sleep(intervalMs);
        }
    }

    private static void SimulateEnter() =>
        SimulateKey(0x0D); // VK_RETURN

    private static void SimulateCtrlV()
    {
        // Press Ctrl
        var ctrlDown = CreateKeyboardInput(0x11, false);
        SendInput(1, new[] { ctrlDown }, Marshal.SizeOf<INPUT>());

        // Press V
        var vDown = CreateKeyboardInput(0x56, false);
        SendInput(1, new[] { vDown }, Marshal.SizeOf<INPUT>());

        // Release V
        var vUp = CreateKeyboardInput(0x56, true);
        SendInput(1, new[] { vUp }, Marshal.SizeOf<INPUT>());

        // Release Ctrl
        var ctrlUp = CreateKeyboardInput(0x11, true);
        SendInput(1, new[] { ctrlUp }, Marshal.SizeOf<INPUT>());
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

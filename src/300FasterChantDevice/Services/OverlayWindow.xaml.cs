using System;
using System.Windows;
using System.Windows.Input;

namespace FasterChantDevice.Services;

public partial class OverlayWindow : Window
{
    public event Action<int>? KeyPressed; // 0-9

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => MakeClickThrough();
    }

    public void SetContent(string[] mainItems, string[] subItems)
    {
        Dispatcher.Invoke(() =>
        {
            if (subItems.Length > 0)
            {
                // Show secondary panel items with numbers
                var items = new List<string>();
                for (int i = 0; i < subItems.Length && i < 10; i++)
                    items.Add($"{i}. {subItems[i]}");
                ContentList.ItemsSource = items;
            }
            else
            {
                // Show main panel groups with numbers
                var items = new List<string>();
                for (int i = 0; i < mainItems.Length && i < 10; i++)
                    items.Add($"{i}. {mainItems[i]}");
                ContentList.ItemsSource = items;
            }
        });
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key >= Key.D0 && e.Key <= Key.D9)
        {
            KeyPressed?.Invoke(e.Key - Key.D0);
        }
        else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
        {
            KeyPressed?.Invoke(e.Key - Key.NumPad0);
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
        }
    }

    private void MakeClickThrough()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}

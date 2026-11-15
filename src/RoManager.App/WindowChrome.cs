using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace RoManager.App;

/** Тёмный заголовок окна и акцентные цвета рамки для всех окон приложения. */
static class WindowChrome
{
    const int DwmwaUseImmersiveDarkMode = 20;
    const int DwmwaBorderColor = 34;
    const int DwmwaCaptionColor = 35;
    const int DwmwaTextColor = 36;

    public static void ApplyDark(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        int enabled = 1;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));

        int caption = 0x0015_110F; // COLORREF 0x00BBGGRR → #0F1115
        int text = 0x00F0_EAE8;    // #E8EAF0
        int border = 0x0024_2424;  // #242424
        _ = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref text, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(int));
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

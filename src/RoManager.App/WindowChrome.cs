using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace RoManager.App;

/** Dark window title bar and accent border colours for every app window. */
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

    /* ---------- window ownership ---------- */

    const int GwlpHwndParent = -8;

    /**
     * Makes a window owned by the main window (like a dialog): it always stays
     * above it and is destroyed with it. Otherwise WinUI windows live separately.
     */
    public static void SetOwner(Window window, Window owner)
    {
        if (window == null || owner == null) return;
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var ownerHwnd = WinRT.Interop.WindowNative.GetWindowHandle(owner);
            if (hwnd != IntPtr.Zero && ownerHwnd != IntPtr.Zero && hwnd != ownerHwnd)
                SetWindowLongPtr(hwnd, GwlpHwndParent, ownerHwnd);
        }
        catch { /* ownership is optional polish */ }
    }

    /* ---------- minimum window size ---------- */

    const int WmGetMinMaxInfo = 0x0024;
    const int GwlWndProc = -4;

    static WndProcDelegate _proc;
    static IntPtr _oldProc;
    static int _minW, _minH;
    static double _dpiScale = 1;

    delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct MinMaxPoint { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    struct MinMaxInfo
    {
        public MinMaxPoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }

    /** The window cannot shrink below what the Accounts/Actions panels need. */
    public static void SetMinSize(Window window, int width, int height)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            _minW = width;
            _minH = height;
            _dpiScale = GetDpiForWindow(hwnd) / 96.0;
            if (_dpiScale <= 0) _dpiScale = 1;

            _proc = MinSizeProc;
            _oldProc = SetWindowLongPtr(hwnd, GwlWndProc, Marshal.GetFunctionPointerForDelegate(_proc));
        }
        catch { /* WM_GETMINMAXINFO is optional polish */ }
    }

    static IntPtr MinSizeProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmGetMinMaxInfo)
        {
            var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            info.MinTrackSize.X = (int)Math.Round(_minW * _dpiScale);
            info.MinTrackSize.Y = (int)Math.Round(_minH * _dpiScale);
            Marshal.StructureToPtr(info, lParam, false);
        }
        return _oldProc == IntPtr.Zero
            ? DefWindowProc(hwnd, msg, wParam, lParam)
            : CallWindowProc(_oldProc, hwnd, msg, wParam, lParam);
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    [DllImport("user32.dll")]
    static extern IntPtr CallWindowProc(IntPtr prev, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(IntPtr hwnd);
}

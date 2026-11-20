<#
    Запуск RoManager.exe (WinUI) и снимок окна через PrintWindow.
    Нужен для проверки UI без ручного скриншота.

    Пример:
      powershell -File tools\capture.ps1 -Out C:\temp\landing.png -Wait 8
#>
param(
    [string] $Exe = 'src\RoManager.App\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\RoManager.exe',
    [string] $Out = (Join-Path $env:TEMP 'rom-shot.png'),
    [int] $Wait = 8,
    [int] $Width = 1000,
    [int] $Height = 680,
    [string] $TitleLike = '',
    [switch] $Screen,
    [switch] $KeepAlive
)

$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path $Exe).Path
$dir = Split-Path $Exe

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RomWin {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int max);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    public static string Title(IntPtr h) {
        var sb = new System.Text.StringBuilder(512);
        GetWindowText(h, sb, sb.Capacity);
        return sb.ToString();
    }
    public static IntPtr Find(int pid, string like = null) {
        IntPtr best = IntPtr.Zero; int area = 0;
        EnumWindows((h, p) => {
            int owner; GetWindowThreadProcessId(h, out owner);
            RECT r; GetWindowRect(h, out r);
            int a = (r.R - r.L) * (r.B - r.T);
            bool titleOk = string.IsNullOrEmpty(like) ||
                Title(h).IndexOf(like, StringComparison.OrdinalIgnoreCase) >= 0;
            if (owner == pid && IsWindowVisible(h) && titleOk && a > area) { area = a; best = h; }
            return true;
        }, IntPtr.Zero);
        return best;
    }
}
"@

Add-Type -AssemblyName System.Drawing

$crash = Join-Path $dir 'winui-crash.log'
Remove-Item $crash -Force -ErrorAction SilentlyContinue

$proc = Start-Process $Exe -WorkingDirectory $dir -PassThru
Start-Sleep -Seconds $Wait

if ($proc.HasExited) {
    Write-Host "EXITED code=$($proc.ExitCode)"
    if (Test-Path $crash) { Get-Content $crash -Raw }
    exit 1
}

$hwnd = [RomWin]::Find($proc.Id, $TitleLike)
if ($hwnd -eq [IntPtr]::Zero) {
    Write-Host 'NO WINDOW'
    if (-not $KeepAlive) { taskkill /PID $proc.Id /T /F 2>$null | Out-Null }
    exit 1
}

[RomWin]::MoveWindow($hwnd, 40, 40, $Width, $Height, $true) | Out-Null
Start-Sleep -Milliseconds 800

$rect = New-Object RomWin+RECT
[RomWin]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
$w = $rect.R - $rect.L
$h = $rect.B - $rect.T

$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
if ($Screen) {
    [RomWin]::ShowWindow($hwnd, 9) | Out-Null   # SW_RESTORE
    [RomWin]::BringWindowToTop($hwnd) | Out-Null
    [RomWin]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Milliseconds 700
    $g.CopyFromScreen($rect.L, $rect.T, 0, 0, (New-Object System.Drawing.Size($w, $h)))
} else {
    $dc = $g.GetHdc()
    [RomWin]::PrintWindow($hwnd, $dc, 2) | Out-Null
    $g.ReleaseHdc($dc)
}
$g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Host "saved $Out (${w}x${h}) pid=$($proc.Id)"

if (-not $KeepAlive) {
    taskkill /PID $proc.Id /T /F 2>$null | Out-Null
} else {
    Write-Host "pid=$($proc.Id) keeps running"
}

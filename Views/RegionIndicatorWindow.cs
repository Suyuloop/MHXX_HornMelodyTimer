using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace HornMelodyTimer.Views;

public sealed class RegionIndicatorWindow : Window
{
    private const int ExtendedStyleIndex = -20;
    private const int TransparentStyle = 0x00000020;
    private const int NoActivateStyle = 0x08000000;
    private const int ToolWindowStyle = 0x00000080;

    public RegionIndicatorWindow(Rectangle region)
    {
        var dpi = GetMonitorDpi(region);
        var scaleX = 96d / dpi.X;
        var scaleY = 96d / dpi.Y;
        Left = region.Left * scaleX;
        Top = region.Top * scaleY;
        Width = region.Width * scaleX;
        Height = region.Height * scaleY;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = MediaBrushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        IsHitTestVisible = false;
        Focusable = false;

        Content = new Border
        {
            BorderBrush = new SolidColorBrush(MediaColor.FromRgb(255, 70, 35)),
            BorderThickness = new Thickness(2),
            Background = MediaBrushes.Transparent,
            IsHitTestVisible = false
        };

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            var extendedStyle = GetWindowLong(handle, ExtendedStyleIndex);
            SetWindowLong(handle, ExtendedStyleIndex, extendedStyle | TransparentStyle | NoActivateStyle | ToolWindowStyle);
        };
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr windowHandle, int index, int newValue);

    private static (uint X, uint Y) GetMonitorDpi(Rectangle region)
    {
        var bounds = new NativeRect
        {
            Left = region.Left,
            Top = region.Top,
            Right = region.Right,
            Bottom = region.Bottom
        };
        var monitor = MonitorFromRect(ref bounds, 2);
        if (monitor == IntPtr.Zero)
            throw new InvalidOperationException("無法取得框選區域所在的螢幕。");

        var result = GetDpiForMonitor(monitor, 0, out var dpiX, out var dpiY);
        Marshal.ThrowExceptionForHR(result);
        return (dpiX, dpiY);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromRect(ref NativeRect rect, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}

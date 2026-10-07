using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace NoxVault;

// Custom chrome uses the whole client area, so its maximized origin must start below the taskbar.
internal static class WindowBounds
{
    [StructLayout(LayoutKind.Sequential)]
    struct NativePoint { internal int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    struct MinMaxInfo
    {
        internal NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }

    internal static void Attach(Window window)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)
            ?? throw new InvalidOperationException("Fensterhandle für die Monitorgrenzen fehlt.");
        source.AddHook((IntPtr hwnd, int message, IntPtr w, IntPtr l, ref bool handled) =>
        {
            if (message != 0x0024 || l == IntPtr.Zero) return IntPtr.Zero;
            var screen = System.Windows.Forms.Screen.FromHandle(hwnd);
            var work = screen.WorkingArea;
            var info = Marshal.PtrToStructure<MinMaxInfo>(l);
            info.MaxPosition = new NativePoint { X = work.Left - screen.Bounds.Left, Y = work.Top - screen.Bounds.Top };
            info.MaxSize = new NativePoint { X = work.Width, Y = work.Height };
            var dpi = VisualTreeHelper.GetDpi(window);
            info.MinTrackSize.X = Math.Max(info.MinTrackSize.X, (int)Math.Ceiling(window.MinWidth * dpi.DpiScaleX));
            info.MinTrackSize.Y = Math.Max(info.MinTrackSize.Y, (int)Math.Ceiling(window.MinHeight * dpi.DpiScaleY));
            Marshal.StructureToPtr(info, l, false);
            handled = true;
            return IntPtr.Zero;
        });
    }
}

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DamageMeter.UI
{
    public static class WindowsServices
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_NOACTIVATE = 0x08000000;


        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        public static void SetWindowExTransparent(IntPtr hwnd)
        {
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        }

        public static void SetWindowExVisible(IntPtr hwnd)
        {
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
        }

        public static IntPtr ClickNoFocus(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_MOUSEACTIVATE = 0x0021;
            const int MA_NOACTIVATE = 3;

            if (msg != WM_MOUSEACTIVATE) { return IntPtr.Zero; }
            handled = true;
            return new IntPtr(MA_NOACTIVATE);
        }

        public static bool IsAbove(IntPtr target, IntPtr reference, Func<IntPtr, IntPtr> previous)
        {
            if (target == IntPtr.Zero || reference == IntPtr.Zero) return false;
            if (target == reference) return true;
            var cursor = reference;
            for (var i = 0; i < 4096; i++)
            {
                cursor = previous(cursor);
                if (cursor == target) return true;
                if (cursor == IntPtr.Zero || cursor == reference) return false;
            }
            return false;
        }

        public static void EnsureAboveForeground(Window window)
        {
            if (!window.IsVisible) return;
            if (!window.Topmost) window.Topmost = true;
            var handle = new WindowInteropHelper(window).Handle;
            var foreground = GetForegroundWindow();
            if (handle == IntPtr.Zero || foreground == IntPtr.Zero ||
                IsAbove(handle, foreground, cursor => GetWindow(cursor, 3))) return;
            // HWND_TOPMOST, NOMOVE | NOSIZE | NOACTIVATE | NOOWNERZORDER.
            // Never demote an existing HUD or disturb the game's activation/capture.
            SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x0213);
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using Automation.Core.Windows;

namespace Automation.Windows
{
    public sealed class WindowService : IWindowService
    {
        public IReadOnlyList<WindowInfo> FindWindows(string titleContains = null)
        {
            var windows = new List<WindowInfo>();

            NativeMethods.EnumWindows(delegate(IntPtr handle, IntPtr lParam)
            {
                var info = GetWindow(handle);
                if (info == null || string.IsNullOrWhiteSpace(info.Title))
                {
                    return true;
                }

                if (titleContains == null ||
                    info.Title.IndexOf(titleContains, StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    windows.Add(info);
                }

                return true;
            }, IntPtr.Zero);

            return windows;
        }

        public WindowInfo GetWindow(IntPtr handle)
        {
            NativeMethods.Rect rect;
            if (!NativeMethods.GetWindowRect(handle, out rect))
            {
                return null;
            }

            // GetWindowRect may include invisible resize borders or virtualized
            // coordinates. DWM returns the actual on-screen frame used by modern
            // Chromium/layered windows.
            NativeMethods.Rect extendedFrame;
            var dwmResult = NativeMethods.DwmGetWindowAttribute(
                handle,
                NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                out extendedFrame,
                System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.Rect)));
            if (dwmResult == 0 &&
                extendedFrame.Right > extendedFrame.Left &&
                extendedFrame.Bottom > extendedFrame.Top)
            {
                rect = extendedFrame;
            }

            var title = new StringBuilder(512);
            NativeMethods.GetWindowText(handle, title, title.Capacity);

            var className = new StringBuilder(256);
            NativeMethods.GetClassName(handle, className, className.Capacity);

            var bounds = new WindowBounds(
                rect.Left,
                rect.Top,
                rect.Right - rect.Left,
                rect.Bottom - rect.Top);

            return new WindowInfo(
                handle,
                title.ToString(),
                className.ToString(),
                bounds,
                NativeMethods.IsWindowVisible(handle),
                NativeMethods.IsIconic(handle));
        }

        public bool Activate(IntPtr handle)
        {
            Restore(handle);
            return NativeMethods.SetForegroundWindow(handle);
        }

        public bool Minimize(IntPtr handle)
        {
            return NativeMethods.ShowWindow(handle, NativeMethods.SW_MINIMIZE);
        }

        public bool Restore(IntPtr handle)
        {
            return NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
        }

        public bool Move(IntPtr handle, WindowBounds bounds)
        {
            if (bounds.IsEmpty)
            {
                return false;
            }

            return NativeMethods.MoveWindow(handle, bounds.X, bounds.Y, bounds.Width, bounds.Height, true);
        }
    }
}

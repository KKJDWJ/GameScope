using System;
using System.Collections.Generic;

namespace Automation.Core.Windows
{
    public interface IWindowService
    {
        IReadOnlyList<WindowInfo> FindWindows(string titleContains = null);
        WindowInfo GetWindow(IntPtr handle);
        bool Activate(IntPtr handle);
        bool Minimize(IntPtr handle);
        bool Restore(IntPtr handle);
        bool Move(IntPtr handle, WindowBounds bounds);
    }
}

using System;

namespace Automation.Core.Windows
{
    public sealed class WindowInfo
    {
        public WindowInfo(IntPtr handle, string title, string className, WindowBounds bounds, bool isVisible, bool isMinimized)
        {
            Handle = handle;
            Title = title;
            ClassName = className;
            Bounds = bounds;
            IsVisible = isVisible;
            IsMinimized = isMinimized;
        }

        public IntPtr Handle { get; private set; }
        public string Title { get; private set; }
        public string ClassName { get; private set; }
        public WindowBounds Bounds { get; private set; }
        public bool IsVisible { get; private set; }
        public bool IsMinimized { get; private set; }
    }
}

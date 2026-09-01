using System;
using Automation.Core.Windows;

namespace Automation.Core.Capture
{
    public abstract class CaptureTarget
    {
        public sealed class Desktop : CaptureTarget
        {
        }

        public sealed class Window : CaptureTarget
        {
            public Window(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; private set; }
        }

        public sealed class Region : CaptureTarget
        {
            public Region(WindowBounds bounds)
            {
                Bounds = bounds;
            }

            public WindowBounds Bounds { get; private set; }
        }
    }
}

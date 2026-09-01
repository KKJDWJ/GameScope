using System;
using Automation.Core.Windows;

namespace Automation.Core.Capture
{
    public sealed class CaptureResult
    {
        public CaptureResult(byte[] pngBytes, WindowBounds bounds, DateTimeOffset capturedAt)
        {
            PngBytes = pngBytes;
            Bounds = bounds;
            CapturedAt = capturedAt;
        }

        public byte[] PngBytes { get; private set; }
        public WindowBounds Bounds { get; private set; }
        public DateTimeOffset CapturedAt { get; private set; }
    }
}

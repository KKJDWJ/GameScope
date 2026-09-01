using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Automation.Core.Capture;
using Automation.Core.Windows;

namespace Automation.Windows.Capture
{
    public sealed class WindowsCaptureService : ICaptureService
    {
        private const uint PrintWindowRenderFullContent = 0x00000002;
        private readonly IWindowService _windowService;

        public WindowsCaptureService(IWindowService windowService)
        {
            _windowService = windowService;
        }

        public CaptureResult Capture(CaptureTarget target)
        {
            var bounds = GetBounds(target);
            return CaptureScreen(bounds);
        }

        public bool TryCaptureWindowOffscreen(IntPtr handle, out CaptureResult result)
        {
            result = null;

            var windowInfo = _windowService.GetWindow(handle);
            if (windowInfo == null || windowInfo.Bounds.IsEmpty || windowInfo.IsMinimized)
            {
                return false;
            }

            var bounds = windowInfo.Bounds;
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Black);

                var hdc = graphics.GetHdc();
                bool printed;
                try
                {
                    printed = NativeMethods.PrintWindow(handle, hdc, PrintWindowRenderFullContent);
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }

                if (!printed || IsNearlyBlack(bitmap))
                {
                    return false;
                }

                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Png);
                    result = new CaptureResult(stream.ToArray(), bounds, DateTimeOffset.Now);
                    return true;
                }
            }
        }

        public static CaptureResult Crop(CaptureResult source, WindowBounds relativeBounds)
        {
            if (relativeBounds.X < 0 || relativeBounds.Y < 0 ||
                relativeBounds.Width <= 0 || relativeBounds.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(relativeBounds));
            }

            using var input = new MemoryStream(source.PngBytes);
            using var bitmap = new Bitmap(input);
            if (relativeBounds.Right > bitmap.Width || relativeBounds.Bottom > bitmap.Height)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(relativeBounds),
                    "AOI is outside the captured window.");
            }

            using var cropped = bitmap.Clone(
                new Rectangle(
                    relativeBounds.X,
                    relativeBounds.Y,
                    relativeBounds.Width,
                    relativeBounds.Height),
                PixelFormat.Format32bppArgb);
            using var output = new MemoryStream();
            cropped.Save(output, ImageFormat.Png);
            return new CaptureResult(
                output.ToArray(),
                new WindowBounds(
                    source.Bounds.X + relativeBounds.X,
                    source.Bounds.Y + relativeBounds.Y,
                    relativeBounds.Width,
                    relativeBounds.Height),
                source.CapturedAt);
        }

        private static CaptureResult CaptureScreen(WindowBounds bounds)
        {
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var stream = new MemoryStream())
            {
                graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, new Size(bounds.Width, bounds.Height));
                bitmap.Save(stream, ImageFormat.Png);

                return new CaptureResult(stream.ToArray(), bounds, DateTimeOffset.Now);
            }
        }

        private static bool IsNearlyBlack(Bitmap bitmap)
        {
            const int sampleColumns = 12;
            const int sampleRows = 12;
            var darkSamples = 0;
            var totalSamples = 0;

            for (var row = 0; row < sampleRows; row++)
            {
                var y = Math.Min(bitmap.Height - 1, row * bitmap.Height / sampleRows);
                for (var column = 0; column < sampleColumns; column++)
                {
                    var x = Math.Min(bitmap.Width - 1, column * bitmap.Width / sampleColumns);
                    var color = bitmap.GetPixel(x, y);
                    if (color.R <= 3 && color.G <= 3 && color.B <= 3)
                    {
                        darkSamples++;
                    }

                    totalSamples++;
                }
            }

            return totalSamples > 0 && darkSamples >= totalSamples * 98 / 100;
        }

        private WindowBounds GetBounds(CaptureTarget target)
        {
            var desktop = target as CaptureTarget.Desktop;
            if (desktop != null)
            {
                return GetVirtualScreenBounds();
            }

            var window = target as CaptureTarget.Window;
            if (window != null)
            {
                var windowInfo = _windowService.GetWindow(window.Handle);
                if (windowInfo == null)
                {
                    throw new InvalidOperationException("Window not found: " + window.Handle);
                }

                return windowInfo.Bounds;
            }

            var region = target as CaptureTarget.Region;
            if (region != null)
            {
                return region.Bounds;
            }

            throw new ArgumentOutOfRangeException("target");
        }

        private static WindowBounds GetVirtualScreenBounds()
        {
            return new WindowBounds(
                System.Windows.Forms.SystemInformation.VirtualScreen.Left,
                System.Windows.Forms.SystemInformation.VirtualScreen.Top,
                System.Windows.Forms.SystemInformation.VirtualScreen.Width,
                System.Windows.Forms.SystemInformation.VirtualScreen.Height);
        }
    }
}

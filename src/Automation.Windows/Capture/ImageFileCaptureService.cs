using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Automation.Core.Capture;
using Automation.Core.Windows;

namespace Automation.Windows.Capture
{
    public sealed class ImageFileCaptureService
    {
        public CaptureResult Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Image file path is required.", nameof(path));
            }

            var extension = Path.GetExtension(path);
            if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Only PNG, JPG/JPEG, and BMP images are supported.");
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Image file was not found.", path);
            }

            // Normalize every supported input to PNG so downstream AOI/analyzer code
            // receives the same representation as window capture.
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var image = System.Drawing.Image.FromStream(input, true, true);
            using var bitmap = new Bitmap(image);
            using var output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png);

            return new CaptureResult(
                output.ToArray(),
                new WindowBounds(0, 0, bitmap.Width, bitmap.Height),
                DateTimeOffset.Now);
        }
    }
}

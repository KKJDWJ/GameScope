using System;
using System.IO;
using Automation.Core.Image;
using OpenCvSharp;

namespace Automation.Windows.Image
{
    public sealed class BitmapImageMatchService : IImageMatchService
    {
        public ImageMatchResult Find(byte[] sourcePngBytes, string templateImagePath, ImageMatchOptions options)
        {
            if (sourcePngBytes == null || sourcePngBytes.Length == 0)
            {
                throw new ArgumentException("Source image is empty.", nameof(sourcePngBytes));
            }

            if (string.IsNullOrWhiteSpace(templateImagePath) || !File.Exists(templateImagePath))
            {
                throw new FileNotFoundException("Template image not found.", templateImagePath);
            }

            options ??= new ImageMatchOptions();

            using var sourceColor = Cv2.ImDecode(sourcePngBytes, ImreadModes.Color);
            using var templateColor = Cv2.ImRead(templateImagePath, ImreadModes.Color);
            if (sourceColor.Empty())
            {
                throw new InvalidDataException("OpenCV could not decode the source image.");
            }

            if (templateColor.Empty())
            {
                throw new InvalidDataException("OpenCV could not decode the template image.");
            }

            if (templateColor.Width > sourceColor.Width || templateColor.Height > sourceColor.Height)
            {
                return new ImageMatchResult(
                    false,
                    0,
                    0,
                    templateColor.Width,
                    templateColor.Height,
                    0);
            }

            using var sourceGray = new Mat();
            using var templateGray = new Mat();
            Cv2.CvtColor(sourceColor, sourceGray, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(templateColor, templateGray, ColorConversionCodes.BGR2GRAY);

            using var result = new Mat();
            Cv2.MeanStdDev(templateGray, out _, out var templateDeviation);
            var isNearlyUniformTemplate = templateDeviation.Val0 < 1.0;
            Cv2.MatchTemplate(
                sourceGray,
                templateGray,
                result,
                isNearlyUniformTemplate
                    ? TemplateMatchModes.SqDiffNormed
                    : TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out var minScore, out var maxScore, out var minLocation, out var maxLocation);

            if (isNearlyUniformTemplate)
            {
                maxScore = 1.0 - minScore;
                maxLocation = minLocation;
            }

            if (double.IsNaN(maxScore) || double.IsInfinity(maxScore))
            {
                maxScore = 0;
            }

            var confidence = Math.Clamp(maxScore, 0, 1);
            return new ImageMatchResult(
                confidence >= options.Threshold,
                maxLocation.X,
                maxLocation.Y,
                templateColor.Width,
                templateColor.Height,
                confidence);
        }
    }
}

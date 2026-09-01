namespace Automation.Core.Image
{
    public sealed class ImageMatchOptions
    {
        public ImageMatchOptions()
        {
            Threshold = 0.9;
            SearchStep = 2;
            PixelSampleStep = 2;
        }

        public double Threshold { get; set; }
        public int SearchStep { get; set; }
        public int PixelSampleStep { get; set; }
    }
}

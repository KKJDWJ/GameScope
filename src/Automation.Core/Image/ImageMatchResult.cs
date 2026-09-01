namespace Automation.Core.Image
{
    public sealed class ImageMatchResult
    {
        public ImageMatchResult(bool found, int x, int y, int width, int height, double confidence)
        {
            Found = found;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Confidence = confidence;
        }

        public bool Found { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public double Confidence { get; private set; }
        public int CenterX { get { return X + Width / 2; } }
        public int CenterY { get { return Y + Height / 2; } }
    }
}

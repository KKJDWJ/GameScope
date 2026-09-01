namespace Automation.Core.Windows
{
    public struct WindowBounds
    {
        public WindowBounds(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; private set; }
        public int Y { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Right { get { return X + Width; } }
        public int Bottom { get { return Y + Height; } }
        public bool IsEmpty { get { return Width <= 0 || Height <= 0; } }
    }
}

using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Automation.UI
{
    public partial class AoiPickerWindow : Window
    {
        private Point _startPoint;
        private bool _isDragging;
        private bool _hasDragged;

        public AoiPickerWindow(
            byte[] pngBytes,
            string title = "Pick AOI",
            string header = "Drag AOI On Target Capture",
            string guide = "Drag a rectangle on the captured target window, then press OK.",
            int initialX = 0,
            int initialY = 0,
            int initialWidth = 0,
            int initialHeight = 0)
        {
            InitializeComponent();
            Title = title;
            HeaderText.Text = header;
            GuideText.Text = guide;
            LoadCapture(pngBytes);
            ShowInitialSelection(initialX, initialY, initialWidth, initialHeight);
        }

        public int SelectedX { get; private set; }
        public int SelectedY { get; private set; }
        public int SelectedWidth { get; private set; }
        public int SelectedHeight { get; private set; }

        private void LoadCapture(byte[] pngBytes)
        {
            var image = new BitmapImage();
            using (var stream = new MemoryStream(pngBytes))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
            }

            CaptureImage.Source = image;
            CaptureImage.Width = image.PixelWidth;
            CaptureImage.Height = image.PixelHeight;
            CaptureCanvas.Width = image.PixelWidth;
            CaptureCanvas.Height = image.PixelHeight;
        }

        private void ShowInitialSelection(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var safeX = Math.Max(0, Math.Min(x, (int)CaptureCanvas.Width - 1));
            var safeY = Math.Max(0, Math.Min(y, (int)CaptureCanvas.Height - 1));
            var safeWidth = Math.Min(width, (int)CaptureCanvas.Width - safeX);
            var safeHeight = Math.Min(height, (int)CaptureCanvas.Height - safeY);
            if (safeWidth <= 0 || safeHeight <= 0)
            {
                return;
            }

            SelectionRectangle.Visibility = Visibility.Visible;
            CanvasSet(safeX, safeY, safeWidth, safeHeight);
            UpdateSelection(safeX, safeY, safeWidth, safeHeight);
        }

        private void CaptureCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(CaptureCanvas);
            _isDragging = true;
            _hasDragged = false;
            CaptureCanvas.CaptureMouse();
        }

        private void CaptureCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            var current = e.GetPosition(CaptureCanvas);
            var x = Math.Min(_startPoint.X, current.X);
            var y = Math.Min(_startPoint.Y, current.Y);
            var width = Math.Abs(current.X - _startPoint.X);
            var height = Math.Abs(current.Y - _startPoint.Y);
            if (!_hasDragged && width < SystemParameters.MinimumHorizontalDragDistance &&
                height < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            _hasDragged = true;
            SelectionRectangle.Visibility = Visibility.Visible;
            CanvasSet(x, y, width, height);
            UpdateSelection(x, y, width, height);
        }

        private void CaptureCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;
            CaptureCanvas.ReleaseMouseCapture();
            if (!_hasDragged)
            {
                // A click (including the tail of a double-click that opened this
                // dialog) must not erase the existing AOI rectangle.
                return;
            }

            OkButton.IsEnabled = SelectedWidth > 0 && SelectedHeight > 0;
        }

        private void CanvasSet(double x, double y, double width, double height)
        {
            System.Windows.Controls.Canvas.SetLeft(SelectionRectangle, x);
            System.Windows.Controls.Canvas.SetTop(SelectionRectangle, y);
            SelectionRectangle.Width = width;
            SelectionRectangle.Height = height;
        }

        private void UpdateSelection(double x, double y, double width, double height)
        {
            SelectedX = Math.Max(0, (int)Math.Round(x));
            SelectedY = Math.Max(0, (int)Math.Round(y));
            SelectedWidth = Math.Max(0, (int)Math.Round(width));
            SelectedHeight = Math.Max(0, (int)Math.Round(height));

            SelectionText.Text = "Selection: X=" + SelectedX + ", Y=" + SelectedY + ", W=" + SelectedWidth + ", H=" + SelectedHeight;
            OkButton.IsEnabled = SelectedWidth > 0 && SelectedHeight > 0;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

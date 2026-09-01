using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Automation.UI
{
    public partial class CapturePreviewWindow : Window
    {
        public CapturePreviewWindow(byte[] pngBytes, string description)
        {
            InitializeComponent();
            DescriptionText.Text = description;

            using var stream = new MemoryStream(pngBytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            PreviewImage.Source = image;
        }
    }
}

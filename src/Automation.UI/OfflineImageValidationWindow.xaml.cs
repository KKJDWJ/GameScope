using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Automation.Core.Capture;
using Automation.Core.Windows;
using Automation.Windows.Capture;
using Microsoft.Win32;

namespace Automation.UI
{
    public partial class OfflineImageValidationWindow : Window
    {
        private readonly ImageFileCaptureService _imageFileCaptureService = new ImageFileCaptureService();
        private CaptureResult _sourceCapture;
        private WindowBounds? _selectedAoi;

        public OfflineImageValidationWindow()
        {
            InitializeComponent();
        }

        private void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select game screenshot",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                _sourceCapture = _imageFileCaptureService.Load(dialog.FileName);
                _selectedAoi = null;
                ImagePathTextBox.Text = dialog.FileName;
                SourcePreviewImage.Source = CreateBitmapImage(_sourceCapture.PngBytes);
                CropPreviewImage.Source = null;
                ImageInfoText.Text = "Image: " + _sourceCapture.Bounds.Width + " × " +
                                     _sourceCapture.Bounds.Height + " px · " + Path.GetFileName(dialog.FileName);
                AoiInfoText.Text = "AOI: none";
                SelectAoiButton.IsEnabled = true;
                CropButton.IsEnabled = false;
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Image Load Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SelectAoiButton_Click(object sender, RoutedEventArgs e)
        {
            if (_sourceCapture == null)
            {
                return;
            }

            var picker = new AoiPickerWindow(
                _sourceCapture.PngBytes,
                "Select Offline Image AOI",
                "Drag AOI On Loaded Image",
                "Drag a rectangle on the original image, then press OK.",
                _selectedAoi?.X ?? 0,
                _selectedAoi?.Y ?? 0,
                _selectedAoi?.Width ?? 0,
                _selectedAoi?.Height ?? 0)
            {
                Owner = this
            };

            if (picker.ShowDialog() != true)
            {
                return;
            }

            _selectedAoi = new WindowBounds(
                picker.SelectedX,
                picker.SelectedY,
                picker.SelectedWidth,
                picker.SelectedHeight);
            var selectedAoi = _selectedAoi.Value;
            AoiInfoText.Text = "AOI: X=" + selectedAoi.X + ", Y=" + selectedAoi.Y +
                               ", Width=" + selectedAoi.Width + ", Height=" + selectedAoi.Height;
            CropButton.IsEnabled = true;
            ShowCropPreview();
        }

        private void CropButton_Click(object sender, RoutedEventArgs e)
        {
            ShowCropPreview();
        }

        private void ShowCropPreview()
        {
            if (_sourceCapture == null || _selectedAoi == null)
            {
                return;
            }

            try
            {
                var cropped = WindowsCaptureService.Crop(_sourceCapture, _selectedAoi.Value);
                CropPreviewImage.Source = CreateBitmapImage(cropped.PngBytes);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Crop Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static BitmapImage CreateBitmapImage(byte[] pngBytes)
        {
            using var stream = new MemoryStream(pngBytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

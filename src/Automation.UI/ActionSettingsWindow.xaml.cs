using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Automation.Core.Automation;

namespace Automation.UI
{
    public partial class ActionSettingsWindow : Window
    {
        private AoiActionType _actionType;
        private readonly int _imageWidth;
        private readonly int _imageHeight;

        public ActionSettingsWindow(AoiActionType actionType, byte[] previewPng, AoiDefinition aoi)
        {
            InitializeComponent();
            _actionType = actionType;

            var image = new BitmapImage();
            using (var stream = new MemoryStream(previewPng))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
            }

            _imageWidth = image.PixelWidth;
            _imageHeight = image.PixelHeight;
            PreviewImage.Source = image;
            PreviewImage.Width = _imageWidth;
            PreviewImage.Height = _imageHeight;
            PreviewCanvas.Width = _imageWidth;
            PreviewCanvas.Height = _imageHeight;

            SelectedX = Math.Max(0, Math.Min(_imageWidth - 1, aoi.ActionX));
            SelectedY = Math.Max(0, Math.Min(_imageHeight - 1, aoi.ActionY));
            ActionValue = aoi.ActionValue ?? string.Empty;
            DelayMilliseconds = aoi.ActionDelayMilliseconds <= 0 ? 1000 : aoi.ActionDelayMilliseconds;

            ActionXTextBox.Text = SelectedX.ToString();
            ActionYTextBox.Text = SelectedY.ToString();
            ActionValueTextBox.Text = ActionValue;
            DelayTextBox.Text = DelayMilliseconds.ToString();
            ShowMarker();
            ActionTypeComboBox.SelectedIndex =
                actionType == AoiActionType.MouseClick ? 1 :
                actionType == AoiActionType.KeyInput ? 2 :
                actionType == AoiActionType.MessagePopup ? 3 : 0;
            ConfigureForAction();
        }

        public AoiActionType SelectedActionType { get { return _actionType; } }
        public int SelectedX { get; private set; }
        public int SelectedY { get; private set; }
        public string ActionValue { get; private set; }
        public int DelayMilliseconds { get; private set; }

        private void ConfigureForAction()
        {
            ValuePanel.Visibility = Visibility.Visible;
            ActionXTextBox.IsEnabled = true;
            ActionYTextBox.IsEnabled = true;
            PointHelpText.Text = "이미지를 클릭해 좌표를 선택하세요.";
            DelayLabel.Visibility = Visibility.Visible;
            DelayTextBox.Visibility = Visibility.Visible;

            if (_actionType == AoiActionType.None)
            {
                Title = "Action 설정";
                DescriptionText.Text = "실행할 Action 종류를 선택하세요.";
                ValuePanel.Visibility = Visibility.Collapsed;
                PointMarker.Visibility = Visibility.Collapsed;
                ActionXTextBox.IsEnabled = false;
                ActionYTextBox.IsEnabled = false;
                PointHelpText.Text = "Action을 선택하면 필요한 설정 항목이 표시됩니다.";
                return;
            }

            if (_actionType == AoiActionType.MouseClick)
            {
                Title = "Mouse Click 설정";
                DescriptionText.Text = "AOI 이미지에서 실제로 클릭할 위치를 선택하세요.";
                ValuePanel.Visibility = Visibility.Collapsed;
                return;
            }

            if (_actionType == AoiActionType.KeyInput)
            {
                Title = "Key Input 설정";
                DescriptionText.Text = "대상 창을 활성화하고 선택 좌표를 클릭한 뒤, 대기시간 후 입력 내용을 전송합니다.";
                ValueLabel.Text = "입력 내용";
                return;
            }

            Title = "Message Popup 설정";
            DescriptionText.Text = "Trigger가 발견되면 사용자에게 표시할 알림 메시지를 입력하세요.";
            ValueLabel.Text = "알림 메시지";
            ActionXTextBox.IsEnabled = false;
            ActionYTextBox.IsEnabled = false;
            PointHelpText.Text = "팝업 Action은 클릭 좌표를 사용하지 않습니다.";
            DelayLabel.Visibility = Visibility.Collapsed;
            DelayTextBox.Visibility = Visibility.Collapsed;
        }

        private void ActionTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DescriptionText == null)
            {
                return;
            }

            var item = ActionTypeComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
            var value = item == null ? string.Empty : item.Content.ToString();
            _actionType =
                string.Equals(value, "MouseClick", StringComparison.OrdinalIgnoreCase) ? AoiActionType.MouseClick :
                string.Equals(value, "KeyInput", StringComparison.OrdinalIgnoreCase) ? AoiActionType.KeyInput :
                string.Equals(value, "MessagePopup", StringComparison.OrdinalIgnoreCase) ? AoiActionType.MessagePopup :
                AoiActionType.None;
            ConfigureForAction();
            ShowMarker();
        }

        private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_actionType == AoiActionType.MessagePopup)
            {
                return;
            }

            var point = e.GetPosition(PreviewCanvas);
            SelectedX = Math.Max(0, Math.Min(_imageWidth - 1, (int)Math.Round(point.X)));
            SelectedY = Math.Max(0, Math.Min(_imageHeight - 1, (int)Math.Round(point.Y)));
            ActionXTextBox.Text = SelectedX.ToString();
            ActionYTextBox.Text = SelectedY.ToString();
            ShowMarker();
        }

        private void ShowMarker()
        {
            if (_actionType == AoiActionType.MessagePopup)
            {
                PointMarker.Visibility = Visibility.Collapsed;
                return;
            }

            PointMarker.Visibility = Visibility.Visible;
            System.Windows.Controls.Canvas.SetLeft(PointMarker, SelectedX - PointMarker.Width / 2);
            System.Windows.Controls.Canvas.SetTop(PointMarker, SelectedY - PointMarker.Height / 2);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_actionType == AoiActionType.None)
            {
                MessageBox.Show(this, "Action 종류를 선택하세요.", "Action 설정");
                return;
            }

            int x;
            int y;
            int delay;
            if (_actionType != AoiActionType.MessagePopup &&
                (!int.TryParse(ActionXTextBox.Text, out x) || !int.TryParse(ActionYTextBox.Text, out y)))
            {
                MessageBox.Show(this, "클릭 좌표를 숫자로 입력하세요.", "Action 설정");
                return;
            }

            x = _actionType == AoiActionType.MessagePopup ? 0 : Math.Max(0, Math.Min(_imageWidth - 1, int.Parse(ActionXTextBox.Text)));
            y = _actionType == AoiActionType.MessagePopup ? 0 : Math.Max(0, Math.Min(_imageHeight - 1, int.Parse(ActionYTextBox.Text)));
            if (!int.TryParse(DelayTextBox.Text, out delay) || delay < 0)
            {
                delay = 1000;
            }

            SelectedX = x;
            SelectedY = y;
            ActionValue = ActionValueTextBox.Text;
            DelayMilliseconds = delay;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Automation.Core.Automation;
using Automation.Core.Capture;
using Automation.Core.Image;
using Automation.Core.Input;
using Automation.Core.Logging;
using Automation.Core.Windows;
using Automation.Windows;
using Automation.Windows.Capture;
using Automation.Windows.Image;
using Automation.Windows.Input;
using Automation.Windows.Logging;
using Microsoft.Win32;

namespace Automation.UI
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<WindowInfo> _windows = new ObservableCollection<WindowInfo>();
        private readonly ObservableCollection<TargetWindowProfile> _targets = new ObservableCollection<TargetWindowProfile>();
        private readonly ObservableCollection<AoiDefinition> _selectedTargetAois = new ObservableCollection<AoiDefinition>();
        private readonly ObservableCollection<MacroDefinition> _macros = new ObservableCollection<MacroDefinition>();
        private readonly HashSet<AoiDefinition> _runningMacroAois = new HashSet<AoiDefinition>();
        private readonly IWindowService _windowService = new WindowService();
        private readonly WindowsCaptureService _captureService;
        private readonly WindowsGraphicsCaptureService _graphicsCaptureService;
        private readonly IImageMatchService _imageMatchService = new BitmapImageMatchService();
        private readonly IInputService _inputService = new WindowsInputService();
        private readonly IAutomationLogger _logger;
        private readonly DispatcherTimer _monitorTimer = new DispatcherTimer();
        private readonly DispatcherTimer _autoSaveTimer = new DispatcherTimer();
        private readonly string _workspacePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutomationFramework",
            "workspace.json");
        private bool _isMonitoring;
        private bool _monitorTickRunning;
        private CancellationTokenSource _macroCancellation = new CancellationTokenSource();
        private bool _isLoadingWorkspace;

        public MainWindow()
        {
            InitializeComponent();

            _captureService = new WindowsCaptureService(_windowService);
            _graphicsCaptureService = new WindowsGraphicsCaptureService(_windowService);
            _logger = new FileAutomationLogger(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "automation.log"));

            _monitorTimer.Interval = TimeSpan.FromSeconds(1);
            _monitorTimer.Tick += MonitorTimer_Tick;
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(2);
            _autoSaveTimer.Tick += delegate { SaveWorkspace(); };

            WindowGrid.ItemsSource = _windows;
            TargetGrid.ItemsSource = _targets;
            AoiGrid.ItemsSource = _selectedTargetAois;
            WorkspaceTree.ItemsSource = _targets;
            MacroComboBox.ItemsSource = _macros;

            RefreshWindows();
            LoadWorkspace();
            RefreshAoiMacroNames();
            _autoSaveTimer.Start();
            SetStatus("GameScope AI ready. Add a target window or open an offline image.");
        }

        private void UserGuideButton_Click(object sender, RoutedEventArgs e)
        {
            var guide = new UserGuideWindow();
            guide.Owner = this;
            guide.ShowDialog();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshWindows();
        }

        private void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            var target = GetSelectedTarget();
            if (target == null)
            {
                SetStatus("Select a target first.");
                return;
            }

            var window = ResolveTargetWindow(target);
            if (window == null)
            {
                target.ActivationState = "Failed";
                SetStatus("Target window not found: " + target.Title);
                return;
            }

            var activated = _windowService.Activate(window.Handle);
            target.ActivationState = activated ? "Active" : "Failed";
            SetStatus(activated ? "Activated: " + window.Title : "Failed to activate: " + window.Title);
        }

        private async void BackgroundCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            var target = GetSelectedTarget();
            var window = target == null ? GetSelectedWindow() : ResolveTargetWindow(target);
            if (window == null)
            {
                SetStatus("Select a window or target first.");
                return;
            }

            SetStatus("WGC background capture testing: " + window.Title);
            try
            {
                var result = await _graphicsCaptureService.CaptureWindowAsync(
                    window.Handle,
                    TimeSpan.FromSeconds(5));
                var path = SaveCapture(result);
                var preview = new CapturePreviewWindow(
                    result.PngBytes,
                    "Target: " + window.Title + "   /   창을 활성화하지 않고 캡처됨   /   " + path);
                preview.Owner = this;
                preview.ShowDialog();
                SetStatus("WGC background capture succeeded: " + path);
            }
            catch (Exception exception)
            {
                _logger.Write(
                    AutomationLogLevel.Error,
                    "WGC background capture failed for " + window.Title + ": " + exception);
                SetStatus("WGC background capture failed: " + exception.Message);
                MessageBox.Show(
                    this,
                    exception.Message,
                    "Background Capture Test",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void OfflineImageButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new OfflineImageValidationWindow { Owner = this };
            window.ShowDialog();
        }

        private void MacrosPageButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new MacroManagerWindow(_macros, TestMacroAsync, SaveWorkspace, CheckWindow, CaptureMacroWindowAsync) { Owner = this };
            window.ShowDialog();
            MacroComboBox.Items.Refresh();
            RefreshAoiMacroNames();
            SaveWorkspace();
        }

        private bool CheckWindow(string value)
        {
            if (_windowService.FindWindows(value).Any()) return true;
            if (!File.Exists(value)) return false;
            var processName = Path.GetFileNameWithoutExtension(value);
            return Process.GetProcessesByName(processName).Any(process => process.MainWindowHandle != IntPtr.Zero);
        }

        private async Task<CaptureResult> CaptureMacroWindowAsync(string value)
        {
            var window = _windowService.FindWindows(value).FirstOrDefault();
            if (window == null && File.Exists(value))
            {
                var processName = Path.GetFileNameWithoutExtension(value);
                var process = Process.GetProcessesByName(processName).FirstOrDefault(item => item.MainWindowHandle != IntPtr.Zero);
                if (process != null) window = _windowService.GetWindow(process.MainWindowHandle);
            }
            if (window == null) throw new InvalidOperationException("실행 중인 창을 찾지 못했습니다: " + value);
            return await _graphicsCaptureService.CaptureWindowAsync(window.Handle, TimeSpan.FromSeconds(5));
        }

        private async Task TestMacroAsync(MacroDefinition macro, CancellationToken token, Action<string> progress)
        {
            var target = GetSelectedTarget();
            var targetWindow = ResolveTargetWindow(target);
            if (targetWindow == null)
            {
                throw new InvalidOperationException("메인 화면에서 테스트에 사용할 Target을 먼저 선택하세요.");
            }

            _logger.Write(AutomationLogLevel.Information, "MACRO TEST REQUEST: " + macro.Name);
            await ExecuteMacro(
                new AoiDefinition { Name = "Macro Test", MacroId = macro.Id },
                targetWindow,
                targetWindow.Bounds,
                token,
                progress);
        }

        private void MacroComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var aoi = GetSelectedAoi();
            if (!_isLoadingWorkspace && aoi != null)
            {
                aoi.MacroId = MacroComboBox.SelectedValue as string;
                aoi.MacroName = GetMacroName(aoi.MacroId);
                AoiGrid.Items.Refresh();
                RefreshWorkspaceTree();
            }
        }

        private string GetMacroName(string macroId)
        {
            if (string.IsNullOrWhiteSpace(macroId))
            {
                return "None";
            }

            var macro = _macros.FirstOrDefault(item => item.Id == macroId);
            return macro == null ? "Not found" : macro.Name;
        }

        private void RefreshAoiMacroNames()
        {
            foreach (var target in _targets)
            {
                foreach (var aoi in target.Aois)
                {
                    aoi.MacroName = GetMacroName(aoi.MacroId);
                    if (string.IsNullOrWhiteSpace(aoi.LastResult))
                    {
                        aoi.LastResult = "-";
                    }
                }
            }

            AoiGrid.Items.Refresh();
            RefreshWorkspaceTree();
        }

        private void AddTargetButton_Click(object sender, RoutedEventArgs e)
        {
            var window = GetSelectedWindow();
            if (window == null)
            {
                SetStatus("Select a window from Available Windows first.");
                return;
            }

            var target = new TargetWindowProfile();
            target.Name = "Target " + (_targets.Count + 1);
            target.TargetWindow = window;
            target.WindowTitle = window.Title;
            target.WindowClassName = window.ClassName;
            target.Aois.Add(new AoiDefinition { Name = "AOI 1", X = 0, Y = 0, Width = 200, Height = 120 });

            _targets.Add(target);
            TargetGrid.SelectedItem = target;
            RefreshWorkspaceTree();
            SetStatus("Target added: " + target.Title);
        }

        private void RemoveTargetButton_Click(object sender, RoutedEventArgs e)
        {
            var target = GetSelectedTarget();
            if (target == null)
            {
                SetStatus("Select a target first.");
                return;
            }

            _targets.Remove(target);
            if (_targets.Count > 0)
            {
                TargetGrid.SelectedIndex = 0;
            }
            else
            {
                _selectedTargetAois.Clear();
            }

            RefreshWorkspaceTree();
            SetStatus("Target removed.");
        }

        private void TargetGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadSelectedTargetAois();
        }

        private void WorkspaceTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var target = e.NewValue as TargetWindowProfile;
            if (target != null)
            {
                TargetGrid.SelectedItem = target;
                return;
            }

            var aoi = e.NewValue as AoiDefinition;
            if (aoi == null)
            {
                return;
            }

            target = _targets.FirstOrDefault(item => item.Aois.Contains(aoi));
            if (target == null)
            {
                return;
            }

            TargetGrid.SelectedItem = target;
            AoiGrid.SelectedItem = aoi;
            LoadAoiToEditor(aoi);
        }

        private async void WorkspaceTree_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var aoi = WorkspaceTree.SelectedItem as AoiDefinition;
            if (aoi == null)
            {
                return;
            }

            e.Handled = true;
            await OpenAoiPicker(aoi);
        }

        private void RefreshWorkspaceTree()
        {
            WorkspaceTree.Items.Refresh();
        }


        private async void PickAoiButton_Click(object sender, RoutedEventArgs e)
        {
            await OpenAoiPicker(GetSelectedAoi());
        }

        private async void AoiGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            var row = ItemsControl.ContainerFromElement(AoiGrid, source) as DataGridRow;
            var clickedAoi = row == null ? null : row.Item as AoiDefinition;
            if (clickedAoi == null)
            {
                return;
            }

            e.Handled = true;
            await OpenAoiPicker(clickedAoi);
        }

        private async System.Threading.Tasks.Task OpenAoiPicker(AoiDefinition existingAoi)
        {
            var target = GetSelectedTarget();
            if (target == null || target.TargetWindow == null)
            {
                SetStatus("Select a target before picking AOI.");
                return;
            }

            if (existingAoi == null)
            {
                SetStatus("먼저 AOI를 선택하세요.");
                return;
            }

            try
            {
                var currentWindow = ResolveTargetWindow(target);
                if (currentWindow == null)
                {
                    SetStatus("Target window is no longer available. Refresh and add the target again.");
                    return;
                }

                target.TargetWindow = currentWindow;
                SetStatus("WGC로 Target 창을 캡처하는 중...");
                var capture = await _graphicsCaptureService.CaptureWindowAsync(
                    currentWindow.Handle,
                    TimeSpan.FromSeconds(5));
                var picker = new AoiPickerWindow(
                    capture.PngBytes,
                    initialX: existingAoi.X,
                    initialY: existingAoi.Y,
                    initialWidth: existingAoi.Width,
                    initialHeight: existingAoi.Height);
                picker.Owner = this;

                if (picker.ShowDialog() == true)
                {
                    existingAoi.X = picker.SelectedX;
                    existingAoi.Y = picker.SelectedY;
                    existingAoi.Width = picker.SelectedWidth;
                    existingAoi.Height = picker.SelectedHeight;
                    AoiGrid.SelectedItem = existingAoi;
                    LoadAoiToEditor(existingAoi);
                    AoiGrid.Items.Refresh();

                    SetStatus("AOI picked: X=" + picker.SelectedX + ", Y=" + picker.SelectedY +
                              ", W=" + picker.SelectedWidth + ", H=" + picker.SelectedHeight);
                }
            }
            catch (Exception ex)
            {
                SetResult(ex.Message);
                SetStatus("AOI pick failed.");
                _logger.Write(AutomationLogLevel.Error, "AOI pick failed.", ex);
            }
        }

        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Select template image";
            dialog.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp|All Files|*.*";

            if (dialog.ShowDialog(this) == true)
            {
                TemplateImagePathTextBox.Text = dialog.FileName;
                var aoi = GetSelectedAoi();
                if (aoi != null)
                {
                    aoi.TemplateImagePath = dialog.FileName;
                    AoiGrid.Items.Refresh();
                }

                SetResult("Registered image: " + dialog.FileName);
            }
        }

        private async void CreatePatternButton_Click(object sender, RoutedEventArgs e)
        {
            var aoi = GetSelectedAoi();
            if (aoi == null)
            {
                SetStatus("Pattern을 등록할 AOI를 선택하세요.");
                return;
            }

            UpdateSelectedAoiFromEditor();

            try
            {
                SetStatus("전체 화면을 캡처하는 중...");
                Hide();
                await System.Threading.Tasks.Task.Delay(200);
                var fullCapture = _captureService.Capture(new CaptureTarget.Desktop());
                Show();
                Activate();

                var picker = new AoiPickerWindow(
                    fullCapture.PngBytes,
                    "Create Pattern",
                    "Drag Pattern On Full Screen Capture",
                    "저장할 패턴 이미지 영역을 드래그한 다음 OK를 누르세요.");
                picker.Owner = this;
                if (picker.ShowDialog() != true)
                {
                    SetStatus("Pattern 이미지 생성을 취소했습니다.");
                    return;
                }

                var capture = WindowsCaptureService.Crop(
                    fullCapture,
                    new WindowBounds(
                        picker.SelectedX,
                        picker.SelectedY,
                        picker.SelectedWidth,
                        picker.SelectedHeight));

                var templateDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AutomationFramework",
                    "templates");
                Directory.CreateDirectory(templateDirectory);
                var templatePath = Path.Combine(
                    templateDirectory,
                    "pattern_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
                File.WriteAllBytes(templatePath, capture.PngBytes);

                aoi.TemplateImagePath = templatePath;
                TemplateImagePathTextBox.Text = templatePath;
                AoiGrid.Items.Refresh();
                SetResult("Created and registered pattern:" + Environment.NewLine + templatePath);
                SetStatus("Pattern 이미지가 생성되어 현재 AOI에 등록되었습니다.");
            }
            catch (Exception ex)
            {
                if (!IsVisible)
                {
                    Show();
                    Activate();
                }

                SetStatus("Pattern 이미지 생성에 실패했습니다.");
                _logger.Write(AutomationLogLevel.Error, "Pattern image creation failed.", ex);
            }
        }

        private void ConfigureActionButton_Click(object sender, RoutedEventArgs e)
        {
            MacrosPageButton_Click(sender, e);
        }

        private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ThresholdText != null)
            {
                ThresholdText.Text = ThresholdSlider.Value.ToString("0.00");
            }
        }

        private void AddAoiButton_Click(object sender, RoutedEventArgs e)
        {
            var target = GetSelectedTarget();
            if (target == null)
            {
                SetStatus("Select a target first.");
                return;
            }

            var aoi = ReadAoiFromEditor();
            if (aoi == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(aoi.Name))
            {
                aoi.Name = "AOI " + (target.Aois.Count + 1);
            }

            target.Aois.Add(aoi);
            LoadSelectedTargetAois();
            AoiGrid.SelectedItem = aoi;
            RefreshWorkspaceTree();
            SetStatus("AOI added to " + target.Name + ": " + aoi.Name);
        }

        private void UpdateAoiButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedAoi();
            if (selected == null)
            {
                SetStatus("Select an AOI first.");
                return;
            }

            var edited = ReadAoiFromEditor();
            if (edited == null)
            {
                return;
            }

            CopyAoi(edited, selected);
            AoiGrid.Items.Refresh();
            TargetGrid.Items.Refresh();
            RefreshWorkspaceTree();
            SetStatus("AOI updated: " + selected.Name);
        }

        private void DeleteAoiButton_Click(object sender, RoutedEventArgs e)
        {
            var target = GetSelectedTarget();
            var selected = GetSelectedAoi();
            if (target == null || selected == null)
            {
                SetStatus("Select an AOI first.");
                return;
            }

            target.Aois.Remove(selected);
            LoadSelectedTargetAois();
            RefreshWorkspaceTree();
            SetStatus("AOI deleted.");
        }

        private void AoiGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadAoiToEditor(GetSelectedAoi());
        }

        private async void TestImageButton_Click(object sender, RoutedEventArgs e)
        {
            var target = GetSelectedTarget();
            var selected = GetSelectedAoi();
            if (target == null || selected == null)
            {
                SetStatus("Select a target and AOI first.");
                return;
            }

            UpdateSelectedAoiFromEditor();
            await RunAoiCheck(target, selected, true);
        }

        private void StartAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isMonitoring)
            {
                return;
            }

            var enabledTargetCount = CountEnabledTargets();
            var enabledAoiCount = CountEnabledAois();
            if (enabledTargetCount == 0)
            {
                SetStatus("실행할 활성 Target이 없습니다.");
                return;
            }

            if (enabledAoiCount == 0)
            {
                SetStatus("실행할 활성 AOI가 없습니다.");
                return;
            }

            UpdateSelectedAoiFromEditor();
            _macroCancellation.Dispose();
            _macroCancellation = new CancellationTokenSource();
            _isMonitoring = true;
            UpdateMonitoringState();
            _monitorTimer.Start();
            SetStatus("전체 감시 시작. Targets: " + enabledTargetCount + ", AOIs: " + enabledAoiCount);
        }

        private void StopAllButton_Click(object sender, RoutedEventArgs e)
        {
            StopMonitoring();
        }

        private async void MonitorTimer_Tick(object sender, EventArgs e)
        {
            if (_monitorTickRunning)
            {
                return;
            }

            _monitorTickRunning = true;
            try
            {
                foreach (var target in _targets)
                {
                    if (!target.IsEnabled)
                    {
                        continue;
                    }

                    var targetWindow = ResolveTargetWindow(target);
                    if (targetWindow == null)
                    {
                        continue;
                    }

                    CaptureResult fullCapture;
                    try
                    {
                        fullCapture = await _graphicsCaptureService.CaptureWindowAsync(
                            targetWindow.Handle,
                            TimeSpan.FromSeconds(3));
                    }
                    catch (Exception ex)
                    {
                        foreach (var aoi in target.Aois.Where(item => item.IsEnabled))
                        {
                            aoi.LastResult = "Capture error";
                        }

                        _logger.Write(AutomationLogLevel.Error, "WGC capture failed: " + target.Name, ex);
                        continue;
                    }

                    foreach (var aoi in target.Aois)
                    {
                        if (aoi.IsEnabled)
                        {
                            await RunAoiCheck(target, aoi, false, fullCapture);
                        }
                    }
                }

                TargetGrid.Items.Refresh();
                AoiGrid.Items.Refresh();
                RefreshWorkspaceTree();
            }
            finally
            {
                _monitorTickRunning = false;
            }
        }

        private void RefreshWindows()
        {
            _windows.Clear();

            foreach (var window in _windowService.FindWindows())
            {
                _windows.Add(window);
            }

            ReconnectTargets();
            SetStatus("Loaded " + _windows.Count + " windows.");
        }

        private async System.Threading.Tasks.Task<bool> RunAoiCheck(
            TargetWindowProfile target,
            AoiDefinition aoi,
            bool showNotFound,
            CaptureResult fullCapture = null)
        {
            var window = ResolveTargetWindow(target);
            if (window == null)
            {
                return false;
            }

            if (aoi.Width <= 0 || aoi.Height <= 0)
            {
                SetStatus("AOI size must be greater than zero.");
                return false;
            }

            try
            {
                if (aoi.TriggerType == AoiTriggerType.TextMatch)
                {
                    aoi.LastResult = "Text OCR pending";
                    if (showNotFound)
                    {
                        SetResult("Text/OCR trigger model is prepared, but OCR engine is not implemented yet." + Environment.NewLine +
                                  "Target: " + target.Name + Environment.NewLine +
                                  "AOI: " + aoi.Name);
                    }
                    return false;
                }

                if (string.IsNullOrWhiteSpace(aoi.TemplateImagePath) || !File.Exists(aoi.TemplateImagePath))
                {
                    aoi.LastResult = "No image";
                    if (showNotFound)
                    {
                        SetResult("Select a template image for AOI: " + aoi.Name);
                    }
                    return false;
                }

                fullCapture ??= await _graphicsCaptureService.CaptureWindowAsync(
                    window.Handle,
                    TimeSpan.FromSeconds(5));
                var capture = WindowsCaptureService.Crop(
                    fullCapture,
                    new WindowBounds(aoi.X, aoi.Y, aoi.Width, aoi.Height));
                var captureBounds = capture.Bounds;
                var options = new ImageMatchOptions();
                options.Threshold = aoi.ImageThreshold;

                var result = _imageMatchService.Find(capture.PngBytes, aoi.TemplateImagePath, options);
                if (result.Found)
                {
                    aoi.LastResult = "Found " + result.Confidence.ToString("0.000");
                    if (aoi.IsTriggerLatched)
                    {
                        return true;
                    }

                    aoi.IsTriggerLatched = true;

                    var message =
                        "TRIGGER FOUND" + Environment.NewLine +
                        "Target: " + target.Name + Environment.NewLine +
                        "Window: " + window.Title + Environment.NewLine +
                        "AOI: " + aoi.Name + Environment.NewLine +
                        "Confidence: " + result.Confidence.ToString("0.000") + Environment.NewLine +
                        "AOI Bounds: " + capture.Bounds.X + ", " + capture.Bounds.Y + ", " + capture.Bounds.Width + ", " + capture.Bounds.Height + Environment.NewLine +
                        "Action: " + aoi.ActionType + Environment.NewLine +
                        "Checked: " + DateTime.Now.ToString("HH:mm:ss");

                    SetResult(message);
                    StartTriggeredMacro(aoi, window, captureBounds);
                    _logger.Write(AutomationLogLevel.Information, message);
                    SetStatus("Trigger fired: " + target.Name + " / " + aoi.Name);
                    return true;
                }

                aoi.LastResult = "Best " + result.Confidence.ToString("0.000");
                aoi.IsTriggerLatched = false;
                if (showNotFound)
                {
                    SetResult(
                        "Not found" + Environment.NewLine +
                        "Target: " + target.Name + Environment.NewLine +
                        "AOI: " + aoi.Name + Environment.NewLine +
                        "Best confidence: " + result.Confidence.ToString("0.000") + Environment.NewLine +
                        "Threshold: " + options.Threshold.ToString("0.000") + Environment.NewLine +
                        "Checked: " + DateTime.Now.ToString("HH:mm:ss"));
                    SetStatus("Trigger not found: " + target.Name + " / " + aoi.Name);
                }

                return false;
            }
            catch (Exception ex)
            {
                aoi.LastResult = "Error";
                SetResult(ex.Message);
                SetStatus("AOI check failed: " + target.Name + " / " + aoi.Name);
                _logger.Write(AutomationLogLevel.Error, "AOI check failed: " + target.Name + " / " + aoi.Name, ex);
                return false;
            }
        }

        private void StartTriggeredMacro(AoiDefinition aoi, WindowInfo window, WindowBounds captureBounds)
        {
            if (!_runningMacroAois.Add(aoi))
            {
                _logger.Write(AutomationLogLevel.Information, "MACRO ALREADY RUNNING: " + aoi.Name);
                return;
            }

            _ = RunTriggeredMacroAsync(aoi, window, captureBounds);
        }

        private async Task RunTriggeredMacroAsync(AoiDefinition aoi, WindowInfo window, WindowBounds captureBounds)
        {
            try
            {
                await ExecuteMacro(aoi, window, captureBounds, _macroCancellation.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                // ExecuteMacro already records the full exception.
            }
            finally
            {
                _runningMacroAois.Remove(aoi);
            }
        }

        private async Task ExecuteMacro(AoiDefinition aoi, WindowInfo targetWindow, WindowBounds absoluteAoiBounds, CancellationToken token, Action<string> progress = null)
        {
            var macro = _macros.FirstOrDefault(x => x.Id == aoi.MacroId);
            if (macro == null) return;
            foreach (var item in macro.Steps) item.IsExecuting = false;
            progress?.Invoke("START  " + macro.Name);
            _logger.Write(AutomationLogLevel.Information, "MACRO START: " + macro.Name + " / AOI: " + aoi.Name);
            try
            {
                var infiniteRepeat = macro.RepeatCount == 0;
                var repeat = 1;
                while (infiniteRepeat || repeat <= macro.RepeatCount)
                {
                    var repeatTotal = infiniteRepeat ? "∞" : macro.RepeatCount.ToString();
                    _logger.Write(AutomationLogLevel.Information, "MACRO REPEAT: " + macro.Name + " / " + repeat + "/" + repeatTotal);
                    progress?.Invoke("REPEAT  " + repeat + "/" + repeatTotal);
                    WindowInfo foundWindow = targetWindow;
                    var previousOk = true;
                    for (var index = 0; index < macro.Steps.Count; index++)
                    {
                        token.ThrowIfCancellationRequested();
                        var step = macro.Steps[index];
                        var shouldRun = step.Condition == MacroStepCondition.Always ||
                                        step.Condition == MacroStepCondition.PreviousOk && previousOk ||
                                        step.Condition == MacroStepCondition.PreviousNg && !previousOk;
                        if (!shouldRun)
                        {
                            _logger.Write(AutomationLogLevel.Information, "MACRO SKIPPED: " + macro.Name + " / " + (index + 1) + " / condition=" + step.Condition);
                            progress?.Invoke("SKIPPED  #" + (index + 1) + "  " + step.Summary + "  (" + step.Condition + ")");
                            continue;
                        }
                        step.IsExecuting = true;
                        _logger.Write(AutomationLogLevel.Information, "MACRO STEP: " + macro.Name + " / " + (index + 1) + " / " + step.Summary);
                        progress?.Invoke("STEP  #" + (index + 1) + "  " + step.Summary);
                        try
                        {
                            switch (step.Type)
                            {
                                case MacroStepType.Delay: await Task.Delay(step.DelayMilliseconds, token); break;
                                case MacroStepType.Hotkey: _inputService.HotKey((step.Value ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)); break;
                                case MacroStepType.TextInput: _inputService.TypeText(step.Value ?? ""); break;
                                case MacroStepType.MouseClick:
                                    if (step.Value == "InProgram")
                                    {
                                        if (foundWindow == null || step.ClickRegionWidth <= 0 || step.ClickRegionHeight <= 0)
                                            throw new InvalidOperationException("MouseClick InProgram의 Set AOI가 필요합니다.");
                                        _inputService.Click(
                                            foundWindow.Bounds.X + step.ClickRegionX + step.ClickRegionWidth / 2,
                                            foundWindow.Bounds.Y + step.ClickRegionY + step.ClickRegionHeight / 2);
                                    }
                                    else _inputService.Click(targetWindow.Bounds.X + step.X, targetWindow.Bounds.Y + step.Y);
                                    break;
                                case MacroStepType.FindWindow:
                                    foundWindow = _windowService.FindWindows(step.Value).FirstOrDefault();
                                    if (foundWindow == null && File.Exists(step.Value))
                                    {
                                        var processName = Path.GetFileNameWithoutExtension(step.Value);
                                        var process = Process.GetProcessesByName(processName).FirstOrDefault(item => item.MainWindowHandle != IntPtr.Zero);
                                        if (process != null) foundWindow = _windowService.GetWindow(process.MainWindowHandle);
                                    }
                                    previousOk = foundWindow != null;
                                    if (foundWindow == null && !step.ContinueWhenNotFound) throw new InvalidOperationException("Window not found: " + step.Value);
                                    break;
                                case MacroStepType.ActivateWindow: if (foundWindow != null) _windowService.Activate(foundWindow.Handle); break;
                                case MacroStepType.RunProgram:
                                    Process.Start(new ProcessStartInfo(step.Value ?? "") { Arguments = step.Arguments ?? "", UseShellExecute = true });
                                    break;
                                case MacroStepType.Screenshot:
                                    var screenshotPath = await SaveMacroScreenshotAsync(
                                        targetWindow,
                                        macro,
                                        aoi,
                                        repeat,
                                        step.Value,
                                        token);
                                    _logger.Write(AutomationLogLevel.Information, "MACRO SCREENSHOT: " + screenshotPath);
                                    progress?.Invoke("SCREENSHOT  " + screenshotPath);
                                    break;
                            }
                            if (step.Type != MacroStepType.FindWindow) previousOk = true;
                            _logger.Write(AutomationLogLevel.Information, "MACRO RESULT: " + macro.Name + " / " + (index + 1) + " / " + (previousOk ? "OK" : "NG"));
                            progress?.Invoke("RESULT  #" + (index + 1) + "  " + (previousOk ? "OK" : "NG"));
                        }
                        finally
                        {
                            step.IsExecuting = false;
                        }
                    }

                    var hasNextRepeat = infiniteRepeat || repeat < macro.RepeatCount;
                    if (hasNextRepeat && macro.RepeatDelayMilliseconds > 0)
                    {
                        _logger.Write(AutomationLogLevel.Information, "MACRO RESTART DELAY: " + macro.Name + " / " + macro.RepeatDelayMilliseconds + " ms");
                        progress?.Invoke("RESTART DELAY  " + macro.RepeatDelayMilliseconds + " ms");
                        await Task.Delay(macro.RepeatDelayMilliseconds, token);
                    }

                    repeat++;
                }
                _logger.Write(AutomationLogLevel.Information, "MACRO COMPLETE: " + macro.Name);
                progress?.Invoke("COMPLETE  " + macro.Name);
            }
            catch (OperationCanceledException)
            {
                _logger.Write(AutomationLogLevel.Information, "MACRO CANCELED: " + macro.Name);
                progress?.Invoke("CANCELED  " + macro.Name);
            }
            catch (Exception ex)
            {
                _logger.Write(AutomationLogLevel.Error, "MACRO FAILED: " + macro.Name, ex);
                progress?.Invoke("FAILED  " + macro.Name + "  " + ex.Message);
                throw;
            }
            finally
            {
                foreach (var item in macro.Steps) item.IsExecuting = false;
            }
        }

        private async Task<string> SaveMacroScreenshotAsync(
            WindowInfo window,
            MacroDefinition macro,
            AoiDefinition aoi,
            int repeat,
            string configuredDirectory,
            CancellationToken token)
        {
            if (window == null)
            {
                throw new InvalidOperationException("Screenshot target window not found.");
            }

            var capture = await _graphicsCaptureService.CaptureWindowAsync(
                window.Handle,
                TimeSpan.FromSeconds(5),
                token);
            var rootDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AutomationFramework",
                    "screenshots")
                : Environment.ExpandEnvironmentVariables(configuredDirectory.Trim());
            var directory = Path.Combine(rootDirectory, DateTime.Now.ToString("yyyyMMdd"));
            Directory.CreateDirectory(directory);
            var fileName =
                DateTime.Now.ToString("HHmmss_fff") + "_" +
                SanitizeFileName(macro.Name) + "_" +
                SanitizeFileName(aoi.Name) + "_R" + repeat + ".png";
            var path = Path.Combine(directory, fileName);
            await File.WriteAllBytesAsync(path, capture.PngBytes, token);
            return path;
        }

        private static string SanitizeFileName(string value)
        {
            var result = string.IsNullOrWhiteSpace(value) ? "Unnamed" : value;
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalidCharacter, '_');
            }

            return result;
        }

        private WindowBounds ToAbsoluteBounds(WindowInfo window, AoiDefinition aoi)
        {
            return new WindowBounds(window.Bounds.X + aoi.X, window.Bounds.Y + aoi.Y, aoi.Width, aoi.Height);
        }

        private void LoadSelectedTargetAois()
        {
            _selectedTargetAois.Clear();
            var target = GetSelectedTarget();
            if (target == null)
            {
                return;
            }

            foreach (var aoi in target.Aois)
            {
                _selectedTargetAois.Add(aoi);
            }

            if (_selectedTargetAois.Count > 0)
            {
                AoiGrid.SelectedIndex = 0;
            }
        }

        private AoiDefinition ReadAoiFromEditor()
        {
            int x;
            int y;
            int width;
            int height;

            if (!int.TryParse(AoiXTextBox.Text, out x) || !int.TryParse(AoiYTextBox.Text, out y) ||
                !int.TryParse(AoiWidthTextBox.Text, out width) || !int.TryParse(AoiHeightTextBox.Text, out height))
            {
                SetStatus("AOI X/Y/W/H must be numbers.");
                return null;
            }

            if (width <= 0 || height <= 0)
            {
                SetStatus("AOI W/H must be greater than zero.");
                return null;
            }

            var aoi = new AoiDefinition();
            aoi.Name = AoiNameTextBox.Text;
            aoi.X = x;
            aoi.Y = y;
            aoi.Width = width;
            aoi.Height = height;
            aoi.TemplateImagePath = TemplateImagePathTextBox.Text;
            aoi.ImageThreshold = ThresholdSlider.Value;
            aoi.TriggerType = ParseTriggerType(GetComboText(TriggerTypeComboBox));
            var selected = GetSelectedAoi();
            if (selected != null)
            {
                aoi.ActionType = selected.ActionType;
                aoi.ActionValue = selected.ActionValue;
                aoi.ActionX = selected.ActionX;
                aoi.ActionY = selected.ActionY;
                aoi.ActionDelayMilliseconds = selected.ActionDelayMilliseconds;
            }
            aoi.IsEnabled = true;
            return aoi;
        }

        private void CopyAoi(AoiDefinition source, AoiDefinition target)
        {
            target.Name = source.Name;
            target.X = source.X;
            target.Y = source.Y;
            target.Width = source.Width;
            target.Height = source.Height;
            target.TriggerType = source.TriggerType;
            target.TemplateImagePath = source.TemplateImagePath;
            target.ImageThreshold = source.ImageThreshold;
            target.ActionType = source.ActionType;
            target.ActionValue = source.ActionValue;
            target.ActionX = source.ActionX;
            target.ActionY = source.ActionY;
            target.ActionDelayMilliseconds = source.ActionDelayMilliseconds;
        }

        private void UpdateSelectedAoiFromEditor()
        {
            var selected = GetSelectedAoi();
            if (selected == null)
            {
                return;
            }

            var edited = ReadAoiFromEditor();
            if (edited == null)
            {
                return;
            }

            CopyAoi(edited, selected);
            AoiGrid.Items.Refresh();
            TargetGrid.Items.Refresh();
            RefreshWorkspaceTree();
        }

        private void LoadAoiToEditor(AoiDefinition aoi)
        {
            if (aoi == null)
            {
                return;
            }

            AoiNameTextBox.Text = aoi.Name;
            AoiXTextBox.Text = aoi.X.ToString();
            AoiYTextBox.Text = aoi.Y.ToString();
            AoiWidthTextBox.Text = aoi.Width.ToString();
            AoiHeightTextBox.Text = aoi.Height.ToString();
            TemplateImagePathTextBox.Text = aoi.TemplateImagePath;
            ThresholdSlider.Value = aoi.ImageThreshold;
            TriggerTypeComboBox.SelectedIndex = aoi.TriggerType == AoiTriggerType.TextMatch ? 1 : 0;
            MacroComboBox.SelectedValue = aoi.MacroId;
        }

        private string GetComboText(ComboBox comboBox)
        {
            var item = comboBox.SelectedItem as ComboBoxItem;
            return item == null ? string.Empty : item.Content.ToString();
        }

        private AoiTriggerType ParseTriggerType(string value)
        {
            return string.Equals(value, "TextMatch", StringComparison.OrdinalIgnoreCase) ? AoiTriggerType.TextMatch : AoiTriggerType.ImageMatch;
        }

        private static string GetActionSummary(AoiDefinition aoi)
        {
            if (aoi.ActionType == AoiActionType.MouseClick)
            {
                return "MouseClick · X=" + aoi.ActionX + ", Y=" + aoi.ActionY;
            }

            if (aoi.ActionType == AoiActionType.KeyInput)
            {
                return "KeyInput · (" + aoi.ActionX + ", " + aoi.ActionY + ") · " +
                       aoi.ActionDelayMilliseconds + "ms";
            }

            if (aoi.ActionType == AoiActionType.MessagePopup)
            {
                return "MessagePopup · 설정됨";
            }

            return "설정 안 됨";
        }

        private int CountEnabledTargets()
        {
            var count = 0;
            foreach (var target in _targets)
            {
                if (target.IsEnabled && target.TargetWindow != null) count++;
            }
            return count;
        }

        private int CountEnabledAois()
        {
            var count = 0;
            foreach (var target in _targets)
            {
                if (!target.IsEnabled || target.TargetWindow == null) continue;
                foreach (var aoi in target.Aois)
                {
                    if (aoi.IsEnabled) count++;
                }
            }
            return count;
        }

        private WindowInfo GetSelectedWindow()
        {
            return WindowGrid.SelectedItem as WindowInfo;
        }

        private TargetWindowProfile GetSelectedTarget()
        {
            return TargetGrid.SelectedItem as TargetWindowProfile;
        }

        private WindowInfo ResolveTargetWindow(TargetWindowProfile target)
        {
            if (target == null)
            {
                return null;
            }

            if (target.TargetWindow != null)
            {
                var current = _windowService.GetWindow(target.TargetWindow.Handle);
                if (current != null &&
                    string.Equals(current.Title, target.WindowTitle, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(current.ClassName, target.WindowClassName, StringComparison.OrdinalIgnoreCase))
                {
                    target.TargetWindow = current;
                    return current;
                }
            }

            var match = _windowService.FindWindows().FirstOrDefault(window =>
                string.Equals(window.Title, target.WindowTitle, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(window.ClassName, target.WindowClassName, StringComparison.OrdinalIgnoreCase));
            target.TargetWindow = match;
            return match;
        }

        private AoiDefinition GetSelectedAoi()
        {
            return AoiGrid.SelectedItem as AoiDefinition;
        }

        private void SaveWorkspace()
        {
            if (_isLoadingWorkspace)
            {
                return;
            }

            try
            {
                var workspace = new WorkspaceSettings();
                workspace.Macros = _macros.ToList();
                foreach (var target in _targets)
                {
                    var savedTarget = new TargetSettings();
                    savedTarget.Name = target.Name;
                    savedTarget.IsEnabled = target.IsEnabled;
                    savedTarget.WindowTitle = target.TargetWindow == null ? target.WindowTitle : target.TargetWindow.Title;
                    savedTarget.WindowClassName = target.TargetWindow == null ? target.WindowClassName : target.TargetWindow.ClassName;

                    foreach (var aoi in target.Aois)
                    {
                        savedTarget.Aois.Add(new AoiSettings
                        {
                            Name = aoi.Name,
                            X = aoi.X,
                            Y = aoi.Y,
                            Width = aoi.Width,
                            Height = aoi.Height,
                            IsEnabled = aoi.IsEnabled,
                            TriggerType = aoi.TriggerType,
                            TemplateImagePath = aoi.TemplateImagePath,
                            ImageThreshold = aoi.ImageThreshold,
                            ExpectedText = aoi.ExpectedText,
                            ExpectedColor = aoi.ExpectedColor,
                            ActionType = aoi.ActionType,
                            ActionValue = aoi.ActionValue,
                            ActionX = aoi.ActionX,
                            ActionY = aoi.ActionY,
                            ActionDelayMilliseconds = aoi.ActionDelayMilliseconds
                            ,MacroId = aoi.MacroId
                        });
                    }

                    workspace.Targets.Add(savedTarget);
                }

                var directory = Path.GetDirectoryName(_workspacePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(workspace, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_workspacePath, json);
            }
            catch (Exception ex)
            {
                _logger.Write(AutomationLogLevel.Error, "Workspace auto-save failed.", ex);
            }
        }

        private void LoadWorkspace()
        {
            if (!File.Exists(_workspacePath))
            {
                return;
            }

            _isLoadingWorkspace = true;
            try
            {
                var workspace = JsonSerializer.Deserialize<WorkspaceSettings>(File.ReadAllText(_workspacePath));
                if (workspace == null)
                {
                    return;
                }

                _targets.Clear();
                _macros.Clear();
                foreach (var macro in workspace.Macros ?? new System.Collections.Generic.List<MacroDefinition>())
                {
                    _macros.Add(macro);
                }
                foreach (var savedTarget in workspace.Targets)
                {
                    var target = new TargetWindowProfile();
                    target.Name = savedTarget.Name;
                    target.IsEnabled = savedTarget.IsEnabled;
                    target.WindowTitle = savedTarget.WindowTitle;
                    target.WindowClassName = savedTarget.WindowClassName;

                    foreach (var savedAoi in savedTarget.Aois)
                    {
                        target.Aois.Add(new AoiDefinition
                        {
                            Name = savedAoi.Name,
                            X = savedAoi.X,
                            Y = savedAoi.Y,
                            Width = savedAoi.Width,
                            Height = savedAoi.Height,
                            IsEnabled = savedAoi.IsEnabled,
                            TriggerType = savedAoi.TriggerType,
                            TemplateImagePath = savedAoi.TemplateImagePath,
                            ImageThreshold = savedAoi.ImageThreshold,
                            ExpectedText = savedAoi.ExpectedText,
                            ExpectedColor = savedAoi.ExpectedColor,
                            ActionType = savedAoi.ActionType,
                            ActionValue = savedAoi.ActionValue,
                            ActionX = savedAoi.ActionX,
                            ActionY = savedAoi.ActionY,
                            ActionDelayMilliseconds = savedAoi.ActionDelayMilliseconds
                            ,MacroId = savedAoi.MacroId
                        });
                    }

                    _targets.Add(target);
                }

                ReconnectTargets();
                if (_targets.Count > 0)
                {
                    TargetGrid.SelectedIndex = 0;
                }

                SetStatus("저장된 Workspace 설정을 불러왔습니다. Targets: " + _targets.Count);
            }
            catch (Exception ex)
            {
                _logger.Write(AutomationLogLevel.Error, "Workspace load failed.", ex);
                SetStatus("저장된 Workspace 설정을 불러오지 못했습니다.");
            }
            finally
            {
                _isLoadingWorkspace = false;
            }
        }

        private void ReconnectTargets()
        {
            foreach (var target in _targets)
            {
                var match = _windows.FirstOrDefault(window =>
                    string.Equals(window.Title, target.WindowTitle, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(window.ClassName, target.WindowClassName, StringComparison.OrdinalIgnoreCase));

                if (match == null && !string.IsNullOrWhiteSpace(target.WindowTitle))
                {
                    match = _windows.FirstOrDefault(window =>
                        string.Equals(window.Title, target.WindowTitle, StringComparison.OrdinalIgnoreCase));
                }

                target.TargetWindow = match;
            }

            TargetGrid.Items.Refresh();
            RefreshWorkspaceTree();
        }

        protected override void OnClosed(EventArgs e)
        {
            _autoSaveTimer.Stop();
            SaveWorkspace();
            base.OnClosed(e);
        }

        private string SaveCapture(CaptureResult result)
        {
            var captureDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "captures");
            Directory.CreateDirectory(captureDir);
            var path = Path.Combine(captureDir, "capture_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            File.WriteAllBytes(path, result.PngBytes);
            return path;
        }

        private void StopMonitoring()
        {
            _monitorTimer.Stop();
            _macroCancellation.Cancel();
            _isMonitoring = false;
            UpdateMonitoringState();
            SetStatus("전체 감시가 정지되었습니다.");
        }

        private void UpdateMonitoringState()
        {
            MonitoringStatusText.Text = _isMonitoring ? "동작중" : "정지";
            MonitoringStatusText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(_isMonitoring ? "#08783E" : "#8B2044"));
            MonitoringStatusBorder.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(_isMonitoring ? "#D8F3E4" : "#F5D3E1"));
            StartAllButton.IsEnabled = !_isMonitoring;
            StopAllButton.IsEnabled = _isMonitoring;
        }

        private void SetResult(string message)
        {
            ImageResultTextBox.Text = message;
        }

        private void SetStatus(string message)
        {
            StatusText.Text = message;
            _logger.Write(AutomationLogLevel.Information, message);
        }

        private sealed class WorkspaceSettings
        {
            public WorkspaceSettings()
            {
                Targets = new System.Collections.Generic.List<TargetSettings>();
                Macros = new System.Collections.Generic.List<MacroDefinition>();
            }

            public System.Collections.Generic.List<TargetSettings> Targets { get; set; }
            public System.Collections.Generic.List<MacroDefinition> Macros { get; set; }
        }

        private sealed class TargetSettings
        {
            public TargetSettings()
            {
                Aois = new System.Collections.Generic.List<AoiSettings>();
            }

            public string Name { get; set; }
            public bool IsEnabled { get; set; }
            public string WindowTitle { get; set; }
            public string WindowClassName { get; set; }
            public System.Collections.Generic.List<AoiSettings> Aois { get; set; }
        }

        private sealed class AoiSettings
        {
            public string Name { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public bool IsEnabled { get; set; }
            public AoiTriggerType TriggerType { get; set; }
            public string TemplateImagePath { get; set; }
            public double ImageThreshold { get; set; }
            public string ExpectedText { get; set; }
            public string ExpectedColor { get; set; }
            public AoiActionType ActionType { get; set; }
            public string MacroId { get; set; }
            public string ActionValue { get; set; }
            public int ActionX { get; set; }
            public int ActionY { get; set; }
            public int ActionDelayMilliseconds { get; set; }
        }
    }
}

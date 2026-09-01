using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using System.Threading.Tasks;
using Automation.Core.Automation;
using Automation.Core.Capture;
using Microsoft.Win32;

namespace Automation.UI
{
    public partial class MacroManagerWindow : Window
    {
        private readonly ObservableCollection<MacroDefinition> _macros;
        private readonly Func<MacroDefinition, CancellationToken, Action<string>, Task> _testRunner;
        private readonly Action _save;
        private readonly Func<string, bool> _windowChecker;
        private readonly Func<string, Task<CaptureResult>> _windowCapture;
        private CancellationTokenSource _testCancellation;
        public MacroManagerWindow(ObservableCollection<MacroDefinition> macros, Func<MacroDefinition, CancellationToken, Action<string>, Task> testRunner, Action save, Func<string, bool> windowChecker, Func<string, Task<CaptureResult>> windowCapture)
        {
            InitializeComponent(); _macros = macros; _testRunner = testRunner; _save = save; _windowChecker = windowChecker; _windowCapture = windowCapture; MacroList.ItemsSource = macros; StepType.SelectedIndex = 0; StepCondition.SelectedIndex = 0;
        }
        private MacroDefinition Current => MacroList.SelectedItem as MacroDefinition;
        private void AddMacro_Click(object s, RoutedEventArgs e) { var m=new MacroDefinition { Name="Macro "+(_macros.Count+1) }; _macros.Add(m); MacroList.SelectedItem=m; }
        private void DeleteMacro_Click(object s, RoutedEventArgs e) { if(Current!=null) _macros.Remove(Current); }
        private void MacroList_SelectionChanged(object s, SelectionChangedEventArgs e) { MacroName.Text=Current?.Name??""; RepeatCount.Text=(Current?.RepeatCount??1).ToString(); RepeatDelay.Text=((Current?.RepeatDelayMilliseconds??5000)/1000.0).ToString("0.###"); StepGrid.ItemsSource=Current?.Steps; }
        private void MacroName_TextChanged(object s, TextChangedEventArgs e) { if(Current!=null) { Current.Name=MacroName.Text; MacroList.Items.Refresh(); } }
        private void RepeatCount_TextChanged(object s, TextChangedEventArgs e) { if(Current!=null && int.TryParse(RepeatCount.Text,out var count)) Current.RepeatCount=Math.Max(0,count); }
        private void RepeatDelay_TextChanged(object s, TextChangedEventArgs e) { if(Current!=null && double.TryParse(RepeatDelay.Text,out var seconds)) Current.RepeatDelayMilliseconds=(int)Math.Max(0,seconds*1000); }
        private void StepGrid_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (StepGrid.SelectedItem is not MacroStepDefinition step || StepType == null) return;

            foreach (var candidate in StepType.Items)
            {
                if (candidate is ComboBoxItem item &&
                    string.Equals(item.Content?.ToString(), step.Type.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    StepType.SelectedItem = item;
                    break;
                }
            }

            StepDelay.Text = step.DelayMilliseconds.ToString();
            foreach (var candidate in StepCondition.Items)
                if (candidate is ComboBoxItem conditionItem && conditionItem.Tag?.ToString() == step.Condition.ToString())
                    StepCondition.SelectedItem = conditionItem;
            StepValue.Text = step.Type == MacroStepType.MouseClick
                ? step.X + "," + step.Y
                : step.Value ?? string.Empty;

            if (step.Type == MacroStepType.Hotkey)
            {
                ComboBoxItem match = null;
                ComboBoxItem custom = null;
                foreach (var candidate in HotkeyList.Items)
                {
                    if (candidate is not ComboBoxItem item) continue;
                    if (string.Equals(item.Tag?.ToString(), "CUSTOM", StringComparison.OrdinalIgnoreCase)) custom = item;
                    if (string.Equals(item.Tag?.ToString(), step.Value, StringComparison.OrdinalIgnoreCase)) match = item;
                }

                if (match != null)
                {
                    HotkeyList.Visibility = Visibility.Visible;
                    StepValue.Visibility = Visibility.Collapsed;
                    HotkeyList.SelectedItem = match;
                }
                else
                {
                    HotkeyList.SelectedItem = custom;
                    StepValue.Text = step.Value ?? string.Empty;
                }
            }
            else if (step.Type == MacroStepType.MouseClick)
            {
                ClickModeList.SelectedIndex = step.Value == "InProgram" ? 0 : 1;
                if (step.Value != "InProgram") StepValue.Text = step.X + "," + step.Y;
            }
        }
        private void StepType_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (StepHelp == null || StepType.SelectedItem is not ComboBoxItem item) return;
            var type = item.Content.ToString();
            HotkeyList.Visibility = type == "Hotkey" ? Visibility.Visible : Visibility.Collapsed;
            ClickModeList.Visibility = type == "MouseClick" ? Visibility.Visible : Visibility.Collapsed;
            StepValue.Visibility = type == "Hotkey" ? Visibility.Collapsed : Visibility.Visible;
            if (type == "Hotkey" && HotkeyList.SelectedIndex < 0) HotkeyList.SelectedIndex = 0;
            if (type == "MouseClick" && ClickModeList.SelectedIndex < 0) ClickModeList.SelectedIndex = 0;
            BrowseProgramButton.Visibility = type == "RunProgram" || type == "Screenshot" ? Visibility.Visible : Visibility.Collapsed;
            CheckWindowButton.Visibility = type == "FindWindow" ? Visibility.Visible : Visibility.Collapsed;
            SetClickAoiButton.Visibility = type == "MouseClick" ? Visibility.Visible : Visibility.Collapsed;
            StepDelay.IsEnabled = type == "Delay";
            StepValue.IsEnabled = type != "Delay" && type != "ActivateWindow";
            StepHelp.Text = type switch {
                "Hotkey" => "단축키 조합을 입력하세요. 예: WIN+D, CTRL+SHIFT+S, ALT+F4",
                "TextInput" => "활성화된 입력란에 입력할 문자열을 입력하세요.",
                "MouseClick" => "InProgram은 AOI 중앙을 클릭합니다. 좌표 입력을 선택하면 Target 기준 X,Y를 사용합니다.",
                "Delay" => "오른쪽 칸에 대기시간(ms)을 입력하세요. 10초는 10000입니다.",
                "FindWindow" => @"창 제목 또는 실행 파일 전체 경로를 입력하세요. 예: C:\ProgramData\Smilegate\STOVE\STOVE.exe",
                "RunProgram" => "실행 파일의 전체 경로가 필요합니다. Browse 버튼으로 .exe를 선택하세요.",
                "ActivateWindow" => "앞선 FindWindow에서 찾은 창을 활성화합니다. 별도 값은 필요 없습니다.",
                "Screenshot" => "스크린샷을 저장할 폴더를 입력하거나 Browse로 선택하세요. 선택 폴더 아래 날짜별 폴더에 PNG로 저장됩니다.",
                _ => ""
            };
        }
        private void ClickModeList_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (StepValue == null || ClickModeList.SelectedItem is not ComboBoxItem item) return;
            var mode = item.Tag?.ToString();
            if (mode == "InProgram")
            {
                StepValue.Text = "InProgram";
                StepValue.Visibility = Visibility.Collapsed;
            }
            else
            {
                StepValue.Text = "";
                StepValue.Visibility = Visibility.Visible;
                StepValue.IsEnabled = true;
                StepValue.Focus();
            }
        }
        private void HotkeyList_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (StepValue == null || HotkeyList.SelectedItem is not ComboBoxItem item) return;
            var value = item.Tag?.ToString();
            if (value == "CUSTOM")
            {
                HotkeyList.Visibility = Visibility.Collapsed;
                StepValue.Visibility = Visibility.Visible;
                StepValue.IsEnabled = true;
                StepValue.Focus();
                StepHelp.Text = "직접 조합을 입력하세요. WIN, CTRL, ALT, SHIFT를 사용할 수 있습니다. 예: CTRL+ALT+F";
                return;
            }
            StepValue.Text = value ?? "";
        }
        private void BrowseProgram_Click(object s, RoutedEventArgs e)
        {
            if (StepType.SelectedItem is ComboBoxItem selectedType && selectedType.Content?.ToString() == "Screenshot")
            {
                var folderDialog = new OpenFolderDialog
                {
                    Title = "스크린샷 저장 폴더 선택",
                    Multiselect = false
                };
                if (folderDialog.ShowDialog(this) == true) StepValue.Text = folderDialog.FolderName;
                return;
            }

            var dialog = new OpenFileDialog { Title = "실행할 프로그램 선택", Filter = "Programs|*.exe;*.bat;*.cmd|All files|*.*" };
            if (dialog.ShowDialog(this) == true) StepValue.Text = dialog.FileName;
        }
        private void CheckWindow_Click(object s, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(StepValue.Text)) { MessageBox.Show(this, "확인할 창 제목 또는 실행 파일 경로를 입력하세요."); return; }
            var found = _windowChecker(StepValue.Text);
            AppendTestLog("FIND WINDOW CHECK  " + (found ? "OK" : "NG") + "  " + StepValue.Text);
            MessageBox.Show(this, found ? "실행 중인 창을 찾았습니다." : "실행 중인 창을 찾지 못했습니다.", "Find Window Check", MessageBoxButton.OK, found ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        private async void SetClickAoi_Click(object s, RoutedEventArgs e)
        {
            if (Current == null || StepGrid.SelectedItem is not MacroStepDefinition step || step.Type != MacroStepType.MouseClick)
            {
                MessageBox.Show(this, "먼저 목록에서 MouseClick 단계를 선택하세요."); return;
            }
            var index = Current.Steps.IndexOf(step);
            var findStep = Current.Steps.Take(index).LastOrDefault(item => item.Type == MacroStepType.FindWindow);
            if (findStep == null) { MessageBox.Show(this, "MouseClick 앞에 FindWindow 단계를 추가하세요."); return; }
            try
            {
                var capture = await _windowCapture(findStep.Value);
                var picker = new AoiPickerWindow(capture.PngBytes, "Set Macro Click AOI", "Drag Click AOI On Program", "클릭할 영역을 드래그한 다음 OK를 누르세요.",
                    step.ClickRegionX, step.ClickRegionY, step.ClickRegionWidth, step.ClickRegionHeight) { Owner = this };
                if (picker.ShowDialog() == true)
                {
                    step.Value = "InProgram"; step.ClickRegionX = picker.SelectedX; step.ClickRegionY = picker.SelectedY;
                    step.ClickRegionWidth = picker.SelectedWidth; step.ClickRegionHeight = picker.SelectedHeight;
                    StepGrid.Items.Refresh(); _save();
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Set AOI 실패"); }
        }
        private void AddStep_Click(object s, RoutedEventArgs e)
        {
            if(Current==null) return;
            var type=Enum.Parse<MacroStepType>(((ComboBoxItem)StepType.SelectedItem).Content.ToString());
            if(type==MacroStepType.RunProgram && !IsRunnableProgram(StepValue.Text)) { MessageBox.Show(this,"Windows 기본 명령을 입력하거나 Browse로 실제 실행 파일을 선택하세요.","Run Program"); return; }
            if(type==MacroStepType.MouseClick && StepValue.Text!="InProgram" && (StepValue.Text.Split(',').Length!=2)) { MessageBox.Show(this,"InProgram을 선택하거나 좌표를 X,Y 형식으로 입력하세요. 예: 200,150","Mouse Click"); return; }
            var step=new MacroStepDefinition { Type=type, Value=StepValue.Text };
            step.Condition = ReadCondition();
            if(type==MacroStepType.FindWindow) step.ContinueWhenNotFound=true;
            if(int.TryParse(StepDelay.Text,out var delay)) step.DelayMilliseconds=Math.Max(0,delay);
            if(type==MacroStepType.MouseClick && StepValue.Text!="InProgram") { var p=StepValue.Text.Split(','); if(p.Length==2){ int.TryParse(p[0],out var x); int.TryParse(p[1],out var y); step.X=x; step.Y=y; } }
            Current.Steps.Add(step); StepGrid.Items.Refresh();
        }
        private void UpdateStep_Click(object s, RoutedEventArgs e)
        {
            if (StepGrid.SelectedItem is not MacroStepDefinition step || StepType.SelectedItem is not ComboBoxItem item) return;
            var type = Enum.Parse<MacroStepType>(item.Content.ToString());
            if (type == MacroStepType.RunProgram && !IsRunnableProgram(StepValue.Text)) { MessageBox.Show(this,"Windows 기본 명령을 입력하거나 Browse로 실행 파일을 선택하세요."); return; }
            step.Type=type; step.Value=StepValue.Text;
            step.Condition = ReadCondition();
            if(int.TryParse(StepDelay.Text,out var delay)) step.DelayMilliseconds=Math.Max(0,delay);
            if(type==MacroStepType.FindWindow) step.ContinueWhenNotFound=true;
            if(type==MacroStepType.MouseClick && StepValue.Text!="InProgram") { var p=StepValue.Text.Split(','); if(p.Length!=2||!int.TryParse(p[0],out var x)||!int.TryParse(p[1],out var y)){MessageBox.Show(this,"좌표를 X,Y 형식으로 입력하세요.");return;} step.X=x;step.Y=y; }
            StepGrid.Items.Refresh();
        }
        private static bool IsRunnableProgram(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (System.IO.File.Exists(value)) return true;
            var command = value.Trim().ToLowerInvariant();
            return command is "notepad" or "notepad.exe" or "calc" or "calc.exe" or "mspaint" or "mspaint.exe" or "explorer" or "explorer.exe" or "cmd" or "cmd.exe" or "powershell" or "powershell.exe";
        }
        private MacroStepCondition ReadCondition()
        {
            return StepCondition.SelectedItem is ComboBoxItem item &&
                   Enum.TryParse<MacroStepCondition>(item.Tag?.ToString(), out var condition)
                ? condition : MacroStepCondition.Always;
        }
        private void SaveMacros_Click(object s, RoutedEventArgs e) { _save(); MessageBox.Show(this,"매크로를 저장했습니다.","Macros"); }
        private void DeleteStep_Click(object s, RoutedEventArgs e) { if(Current!=null && StepGrid.SelectedItem is MacroStepDefinition x) Current.Steps.Remove(x); }
        private void MoveUp_Click(object s, RoutedEventArgs e) { Move(-1); }
        private void MoveDown_Click(object s, RoutedEventArgs e) { Move(1); }
        private void Move(int d) { if(Current==null||StepGrid.SelectedItem is not MacroStepDefinition x)return; var i=Current.Steps.IndexOf(x); var n=i+d;if(n<0||n>=Current.Steps.Count)return;Current.Steps.Move(i,n);StepGrid.SelectedItem=x; }
        private async void TestStart_Click(object s, RoutedEventArgs e)
        {
            if (Current == null) { MessageBox.Show(this, "테스트할 매크로를 선택하세요."); return; }
            _testCancellation = new CancellationTokenSource();
            MacroTestLog.Clear();
            TestStartButton.IsEnabled = false; TestStopButton.IsEnabled = true;
            try { await _testRunner(Current, _testCancellation.Token, AppendTestLog); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "매크로 테스트 실패"); }
            finally { TestStartButton.IsEnabled = true; TestStopButton.IsEnabled = false; _testCancellation.Dispose(); _testCancellation = null; }
        }
        private void TestStop_Click(object s, RoutedEventArgs e) { _testCancellation?.Cancel(); }
        private void AppendTestLog(string message)
        {
            MacroTestLog.AppendText(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine);
            MacroTestLog.ScrollToEnd();
        }
    }
}

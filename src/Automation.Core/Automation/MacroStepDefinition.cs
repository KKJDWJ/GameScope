using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Automation.Core.Automation
{
    public sealed class MacroStepDefinition : INotifyPropertyChanged
    {
        private bool _isExecuting;
        public MacroStepDefinition()
        {
            Type = MacroStepType.Delay;
            DelayMilliseconds = 1000;
        }

        public MacroStepType Type { get; set; }
        public string Value { get; set; }
        public string Arguments { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int DelayMilliseconds { get; set; }
        public int ClickRegionX { get; set; }
        public int ClickRegionY { get; set; }
        public int ClickRegionWidth { get; set; }
        public int ClickRegionHeight { get; set; }
        public bool ContinueWhenNotFound { get; set; }
        public MacroStepCondition Condition { get; set; }
        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                if (_isExecuting == value) return;
                _isExecuting = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExecuting)));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        public string Summary
        {
            get
            {
                return Type switch
                {
                    MacroStepType.Hotkey => "Hotkey · " + Value,
                    MacroStepType.TextInput => "Text · " + Value,
                    MacroStepType.MouseClick => Value == "InProgram"
                        ? "Click · InProgram · AOI " + ClickRegionX + "," + ClickRegionY + "," + ClickRegionWidth + "," + ClickRegionHeight
                        : "Click · " + X + ", " + Y,
                    MacroStepType.Delay => "Delay · " + DelayMilliseconds + " ms",
                    MacroStepType.FindWindow => "Find Window · " + Value,
                    MacroStepType.RunProgram => "Run · " + Value,
                    MacroStepType.ActivateWindow => "Activate found window",
                    MacroStepType.Screenshot => "Screenshot target window · " +
                        (string.IsNullOrWhiteSpace(Value) ? "Default folder" : Value),
                    _ => Type.ToString()
                };
            }
        }
    }
}

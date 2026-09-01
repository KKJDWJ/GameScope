using Automation.Core.Windows;

namespace Automation.Core.Automation
{
    public sealed class AoiDefinition
    {
        public AoiDefinition()
        {
            Name = "AOI";
            Width = 100;
            Height = 100;
            TriggerType = AoiTriggerType.ImageMatch;
            ActionType = AoiActionType.None;
            ImageThreshold = 0.9;
            IsEnabled = true;
            ActionDelayMilliseconds = 1000;
            MacroName = "None";
            LastResult = "-";
        }

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
        public string ActionValue { get; set; }
        public int ActionX { get; set; }
        public int ActionY { get; set; }
        public int ActionDelayMilliseconds { get; set; }
        public string LastResult { get; set; }
        public string MacroId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string MacroName { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public System.Collections.Generic.IEnumerable<string> DetailItems
        {
            get
            {
                yield return "Trigger: " + TriggerType;
                yield return "Macro: " + (string.IsNullOrWhiteSpace(MacroName) ? "None" : MacroName);
                yield return "Last: " + (string.IsNullOrWhiteSpace(LastResult) ? "-" : LastResult);
            }
        }

        public bool IsTriggerLatched { get; set; }

        public WindowBounds ToRelativeBounds()
        {
            return new WindowBounds(X, Y, Width, Height);
        }
    }
}

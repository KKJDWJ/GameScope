using System;
using System.Collections.ObjectModel;

namespace Automation.Core.Automation
{
    public sealed class MacroDefinition
    {
        public MacroDefinition()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "New Macro";
            Steps = new ObservableCollection<MacroStepDefinition>();
            RepeatCount = 1;
            RepeatDelayMilliseconds = 5000;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public ObservableCollection<MacroStepDefinition> Steps { get; set; }
        public int RepeatCount { get; set; }
        public int RepeatDelayMilliseconds { get; set; }
    }
}

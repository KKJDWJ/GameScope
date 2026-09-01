using System.Collections.Generic;
using Automation.Core.Windows;

namespace Automation.Core.Automation
{
    public sealed class AutomationProfile
    {
        public AutomationProfile()
        {
            Aois = new List<AoiDefinition>();
        }

        public string Name { get; set; }
        public WindowInfo TargetWindow { get; set; }
        public List<AoiDefinition> Aois { get; private set; }
    }
}

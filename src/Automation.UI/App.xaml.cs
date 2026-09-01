using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Automation.UI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Some Windows/GPU driver combinations create the WPF window but fail
            // to compose its visual tree, leaving an entirely blank client area.
            // This application does not need GPU rendering, so prefer the stable
            // software rendering path.
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            base.OnStartup(e);
        }
    }
}

namespace Automation.Core.Capture
{
    public interface ICaptureService
    {
        CaptureResult Capture(CaptureTarget target);
    }
}

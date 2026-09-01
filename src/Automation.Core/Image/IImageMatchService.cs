namespace Automation.Core.Image
{
    public interface IImageMatchService
    {
        ImageMatchResult Find(byte[] sourcePngBytes, string templateImagePath, ImageMatchOptions options);
    }
}

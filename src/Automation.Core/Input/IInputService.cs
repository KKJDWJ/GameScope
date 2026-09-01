namespace Automation.Core.Input
{
    public interface IInputService
    {
        void MoveMouse(int x, int y);
        void Click(int x, int y);
        void DoubleClick(int x, int y);
        void TypeText(string text);
        void HotKey(params string[] keys);
    }
}

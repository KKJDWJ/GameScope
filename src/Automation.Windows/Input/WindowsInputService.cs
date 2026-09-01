using System;
using System.Linq;
using System.Threading;
using Automation.Core.Input;

namespace Automation.Windows.Input
{
    public sealed class WindowsInputService : IInputService
    {
        private const uint LeftDown = 0x0002;
        private const uint LeftUp = 0x0004;

        public void MoveMouse(int x, int y)
        {
            NativeMethods.SetCursorPos(x, y);
        }

        public void Click(int x, int y)
        {
            MoveMouse(x, y);
            NativeMethods.mouse_event(LeftDown, 0, 0, 0, UIntPtr.Zero);
            NativeMethods.mouse_event(LeftUp, 0, 0, 0, UIntPtr.Zero);
        }

        public void DoubleClick(int x, int y)
        {
            Click(x, y);
            Thread.Sleep(80);
            Click(x, y);
        }

        public void TypeText(string text)
        {
            System.Windows.Forms.SendKeys.SendWait(text);
        }

        public void HotKey(params string[] keys)
        {
            var hasWindows = keys.Any(key => string.Equals(key, "WIN", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(key, "WINDOWS", StringComparison.OrdinalIgnoreCase));
            if (hasWindows) NativeMethods.keybd_event(0x5B, 0, 0, UIntPtr.Zero);
            try
            {
                var sequence = string.Concat(keys.Where(IsSendKeysModifier).Select(ToSendKeysToken)) +
                               string.Concat(keys.Where(key => !IsSendKeysModifier(key) && !IsWindowsKey(key)).Select(ToSendKeysToken));
                if (!string.IsNullOrEmpty(sequence)) System.Windows.Forms.SendKeys.SendWait(sequence);
            }
            finally
            {
                if (hasWindows) NativeMethods.keybd_event(0x5B, 0, 0x0002, UIntPtr.Zero);
            }
        }

        private static bool IsWindowsKey(string key) => key.Equals("WIN", StringComparison.OrdinalIgnoreCase) || key.Equals("WINDOWS", StringComparison.OrdinalIgnoreCase);
        private static bool IsSendKeysModifier(string key) => key.Equals("CTRL", StringComparison.OrdinalIgnoreCase) || key.Equals("CONTROL", StringComparison.OrdinalIgnoreCase) || key.Equals("ALT", StringComparison.OrdinalIgnoreCase) || key.Equals("SHIFT", StringComparison.OrdinalIgnoreCase);

        private static string ToSendKeysToken(string key)
        {
            switch (key.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    return "^";
                case "ALT":
                    return "%";
                case "SHIFT":
                    return "+";
                case "ENTER":
                    return "{ENTER}";
                case "TAB":
                    return "{TAB}";
                case "ESC":
                case "ESCAPE":
                    return "{ESC}";
                default:
                    return key;
            }
        }
    }
}

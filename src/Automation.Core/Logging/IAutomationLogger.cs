using System;

namespace Automation.Core.Logging
{
    public interface IAutomationLogger
    {
        void Write(AutomationLogLevel level, string message, Exception exception = null);
    }
}

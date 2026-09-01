using System;

namespace Automation.Core.Logging
{
    public sealed class AutomationLogEntry
    {
        public AutomationLogEntry(DateTimeOffset timestamp, AutomationLogLevel level, string message, Exception exception = null)
        {
            Timestamp = timestamp;
            Level = level;
            Message = message;
            Exception = exception;
        }

        public DateTimeOffset Timestamp { get; private set; }
        public AutomationLogLevel Level { get; private set; }
        public string Message { get; private set; }
        public Exception Exception { get; private set; }
    }
}

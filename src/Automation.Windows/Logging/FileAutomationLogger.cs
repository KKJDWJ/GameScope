using System;
using System.IO;
using Automation.Core.Logging;

namespace Automation.Windows.Logging
{
    public sealed class FileAutomationLogger : IAutomationLogger
    {
        private readonly string _filePath;
        private readonly object _sync = new object();

        public FileAutomationLogger(string filePath)
        {
            _filePath = filePath;

            var directory = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(string.IsNullOrEmpty(directory) ? "." : directory);
        }

        public void Write(AutomationLogLevel level, string message, Exception exception = null)
        {
            var entry = new AutomationLogEntry(DateTimeOffset.Now, level, message, exception);
            var line = Format(entry);

            lock (_sync)
            {
                File.AppendAllText(_filePath, line + Environment.NewLine);
            }
        }

        private static string Format(AutomationLogEntry entry)
        {
            var text = string.Format("[{0:yyyy-MM-dd HH:mm:ss.fff zzz}] {1}: {2}", entry.Timestamp, entry.Level, entry.Message);
            return entry.Exception == null ? text : text + Environment.NewLine + entry.Exception;
        }
    }
}

using System.IO;

namespace b2b_support_tool.Infrastructure
{
    public class SupportLogger : ISupportLogger
    {
        private const long MaxLogSizeBytes = 5_000_000;

        private readonly string _logPath;
        private readonly Action<string> _appendToUi;

        public SupportLogger(string logPath, Action<string> appendToUi)
        {
            _logPath = logPath;
            _appendToUi = appendToUi;
        }

        public void Initialize()
        {
            if (!File.Exists(_logPath))
                return;

            var info = new FileInfo(_logPath);

            if (info.Length > MaxLogSizeBytes)
            {
                File.Delete(_logPath);
            }
        }

        public void Write(string text)
        {
            text = NormalizeText(text);
            var now = DateTime.Now;
            string logLine = $"[{now:yyyy-MM-dd HH:mm:ss}] {text}";
            string uiLine = $"[{now:HH:mm:ss}] {text}";

            _appendToUi(uiLine);

            try
            {
                File.AppendAllText(_logPath, logLine + Environment.NewLine);
            }
            catch
            {
                // Logging must never break support actions.
            }
        }

        private static string NormalizeText(string text)
        {
            return text
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
        }
    }
}

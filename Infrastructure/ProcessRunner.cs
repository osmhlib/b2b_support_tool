using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace b2b_support_tool.Infrastructure
{
    public class ProcessRunner
    {
        private static readonly Regex ProgressRegex = new(@"(?<percent>\d{1,3}(?:[.,]\d+)?)\s*%", RegexOptions.Compiled);

        private readonly ISupportLogger _logger;
        private string _lastProgressPercent = "";

        public ProcessRunner(ISupportLogger logger)
        {
            _logger = logger;
        }

        public async Task RunAsync(
            string fileName,
            string arguments,
            bool logStandardOutput = true,
            bool throwOnNonZeroExit = true)
        {
            _lastProgressPercent = "";

            await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = GetConsoleOutputEncoding(),
                    StandardErrorEncoding = GetConsoleOutputEncoding(),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = new Process
                {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null)
                        return;

                    foreach (var line in NormalizeOutput(e.Data, logStandardOutput))
                    {
                        _logger.Write(line);
                    }
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                        _logger.Write("ERROR: " + e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                _logger.Write($"Process exited with code {process.ExitCode}");

                if (throwOnNonZeroExit && process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"{fileName} exited with code {process.ExitCode}.");
                }
            });
        }

        public void RunBlocking(string fileName, string arguments, bool throwOnNonZeroExit = true)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(psi);
            if (process == null)
            {
                _logger.Write("ERROR: Failed to start process.");
                return;
            }

            process.WaitForExit();

            _logger.Write($"Exit code: {process.ExitCode}");

            if (throwOnNonZeroExit && process.ExitCode != 0)
            {
                throw new InvalidOperationException($"{fileName} exited with code {process.ExitCode}.");
            }
        }

        private IEnumerable<string> NormalizeOutput(string output, bool includeStandardText)
        {
            foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (TryFormatProgress(line, out var progressLine))
                {
                    if (!string.IsNullOrEmpty(progressLine))
                    {
                        yield return progressLine;
                    }

                    continue;
                }

                if (includeStandardText)
                {
                    yield return line;
                }
            }
        }

        private bool TryFormatProgress(string line, out string progressLine)
        {
            progressLine = "";

            if (!IsProgressBarLine(line))
                return false;

            var match = ProgressRegex.Match(line);
            if (!match.Success)
                return false;

            string percent = match.Groups["percent"].Value.Replace(',', '.');
            if (percent == _lastProgressPercent)
                return true;

            _lastProgressPercent = percent;
            progressLine = $"Progress: {percent}%";
            return true;
        }

        private static bool IsProgressBarLine(string line)
        {
            return line.Contains('%')
                && line.Contains('[')
                && line.Contains(']')
                && line.Contains('=');
        }

        private static Encoding GetConsoleOutputEncoding()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
            }
            catch
            {
                return Encoding.Default;
            }
        }
    }
}

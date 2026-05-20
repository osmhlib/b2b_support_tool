using System.Diagnostics;

namespace b2b_support_tool.Infrastructure
{
    public class ProcessRunner
    {
        private readonly ISupportLogger _logger;
        private string _lastOutputLine = "";

        public ProcessRunner(ISupportLogger logger)
        {
            _logger = logger;
        }

        public async Task RunAsync(string fileName, string arguments)
        {
            _lastOutputLine = "";

            await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
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

                    string line = e.Data.Trim();

                    if (string.IsNullOrWhiteSpace(line))
                        return;

                    if (line.Contains("%"))
                    {
                        if (line != _lastOutputLine)
                        {
                            _lastOutputLine = line;
                            _logger.Write(line);
                        }
                    }
                    else
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
            });
        }

        public void RunBlocking(string fileName, string arguments)
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
        }
    }
}

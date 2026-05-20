using System.Diagnostics;
using System.IO;
using b2b_support_tool.Infrastructure;

namespace b2b_support_tool.Services
{
    public class ModuleService
    {
        private readonly ISupportLogger _logger;

        public ModuleService(ISupportLogger logger)
        {
            _logger = logger;
        }

        public Task RestartAsync(string displayName, string processName, string executablePath)
        {
            return Task.Run(() =>
            {
                try
                {
                    _logger.Write($"Searching for running {displayName}...");

                    var processes = Process.GetProcessesByName(processName);

                    if (processes.Length == 0)
                    {
                        _logger.Write("No running instances found.");
                    }
                    else
                    {
                        foreach (var proc in processes)
                        {
                            try
                            {
                                _logger.Write($"Stopping PID {proc.Id}...");
                                proc.Kill();
                                proc.WaitForExit();
                                _logger.Write("Stopped successfully.");
                            }
                            catch (Exception ex)
                            {
                                _logger.Write("ERROR stopping process: " + ex.Message);
                            }
                        }
                    }

                    _logger.Write("Waiting 2 seconds...");
                    Thread.Sleep(2000);

                    string exePath = ResolveExecutablePath(executablePath);

                    if (!File.Exists(exePath))
                    {
                        _logger.Write("ERROR: Executable not found.");
                        return;
                    }

                    _logger.Write($"Starting {displayName}...");
                    Process.Start(exePath);

                    _logger.Write("Restart completed.");
                }
                catch (Exception ex)
                {
                    _logger.Write("ERROR: " + ex.Message);
                }
            });
        }

        private static string ResolveExecutablePath(string executablePath)
        {
            if (File.Exists(executablePath))
                return executablePath;

            if (!Path.HasExtension(executablePath))
            {
                string pathWithExeExtension = executablePath + ".exe";

                if (File.Exists(pathWithExeExtension))
                    return pathWithExeExtension;
            }

            return executablePath;
        }
    }
}

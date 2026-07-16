using System.IO;
using b2b_support_tool.Infrastructure;

namespace b2b_support_tool.Services
{
    public class WindowsActivationService
    {
        private readonly ISupportLogger _logger;
        private readonly EmbeddedResourceExtractor _resourceExtractor;
        private readonly ProcessRunner _processRunner;

        public WindowsActivationService(
            ISupportLogger logger,
            EmbeddedResourceExtractor resourceExtractor,
            ProcessRunner processRunner)
        {
            _logger = logger;
            _resourceExtractor = resourceExtractor;
            _processRunner = processRunner;
        }

        public async Task ActivateAsync()
        {
            string scriptPath = Path.Combine(
                Path.GetTempPath(),
                "regwin_script.ps1"
            );

            _resourceExtractor.Extract("b2b_support_tool.Resources.regwin_script.ps1", scriptPath);

            _logger.Write("Starting PowerShell script...");

            await _processRunner.RunAsync(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                logStandardOutput: false
            );

            TryDeleteFile(scriptPath);
        }

        private void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    _logger.Write("Temporary script deleted.");
                }
            }
            catch
            {
                _logger.Write("Could not delete temporary script.");
            }
        }
    }
}

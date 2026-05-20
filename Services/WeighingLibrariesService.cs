using System.IO;
using b2b_support_tool.Infrastructure;

namespace b2b_support_tool.Services
{
    public class WeighingLibrariesService
    {
        private readonly ISupportLogger _logger;
        private readonly EmbeddedResourceExtractor _resourceExtractor;
        private readonly ProcessRunner _processRunner;

        public WeighingLibrariesService(
            ISupportLogger logger,
            EmbeddedResourceExtractor resourceExtractor,
            ProcessRunner processRunner)
        {
            _logger = logger;
            _resourceExtractor = resourceExtractor;
            _processRunner = processRunner;
        }

        public Task RegisterAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    string targetPath = @"C:\Windows\SysWOW64";

                    _logger.Write("Extracting DLLs...");

                    _resourceExtractor.Extract(
                        "b2b_support_tool.Resources.DigiSM.dll",
                        Path.Combine(targetPath, "DigiSM.dll"));

                    _resourceExtractor.Extract(
                        "b2b_support_tool.Resources.ShopdeskTools.dll",
                        Path.Combine(targetPath, "ShopdeskTools.dll"));

                    _logger.Write("Registering DigiSM.dll...");
                    _processRunner.RunBlocking("regsvr32", "/s \"C:\\Windows\\SysWOW64\\DigiSM.dll\"");

                    _logger.Write("Registering ShopdeskTools.dll...");
                    _processRunner.RunBlocking("regsvr32", "/s \"C:\\Windows\\SysWOW64\\ShopdeskTools.dll\"");

                    _logger.Write("DLL registration completed.");
                }
                catch (Exception ex)
                {
                    _logger.Write("ERROR: " + ex.Message);
                }
            });
        }
    }
}

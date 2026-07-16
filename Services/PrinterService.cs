using b2b_support_tool.Infrastructure;

namespace b2b_support_tool.Services
{
    public class PrinterService
    {
        private readonly ISupportLogger _logger;
        private readonly ProcessRunner _processRunner;

        public PrinterService(ISupportLogger logger, ProcessRunner processRunner)
        {
            _logger = logger;
            _processRunner = processRunner;
        }

        public async Task RestartAsync()
        {
            _logger.Write("Stopping spooler...");
            await _processRunner.RunAsync("net", "stop spooler", logStandardOutput: true, throwOnNonZeroExit: false);

            _logger.Write("Clearing print queue...");
            await _processRunner.RunAsync("cmd.exe", "/c del /f /q %systemroot%\\system32\\spool\\PRINTERS\\*.*", logStandardOutput: true, throwOnNonZeroExit: false);

            _logger.Write("Starting spooler...");
            await _processRunner.RunAsync("net", "start spooler", logStandardOutput: true, throwOnNonZeroExit: false);

            _logger.Write("Printer service restarted.");
        }
    }
}

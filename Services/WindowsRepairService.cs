using b2b_support_tool.Infrastructure;

namespace b2b_support_tool.Services
{
    public class WindowsRepairService
    {
        private readonly ISupportLogger _logger;
        private readonly ProcessRunner _processRunner;

        public WindowsRepairService(ISupportLogger logger, ProcessRunner processRunner)
        {
            _logger = logger;
            _processRunner = processRunner;
        }

        public async Task RunSfcAsync()
        {
            _logger.Write("Starting SFC scan...");
            await _processRunner.RunAsync("cmd.exe", "/c sfc /scannow");
        }

        public async Task RunDismAsync()
        {
            _logger.Write("Starting DISM restore health...");
            await _processRunner.RunAsync("cmd.exe", "/c DISM /Online /Cleanup-Image /RestoreHealth");
        }

        public async Task ScheduleChkdskAsync()
        {
            _logger.Write("Scheduling CHKDSK...");
            await _processRunner.RunAsync("cmd.exe", "/c echo Y | chkdsk C: /f /r");
        }
    }
}

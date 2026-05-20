using b2b_support_tool.Infrastructure;

namespace b2b_support_tool.Services
{
    public class NetworkDiagnosticsService
    {
        private readonly ProcessRunner _processRunner;

        public NetworkDiagnosticsService(ProcessRunner processRunner)
        {
            _processRunner = processRunner;
        }

        public async Task RunAsync(bool pingFtp, bool traceFtp, bool pingCrm, bool traceCrm)
        {
            if (pingFtp)
                await _processRunner.RunAsync("ping", "ftp.base2base.com.ua");

            if (traceFtp)
                await _processRunner.RunAsync("tracert", "ftp.base2base.com.ua");

            if (pingCrm)
                await _processRunner.RunAsync("ping", "crm.base2base.com.ua");

            if (traceCrm)
                await _processRunner.RunAsync("tracert", "crm.base2base.com.ua");
        }
    }
}

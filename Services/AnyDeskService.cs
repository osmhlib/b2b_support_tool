using System.Diagnostics;
using System.IO;
using b2b_support_tool.Infrastructure;

namespace b2b_support_tool.Services
{
    public class AnyDeskService
    {
        private readonly ISupportLogger _logger;

        public AnyDeskService(ISupportLogger logger)
        {
            _logger = logger;
        }

        public Task RenewIdAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    _logger.Write("Stopping AnyDesk...");
                    KillProcess("AnyDesk");

                    string programData = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "AnyDesk");

                    string appData = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "AnyDesk");

                    string backupDir = Path.Combine(programData, "backup");

                    if (!Directory.Exists(programData))
                    {
                        _logger.Write("ProgramData AnyDesk folder not found.");
                        return;
                    }

                    string userConf = Path.Combine(appData, "user.conf");
                    if (File.Exists(userConf))
                    {
                        Directory.CreateDirectory(backupDir);
                        File.Copy(userConf, Path.Combine(backupDir, "user.conf"), true);
                        _logger.Write("Backup created.");
                    }

                    foreach (var file in Directory.GetFiles(programData))
                    {
                        try
                        {
                            File.Delete(file);
                            _logger.Write($"Deleted: {file}");
                        }
                        catch { }
                    }

                    if (Directory.Exists(appData))
                    {
                        foreach (var file in Directory.GetFiles(appData))
                        {
                            try
                            {
                                File.Delete(file);
                                _logger.Write($"Deleted: {file}");
                            }
                            catch { }
                        }
                    }

                    _logger.Write("Starting AnyDesk...");
                    Process.Start(@"C:\Program Files (x86)\AnyDesk\AnyDesk.exe");

                    Thread.Sleep(5000);

                    _logger.Write("Stopping AnyDesk again...");
                    KillProcess("AnyDesk");

                    string backupConf = Path.Combine(backupDir, "user.conf");
                    if (File.Exists(backupConf))
                    {
                        Directory.CreateDirectory(appData);
                        File.Copy(backupConf, Path.Combine(appData, "user.conf"), true);
                        _logger.Write("Configuration restored.");
                    }

                    _logger.Write("Starting AnyDesk final time...");
                    Process.Start(@"C:\Program Files (x86)\AnyDesk\AnyDesk.exe");

                    _logger.Write("AnyDesk ID renew completed.");
                }
                catch (Exception ex)
                {
                    _logger.Write("ERROR: " + ex.Message);
                }
            });
        }

        private void KillProcess(string processName)
        {
            foreach (var proc in Process.GetProcessesByName(processName))
            {
                try
                {
                    proc.Kill();
                    _logger.Write($"Killed: {proc.ProcessName}");
                }
                catch { }
            }
        }
    }
}

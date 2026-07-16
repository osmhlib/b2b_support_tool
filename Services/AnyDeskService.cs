using System.Diagnostics;
using System.IO;
using b2b_support_tool.Infrastructure;

namespace b2b_support_tool.Services
{
    public class AnyDeskService
    {
        private static readonly string[] AnyDeskExecutablePaths =
        {
            @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe",
            @"C:\Program Files\AnyDesk\AnyDesk.exe"
        };

        private readonly ISupportLogger _logger;

        public AnyDeskService(ISupportLogger logger)
        {
            _logger = logger;
        }

        public Task RenewIdAsync()
        {
            return Task.Run(() =>
            {
                string? backupConf = null;
                string? appData = null;

                try
                {
                    string? anyDeskPath = ResolveAnyDeskExecutablePath();
                    if (anyDeskPath == null)
                    {
                        _logger.Write("ERROR: AnyDesk executable not found.");
                        return;
                    }

                    _logger.Write($"Using AnyDesk executable: {anyDeskPath}");

                    _logger.Write("Stopping AnyDesk...");
                    KillProcess("AnyDesk");

                    string programData = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "AnyDesk");

                    appData = Path.Combine(
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
                        backupConf = Path.Combine(backupDir, "user.conf");
                        File.Copy(userConf, backupConf, true);
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
                    StartAnyDesk(anyDeskPath);

                    Thread.Sleep(5000);

                    _logger.Write("Stopping AnyDesk again...");
                    KillProcess("AnyDesk");

                    RestoreConfiguration(backupConf, appData);
                    backupConf = null;

                    _logger.Write("Starting AnyDesk final time...");
                    StartAnyDesk(anyDeskPath);

                    _logger.Write("AnyDesk ID renew completed.");
                }
                catch
                {
                    _logger.Write("ERROR: AnyDesk ID renew failed.");
                }
                finally
                {
                    RestoreConfiguration(backupConf, appData);
                }
            });
        }

        private static void StartAnyDesk(string anyDeskPath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = anyDeskPath,
                WorkingDirectory = Path.GetDirectoryName(anyDeskPath) ?? Environment.CurrentDirectory,
                UseShellExecute = true
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

        private static string? ResolveAnyDeskExecutablePath(string? preferredPath = null)
        {
            return GetAnyDeskPathCandidates(preferredPath).FirstOrDefault(File.Exists);
        }

        private static IEnumerable<string> GetAnyDeskPathCandidates(string? preferredPath)
        {
            if (!string.IsNullOrWhiteSpace(preferredPath))
            {
                yield return preferredPath;
            }

            foreach (var path in AnyDeskExecutablePaths)
            {
                yield return path;
            }
        }

        private void RestoreConfiguration(string? backupConf, string? appData)
        {
            if (string.IsNullOrWhiteSpace(backupConf)
                || string.IsNullOrWhiteSpace(appData)
                || !File.Exists(backupConf))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(appData);
                File.Copy(backupConf, Path.Combine(appData, "user.conf"), true);
                _logger.Write("Configuration restored.");
            }
            catch
            {
                _logger.Write("ERROR: Configuration restore failed.");
            }
        }
    }
}

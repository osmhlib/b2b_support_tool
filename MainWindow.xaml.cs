using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace b2b_support_tool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            clean.Click += async (s, e) =>
            {
                outputBox.Clear();
                clean.IsEnabled = false;

                // Зчитуємо значення в UI потоці
                bool deleteChecks = delCheck.IsChecked == true;
                bool deleteLogs = delLog.IsChecked == true;
                bool deleteTemp = delTemp.IsChecked == true;
                bool deleteRecycle = delRecyclebin.IsChecked == true;

                try
                {
                    await Task.Run(() =>
                    {
                        if (deleteChecks)
                            DeleteOldFiles(@"C:\Shopdesk\offline\grr\arh", "*.*", 14);

                        if (deleteLogs)
                            DeleteOldFiles(@"C:\Shopdesk", "*.log", 14, true);

                        if (deleteTemp)
                            CleanTemp();

                        if (deleteRecycle)
                            EmptyRecycleBin();
                    });
                }
                catch (Exception ex)
                {
                    AppendOutput("CRITICAL ERROR: " + ex.Message);
                }

                clean.IsEnabled = true;
                AppendOutput("Cleaning finished.");
            };

            netTest.Click += async (s, e) =>
            {
                outputBox.Clear();

                if (pingFtp.IsChecked == true)
                    await RunProcessAsync("ping", "ftp.base2base.com.ua");

                if (traceFtp.IsChecked == true)
                    await RunProcessAsync("tracert", "ftp.base2base.com.ua");

                if (pingCrm.IsChecked == true)
                    await RunProcessAsync("ping", "crm.base2base.com.ua");

                if (traceCrm.IsChecked == true)
                    await RunProcessAsync("tracert", "crm.base2base.com.ua");
            };

            sfc.Click += async (s, e) =>
            {
                outputBox.Clear();
                AppendOutput("Starting SFC scan...");
                await RunProcessAsync("cmd.exe", "/c sfc /scannow");
            };

            dism.Click += async (s, e) =>
            {
                outputBox.Clear();
                AppendOutput("Starting DISM restore health...");
                await RunProcessAsync("cmd.exe", "/c DISM /Online /Cleanup-Image /RestoreHealth");
            };

            chkdsk.Click += async (s, e) =>
            {
                outputBox.Clear();
                AppendOutput("Scheduling CHKDSK...");
                await RunProcessAsync("cmd.exe", "/c echo Y | chkdsk C: /f /r");
            };

            printRestart.Click += async (s, e) =>
            {
                outputBox.Clear();
                AppendOutput("Stopping spooler...");

                await RunProcessAsync("net", "stop spooler");
                await RunProcessAsync("cmd.exe", "/c del /f /q %systemroot%\\system32\\spool\\PRINTERS\\*.*");
                await RunProcessAsync("net", "start spooler");

                AppendOutput("Printer service restarted.");
            };

            indicatorRestart.Click += async (s, e) =>
            {
                outputBox.Clear();
                indicatorRestart.IsEnabled = false;

                await Task.Run(() => RestartIndicator());

                indicatorRestart.IsEnabled = true;
            };

            anydeskIdRenew.Click += async (s, e) =>
            {
                outputBox.Clear();
                anydeskIdRenew.IsEnabled = false;

                await Task.Run(() => RenewAnyDeskId());

                anydeskIdRenew.IsEnabled = true;
            };

            regDll.Click += async (s, e) =>
            {
                outputBox.Clear();
                regDll.IsEnabled = false;

                await Task.Run(() => RegisterLibraries());

                regDll.IsEnabled = true;
            };
        }

        private string _lastOutputLine = "";

        private void DeleteOldFiles(string path, string pattern, int days, bool recursive = false)
        {
            if (!Directory.Exists(path))
            {
                AppendOutput($"Path not found: {path}");
                return;
            }

            var cutoff = DateTime.Now.AddDays(-days);
            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            int deletedCount = 0;
            int errorCount = 0;

            foreach (var file in SafeEnumerateFiles(path, pattern, option))
            {
                try
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                }
                catch
                {
                    errorCount++;
                }
            }

            AppendOutput($"Deleted files: {deletedCount}");
            if (errorCount > 0)
                AppendOutput($"Errors: {errorCount}");
        }
        private IEnumerable<string> SafeEnumerateFiles(string path, string pattern, SearchOption option)
        {
            var files = new List<string>();

            try
            {
                files.AddRange(Directory.GetFiles(path, pattern));
            }
            catch { }

            if (option == SearchOption.AllDirectories)
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        files.AddRange(SafeEnumerateFiles(dir, pattern, option));
                    }
                }
                catch { }
            }

            return files;
        }
        private void CleanTemp()
        {
            string tempPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Temp");

            if (!Directory.Exists(tempPath))
            {
                AppendOutput("Temp folder not found.");
                return;
            }

            int deletedFiles = 0;
            int deletedDirs = 0;

            foreach (var file in SafeEnumerateFiles(tempPath, "*.*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(file);
                    deletedFiles++;
                }
                catch { }
            }

            foreach (var dir in Directory.GetDirectories(tempPath))
            {
                try
                {
                    Directory.Delete(dir, true);
                    deletedDirs++;
                }
                catch { }
            }

            AppendOutput($"Temp cleaned. Files: {deletedFiles}, Folders: {deletedDirs}");
        }
        private void EmptyRecycleBin()
        {
            try
            {
                SHEmptyRecycleBin(IntPtr.Zero, null, 0);
                AppendOutput("Recycle bin cleaned.");
            }
            catch (Exception ex)
            {
                AppendOutput("ERROR cleaning recycle bin: " + ex.Message);
            }
        }

        [DllImport("Shell32.dll")]
        private static extern uint SHEmptyRecycleBin(
            IntPtr hwnd,
         string pszRootPath,
         uint dwFlags);

        private void AppendOutput(string text)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                outputBox.AppendText(text + Environment.NewLine);
                outputBox.ScrollToEnd();
            }));
        }
        private void RegisterLibraries()
        {
            try
            {
                string targetPath = @"C:\Windows\SysWOW64";

                AppendOutput("Extracting DLLs...");

                ExtractEmbeddedDll("b2b_support_tool.Resources.DigiSM.dll",
                                   System.IO.Path.Combine(targetPath, "DigiSM.dll"));

                ExtractEmbeddedDll("b2b_support_tool.Resources.ShopdeskTools.dll",
                                   System.IO.Path.Combine(targetPath, "ShopdeskTools.dll"));

                AppendOutput("Registering DigiSM.dll...");
                RunProcessBlocking("regsvr32", "/s \"C:\\Windows\\SysWOW64\\DigiSM.dll\"");

                AppendOutput("Registering ShopdeskTools.dll...");
                RunProcessBlocking("regsvr32", "/s \"C:\\Windows\\SysWOW64\\ShopdeskTools.dll\"");

                AppendOutput("DLL registration completed.");
            }
            catch (Exception ex)
            {
                AppendOutput("ERROR: " + ex.Message);
            }
        }
        private void ExtractEmbeddedDll(string resourceName, string outputPath)
        {
            using (var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    AppendOutput("Resource not found: " + resourceName);
                    return;
                }

                using (var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fileStream);
                }
            }

            AppendOutput("Copied: " + outputPath);
        }
        private void RunProcessBlocking(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(psi);
            process.WaitForExit();

            AppendOutput($"Exit code: {process.ExitCode}");
        }
        private void RestartIndicator()
        {
            try
            {
                string exePath = @"C:\ShopDesk\modules\indicator\ShopdeskIndicator.exe";

                AppendOutput("Searching for running ShopdeskIndicator...");

                var processes = Process.GetProcessesByName("ShopdeskIndicator");

                if (processes.Length == 0)
                {
                    AppendOutput("No running instances found.");
                }
                else
                {
                    foreach (var proc in processes)
                    {
                        try
                        {
                            AppendOutput($"Stopping PID {proc.Id}...");
                            proc.Kill();
                            proc.WaitForExit();
                            AppendOutput("Stopped successfully.");
                        }
                        catch (Exception ex)
                        {
                            AppendOutput("ERROR stopping process: " + ex.Message);
                        }
                    }
                }

                AppendOutput("Waiting 2 seconds...");
                Thread.Sleep(2000);

                if (!File.Exists(exePath))
                {
                    AppendOutput("ERROR: Executable not found.");
                    return;
                }

                AppendOutput("Starting ShopdeskIndicator...");
                Process.Start(exePath);

                AppendOutput("Restart completed.");
            }
            catch (Exception ex)
            {
                AppendOutput("ERROR: " + ex.Message);
            }
        }
        private void RenewAnyDeskId()
        {
            try
            {
                AppendOutput("Stopping AnyDesk...");
                KillProcess("AnyDesk");

                string programData = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "AnyDesk");

                string appData = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AnyDesk");

                string backupDir = System.IO.Path.Combine(programData, "backup");

                if (!Directory.Exists(programData))
                {
                    AppendOutput("ProgramData AnyDesk folder not found.");
                    return;
                }

                // backup user.conf
                string userConf = System.IO.Path.Combine(appData, "user.conf");
                if (File.Exists(userConf))
                {
                    Directory.CreateDirectory(backupDir);
                    File.Copy(userConf, System.IO.Path.Combine(backupDir, "user.conf"), true);
                    AppendOutput("Backup created.");
                }

                // delete ProgramData files
                foreach (var file in Directory.GetFiles(programData))
                {
                    try
                    {
                        File.Delete(file);
                        AppendOutput($"Deleted: {file}");
                    }
                    catch { }
                }

                // delete AppData files
                if (Directory.Exists(appData))
                {
                    foreach (var file in Directory.GetFiles(appData))
                    {
                        try
                        {
                            File.Delete(file);
                            AppendOutput($"Deleted: {file}");
                        }
                        catch { }
                    }
                }

                AppendOutput("Starting AnyDesk...");
                Process.Start(@"C:\Program Files (x86)\AnyDesk\AnyDesk.exe");

                Thread.Sleep(5000);

                AppendOutput("Stopping AnyDesk again...");
                KillProcess("AnyDesk");

                // restore config
                string backupConf = System.IO.Path.Combine(backupDir, "user.conf");
                if (File.Exists(backupConf))
                {
                    Directory.CreateDirectory(appData);
                    File.Copy(backupConf, System.IO.Path.Combine(appData, "user.conf"), true);
                    AppendOutput("Configuration restored.");
                }

                AppendOutput("Starting AnyDesk final time...");
                Process.Start(@"C:\Program Files (x86)\AnyDesk\AnyDesk.exe");

                AppendOutput("AnyDesk ID renew completed.");
            }
            catch (Exception ex)
            {
                AppendOutput("ERROR: " + ex.Message);
            }
        }
        private void KillProcess(string processName)
        {
            foreach (var proc in Process.GetProcessesByName(processName))
            {
                try
                {
                    proc.Kill();
                    AppendOutput($"Killed: {proc.ProcessName}");
                }
                catch { }
            }
        }
        private async Task RunProcessAsync(string fileName, string arguments)
        {
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

                var process = new Process();
                process.StartInfo = psi;
                process.EnableRaisingEvents = true;

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data == null)
                        return;

                    string line = e.Data.Trim();

                    if (string.IsNullOrWhiteSpace(line))
                        return;

                    // Якщо рядок з відсотком — показуємо тільки якщо змінився
                    if (line.Contains("%"))
                    {
                        if (line != _lastOutputLine)
                        {
                            _lastOutputLine = line;
                            AppendOutput(line);
                        }
                    }
                    else
                    {
                        AppendOutput(line);
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                        AppendOutput("ERROR: " + e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                AppendOutput($"Process exited with code {process.ExitCode}");
            });
        }
    }
}
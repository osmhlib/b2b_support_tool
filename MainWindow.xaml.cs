using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

namespace b2b_support_tool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Title = GetAppTitle();

            if (File.Exists(logPath))
            {
                var info = new FileInfo(logPath);

                if (info.Length > 5_000_000) // 5 MB
                {
                    File.Delete(logPath);
                }
            }

            clean.Click += async (s, e) =>
            {
                outputBox.Clear();

                bool deleteChecks = delCheck.IsChecked == true;
                bool deleteLogs = delLog.IsChecked == true;
                bool deleteTemp = delTemp.IsChecked == true;
                bool deleteRecycle = delRecyclebin.IsChecked == true;

                if (!deleteChecks && !deleteLogs && !deleteTemp && !deleteRecycle)
                {
                    AppendOutput("Nothing selected.");
                    return;
                }

                await RunProtectedOperation(async () =>
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

                    AppendOutput("Cleaning finished.");
                });
            };

            netTest.Click += async (s, e) =>
            {
                outputBox.Clear();

                bool doPingFtp = pingFtp.IsChecked == true;
                bool doTraceFtp = traceFtp.IsChecked == true;
                bool doPingCrm = pingCrm.IsChecked == true;
                bool doTraceCrm = traceCrm.IsChecked == true;

                if (!doPingFtp && !doTraceFtp && !doPingCrm && !doTraceCrm)
                {
                    AppendOutput("Nothing selected.");
                    return;
                }

                await RunProtectedOperation(async () =>
                {
                    if (doPingFtp)
                        await RunProcessAsync("ping", "ftp.base2base.com.ua");

                    if (doTraceFtp)
                        await RunProcessAsync("tracert", "ftp.base2base.com.ua");

                    if (doPingCrm)
                        await RunProcessAsync("ping", "crm.base2base.com.ua");

                    if (doTraceCrm)
                        await RunProcessAsync("tracert", "crm.base2base.com.ua");
                });
            };

            sfc.Click += async (s, e) =>
            {
                outputBox.Clear();

                await RunProtectedOperation(async () =>
                {
                    AppendOutput("Starting SFC scan...");
                    await RunProcessAsync("cmd.exe", "/c sfc /scannow");
                });
            };

            dism.Click += async (s, e) =>
            {
                outputBox.Clear();

                await RunProtectedOperation(async () =>
                {
                    AppendOutput("Starting DISM restore health...");
                    await RunProcessAsync("cmd.exe", "/c DISM /Online /Cleanup-Image /RestoreHealth");
                });
            };

            chkdsk.Click += async (s, e) =>
            {
                outputBox.Clear();

                await RunProtectedOperation(async () =>
                {
                    AppendOutput("Scheduling CHKDSK...");
                    await RunProcessAsync("cmd.exe", "/c echo Y | chkdsk C: /f /r");
                });
            };

            printRestart.Click += async (s, e) =>
            {
                outputBox.Clear();

                await RunProtectedOperation(async () =>
                {
                    AppendOutput("Stopping spooler...");

                    await RunProcessAsync("net", "stop spooler");
                    await RunProcessAsync("cmd.exe", "/c del /f /q %systemroot%\\system32\\spool\\PRINTERS\\*.*");
                    await RunProcessAsync("net", "start spooler");

                    AppendOutput("Printer service restarted.");
                });
            };

            modulesStart.Click += async (s, e) =>
            {
                outputBox.Clear();

                bool doRestartIndicator = restartIndicator.IsChecked == true;
                bool doRestartBridge = restartBridge.IsChecked == true;
                bool doRestartTccLocalProcessor = restartTccLocalProcessor.IsChecked == true;

                if (!doRestartIndicator && !doRestartBridge && !doRestartTccLocalProcessor)
                {
                    AppendOutput("Nothing selected.");
                    return;
                }

                await RunProtectedOperation(async () =>
                {
                    if (doRestartIndicator)
                        await Task.Run(() => RestartIndicator());

                    if (doRestartBridge)
                        await Task.Run(() => RestartModule(
                            "ShopDeskBridge",
                            "ShopDeskBridge",
                            @"C:\ShopDesk\modules\bridge\ShopDeskBridge.exe"));

                    if (doRestartTccLocalProcessor)
                        await Task.Run(() => RestartModule(
                            "TccLocalProcessor",
                            "TccLocalProcessor",
                            @"C:\ShopDesk\modules\crm\tcc\TccLocalProcessor.exe"));
                });
            };

            updateStart.Click += async (s, e) =>
            {
                outputBox.Clear();

                bool doUpdateShopdesk = updateShopdesk.IsChecked == true;
                bool doUpdateScaleServer = updateScaleServer.IsChecked == true;

                if (!doUpdateShopdesk && !doUpdateScaleServer)
                {
                    AppendOutput("Nothing selected.");
                    return;
                }

                await RunProtectedOperation(async () =>
                {
                    if (doUpdateShopdesk)
                        await DownloadAndRunShopdeskUpdate();

                    if (doUpdateScaleServer)
                        await DownloadAndRunScaleServerUpdate();
                });
            };

            anydeskIdRenew.Click += async (s, e) =>
            {
                outputBox.Clear();

                await RunProtectedOperation(async () =>
                {
                    await Task.Run(() => RenewAnyDeskId());
                });
            };

            regDll.Click += async (s, e) =>
            {
                outputBox.Clear();

                await RunProtectedOperation(async () =>
                {
                    await Task.Run(() => RegisterLibraries());
                });
            };

            regWIN.Click += async (s, e) =>
            {
                outputBox.Clear();

                await RunProtectedOperation(async () =>
                {
                    await ExtractAndRunPowerShellScript(
                        "b2b_support_tool.Resources.regwin_script.ps1"
                    );
                });
            };
        }

        private string GetAppTitle()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;

            return $"B2B Support Tool v{version?.Major}.{version?.Minor}.{version?.Build}";
        }

        private string _lastOutputLine = "";
        private static readonly HttpClient httpClient = new HttpClient();
        private readonly string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"support_tool.log");

        private void SetControlsEnabled(bool enabled)
        {
            Dispatcher.Invoke(() =>
            {
                clean.IsEnabled = enabled;
                netTest.IsEnabled = enabled;

                sfc.IsEnabled = enabled;
                dism.IsEnabled = enabled;
                chkdsk.IsEnabled = enabled;

                printRestart.IsEnabled = enabled;
                modulesStart.IsEnabled = enabled;
                anydeskIdRenew.IsEnabled = enabled;

                regDll.IsEnabled = enabled;
                updateStart.IsEnabled = enabled;
                regWIN.IsEnabled = enabled;
            });
        }

        private async Task RunProtectedOperation(Func<Task> operation)
        {
            SetControlsEnabled(false);

            Title = GetAppTitle() + " [BUSY]";

            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                AppendOutput("ERROR: " + ex.Message);
            }
            finally
            {
                Title = GetAppTitle();

                SetControlsEnabled(true);
            }
        }
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
         string? pszRootPath,
         uint dwFlags);

        private void AppendOutput(string text)
        {
            string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}";

            // UI
            Dispatcher.BeginInvoke(new Action(() =>
            {
                outputBox.AppendText(logLine + Environment.NewLine);
                outputBox.ScrollToEnd();
            }));

            // File log
            try
            {
                File.AppendAllText(logPath, logLine + Environment.NewLine);
            }
            catch
            {
                // лог не повинен валити програму
            }
        }
        private void RegisterLibraries()
        {
            try
            {
                string targetPath = @"C:\Windows\SysWOW64";

                AppendOutput("Extracting DLLs...");

                ExtractEmbeddedFile("b2b_support_tool.Resources.DigiSM.dll",
                                   System.IO.Path.Combine(targetPath, "DigiSM.dll"));

                ExtractEmbeddedFile("b2b_support_tool.Resources.ShopdeskTools.dll",
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
        private void ExtractEmbeddedFile(string resourceName, string outputPath)
        {
            using (var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new Exception("Resource not found: " + resourceName);
                }

                using (var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fileStream);
                }
            }

            AppendOutput("Extracted: " + outputPath);
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
            if (process == null)
            {
                AppendOutput("ERROR: Failed to start process.");
                return;
            }

            process.WaitForExit();

            AppendOutput($"Exit code: {process.ExitCode}");
        }
        private void RestartIndicator()
        {
            RestartModule(
                "ShopdeskIndicator",
                "ShopdeskIndicator",
                @"C:\ShopDesk\modules\indicator\ShopdeskIndicator.exe");
        }

        private void RestartModule(string displayName, string processName, string executablePath)
        {
            try
            {
                AppendOutput($"Searching for running {displayName}...");

                var processes = Process.GetProcessesByName(processName);

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

                string exePath = ResolveExecutablePath(executablePath);

                if (!File.Exists(exePath))
                {
                    AppendOutput("ERROR: Executable not found.");
                    return;
                }

                AppendOutput($"Starting {displayName}...");
                Process.Start(exePath);

                AppendOutput("Restart completed.");
            }
            catch (Exception ex)
            {
                AppendOutput("ERROR: " + ex.Message);
            }
        }

        private string ResolveExecutablePath(string executablePath)
        {
            if (File.Exists(executablePath))
                return executablePath;

            if (!System.IO.Path.HasExtension(executablePath))
            {
                string pathWithExeExtension = executablePath + ".exe";

                if (File.Exists(pathWithExeExtension))
                    return pathWithExeExtension;
            }

            return executablePath;
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

        private async Task ExtractAndRunPowerShellScript(string resourceName)
        {
            string scriptPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "regwin_script.ps1"
            );

            ExtractEmbeddedFile(resourceName, scriptPath);

            AppendOutput("Starting PowerShell script...");

            await RunProcessAsync(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\""
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
                    AppendOutput("Temporary script deleted.");
                }
            }
            catch (Exception ex)
            {
                AppendOutput("Could not delete temporary script: " + ex.Message);
            }
        }

        private async Task DownloadAndRunShopdeskUpdate()
        {
            await DownloadAndRunInstaller(
                "Shopdesk",
                "https://andriy.co/download.ashx?dl=Shopdesk_setup.exe",
                "Shopdesk_setup.exe");
        }

        private async Task DownloadAndRunScaleServerUpdate()
        {
            await DownloadAndRunInstaller(
                "ScaleServer",
                "https://andriy.co/download/products/ScaleServerSetup.exe",
                "ScaleServerSetup.exe");
        }

        private async Task DownloadAndRunInstaller(string productName, string url, string installerFileName)
        {
            string installerPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                installerFileName
            );

            AppendOutput($"[1/3] Downloading latest {productName} version...");

            await DownloadFileWithProgress(url, installerPath);

            AppendOutput("Download completed:");
            AppendOutput(installerPath);

            AppendOutput("[2/3] Starting installer...");

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });

            if (process != null)
            {
                await Task.Run(() => process.WaitForExit());
            }

            AppendOutput("[3/3] Removing installer...");

            DeleteInstallerWithRetry(installerPath);

            AppendOutput($"{productName} update completed.");
        }
        private async Task DownloadFileWithProgress(string url, string outputPath)
        {

            using var response = await httpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead
            );

            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 8192,
                useAsync: true
            );

            byte[] buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;
            int lastProgress = -1;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (totalBytes.HasValue)
                {
                    int progress = (int)((totalRead * 100) / totalBytes.Value);

                    if (progress >= lastProgress + 1)
                    {
                        lastProgress = progress;
                        AppendOutput($"Download progress: {progress}%");
                    }
                }
                else
                {
                    AppendOutput($"Downloaded: {totalRead / 1024 / 1024} MB");
                }
            }
        }

        private void DeleteInstallerWithRetry(string path)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        AppendOutput("Installer deleted.");
                    }

                    return;
                }
                catch
                {
                    Thread.Sleep(1000);
                }
            }

            AppendOutput("Installer was not deleted. It may still be in use.");
        }

        private async Task RunProcessAsync(string fileName, string arguments)
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

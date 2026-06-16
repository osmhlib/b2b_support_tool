using System.Reflection;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using b2b_support_tool.Infrastructure;
using b2b_support_tool.Services;

namespace b2b_support_tool
{
    public partial class MainWindow : Window
    {
        private readonly SupportLogger _logger;
        private readonly CleanupService _cleanupService;
        private readonly NetworkDiagnosticsService _networkDiagnosticsService;
        private readonly WindowsRepairService _windowsRepairService;
        private readonly PrinterService _printerService;
        private readonly ModuleService _moduleService;
        private readonly UpdateService _updateService;
        private readonly ExternalIpService _externalIpService;
        private readonly AnyDeskService _anyDeskService;
        private readonly WeighingLibrariesService _weighingLibrariesService;
        private readonly WindowsActivationService _windowsActivationService;

        public MainWindow()
        {
            InitializeComponent();

            _logger = new SupportLogger(
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "support_tool.log"),
                AppendOutputToUi);

            var processRunner = new ProcessRunner(_logger);
            var resourceExtractor = new EmbeddedResourceExtractor(_logger, Assembly.GetExecutingAssembly());

            _cleanupService = new CleanupService(_logger);
            _networkDiagnosticsService = new NetworkDiagnosticsService(processRunner);
            _windowsRepairService = new WindowsRepairService(_logger, processRunner);
            _printerService = new PrinterService(_logger, processRunner);
            _moduleService = new ModuleService(_logger);
            _updateService = new UpdateService(_logger);
            _externalIpService = new ExternalIpService();
            _anyDeskService = new AnyDeskService(_logger);
            _weighingLibrariesService = new WeighingLibrariesService(_logger, resourceExtractor, processRunner);
            _windowsActivationService = new WindowsActivationService(_logger, resourceExtractor, processRunner);

            _logger.Initialize();

            Title = GetAppTitle();
            copyExternalIp.IsEnabled = false;

            BindEvents();
            Loaded += async (_, _) => await RefreshExternalIpAsync();
        }

        private string GetAppTitle()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;

            return $"B2B Support Tool v{version?.Major}.{version?.Minor}.{version?.Build}";
        }

        private void BindEvents()
        {
            clean.Click += async (_, _) =>
            {
                outputBox.Clear();

                bool deleteChecks = delCheck.IsChecked == true;
                bool deleteLogs = delLog.IsChecked == true;
                bool deleteTemp = delTemp.IsChecked == true;
                bool deleteRecycle = delRecyclebin.IsChecked == true;

                if (!deleteChecks && !deleteLogs && !deleteTemp && !deleteRecycle)
                {
                    _logger.Write("Nothing selected.");
                    return;
                }

                await RunProtectedOperation(() => _cleanupService.CleanAsync(
                    deleteChecks,
                    deleteLogs,
                    deleteTemp,
                    deleteRecycle));
            };

            netTest.Click += async (_, _) =>
            {
                outputBox.Clear();

                bool doPingFtp = pingFtp.IsChecked == true;
                bool doTraceFtp = traceFtp.IsChecked == true;
                bool doPingCrm = pingCrm.IsChecked == true;
                bool doTraceCrm = traceCrm.IsChecked == true;

                if (!doPingFtp && !doTraceFtp && !doPingCrm && !doTraceCrm)
                {
                    _logger.Write("Nothing selected.");
                    return;
                }

                await RunProtectedOperation(() => _networkDiagnosticsService.RunAsync(
                    doPingFtp,
                    doTraceFtp,
                    doPingCrm,
                    doTraceCrm));
            };

            copyExternalIp.Click += (_, _) =>
            {
                if (IsValidIpv4(externalIpBox.Text))
                {
                    Clipboard.SetText(externalIpBox.Text);
                }
            };

            refreshExternalIp.Click += async (_, _) =>
            {
                await RefreshExternalIpAsync();
            };

            modulesStart.Click += async (_, _) =>
            {
                outputBox.Clear();

                var actions = new List<Func<Task>>();

                if (restartIndicator.IsChecked == true)
                {
                    actions.Add(() => _moduleService.RestartAsync(
                        "ShopdeskIndicator",
                        "ShopdeskIndicator",
                        @"C:\ShopDesk\modules\indicator\ShopdeskIndicator.exe"));
                }

                if (restartBridge.IsChecked == true)
                {
                    actions.Add(() => _moduleService.RestartAsync(
                        "ShopDeskBridge",
                        "ShopDeskBridge",
                        @"C:\ShopDesk\modules\bridge\ShopDeskBridge.exe"));
                }

                if (restartTccLocalProcessor.IsChecked == true)
                {
                    actions.Add(() => _moduleService.RestartAsync(
                        "TccLocalProcessor",
                        "TccLocalProcessor",
                        @"C:\ShopDesk\modules\crm\tcc\TccLocalProcessor.exe"));
                }

                await RunSelectedActions(actions);
            };

            updateStart.Click += async (_, _) =>
            {
                outputBox.Clear();

                var actions = new List<Func<Task>>();

                if (updateShopdesk.IsChecked == true)
                {
                    actions.Add(() => _updateService.InstallOrUpdateAsync(
                        "Shopdesk",
                        "https://andriy.co/download.ashx?dl=Shopdesk_setup.exe",
                        "Shopdesk_setup.exe"));
                }

                if (updateScaleServer.IsChecked == true)
                {
                    actions.Add(() => _updateService.InstallOrUpdateAsync(
                        "ScaleServer",
                        "https://andriy.co/download/products/ScaleServerSetup.exe",
                        "ScaleServerSetup.exe"));
                }

                await RunSelectedActions(actions);
            };

            sfc.Click += async (_, _) =>
            {
                outputBox.Clear();
                await RunProtectedOperation(_windowsRepairService.RunSfcAsync);
            };

            dism.Click += async (_, _) =>
            {
                outputBox.Clear();
                await RunProtectedOperation(_windowsRepairService.RunDismAsync);
            };

            chkdsk.Click += async (_, _) =>
            {
                outputBox.Clear();
                await RunProtectedOperation(_windowsRepairService.ScheduleChkdskAsync);
            };

            printRestart.Click += async (_, _) =>
            {
                outputBox.Clear();
                await RunProtectedOperation(_printerService.RestartAsync);
            };

            anydeskIdRenew.Click += async (_, _) =>
            {
                outputBox.Clear();
                await RunProtectedOperation(_anyDeskService.RenewIdAsync);
            };

            regDll.Click += async (_, _) =>
            {
                outputBox.Clear();
                await RunProtectedOperation(_weighingLibrariesService.RegisterAsync);
            };

            regWIN.Click += async (_, _) =>
            {
                outputBox.Clear();
                await RunProtectedOperation(_windowsActivationService.ActivateAsync);
            };
        }

        private async Task RunSelectedActions(IReadOnlyCollection<Func<Task>> actions)
        {
            if (actions.Count == 0)
            {
                _logger.Write("Nothing selected.");
                return;
            }

            await RunProtectedOperation(async () =>
            {
                foreach (var action in actions)
                {
                    await action();
                }
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
                _logger.Write("ERROR: " + ex.Message);
            }
            finally
            {
                Title = GetAppTitle();
                SetControlsEnabled(true);
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            Dispatcher.Invoke(() =>
            {
                clean.IsEnabled = enabled;
                netTest.IsEnabled = enabled;
                refreshExternalIp.IsEnabled = enabled;

                modulesStart.IsEnabled = enabled;
                updateStart.IsEnabled = enabled;

                sfc.IsEnabled = enabled;
                dism.IsEnabled = enabled;
                chkdsk.IsEnabled = enabled;

                printRestart.IsEnabled = enabled;
                anydeskIdRenew.IsEnabled = enabled;
                regDll.IsEnabled = enabled;
                regWIN.IsEnabled = enabled;
            });
        }

        private void AppendOutputToUi(string logLine)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                outputBox.AppendText(logLine + Environment.NewLine);
                outputBox.ScrollToEnd();
            }));
        }

        private async Task RefreshExternalIpAsync()
        {
            SetExternalIpText("Getting IP...");
            refreshExternalIp.IsEnabled = false;

            try
            {
                string? ipAddress = await _externalIpService.GetPublicIpv4Async();

                SetExternalIpText(IsValidIpv4(ipAddress) ? ipAddress! : "No IP address");
            }
            finally
            {
                refreshExternalIp.IsEnabled = true;
            }
        }

        private void SetExternalIpText(string text)
        {
            externalIpBox.Text = text;

            if (copyExternalIp != null)
            {
                copyExternalIp.IsEnabled = IsValidIpv4(text);
            }
        }

        private static bool IsValidIpv4(string? value)
        {
            return IPAddress.TryParse(value, out var ipAddress)
                && ipAddress.AddressFamily == AddressFamily.InterNetwork
                && value?.Split('.').Length == 4;
        }

        private void ExternalIpBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (copyExternalIp != null)
            {
                copyExternalIp.IsEnabled = IsValidIpv4(externalIpBox.Text);
            }
        }
    }
}

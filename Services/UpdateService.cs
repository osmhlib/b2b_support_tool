using System.Diagnostics;
using System.IO;
using System.Net.Http;
using b2b_support_tool.Infrastructure;

namespace b2b_support_tool.Services
{
    public class UpdateService
    {
        private static readonly HttpClient HttpClient = new();

        private readonly ISupportLogger _logger;

        public UpdateService(ISupportLogger logger)
        {
            _logger = logger;
        }

        public async Task InstallOrUpdateAsync(string productName, string url, string installerFileName)
        {
            string installerPath = Path.Combine(
                Path.GetTempPath(),
                installerFileName
            );

            _logger.Write($"[1/3] Downloading latest {productName} version...");

            await DownloadFileWithProgress(url, installerPath);

            _logger.Write("Download completed:");
            _logger.Write(installerPath);

            _logger.Write("[2/3] Starting installer...");

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });

            if (process != null)
            {
                await Task.Run(() => process.WaitForExit());
            }

            _logger.Write("[3/3] Removing installer...");

            DeleteInstallerWithRetry(installerPath);

            _logger.Write($"{productName} update completed.");
        }

        private async Task DownloadFileWithProgress(string url, string outputPath)
        {
            using var response = await HttpClient.GetAsync(
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
                        _logger.Write($"Download progress: {progress}%");
                    }
                }
                else
                {
                    _logger.Write($"Downloaded: {totalRead / 1024 / 1024} MB");
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
                        _logger.Write("Installer deleted.");
                    }

                    return;
                }
                catch
                {
                    Thread.Sleep(1000);
                }
            }

            _logger.Write("Installer was not deleted. It may still be in use.");
        }
    }
}

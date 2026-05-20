using System.IO;
using System.Runtime.InteropServices;
using b2b_support_tool.Infrastructure;

namespace b2b_support_tool.Services
{
    public class CleanupService
    {
        private readonly ISupportLogger _logger;

        public CleanupService(ISupportLogger logger)
        {
            _logger = logger;
        }

        public async Task CleanAsync(
            bool deleteChecks,
            bool deleteLogs,
            bool deleteTemp,
            bool deleteRecycleBin)
        {
            await Task.Run(() =>
            {
                if (deleteChecks)
                    DeleteOldFiles(@"C:\Shopdesk\offline\grr\arh", "*.*", 14);

                if (deleteLogs)
                    DeleteOldFiles(@"C:\Shopdesk", "*.log", 14, true);

                if (deleteTemp)
                    CleanTemp();

                if (deleteRecycleBin)
                    EmptyRecycleBin();
            });

            _logger.Write("Cleaning finished.");
        }

        private void DeleteOldFiles(string path, string pattern, int days, bool recursive = false)
        {
            if (!Directory.Exists(path))
            {
                _logger.Write($"Path not found: {path}");
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

            _logger.Write($"Deleted files: {deletedCount}");
            if (errorCount > 0)
                _logger.Write($"Errors: {errorCount}");
        }

        private static IEnumerable<string> SafeEnumerateFiles(string path, string pattern, SearchOption option)
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
            string tempPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Temp");

            if (!Directory.Exists(tempPath))
            {
                _logger.Write("Temp folder not found.");
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

            _logger.Write($"Temp cleaned. Files: {deletedFiles}, Folders: {deletedDirs}");
        }

        private void EmptyRecycleBin()
        {
            try
            {
                SHEmptyRecycleBin(IntPtr.Zero, null, 0);
                _logger.Write("Recycle bin cleaned.");
            }
            catch (Exception ex)
            {
                _logger.Write("ERROR cleaning recycle bin: " + ex.Message);
            }
        }

        [DllImport("Shell32.dll")]
        private static extern uint SHEmptyRecycleBin(
            IntPtr hwnd,
            string? pszRootPath,
            uint dwFlags);
    }
}

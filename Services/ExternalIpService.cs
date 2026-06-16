using System.Net.Http;

namespace b2b_support_tool.Services
{
    public class ExternalIpService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        public async Task<string?> GetPublicIpv4Async()
        {
            try
            {
                string response = await HttpClient.GetStringAsync("https://api.ipify.org");
                string ipAddress = response.Trim();

                return string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress;
            }
            catch
            {
                return null;
            }
        }
    }
}

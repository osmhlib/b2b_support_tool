using b2b_support_tool.Infrastructure;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace b2b_support_tool.Services
{
    public class NetworkDiagnosticsService
    {
        private const int PingAttempts = 4;
        private const int TimeoutMs = 4000;
        private const int MaxTraceHops = 30;
        private const int StepDelayMs = 180;
        private const int AttemptDelayMs = 250;

        private static readonly byte[] PingBuffer = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"u8.ToArray();

        private readonly ISupportLogger _logger;

        public NetworkDiagnosticsService(ISupportLogger logger)
        {
            _logger = logger;
        }

        public async Task RunAsync(bool pingFtp, bool traceFtp, bool pingCrm, bool traceCrm)
        {
            if (pingFtp)
                await PingHostAsync("ftp.base2base.com.ua");

            if (traceFtp)
                await TraceRouteAsync("ftp.base2base.com.ua");

            if (pingCrm)
                await PingHostAsync("crm.base2base.com.ua");

            if (traceCrm)
                await TraceRouteAsync("crm.base2base.com.ua");
        }

        private async Task PingHostAsync(string host)
        {
            IPAddress[] addresses;

            try
            {
                addresses = await Dns.GetHostAddressesAsync(host);
            }
            catch
            {
                _logger.Write($"Could not resolve {host}.");
                return;
            }

            var target = addresses.FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault();

            if (target == null)
            {
                _logger.Write($"Could not resolve {host}: no IP address found.");
                return;
            }

            _logger.Write($"PING {host} [{target}]");
            await Task.Delay(StepDelayMs);

            using var ping = new Ping();
            int sent = 0;
            int received = 0;
            long min = long.MaxValue;
            long max = 0;
            long total = 0;

            for (int attempt = 0; attempt < PingAttempts; attempt++)
            {
                sent++;

                try
                {
                    var reply = await ping.SendPingAsync(target, TimeoutMs, PingBuffer);

                    if (reply.Status == IPStatus.Success)
                    {
                        received++;
                        min = Math.Min(min, reply.RoundtripTime);
                        max = Math.Max(max, reply.RoundtripTime);
                        total += reply.RoundtripTime;
                        _logger.Write($"{attempt + 1,2}. OK      {FormatTime(reply.RoundtripTime),6}  TTL={reply.Options?.Ttl}");
                    }
                    else
                    {
                        _logger.Write($"{attempt + 1,2}. FAIL    {reply.Status}");
                    }
                }
                catch
                {
                    _logger.Write($"{attempt + 1,2}. FAIL    network error");
                }

                if (attempt < PingAttempts - 1)
                {
                    await Task.Delay(AttemptDelayMs);
                }
            }

            int lost = sent - received;
            int lossPercent = sent == 0 ? 0 : lost * 100 / sent;

            _logger.Write($"PING summary: sent={sent}, received={received}, lost={lost} ({lossPercent}% loss)");

            if (received > 0)
            {
                _logger.Write($"PING time: min/avg/max={min}/{total / received}/{max} ms");
            }
        }

        private async Task TraceRouteAsync(string host)
        {
            IPAddress[] addresses;

            try
            {
                addresses = await Dns.GetHostAddressesAsync(host);
            }
            catch
            {
                _logger.Write($"Could not resolve {host}.");
                return;
            }

            var target = addresses.FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault();

            if (target == null)
            {
                _logger.Write($"Could not resolve {host}: no IP address found.");
                return;
            }

            _logger.Write($"TRACE {host} [{target}], max hops={MaxTraceHops}");
            _logger.Write("Hop  Time    Address          Status");
            await Task.Delay(StepDelayMs);

            using var ping = new Ping();

            for (int ttl = 1; ttl <= MaxTraceHops; ttl++)
            {
                try
                {
                    var options = new PingOptions(ttl, true);
                    var stopwatch = Stopwatch.StartNew();
                    var reply = await ping.SendPingAsync(target, TimeoutMs, PingBuffer, options);
                    stopwatch.Stop();

                    if (reply.Status == IPStatus.TimedOut)
                    {
                        _logger.Write($"{ttl,3}  *       -                Timeout");
                        await Task.Delay(AttemptDelayMs);
                        continue;
                    }

                    string address = reply.Address?.ToString() ?? "unknown";
                    _logger.Write($"{ttl,3}  {FormatTime(stopwatch.ElapsedMilliseconds),6}  {address,-15}  {reply.Status}");

                    if (reply.Status == IPStatus.Success)
                    {
                        break;
                    }
                }
                catch
                {
                    _logger.Write($"{ttl,3}  *       -                network error");
                }

                if (ttl < MaxTraceHops)
                {
                    await Task.Delay(AttemptDelayMs);
                }
            }

            _logger.Write("Trace complete.");
        }

        private static string FormatTime(long milliseconds)
        {
            return milliseconds < 1 ? "<1ms" : $"{milliseconds}ms";
        }
    }
}

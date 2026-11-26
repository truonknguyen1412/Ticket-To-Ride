using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TicketToRide.Client
{
    public static class ServerEndpointResolver
    {
        // Thêm IP LAN của laptop vào đây nếu có test qua mạng nội bộ từ máy khác
        private static readonly string[] LocalCandidates =
        {
            "127.0.0.1:5000"
            // "192.168.1.50:5000"
        };

        public static string ResolveServerAddress(string configFilePath = "server.txt", int localTimeoutMs = 400)
        {
            string fallbackAddress = ReadFallbackFromFile(configFilePath);

            foreach (string candidate in LocalCandidates)
            {
                if (TryParseHostPort(candidate, out string host, out int port) &&
                    CanConnect(host, port, localTimeoutMs))
                {
                    return $"{host}:{port}";
                }
            }

            return fallbackAddress;
        }

        private static string ReadFallbackFromFile(string path)
        {
            string fullPath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(AppContext.BaseDirectory, path);

            if (File.Exists(fullPath))
            {
                string content = File.ReadAllText(fullPath).Trim();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content;
                }
            }

            // Giá trị dự phòng cuối cùng nếu thiếu cả file server.txt
            return "20.189.72.221:5000";
        }

        private static bool CanConnect(string host, int port, int timeoutMs)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                connectTask.Wait(cts.Token);
                return client.Connected;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseHostPort(string value, out string host, out int port)
        {
            host = string.Empty;
            port = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            int lastColon = value.LastIndexOf(':');
            if (lastColon <= 0 || lastColon == value.Length - 1)
                return false;

            host = value[..lastColon].Trim();
            string portPart = value[(lastColon + 1)..].Trim();

            return int.TryParse(portPart, out port) && port is > 0 and <= 65535;
        }
    }
}
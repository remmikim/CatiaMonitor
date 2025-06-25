using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CatiaMonitor.Client
{
    public class Program
    {
        private const int ServerPort = 12345;
        private const int ReconnectDelaySeconds = 30;

        public static async Task Main(string[] args)
        {
            Console.Title = "CATIA Monitor Client";
            Console.WriteLine("--- CATIA Monitor Client ---");

            // <<<<< 새로운 기능 시작 >>>>>
            // 1. 자동으로 서버를 찾습니다.
            string? serverIp = await ServerFinder.DiscoverServerAsync();

            // 2. 서버를 찾지 못한 경우, 사용자에게 수동으로 선택하도록 합니다.
            if (serverIp == null)
            {
                Console.WriteLine("\nCould not find a server automatically. Please select from the list or enter a custom IP.");
                serverIp = await SelectServerIpAddressAsync();
            }
            // <<<<< 새로운 기능 끝 >>>>>

            Console.WriteLine($"[Config] Server IP has been set to: {serverIp}");

            CheckAndCorrectAutoStartRegistration();

            while (true)
            {
                try
                {
                    await ConnectAndProcessAsync(serverIp, ServerPort);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Connection failed to {serverIp}: {ex.Message}. Retrying in {ReconnectDelaySeconds} seconds.");
                }
                await Task.Delay(TimeSpan.FromSeconds(ReconnectDelaySeconds));
            }
        }

        /// <summary>
        /// 로컬 네트워크 인터페이스를 스캔하여 사용자에게 서버 IP를 선택하도록 요청하는 메서드입니다.
        /// (서버 자동 탐지 실패 시에만 호출됩니다.)
        /// </summary>
        private static async Task<string> SelectServerIpAddressAsync()
        {
            Console.WriteLine("\nSearching for available network interfaces...");
            var ipAddresses = new List<string> { "127.0.0.1" };

            try
            {
                var hostAddresses = await Dns.GetHostAddressesAsync(Dns.GetHostName());
                foreach (var ip in hostAddresses)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !ipAddresses.Contains(ip.ToString()))
                    {
                        ipAddresses.Add(ip.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Could not automatically detect network IP addresses: {ex.Message}");
            }

            while (true)
            {
                Console.WriteLine("\nPlease select the server IP address to connect to:");
                for (int i = 0; i < ipAddresses.Count; i++)
                {
                    Console.WriteLine($"  [{i + 1}] {ipAddresses[i]}");
                }
                Console.WriteLine("  [0] Enter a custom IP address");
                Console.Write("\nEnter your choice: ");

                string? choice = Console.ReadLine();
                if (int.TryParse(choice, out int selection))
                {
                    if (selection > 0 && selection <= ipAddresses.Count)
                    {
                        return ipAddresses[selection - 1];
                    }
                    else if (selection == 0)
                    {
                        Console.Write("Please enter the custom server IP address: ");
                        string? customIp = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(customIp) && IPAddress.TryParse(customIp.Trim(), out _))
                        {
                            return customIp.Trim();
                        }
                        else
                        {
                            Console.WriteLine("Invalid IP address format. Please try again.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid selection. Please try again.");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid input. Please enter a number.");
                }
            }
        }

        private static void CheckAndCorrectAutoStartRegistration()
        {
            Console.WriteLine("[AutoStart] Checking startup registration...");
            string? registeredPath = AutoStarter.GetRegisteredPath();
            string expectedPath = $"\"{AutoStarter.ExecutablePath}\"";

            if (registeredPath == null)
            {
                Console.WriteLine("[AutoStart] Not registered for startup. Registering now...");
                AutoStarter.RegisterInStartup();
            }
            else if (!registeredPath.Equals(expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[AutoStart] Registered path is outdated. Re-registering...");
                AutoStarter.RegisterInStartup();
            }
            else
            {
                Console.WriteLine("[AutoStart] Already registered correctly.");
            }
        }

        private static async Task ConnectAndProcessAsync(string serverIpAddress, int serverPort)
        {
            using (var client = new TcpClient())
            {
                Console.WriteLine($"\n[Network] Attempting to connect to {serverIpAddress}:{serverPort}...");
                await client.ConnectAsync(serverIpAddress, serverPort);
                Console.WriteLine("[Network] Successfully connected to server.");

                NetworkStream stream = client.GetStream();

                while (client.Connected)
                {
                    Console.WriteLine("[Network] Waiting for a request from the server...");
                    var buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        Console.WriteLine("[Network] Server closed the connection.");
                        break;
                    }

                    string serverRequest = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[Network] Received request: '{serverRequest}'");

                    if (serverRequest.Trim().Equals("CHECK_STATUS", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isRunning = StatusChecker.IsCatiaRunning();
                        var response = new { IsCatiaRunning = isRunning };
                        string jsonResponse = JsonSerializer.Serialize(response);

                        byte[] dataToSend = Encoding.UTF8.GetBytes(jsonResponse);
                        await stream.WriteAsync(dataToSend, 0, dataToSend.Length);
                        Console.WriteLine($"[Network] Sent response to server: {jsonResponse}");
                    }
                }
            }
        }
    }
}

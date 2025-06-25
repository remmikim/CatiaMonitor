using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CatiaMonitor.Client
{
    public class Program
    {
        private const string ServerIpAddress = "127.0.0.1";
        private const int ServerPort = 12345;
        private const int ReconnectDelaySeconds = 30;

        public static async Task Main(string[] args)
        {
            Console.Title = "CATIA Monitor Client";
            Console.WriteLine("--- CATIA Monitor Client ---");

            // 자동 시작 등록 상태를 확인하고 필요 시 재등록합니다.
            CheckAndCorrectAutoStartRegistration();

            while (true)
            {
                try
                {
                    await ConnectAndProcessAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Connection failed: {ex.Message}. Retrying in {ReconnectDelaySeconds} seconds.");
                }
                await Task.Delay(TimeSpan.FromSeconds(ReconnectDelaySeconds));
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
                Console.WriteLine($"[AutoStart] Registered path is outdated.");
                Console.WriteLine($"    Old: {registeredPath}");
                Console.WriteLine($"    New: {expectedPath}");
                Console.WriteLine($"[AutoStart] Re-registering with correct path...");
                AutoStarter.RegisterInStartup();
            }
            else
            {
                Console.WriteLine("[AutoStart] Already registered correctly.");
            }
        }

        private static async Task ConnectAndProcessAsync()
        {
            using (var client = new TcpClient())
            {
                Console.WriteLine($"[Network] Attempting to connect to {ServerIpAddress}:{ServerPort}...");
                await client.ConnectAsync(ServerIpAddress, ServerPort);
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

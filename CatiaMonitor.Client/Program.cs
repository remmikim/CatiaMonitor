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
        private static bool s_isConsoleVisible = false;

        private static void Log(string message)
        {
            if (s_isConsoleVisible)
            {
                Console.WriteLine(message);
            }
            // 향후 파일 로그 등을 추가할 수 있습니다.
        }

        public static async Task Main(string[] args)
        {
            bool runInBackground = args.Contains("/background", StringComparer.OrdinalIgnoreCase);

            if (!runInBackground)
            {
                ConsoleManager.Show();
                s_isConsoleVisible = true;
                ConsoleManager.SetupCloseHandler();

                Console.Title = "CATIA Monitor Client";
                Log("--- CATIA Monitor Client ---");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Log("\n[Info] 이 콘솔 창의 닫기(X) 버튼을 눌러도 프로그램은 종료되지 않고 백그라운드에서 계속 실행됩니다.");
                Log("[Info] 프로그램을 완전히 종료하려면 작업 관리자에서 'CatiaMonitor.Client.exe' 프로세스를 직접 종료해야 합니다.");
                Console.ResetColor();
            }

            string? serverIp = await ServerFinder.DiscoverServerAsync();

            if (serverIp == null)
            {
                if (runInBackground)
                {
                    serverIp = "127.0.0.1"; // 백그라운드 모드이고 서버를 못 찾으면 localhost로 기본 설정
                }
                else
                {
                    Log("\nCould not find a server automatically. Please select from the list or enter a custom IP.");
                    serverIp = await SelectServerIpAddressAsync();
                }
            }

            Log($"[Config] Server IP has been set to: {serverIp}");

            CheckAndCorrectAutoStartRegistration();

            while (true)
            {
                try
                {
                    await ConnectAndProcessAsync(serverIp, ServerPort);
                }
                catch (Exception ex)
                {
                    Log($"[Error] Connection failed to {serverIp}: {ex.Message}. Retrying in {ReconnectDelaySeconds} seconds.");
                }
                await Task.Delay(TimeSpan.FromSeconds(ReconnectDelaySeconds));
            }
        }

        // SelectServerIpAddressAsync와 CheckAndCorrectAutoStartRegistration, ConnectAndProcessAsync 내부의
        // Console.WriteLine() 호출도 모두 Log()로 변경해야 합니다.
        // 편의를 위해 아래에 수정된 전체 메서드를 제공합니다.

        private static async Task<string> SelectServerIpAddressAsync()
        {
            Log("\nSearching for available network interfaces...");
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
                Log($"[Warning] Could not automatically detect network IP addresses: {ex.Message}");
            }
            while (true)
            {
                Log("\nPlease select the server IP address to connect to:");
                for (int i = 0; i < ipAddresses.Count; i++)
                {
                    Log($"  [{i + 1}] {ipAddresses[i]}");
                }
                Log("  [0] Enter a custom IP address");
                Console.Write("\nEnter your choice: "); // 사용자 입력 프롬프트는 그대로 둠
                string? choice = Console.ReadLine();
                if (int.TryParse(choice, out int selection))
                {
                    if (selection > 0 && selection <= ipAddresses.Count) return ipAddresses[selection - 1];
                    if (selection == 0)
                    {
                        Console.Write("Please enter the custom server IP address: ");
                        string? customIp = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(customIp) && IPAddress.TryParse(customIp.Trim(), out _))
                        {
                            return customIp.Trim();
                        }
                        else Log("Invalid IP address format. Please try again.");
                    }
                    else Log("Invalid selection. Please try again.");
                }
                else Log("Invalid input. Please enter a number.");
            }
        }

        private static void CheckAndCorrectAutoStartRegistration()
        {
            Log("[AutoStart] Checking startup registration...");
            string? registeredPath = AutoStarter.GetRegisteredPath();
            string expectedPath = $"\"{AutoStarter.ExecutablePath}\" /background";

            if (registeredPath == null)
            {
                Log("[AutoStart] Not registered for startup. Registering now...");
                AutoStarter.RegisterInStartup();
            }
            else if (!registeredPath.Equals(expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                Log($"[AutoStart] Registered path is outdated. Re-registering...");
                AutoStarter.RegisterInStartup();
            }
            else
            {
                Log("[AutoStart] Already registered correctly.");
            }
        }

        private static async Task ConnectAndProcessAsync(string serverIpAddress, int serverPort)
        {
            using (var client = new TcpClient())
            {
                Log($"\n[Network] Attempting to connect to {serverIpAddress}:{serverPort}...");
                await client.ConnectAsync(serverIpAddress, serverPort);
                Log("[Network] Successfully connected to server.");
                NetworkStream stream = client.GetStream();
                while (client.Connected)
                {
                    Log("[Network] Waiting for a request from the server...");
                    var buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        Log("[Network] Server closed the connection.");
                        break;
                    }
                    string serverRequest = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Log($"[Network] Received request: '{serverRequest}'");
                    if (serverRequest.Trim().Equals("CHECK_STATUS", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isRunning = StatusChecker.IsCatiaRunning(); // IsCatiaRunning 내부의 로그도 수정 필요
                        var response = new { IsCatiaRunning = isRunning };
                        string jsonResponse = JsonSerializer.Serialize(response);
                        byte[] dataToSend = Encoding.UTF8.GetBytes(jsonResponse);
                        await stream.WriteAsync(dataToSend, 0, dataToSend.Length);
                        Log($"[Network] Sent response to server: {jsonResponse}");
                    }
                }
            }
        }
    }
}

// 참고: StatusChecker.cs, AutoStarter.cs 등 다른 파일의 Console.WriteLine도
// 위와 같은 방식으로 Log() 메서드를 사용하도록 수정해주시면 좋습니다.
// 하지만 Program.cs만 수정해도 핵심 기능은 정상적으로 동작합니다.
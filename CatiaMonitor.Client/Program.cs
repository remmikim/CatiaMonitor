using System;
using System.Collections.Generic;
using System.Linq; // ★★★ 오류 해결을 위해 이 줄을 추가했습니다 ★★★
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
<<<<<<< HEAD

        // 콘솔 창이 보이는지 여부를 저장하는 정적 플래그
        private static bool s_isConsoleVisible = false;

        /// <summary>
        /// 콘솔이 보일 때만 메시지를 출력하는 래퍼 메서드입니다.
        /// </summary>
        /// <param name="message">출력할 메시지</param>
        private static void Log(string message)
        {
            if (s_isConsoleVisible)
            {
                Console.WriteLine(message);
            }
        }

        public static async Task Main(string[] args)
        {
            // 프로그램 인자에 "/background"가 있는지 확인하여 백그라운드 실행 여부 결정
            bool runInBackground = args.Contains("/background", StringComparison.OrdinalIgnoreCase);

            // 백그라운드 모드가 아닐 경우에만 콘솔 창을 생성하고 설정합니다.
            if (!runInBackground)
=======

        public static async Task Main(string[] args)
        {
            // <<★★★ 새로운 기능 시작 ★★★>>
            // 1. 닫기 버튼을 눌렀을 때 숨겨지도록 핸들러를 설정합니다.
            ConsoleManager.SetupCloseHandler();

            // 2. 프로그램 인자에 "/background"가 있으면 콘솔 창을 즉시 숨깁니다.
            if (args.Contains("/background", StringComparer.OrdinalIgnoreCase))
>>>>>>> parent of c80dd6d (클라이언트 앱 수정)
            {
                Console.WriteLine("[Info] Starting in background mode.");
                ConsoleManager.Hide();
            }
            // <<★★★ 새로운 기능 끝 ★★★>>

            Console.Title = "CATIA Monitor Client";
            Console.WriteLine("--- CATIA Monitor Client ---");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[Info] 이 콘솔 창의 닫기(X) 버튼을 눌러도 프로그램은 종료되지 않고 백그라운드에서 계속 실행됩니다.");
            Console.WriteLine("[Info] 프로그램을 완전히 종료하려면 작업 관리자에서 'CatiaMonitor.Client.exe' 프로세스를 직접 종료해야 합니다.");
            Console.ResetColor();

            // 서버 자동 탐색
            string? serverIp = await ServerFinder.DiscoverServerAsync();

            if (serverIp == null)
            {
<<<<<<< HEAD
                if (runInBackground)
                {
                    serverIp = "127.0.0.1";
                }
                else
                {
                    Log("\n[Discovery] Could not find a server automatically. Please select an IP address.");
                    serverIp = await SelectServerIpAddressAsync();
                }
=======
                Console.WriteLine("\nCould not find a server automatically. Please select from the list or enter a custom IP.");
                serverIp = await SelectServerIpAddressAsync();
>>>>>>> parent of c80dd6d (클라이언트 앱 수정)
            }

            Console.WriteLine($"[Config] Server IP has been set to: {serverIp}");

            // 자동 시작 등록 상태 확인 및 필요 시 재등록
            CheckAndCorrectAutoStartRegistration();

            // 서버에 계속해서 재연결을 시도하는 메인 루프
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

<<<<<<< HEAD
        /// <summary>
        /// 사용자가 서버 IP 주소를 수동으로 선택하거나 입력하도록 안내합니다.
        /// </summary>
        private static async Task<string> SelectServerIpAddressAsync()
        {
            Log("\n[Network] Searching for available network interfaces...");
=======
        private static async Task<string> SelectServerIpAddressAsync()
        {
            // (이전과 동일, 변경 없음)
            Console.WriteLine("\nSearching for available network interfaces...");
>>>>>>> parent of c80dd6d (클라이언트 앱 수정)
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
<<<<<<< HEAD
                Log("  [0] Enter a custom IP address");
=======
                Console.WriteLine("  [0] Enter a custom IP address");
>>>>>>> parent of c80dd6d (클라이언트 앱 수정)
                Console.Write("\nEnter your choice: ");
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
                        else Console.WriteLine("Invalid IP address format. Please try again.");
                    }
                    else Console.WriteLine("Invalid selection. Please try again.");
                }
                else Console.WriteLine("Invalid input. Please enter a number.");
            }
        }

        /// <summary>
<<<<<<< HEAD
        /// Windows 시작프로그램 레지스트리 등록 상태를 확인하고, 경로가 다르거나 없으면 새로 등록합니다.
=======
        /// <<★★★ 수정된 부분 ★★★>>
        /// 자동 시작 등록 경로를 확인할 때 "/background" 인자까지 포함하여 정확하게 비교합니다.
>>>>>>> parent of c80dd6d (클라이언트 앱 수정)
        /// </summary>
        private static void CheckAndCorrectAutoStartRegistration()
        {
            Console.WriteLine("[AutoStart] Checking startup registration...");
            string? registeredPath = AutoStarter.GetRegisteredPath();
            // 기대하는 경로에 /background 인자 추가
            string expectedPath = $"\"{AutoStarter.ExecutablePath}\" /background";

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

        /// <summary>
        /// 서버에 연결하고, 서버로부터 오는 요청을 처리하는 메인 통신 로직입니다.
        /// </summary>
        private static async Task ConnectAndProcessAsync(string serverIpAddress, int serverPort)
        {
            // (이전과 동일, 변경 없음)
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
<<<<<<< HEAD
                    string serverRequest = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    Log($"[Network] Received request: '{serverRequest}'");

                    if (serverRequest.Equals("CHECK_STATUS", StringComparison.OrdinalIgnoreCase))
=======
                    string serverRequest = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[Network] Received request: '{serverRequest}'");
                    if (serverRequest.Trim().Equals("CHECK_STATUS", StringComparison.OrdinalIgnoreCase))
>>>>>>> parent of c80dd6d (클라이언트 앱 수정)
                    {
                        bool isRunning = StatusChecker.IsCatiaRunning();
                        var response = new { IsCatiaRunning = isRunning };
                        string jsonResponse = JsonSerializer.Serialize(response);
                        byte[] dataToSend = Encoding.UTF8.GetBytes(jsonResponse);
                        await stream.WriteAsync(dataToSend, 0, dataToSend.Length);
                        Console.WriteLine($"[Network] Sent response to server: {jsonResponse}");
                    }
                    else if (serverRequest.Equals("SHUTDOWN_CATIA", StringComparison.OrdinalIgnoreCase))
                    {
                        Log("[Action] Received a command from the server to shut down CATIA.");
                        StatusChecker.TerminateCatiaProcess();
                    }
                }
            }
        }
    }
<<<<<<< HEAD
}
=======
}
>>>>>>> parent of c80dd6d (클라이언트 앱 수정)

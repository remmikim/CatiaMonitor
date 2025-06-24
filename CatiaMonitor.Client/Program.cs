using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CatiaMonitor.Client
{
    public class Program
    {
        // --- 설정 ---
        // 실제 운영 시에는 설정 파일(예: appsettings.json)에서 읽어오는 것이 좋습니다.
        private const string ServerIpAddress = "127.0.0.1"; // 서버의 IP 주소 (localhost 테스트용)
        private const int ServerPort = 12345;              // 서버와 통신할 포트 번호
        private const int ReconnectDelaySeconds = 30;      // 서버 연결 실패 시 재시도 대기 시간 (초)
        // --- --- ---

        public static async Task Main(string[] args)
        {
            Console.WriteLine("--- CATIA Monitor Client ---");

            // 1. Windows 시작 시 자동 실행되도록 등록
            //    만약 등록되어 있지 않다면 등록을 시도합니다.
            if (!AutoStarter.IsRegisteredForStartup())
            {
                Console.WriteLine("Application is not registered for startup. Registering...");
                AutoStarter.RegisterInStartup();
            }
            else
            {
                Console.WriteLine("Application is already registered for startup.");
            }

            // 2. 무한 루프를 돌며 서버에 계속 연결을 시도하고 통신합니다.
            while (true)
            {
                try
                {
                    // 서버에 연결을 시도합니다.
                    await ConnectAndProcessAsync();
                }
                catch (SocketException ex)
                {
                    // 서버가 꺼져 있거나 네트워크 문제로 연결에 실패한 경우
                    Console.WriteLine($"[Error] Connection failed: {ex.Message}. Retrying in {ReconnectDelaySeconds} seconds.");
                }
                catch (Exception ex)
                {
                    // 기타 예상치 못한 오류 발생 시
                    Console.WriteLine($"[Error] An unexpected error occurred: {ex.Message}. Retrying in {ReconnectDelaySeconds} seconds.");
                }

                // 재연결 시도 전 잠시 대기하여 CPU 사용률 급증을 방지합니다.
                await Task.Delay(TimeSpan.FromSeconds(ReconnectDelaySeconds));
            }
        }

        /// <summary>
        /// 서버에 연결하고, 상태 확인 요청을 처리하는 메인 로직입니다.
        /// </summary>
        private static async Task ConnectAndProcessAsync()
        {
            // using 구문은 TcpClient 객체가 범위를 벗어날 때 자동으로 리소스를 해제(연결 종료)해줍니다.
            using (var client = new TcpClient())
            {
                Console.WriteLine($"Attempting to connect to server at {ServerIpAddress}:{ServerPort}...");
                await client.ConnectAsync(ServerIpAddress, ServerPort);
                Console.WriteLine("Successfully connected to the server.");

                NetworkStream stream = client.GetStream();

                // 클라이언트가 연결되어 있는 동안 계속 통신합니다.
                while (client.Connected)
                {
                    var buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    // 서버가 연결을 끊었거나 데이터가 없으면 루프를 탈출합니다.
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("Server closed the connection.");
                        break;
                    }

                    string serverRequest = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    
                    // 서버로부터 상태 확인 요청("CHECK_STATUS")을 받았는지 확인합니다.
                    if (serverRequest.Trim().Equals("CHECK_STATUS", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Received status check request from server.");

                        // CATIA 실행 상태를 확인합니다.
                        bool isRunning = StatusChecker.IsCatiaRunning();

                        // 서버에 보낼 응답 데이터를 JSON 형식으로 생성합니다.
                        var response = new { IsCatiaRunning = isRunning, Timestamp = DateTime.UtcNow };
                        string jsonResponse = JsonSerializer.Serialize(response);
                        byte[] dataToSend = Encoding.UTF8.GetBytes(jsonResponse);
                        
                        // 서버에 응답을 전송합니다.
                        await stream.WriteAsync(dataToSend, 0, dataToSend.Length);
                        Console.WriteLine($"Sent status to server: {jsonResponse}");
                    }
                }
            }
        }
    }
}

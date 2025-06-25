using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CatiaMonitor.Server
{
    public class Program
    {
        private const int TcpPort = 12345;
        private const int DiscoveryPort = 12346; // 탐색을 위한 새 포트
        private const string HttpUrl = "http://+:8080/";

        public static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("--- CATIA Monitor Server ---");
                Console.Title = "CATIA Monitor Server";

                var dbManager = new DatabaseManager();
                await dbManager.InitializeDatabaseAsync();

                var webServer = new WebServer(dbManager, HttpUrl);
                _ = Task.Run(webServer.Start);

                // <<<<< 새로운 기능 시작 >>>>>
                // 서버 탐색 응답 리스너 시작
                _ = Task.Run(StartDiscoveryListener);
                // <<<<< 새로운 기능 끝 >>>>>

                var tcpListener = new TcpListener(IPAddress.Any, TcpPort);
                tcpListener.Start();
                Console.WriteLine($"[TCP Server] Started. Listening for clients on port {TcpPort}...");

                while (true)
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    var handler = new ClientHandler(client, dbManager);
                    _ = Task.Run(handler.HandleClientAsync);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("=================================================");
                Console.WriteLine("    PROGRAM FAILED TO START (FATAL ERROR)");
                Console.WriteLine("=================================================");
                Console.WriteLine($"[Error Type]: {ex.GetType().FullName}");
                Console.WriteLine($"\n[Message]: {ex.Message}");
                Console.WriteLine("\n--- [Stack Trace] ---");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();

                if (ex is HttpListenerException httpEx && httpEx.ErrorCode == 5)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n--- [추정 원인 및 해결 방법] ---");
                    Console.WriteLine("오류 코드 5는 '액세스 거부'를 의미합니다. 이 프로그램이 네트워크 포트를 사용하려면 관리자 권한이 필요합니다.");
                    Console.WriteLine("Visual Studio 또는 이 프로그램을 '관리자 권한으로 실행' 하셨는지 다시 한번 확인해주세요.");
                    Console.ResetColor();
                }

                Console.WriteLine("\n프로그램을 시작할 수 없습니다. 아무 키나 눌러 종료하세요.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// <<<<< 새로운 기능 >>>>>
        /// UDP 브로드캐스트를 수신하여 서버의 존재를 알리는 응답을 보냅니다.
        /// </summary>
        private static async Task StartDiscoveryListener()
        {
            using (var udpServer = new UdpClient(DiscoveryPort))
            {
                Console.WriteLine($"[Discovery] Server discovery listener started on UDP port {DiscoveryPort}.");
                const string requestMessage = "CATIAMONITOR_DISCOVERY_REQUEST";
                const string responseMessage = "CATIAMONITOR_DISCOVERY_RESPONSE";
                var responseBytes = Encoding.UTF8.GetBytes(responseMessage);

                while (true)
                {
                    try
                    {
                        // 클라이언트로부터의 요청을 비동기적으로 기다립니다.
                        UdpReceiveResult result = await udpServer.ReceiveAsync();
                        string receivedString = Encoding.UTF8.GetString(result.Buffer);

                        // 약속된 요청 메시지인지 확인합니다.
                        if (receivedString.Equals(requestMessage))
                        {
                            Console.WriteLine($"[Discovery] Received a discovery request from {result.RemoteEndPoint}. Sending response.");
                            // 요청을 보낸 클라이언트에게만 응답을 보냅니다.
                            await udpServer.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Discovery] Error in discovery listener: {ex.Message}");
                    }
                }
            }
        }
    }
}

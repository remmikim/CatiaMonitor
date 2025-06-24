using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CatiaMonitor.Server
{
    public class Program
    {
        private const int TcpPort = 12345;
        private const string HttpUrl = "http://+:8080/";

        public static async Task Main(string[] args)
        {
            // 프로그램의 모든 로직을 최상위 try-catch 블록으로 감싸서
            // 어떤 예외가 발생하더라도 상세 내용을 출력하고 종료되도록 합니다.
            try
            {
                Console.WriteLine("--- CATIA Monitor Server ---");
                Console.Title = "CATIA Monitor Server";

                // 1. 데이터베이스 관리자 인스턴스 생성 및 초기화
                var dbManager = new DatabaseManager();
                await dbManager.InitializeDatabaseAsync();

                // 2. 웹 서버 인스턴스 생성 및 시작
                var webServer = new WebServer(dbManager, HttpUrl);
                _ = Task.Run(webServer.Start);

                // 3. TCP 리스너 설정 및 시작
                var tcpListener = new TcpListener(IPAddress.Any, TcpPort);
                tcpListener.Start();
                Console.WriteLine($"[TCP Server] Started. Listening for clients on port {TcpPort}...");

                // 무한 루프를 돌며 클라이언트의 연결을 계속해서 수락합니다.
                while (true)
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    var handler = new ClientHandler(client, dbManager);
                    _ = Task.Run(handler.HandleClientAsync);
                }
            }
            catch (Exception ex)
            {
                // 프로그램 시작 중 치명적인 오류 발생 시 상세 내용 출력
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("=================================================");
                Console.WriteLine("    PROGRAM FAILED TO START (FATAL ERROR)");
                Console.WriteLine("=================================================");
                Console.WriteLine($"[Error Type]: {ex.GetType().FullName}");
                Console.WriteLine($"\n[Message]: {ex.Message}");
                Console.WriteLine("\n--- [Stack Trace] ---");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();

                // 가장 흔한 원인인 관리자 권한 문제를 확인하고 팁을 제공합니다.
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
    }
}

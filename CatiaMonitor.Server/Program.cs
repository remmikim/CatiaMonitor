using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CatiaMonitor.Server
{
    public class Program
    {
        // --- 설정 ---
        private const int Port = 12345; // 클라이언트가 접속할 포트 번호
        // --- --- ---

        public static async Task Main(string[] args)
        {
            Console.WriteLine("--- CATIA Monitor Server ---");

            // 1. 데이터베이스 관리자 인스턴스 생성
            var dbManager = new DatabaseManager();

            try
            {
                // 2. 데이터베이스 초기화 (테이블 생성 등)
                await dbManager.InitializeDatabaseAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Fatal] Database initialization failed: {ex.Message}");
                Console.WriteLine("Server cannot start without a database. Press any key to exit.");
                Console.ReadKey();
                return; // DB 초기화 실패 시 서버 종료
            }


            // 3. 모든 IP 주소로부터의 연결을 수신 대기하는 TCP 리스너 설정
            var listener = new TcpListener(IPAddress.Any, Port);

            try
            {
                listener.Start();
                Console.WriteLine($"Server started. Listening for clients on port {Port}...");

                // 4. 무한 루프를 돌며 클라이언트의 연결을 계속해서 수락
                while (true)
                {
                    // 비동기적으로 클라이언트의 연결을 기다립니다.
                    TcpClient client = await listener.AcceptTcpClientAsync();

                    // 새 클라이언트가 연결되면, 해당 클라이언트 처리를 위한 ClientHandler 인스턴스를 생성합니다.
                    var handler = new ClientHandler(client, dbManager);
                    
                    // Task.Run을 사용하여 클라이언트 처리를 백그라운드 스레드에서 시작합니다.
                    // 이렇게 하면 메인 루프는 즉시 다음 클라이언트 연결을 기다릴 수 있어
                    // 여러 클라이언트를 동시에 처리할 수 있습니다.
                    _ = Task.Run(handler.HandleClientAsync);
                }
            }
            catch (Exception ex)
            {
                // 리스너 관련 오류 발생 시 처리
                Console.WriteLine($"[Fatal] An error occurred in the server listener: {ex.Message}");
            }
            finally
            {
                // 서버가 어떤 이유로든 종료될 때 리스너를 중지합니다.
                listener.Stop();
                Console.WriteLine("Server stopped.");
            }
        }
    }
}

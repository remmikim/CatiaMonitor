using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace CatiaMonitor.Server
{
    class Program
    {
        private const int TcpPort = 12345;
        private const string HttpUrl = "http://+:8080/";

        // 활성 클라이언트를 저장할 스레드 안전한 딕셔너리
        private static readonly ConcurrentDictionary<int, ClientHandler> s_activeClients = new();

        static async Task Main(string[] args)
        {
            Console.WriteLine("--- CATIA Monitor Server ---");
            Console.WriteLine("Initializing...");

            var dbManager = new DatabaseManager("catia_monitor.db");
            await dbManager.InitializeDatabaseAsync();

            // WebServer에 딕셔너리 인스턴스를 전달
            var webServer = new WebServer(HttpUrl, dbManager, s_activeClients);
            _ = Task.Run(webServer.Run); // 웹 서버를 백그라운드에서 실행

            var tcpListener = new TcpListener(IPAddress.Any, TcpPort);
            try
            {
                tcpListener.Start();
                Console.WriteLine($"[TCP Server] Started and listening on port {TcpPort}.");

                while (true)
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();

                    // 클라이언트 핸들러를 생성하기 전에 DB에서 ID를 먼저 확보하고 딕셔너리에 추가
                    _ = Task.Run(async () => {
                        string clientIp = ((IPEndPoint?)client.Client.RemoteEndPoint)?.Address.ToString() ?? "Unknown";
                        int clientId = await dbManager.EnsureClientExists(clientIp);

                        var handler = new ClientHandler(client, dbManager, clientId, s_activeClients);

                        if (s_activeClients.TryAdd(clientId, handler))
                        {
                            Console.WriteLine($"[System] Client {clientIp} (ID: {clientId}) added to active list. Total active clients: {s_activeClients.Count}");
                            await handler.HandleClientAsync();
                        }
                        else
                        {
                            Console.WriteLine($"[Warning] Client with ID {clientId} is already connected. Closing new connection.");
                            client.Close();
                        }
                    });
                }
            }
            catch (SocketException se)
            {
                Console.WriteLine($"[Error] A socket error occurred: {se.Message}");
                Console.WriteLine("Please ensure no other application is using port 12345 and you have network permissions.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] An unexpected error occurred: {ex.Message}");
                if (ex is HttpListenerException httpEx && httpEx.ErrorCode == 5)
                {
                    Console.WriteLine("[Hint] This error often means the program needs to be run with Administrator privileges to bind to the HTTP URL.");
                }
            }
            finally
            {
                tcpListener.Stop();
                Console.WriteLine("[TCP Server] Server stopped.");
            }
        }
    }
}
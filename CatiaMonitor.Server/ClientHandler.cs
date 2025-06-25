using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CatiaMonitor.Server
{
    public class ClientStatus { public bool IsCatiaRunning { get; set; } }

    public class ClientHandler
    {
        private readonly TcpClient _client;
        private readonly DatabaseManager _dbManager;
        private readonly string _clientIp;

        public ClientHandler(TcpClient client, DatabaseManager dbManager)
        {
            _client = client;
            _dbManager = dbManager;
            _clientIp = ((IPEndPoint?)_client.Client.RemoteEndPoint)?.Address.ToString() ?? "Unknown";
        }

        public async Task HandleClientAsync()
        {
            Console.WriteLine($"[Connection] Client connected: {_clientIp}");
            NetworkStream stream = _client.GetStream();

            // --- ▼ 해결 방안 적용 ▼ ---
            // 15초의 읽기 및 쓰기 타임아웃 설정
            stream.ReadTimeout = 15000;
            stream.WriteTimeout = 15000;
            // --- ▲ 해결 방안 적용 ▲ ---

            try
            {
                int clientId = await _dbManager.EnsureClientExists(_clientIp);
                Console.WriteLine($"[Database] Client ID for {_clientIp} is {clientId}.");

                while (_client.Connected)
                {
                    Console.WriteLine($"[Request -> {_clientIp}] Sending status check request.");
                    byte[] requestData = Encoding.UTF8.GetBytes("CHECK_STATUS");
                    await stream.WriteAsync(requestData, 0, requestData.Length);

                    var buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0) break;

                    string responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[Response <- {_clientIp}] Received: {responseJson}");

                    try
                    {
                        var status = JsonSerializer.Deserialize<ClientStatus>(responseJson);
                        if (status != null)
                        {
                            await _dbManager.LogUsage(clientId, status.IsCatiaRunning);
                            Console.WriteLine($"[Database] Logged status for client {clientId}: IsCatiaRunning = {status.IsCatiaRunning}");
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"[Error] Failed to parse JSON from {_clientIp}. Details: {jsonEx.Message}");
                    }

                    Console.WriteLine($"[Handler] Waiting 1 minute before next check for {_clientIp}.");
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            }
            catch (IOException ex)
            {
                // 타임아웃 발생 시 또는 네트워크 문제 발생 시 "Connection lost" 메시지 출력
                Console.WriteLine($"[Warning] Connection lost for {_clientIp}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] An error occurred with client {_clientIp}: {ex.Message}");
            }
            finally
            {
                _client.Close();
                Console.WriteLine($"[Connection] Client disconnected: {_clientIp}");
            }
        }
    }
}
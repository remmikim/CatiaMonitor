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

            try
            {
                int clientId = await _dbManager.EnsureClientExists(_clientIp);
                Console.WriteLine($"[Database] Client ID for {_clientIp} is {clientId}.");

                // 클라이언트가 연결되어 있는 동안 상태 확인을 반복합니다.
                while (_client.Connected)
                {
                    // 1. 클라이언트에게 상태 확인 요청("CHECK_STATUS") 전송
                    Console.WriteLine($"[Request -> {_clientIp}] Sending status check request.");
                    byte[] requestData = Encoding.UTF8.GetBytes("CHECK_STATUS");
                    await stream.WriteAsync(requestData, 0, requestData.Length);

                    // 2. 클라이언트로부터 응답 수신
                    var buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0) break; // 클라이언트 연결 끊김

                    string responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[Response <- {_clientIp}] Received: {responseJson}");

                    // 3. 수신한 JSON 데이터를 역직렬화하고 DB에 로그 기록
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

                    // 4. 다음 요청 전 1분 대기
                    Console.WriteLine($"[Handler] Waiting 1 minute before next check for {_clientIp}.");
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            }
            catch (IOException ex)
            {
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

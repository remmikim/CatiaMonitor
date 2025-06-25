using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace CatiaMonitor.Server
{
    public class ClientStatus { public bool IsCatiaRunning { get; set; } }

    public class ClientHandler
    {
        private readonly TcpClient _client;
        private readonly DatabaseManager _dbManager;
        private readonly string _clientIp;
        private readonly int _clientId;
        private readonly ConcurrentDictionary<int, ClientHandler> _activeClients;
        private NetworkStream? _stream;

        public ClientHandler(TcpClient client, DatabaseManager dbManager, int clientId, ConcurrentDictionary<int, ClientHandler> activeClients)
        {
            _client = client;
            _dbManager = dbManager;
            _clientIp = ((IPEndPoint?)_client.Client.RemoteEndPoint)?.Address.ToString() ?? "Unknown";
            _clientId = clientId;
            _activeClients = activeClients;
        }

        // ★★★ 클라이언트에 종료 명령을 보내는 메서드 ★★★
        public async Task SendShutdownCommandAsync()
        {
            if (_stream != null && _client.Connected)
            {
                try
                {
                    Console.WriteLine($"[Command -> {_clientIp}] Sending SHUTDOWN_CATIA command.");
                    byte[] commandData = Encoding.UTF8.GetBytes("SHUTDOWN_CATIA");
                    await _stream.WriteAsync(commandData, 0, commandData.Length);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"[Error] Failed to send shutdown command to {_clientIp}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[Warning] Cannot send shutdown command. Client {_clientIp} is not connected.");
            }
        }

        public async Task HandleClientAsync()
        {
            Console.WriteLine($"[Connection] Client connected: {_clientIp} (ID: {_clientId})");
            _stream = _client.GetStream();
            _stream.ReadTimeout = 15000;
            _stream.WriteTimeout = 15000;

            try
            {
                while (_client.Connected)
                {
                    Console.WriteLine($"[Request -> {_clientIp}] Sending status check request.");
                    byte[] requestData = Encoding.UTF8.GetBytes("CHECK_STATUS");
                    await _stream.WriteAsync(requestData, 0, requestData.Length);

                    var buffer = new byte[1024];
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0) break;

                    string responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[Response <- {_clientIp}] Received: {responseJson}");

                    try
                    {
                        var status = JsonSerializer.Deserialize<ClientStatus>(responseJson);
                        if (status != null)
                        {
                            await _dbManager.LogUsage(_clientId, status.IsCatiaRunning);
                            Console.WriteLine($"[Database] Logged status for client {_clientId}: IsCatiaRunning = {status.IsCatiaRunning}");
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"[Error] Failed to parse JSON from {_clientIp}. Details: {jsonEx.Message}");
                    }

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
                // 활성 클라이언트 목록에서 자신을 제거
                _activeClients.TryRemove(_clientId, out _);
                _client.Close();
                Console.WriteLine($"[Connection] Client disconnected: {_clientIp} (ID: {_clientId}). Active clients: {_activeClients.Count}");
            }
        }
    }
}
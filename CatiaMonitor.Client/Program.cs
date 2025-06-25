// ... 기존 using 문들 ...

namespace CatiaMonitor.Client
{
    public class Program
    {
        // ... 기존 필드들 ...

        // ... 기존 Main 및 다른 메서드들 ...

        // ConnectAndProcessAsync 메서드만 수정 또는 교체
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
                    string serverRequest = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    Log($"[Network] Received request: '{serverRequest}'");

                    if (serverRequest.Equals("CHECK_STATUS", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isRunning = StatusChecker.IsCatiaRunning();
                        var response = new { IsCatiaRunning = isRunning };
                        string jsonResponse = JsonSerializer.Serialize(response);
                        byte[] dataToSend = Encoding.UTF8.GetBytes(jsonResponse);
                        await stream.WriteAsync(dataToSend, 0, dataToSend.Length);
                        Log($"[Network] Sent response to server: {jsonResponse}");
                    }
                    // ★★★ 종료 명령을 처리하는 로직 추가 ★★★
                    else if (serverRequest.Equals("SHUTDOWN_CATIA", StringComparison.OrdinalIgnoreCase))
                    {
                        Log("[Action] Received a command from the server to shut down CATIA.");
                        StatusChecker.TerminateCatiaProcess();
                        // 종료 후 별도의 응답은 보내지 않음 (필요 시 추가 가능)
                    }
                }
            }
        }
    }
}
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CatiaMonitor.Server
{
    /// <summary>
    /// Represents the response received from the client.
    /// 클라이언트로부터 받은 응답을 표현하는 데이터 모델입니다.
    /// </summary>
    public class ClientStatus
    {
        public bool IsCatiaRunning { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Handles all communication with a single connected client.
    /// 연결된 단일 클라이언트와의 모든 통신을 처리합니다.
    /// </summary>
    public class ClientHandler
    {
        private readonly TcpClient _client;
        private readonly DatabaseManager _dbManager;
        private readonly string _clientIp;

        /// <summary>
        /// Initializes a new instance of the ClientHandler class.
        /// ClientHandler 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="client">The TcpClient object for the connected client. 연결된 클라이언트의 TcpClient 객체입니다.</param>
        /// <param name="dbManager">The database manager instance for data operations. 데이터 작업을 위한 데이터베이스 관리자 인스턴스입니다.</param>
        public ClientHandler(TcpClient client, DatabaseManager dbManager)
        {
            _client = client;
            _dbManager = dbManager;
            // 클라이언트의 IP 주소를 저장합니다.
            _clientIp = ((IPEndPoint?)_client.Client.RemoteEndPoint)?.Address.ToString() ?? "Unknown";
        }

        /// <summary>
        /// Starts the process of handling client communication: sending requests and processing responses.
        /// 클라이언트 통신 처리 프로세스를 시작합니다: 요청을 보내고 응답을 처리합니다.
        /// </summary>
        public async Task HandleClientAsync()
        {
            Console.WriteLine($"[Connection] Client connected: {_clientIp}");
            NetworkStream stream = _client.GetStream();

            try
            {
                // 먼저 클라이언트 정보를 DB에 등록/업데이트하고 ID를 가져옵니다.
                // 호스트 이름은 Dns.GetHostEntry를 통해 가져올 수 있으나, 시간이 걸릴 수 있어 IP만 우선 사용합니다.
                int clientId = await _dbManager.EnsureClientExists(_clientIp);
                Console.WriteLine($"[Database] Client ID for {_clientIp} is {clientId}.");


                // 클라이언트가 연결되어 있는 동안 1분마다 상태 확인을 반복합니다.
                while (_client.Connected)
                {
                    // 1. 1분 대기 (첫 요청은 즉시 보낼 수 있도록 루프 끝에서 대기)
                    await Task.Delay(TimeSpan.FromMinutes(1));

                    // 2. 클라이언트에게 상태 확인 요청("CHECK_STATUS") 전송
                    byte[] requestData = Encoding.UTF8.GetBytes("CHECK_STATUS");
                    await stream.WriteAsync(requestData, 0, requestData.Length);
                    Console.WriteLine($"[Request] Sent status check to {_clientIp}.");

                    // 3. 클라이언트로부터 응답 수신
                    var buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        // 클라이언트가 연결을 정상적으로 종료했거나 연결이 끊어졌습니다.
                        break;
                    }

                    string responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[Response] Received from {_clientIp}: {responseJson}");

                    // 4. 수신한 JSON 데이터를 역직렬화하고 DB에 로그 기록
                    var status = JsonSerializer.Deserialize<ClientStatus>(responseJson);
                    if (status != null)
                    {
                        await _dbManager.LogUsage(clientId, status.IsCatiaRunning);
                        Console.WriteLine($"[Database] Logged status for client {clientId}: IsCatiaRunning = {status.IsCatiaRunning}");
                    }
                }
            }
            catch (IOException ex)
            {
                 // 클라이언트가 비정상적으로 연결을 종료했을 때(예: 컴퓨터 강제 종료) 발생할 수 있습니다.
                Console.WriteLine($"[Warning] Connection lost for {_clientIp}: {ex.Message}");
            }
            catch (Exception ex)
            {
                // 기타 예상치 못한 오류를 처리합니다.
                Console.WriteLine($"[Error] An error occurred with client {_clientIp}: {ex.Message}");
            }
            finally
            {
                // 루프가 종료되면(정상/오류 모두) 클라이언트 연결을 확실히 닫습니다.
                _client.Close();
                Console.WriteLine($"[Connection] Client disconnected: {_clientIp}");
            }
        }
    }
}

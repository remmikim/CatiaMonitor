using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CatiaMonitor.Client
{
    /// <summary>
    /// 로컬 네트워크에서 CatiaMonitor 서버를 자동으로 찾기 위한 클래스입니다.
    /// </summary>
    public static class ServerFinder
    {
        private const int DiscoveryPort = 12346; // 서버와 동일한 탐색 포트
        private const string RequestMessage = "CATIAMONITOR_DISCOVERY_REQUEST";
        private const string ResponseMessage = "CATIAMONITOR_DISCOVERY_RESPONSE";

        /// <summary>
        /// UDP 브로드캐스트를 사용하여 로컬 네트워크의 서버를 찾습니다.
        /// </summary>
        /// <param name="timeoutMilliseconds">응답을 기다릴 최대 시간 (밀리초)</param>
        /// <returns>서버를 찾으면 해당 IP 주소를, 찾지 못하면 null을 반환합니다.</returns>
        public static async Task<string?> DiscoverServerAsync(int timeoutMilliseconds = 3000)
        {
            using (var udpClient = new UdpClient())
            {
                // 브로드캐스트 활성화
                udpClient.EnableBroadcast = true;
                var requestBytes = Encoding.UTF8.GetBytes(RequestMessage);

                try
                {
                    // 로컬 네트워크 전체에 탐색 요청 메시지를 보냅니다.
                    await udpClient.SendAsync(requestBytes, requestBytes.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
                    Console.WriteLine("[Discovery] Sent broadcast message. Waiting for server response...");

                    // 서버로부터의 응답을 기다립니다. 지정된 시간이 지나면 타임아웃됩니다.
                    var receiveTask = udpClient.ReceiveAsync();
                    var completedTask = await Task.WhenAny(receiveTask, Task.Delay(timeoutMilliseconds));

                    if (completedTask == receiveTask)
                    {
                        // 응답이 도착한 경우
                        var result = await receiveTask;
                        string responseString = Encoding.UTF8.GetString(result.Buffer);

                        // 올바른 응답인지 확인
                        if (responseString.Equals(ResponseMessage))
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[Discovery] Server found at {result.RemoteEndPoint.Address}");
                            Console.ResetColor();
                            return result.RemoteEndPoint.Address.ToString();
                        }
                    }
                    else
                    {
                        // 타임아웃된 경우
                        Console.WriteLine("[Discovery] No server responded in time.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Discovery] An error occurred during discovery: {ex.Message}");
                }
            }
            return null;
        }
    }
}

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Concurrent; // 네임스페이스 추가

namespace CatiaMonitor.Server
{
    public class WebServer
    {
        private readonly HttpListener _listener;
        private readonly DatabaseManager _dbManager;
        // ★★★ 활성 클라이언트 목록을 참조하기 위한 필드 추가 ★★★
        private readonly ConcurrentDictionary<int, ClientHandler> _activeClients;

        // ★★★ 생성자에서 activeClients를 받도록 수정 ★★★
        public WebServer(string url, DatabaseManager dbManager, ConcurrentDictionary<int, ClientHandler> activeClients)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(url);
            _dbManager = dbManager;
            _activeClients = activeClients;
        }

        public async Task Run()
        {
            _listener.Start();
            Console.WriteLine("[Web Server] Started and listening for requests.");

            while (true)
            {
                var context = await _listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                try
                {
                    switch (request.Url?.AbsolutePath)
                    {
                        case "/api/status":
                            await HandleStatusRequest(response);
                            break;
                        // ★★★ '/api/shutdown' 경로에 대한 처리 추가 ★★★
                        case "/api/shutdown":
                            if (request.HttpMethod == "POST")
                            {
                                await HandleShutdownRequest(request, response);
                            }
                            else
                            {
                                SendResponse(response, HttpStatusCode.MethodNotAllowed);
                            }
                            break;
                        default:
                            if (request.Url?.AbsolutePath == "/" || request.Url?.AbsolutePath?.EndsWith("index.html") == true)
                            {
                                await HandleFileRequest(response, "dashboard/index.html");
                            }
                            else
                            {
                                SendResponse(response, HttpStatusCode.NotFound);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Web Server] Error processing request: {ex.Message}");
                    try
                    {
                        SendResponse(response, HttpStatusCode.InternalServerError);
                    }
                    catch (Exception innerEx)
                    {
                        Console.WriteLine($"[Web Server] Critical error: Could not send error response. {innerEx.Message}");
                    }
                }
                finally
                {
                    response.Close();
                }
            }
        }

        private async Task HandleStatusRequest(HttpListenerResponse response)
        {
            var summary = await _dbManager.GetClientStatusSummaryAsync();
            var json = JsonSerializer.Serialize(summary);
            SendResponse(response, HttpStatusCode.OK, "application/json", json);
        }

        private async Task HandleFileRequest(HttpListenerResponse response, string filePath)
        {
            if (File.Exists(filePath))
            {
                var content = await File.ReadAllBytesAsync(filePath);
                var mimeType = "text/html; charset=utf-8";
                SendResponse(response, HttpStatusCode.OK, mimeType, Encoding.UTF8.GetString(content));
            }
            else
            {
                SendResponse(response, HttpStatusCode.NotFound);
            }
        }

        // ★★★ 종료 요청을 처리하는 새 메서드 추가 ★★★
        private async Task HandleShutdownRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string jsonBody = await reader.ReadToEndAsync();
                var body = JsonSerializer.Deserialize<JsonElement>(jsonBody);

                if (body.TryGetProperty("clientId", out var clientIdElement) && clientIdElement.TryGetInt32(out int clientId))
                {
                    if (_activeClients.TryGetValue(clientId, out var handler))
                    {
                        await handler.SendShutdownCommandAsync();
                        var successResponse = new { success = true, message = "Shutdown command sent." };
                        SendResponse(response, HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(successResponse));
                    }
                    else
                    {
                        var notFoundResponse = new { success = false, message = "Client not found or is not connected." };
                        SendResponse(response, HttpStatusCode.NotFound, "application/json", JsonSerializer.Serialize(notFoundResponse));
                    }
                }
                else
                {
                    var badRequestResponse = new { success = false, message = "Invalid request body. 'clientId' is required." };
                    SendResponse(response, HttpStatusCode.BadRequest, "application/json", JsonSerializer.Serialize(badRequestResponse));
                }
            }
            catch (JsonException)
            {
                var badRequestResponse = new { success = false, message = "Invalid JSON format." };
                SendResponse(response, HttpStatusCode.BadRequest, "application/json", JsonSerializer.Serialize(badRequestResponse));
            }
        }

        private void SendResponse(HttpListenerResponse response, HttpStatusCode statusCode, string contentType = "text/plain", string content = "")
        {
            response.StatusCode = (int)statusCode;
            response.ContentType = contentType;
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
    }
}
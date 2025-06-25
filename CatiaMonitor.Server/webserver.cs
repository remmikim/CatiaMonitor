using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace CatiaMonitor.Server
{
    public class WebServer
    {
        private readonly HttpListener _listener;
        private readonly DatabaseManager _dbManager;
        private readonly ConcurrentDictionary<int, ClientHandler> _activeClients;

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
            Console.WriteLine($"[Web Server] Started. Listening on {_listener.Prefixes.First()}");

            while (true)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    await ProcessRequestAsync(context);
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 995)
                {
                    Console.WriteLine("[Web Server] Listener is shutting down.");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Web Server] Error processing request: {ex.Message}");
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                switch (request.Url?.AbsolutePath)
                {
                    case "/api/status":
                        await HandleStatusRequest(response);
                        break;
                    // ★★★ '/api/shutdown' 경로에 대한 처리 ★★★
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
                    case "/":
                    case "/index.html":
                        await HandleFileRequest(response, "dashboard/index.html", "text/html; charset=utf-8");
                        break;
                    default:
                        SendResponse(response, HttpStatusCode.NotFound);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Web Server] Error handling request for {request.Url}: {ex.Message}");
                if (!response.OutputStream.CanWrite) return;
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

        private async Task HandleStatusRequest(HttpListenerResponse response)
        {
            var summary = await _dbManager.GetClientStatusSummaryAsync();
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(summary, options);
            SendResponse(response, HttpStatusCode.OK, "application/json", json);
        }

        private async Task HandleFileRequest(HttpListenerResponse response, string filePath, string mimeType)
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, filePath);
            if (File.Exists(fullPath))
            {
                var content = await File.ReadAllBytesAsync(fullPath);
                SendResponse(response, HttpStatusCode.OK, mimeType, content);
            }
            else
            {
                Console.WriteLine($"[Web Server] File not found: {fullPath}");
                SendResponse(response, HttpStatusCode.NotFound);
            }
        }

        // ★★★ 종료 요청을 처리하는 메서드 ★★★
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
            SendResponse(response, statusCode, contentType, Encoding.UTF8.GetBytes(content));
        }

        private void SendResponse(HttpListenerResponse response, HttpStatusCode statusCode, string contentType, byte[] content)
        {
            response.StatusCode = (int)statusCode;
            response.ContentType = contentType;
            response.ContentLength64 = content.Length;
            response.OutputStream.Write(content, 0, content.Length);
        }
    }
}
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CatiaMonitor.Server
{
    /// <summary>
    /// Handles HTTP requests to serve the web dashboard and API data.
    /// 웹 대시보드 및 API 데이터를 제공하기 위한 HTTP 요청을 처리합니다.
    /// </summary>
    public class WebServer
    {
        private readonly HttpListener _listener = new HttpListener();
        private readonly DatabaseManager _dbManager;
        private readonly string _dashboardHtmlPath = Path.Combine(AppContext.BaseDirectory, "dashboard", "index.html");

        public WebServer(DatabaseManager dbManager, string url = "http://+:8080/")
        {
            _dbManager = dbManager;
            _listener.Prefixes.Add(url);
        }

        public async Task Start()
        {
            try
            {
                _listener.Start();
                Console.WriteLine($"[Web Server] Started. Listening for requests on the specified URL.");
                Console.WriteLine($"[Web Server] Access the dashboard at http://localhost:8080 or http://<your-server-ip>:8080");

                while (true)
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequestAsync(context));
                }
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"[Web Server] Critical error: {ex.Message}");
                Console.WriteLine("[Web Server] Tip: Try running the application as an administrator, or check if another program is using port 8080.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Web Server] An unexpected error occurred: {ex.Message}");
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                Console.WriteLine($"[Web Server] Request received: {request.HttpMethod} {request.Url?.AbsolutePath}");

                string responseString = "";
                string contentType = "text/plain; charset=utf-8";
                byte[] buffer;

                switch (request.Url?.AbsolutePath.ToLower())
                {
                    case "/":
                    case "/index.html":
                        if (File.Exists(_dashboardHtmlPath))
                        {
                            responseString = await File.ReadAllTextAsync(_dashboardHtmlPath);
                            contentType = "text/html; charset=utf-8";
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.NotFound;
                            responseString = $"Error 404: Dashboard file not found at '{_dashboardHtmlPath}'.";
                        }
                        break;

                    case "/api/status":
                        var statuses = await _dbManager.GetClientStatusSummaryAsync();

                        // <<★★★ 수정된 부분 ★★★>>
                        // JavaScript에서 사용하는 camelCase(예: ipAddress)로 속성 이름을 변환하는 옵션을 추가합니다.
                        var jsonOptions = new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        };
                        responseString = JsonSerializer.Serialize(statuses, jsonOptions);
                        // <<★★★ 수정 완료 ★★★>>

                        contentType = "application/json; charset=utf-8";
                        break;

                    default:
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        responseString = "Error 404: Page Not Found";
                        break;
                }

                buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentType = contentType;
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Web Server] Error during request processing: {ex.Message}");
                if (response.OutputStream.CanWrite)
                {
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
            finally
            {
                response.OutputStream.Close();
            }
        }
    }
}

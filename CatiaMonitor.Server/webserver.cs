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
            // http://+:8080/ : 로컬호스트를 포함한 모든 네트워크 인터페이스에서 8080 포트로 들어오는 요청을 수신합니다.
            // 이렇게 설정해야 다른 PC의 웹 브라우저에서도 서버 IP로 접속할 수 있습니다.
            _listener.Prefixes.Add(url);
        }

        /// <summary>
        /// Starts the web server to listen for incoming requests.
        /// 들어오는 요청을 수신 대기하기 위해 웹 서버를 시작합니다.
        /// </summary>
        public async Task Start()
        {
            try
            {
                _listener.Start();
                Console.WriteLine($"[Web Server] Started. Listening for requests on the specified URL.");
                Console.WriteLine($"[Web Server] Access the dashboard at http://localhost:8080 or http://<your-server-ip>:8080");

                // 서버가 중단되지 않고 계속해서 요청을 처리하도록 무한 루프를 실행합니다.
                while (true)
                {
                    // 비동기적으로 들어오는 요청을 기다립니다.
                    var context = await _listener.GetContextAsync();
                    // 요청 처리를 백그라운드 스레드에 맡기고 바로 다음 요청을 기다립니다.
                    _ = Task.Run(() => ProcessRequestAsync(context));
                }
            }
            catch (HttpListenerException ex)
            {
                // 포트가 이미 사용 중이거나 권한 문제 발생 시
                Console.WriteLine($"[Web Server] Critical error: {ex.Message}");
                Console.WriteLine("[Web Server] Tip: Try running the application as an administrator, or check if another program is using port 8080.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Web Server] An unexpected error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes an individual HTTP request.
        /// 개별 HTTP 요청을 처리합니다.
        /// </summary>
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
                    // 루트 경로 또는 index.html 요청 시
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
                            responseString = $"Error 404: Dashboard file not found at '{_dashboardHtmlPath}'. Make sure the file is copied to the output directory.";
                        }
                        break;

                    // API 경로 요청 시
                    case "/api/status":
                        var statuses = await _dbManager.GetClientStatusSummaryAsync();
                        responseString = JsonSerializer.Serialize(statuses, new JsonSerializerOptions { WriteIndented = true });
                        contentType = "application/json; charset=utf-8";
                        break;

                    // 그 외 모든 경로
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
                // 응답 스트림을 닫아 요청 처리를 완료합니다.
                response.OutputStream.Close();
            }
        }
    }
}

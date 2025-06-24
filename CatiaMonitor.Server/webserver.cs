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
    /// �� ��ú��� �� API �����͸� �����ϱ� ���� HTTP ��û�� ó���մϴ�.
    /// </summary>
    public class WebServer
    {
        private readonly HttpListener _listener = new HttpListener();
        private readonly DatabaseManager _dbManager;
        private readonly string _dashboardHtmlPath = Path.Combine(AppContext.BaseDirectory, "dashboard", "index.html");

        public WebServer(DatabaseManager dbManager, string url = "http://+:8080/")
        {
            _dbManager = dbManager;
            // http://+:8080/ : ����ȣ��Ʈ�� ������ ��� ��Ʈ��ũ �������̽����� 8080 ��Ʈ�� ������ ��û�� �����մϴ�.
            // �̷��� �����ؾ� �ٸ� PC�� �� ������������ ���� IP�� ������ �� �ֽ��ϴ�.
            _listener.Prefixes.Add(url);
        }

        /// <summary>
        /// Starts the web server to listen for incoming requests.
        /// ������ ��û�� ���� ����ϱ� ���� �� ������ �����մϴ�.
        /// </summary>
        public async Task Start()
        {
            try
            {
                _listener.Start();
                Console.WriteLine($"[Web Server] Started. Listening for requests on the specified URL.");
                Console.WriteLine($"[Web Server] Access the dashboard at http://localhost:8080 or http://<your-server-ip>:8080");

                // ������ �ߴܵ��� �ʰ� ����ؼ� ��û�� ó���ϵ��� ���� ������ �����մϴ�.
                while (true)
                {
                    // �񵿱������� ������ ��û�� ��ٸ��ϴ�.
                    var context = await _listener.GetContextAsync();
                    // ��û ó���� ��׶��� �����忡 �ñ�� �ٷ� ���� ��û�� ��ٸ��ϴ�.
                    _ = Task.Run(() => ProcessRequestAsync(context));
                }
            }
            catch (HttpListenerException ex)
            {
                // ��Ʈ�� �̹� ��� ���̰ų� ���� ���� �߻� ��
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
        /// ���� HTTP ��û�� ó���մϴ�.
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
                    // ��Ʈ ��� �Ǵ� index.html ��û ��
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

                    // API ��� ��û ��
                    case "/api/status":
                        var statuses = await _dbManager.GetClientStatusSummaryAsync();
                        responseString = JsonSerializer.Serialize(statuses, new JsonSerializerOptions { WriteIndented = true });
                        contentType = "application/json; charset=utf-8";
                        break;

                    // �� �� ��� ���
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
                // ���� ��Ʈ���� �ݾ� ��û ó���� �Ϸ��մϴ�.
                response.OutputStream.Close();
            }
        }
    }
}

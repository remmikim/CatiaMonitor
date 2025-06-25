using System;
using System.Diagnostics;

namespace CatiaMonitor.Client
{
    public static class StatusChecker
    {
        private const string CatiaProcessName = "CNEXT";

        public static bool IsCatiaRunning()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(CatiaProcessName);
                bool isRunning = processes.Length > 0;

                // 확인 결과를 콘솔에 로그로 남깁니다.
                if (isRunning)
                {
                    Console.WriteLine($"[StatusCheck] Found {processes.Length} instance(s) of '{CatiaProcessName}.exe'. CATIA is running.");
                }
                else
                {
                    Console.WriteLine($"[StatusCheck] '{CatiaProcessName}.exe' process not found. CATIA is not running.");
                }
                return isRunning;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StatusCheck] An error occurred while checking for process: {ex.Message}");
                return false;
            }
        }
    }
}

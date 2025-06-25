using System.Diagnostics;

namespace CatiaMonitor.Client
{
    public static class StatusChecker
    {
        private const string CatiaProcessName = "CNEXT";

        public static bool IsCatiaRunning()
        {
            // Console.WriteLine("[Status] Checking for CATIA process (CNEXT.exe)..."); // 로그는 Program.cs에서 관리
            return Process.GetProcessesByName(CatiaProcessName).Length > 0;
        }

        // ★★★ CATIA 프로세스를 종료하는 새 메서드 추가 ★★★
        public static void TerminateCatiaProcess()
        {
            Console.WriteLine($"[Action] Attempting to terminate '{CatiaProcessName}.exe' processes...");
            try
            {
                var processes = Process.GetProcessesByName(CatiaProcessName);
                if (processes.Length == 0)
                {
                    Console.WriteLine($"[Action] No '{CatiaProcessName}.exe' process found running.");
                    return;
                }

                foreach (var process in processes)
                {
                    Console.WriteLine($"[Action] Terminating process ID: {process.Id}");
                    process.Kill();
                    process.WaitForExit(); // 프로세스가 완전히 종료될 때까지 대기
                    Console.WriteLine($"[Action] Process ID: {process.Id} has been terminated.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to terminate CATIA process: {ex.Message}");
            }
        }
    }
}
using System;
using System.Diagnostics;

namespace CatiaMonitor.Client
{
    public static class StatusChecker
    {
        private const string CatiaProcessName = "CNEXT";

        public static bool IsCatiaRunning()
        {
            return Process.GetProcessesByName(CatiaProcessName).Length > 0;
        }

        // ★★★ CATIA 프로세스를 종료하는 메서드 ★★★
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
                    try
                    {
                        Console.WriteLine($"[Action] Terminating process ID: {process.Id}");
                        process.Kill();
                        process.WaitForExit(5000); // 5초간 대기
                        Console.WriteLine($"[Action] Process ID: {process.Id} has been terminated.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Error] Could not terminate process {process.Id}. It might already be closed. Details: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to terminate CATIA process: {ex.Message}");
            }
        }
    }
}
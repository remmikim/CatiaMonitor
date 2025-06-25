using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace CatiaMonitor.Client
{
    public static class AutoStarter
    {
        private const string AppName = "CatiaMonitorClient";
        private const string StartupRegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        // 현재 실행 중인 프로그램의 전체 경로를 가져옵니다.
        public static string ExecutablePath => Environment.ProcessPath ?? throw new InvalidOperationException("Cannot get executable path.");

        public static void RegisterInStartup()
        {
            try
            {
                using (RegistryKey? startupKey = Registry.CurrentUser.OpenSubKey(StartupRegistryKeyPath, true))
                {
                    if (startupKey == null)
                    {
                        Console.WriteLine($"[AutoStart] Error: Unable to open registry key: {StartupRegistryKeyPath}");
                        return;
                    }

                    string currentPath = $"\"{ExecutablePath}\"";
                    Console.WriteLine($"[AutoStart] Registering path in registry: {currentPath}");
                    startupKey.SetValue(AppName, currentPath);
                    Console.WriteLine($"[AutoStart] Successfully registered for startup.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutoStart] An error occurred while registering: {ex.Message}");
            }
        }

        // 레지스트리에 등록된 경로를 가져오는 메소드 추가
        public static string? GetRegisteredPath()
        {
            try
            {
                using (RegistryKey? startupKey = Registry.CurrentUser.OpenSubKey(StartupRegistryKeyPath, false))
                {
                    return startupKey?.GetValue(AppName) as string;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutoStart] An error occurred while reading registry: {ex.Message}");
                return null;
            }
        }
    }
}

using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace CatiaMonitor.Client
{
    public static class AutoStarter
    {
        private const string AppName = "CatiaMonitorClient";
        private const string StartupRegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

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

                    // ★★★ 실행 경로 뒤에 /background 인자를 추가하여 최소화 상태로 시작하도록 합니다 ★★★
                    string currentPath = $"\"{ExecutablePath}\" /background";

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
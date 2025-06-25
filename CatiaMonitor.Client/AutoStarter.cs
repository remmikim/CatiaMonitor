using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Reflection;

namespace CatiaMonitor.Client
{
    /// <summary>
    /// Provides functionality to manage the application's auto-start behavior
    /// by interacting with the Windows Registry.
    /// 윈도우 레지스트리와 상호작용하여 프로그램의 자동 시작 동작을 관리하는 기능을 제공합니다.
    /// </summary>
    public static class AutoStarter
    {
        // 레지스트리에 등록될 프로그램의 이름입니다.
        // 이 이름으로 시작프로그램 목록에 표시됩니다.
        private const string AppName = "CatiaMonitorClient";

        // Windows 시작프로그램 레지스트리 경로입니다.
        // HKEY_CURRENT_USER는 현재 로그인한 사용자에게만 적용되며, 관리자 권한이 필요 없습니다.
        private const string StartupRegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// Gets the full path of the current executable.
        /// 현재 실행 중인 프로그램의 전체 경로를 가져옵니다.
        /// </summary>
        private static string ExecutablePath
        {
            get
            {
                // .NET 6 이상에서는 Environment.ProcessPath를 사용하는 것이 더 간단하고 안정적입니다.
                // 이전 버전을 고려한다면 Assembly.GetExecutingAssembly().Location을 사용할 수 있습니다.
                return Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
            }
        }

        /// <summary>
        /// Registers the application to run at system startup for the current user.
        /// 현재 사용자로 로그인 시 프로그램이 자동으로 시작되도록 레지스트리에 등록합니다.
        /// </summary>
        public static void RegisterInStartup()
        {
            try
            {
                // 'Run' 레지스트리 키를 쓰기 모드로 엽니다. 키가 없으면 생성합니다.
                using (RegistryKey? startupKey = Registry.CurrentUser.OpenSubKey(StartupRegistryKeyPath, true))
                {
                    if (startupKey == null)
                    {
                        Console.WriteLine($"Error: Unable to open registry key: {StartupRegistryKeyPath}");
                        return;
                    }

                    // 현재 실행 파일 경로를 값으로 하여 레지스트리에 등록합니다.
                    // 이미 같은 이름의 값이 있어도 덮어쓰므로 경로가 변경되어도 문제가 없습니다.
                    startupKey.SetValue(AppName, $"\"{ExecutablePath}\"");
                    Console.WriteLine("Successfully registered the application for startup.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while registering for startup: {ex.Message}");
                // 프로덕션 환경에서는 로깅 프레임워크를 사용하는 것이 좋습니다.
                Debug.WriteLine(ex);
            }
        }

        /// <summary>
        /// Removes the application from the system startup for the current user.
        /// 현재 사용자의 시작프로그램 목록에서 이 프로그램을 제거합니다.
        /// </summary>
        public static void UnregisterFromStartup()
        {
            try
            {
                // 'Run' 레지스트리 키를 쓰기 모드로 엽니다.
                using (RegistryKey? startupKey = Registry.CurrentUser.OpenSubKey(StartupRegistryKeyPath, true))
                {
                    if (startupKey == null)
                    {
                        Console.WriteLine($"Warning: Registry key not found, nothing to unregister: {StartupRegistryKeyPath}");
                        return;
                    }

                    // 등록된 값이 있는지 확인합니다.
                    if (startupKey.GetValue(AppName) != null)
                    {
                        // 레지스트리에서 값을 제거합니다.
                        startupKey.DeleteValue(AppName, false);
                        Console.WriteLine("Successfully unregistered the application from startup.");
                    }
                    else
                    {
                        Console.WriteLine("Application was not registered for startup.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while unregistering from startup: {ex.Message}");
                Debug.WriteLine(ex);
            }
        }

        /// <summary>
        /// Checks if the application is currently registered to run at startup.
        /// 프로그램이 현재 시작프로그램으로 등록되어 있는지 확인합니다.
        /// </summary>
        /// <returns>True if registered, otherwise false. 등록되어 있으면 true, 아니면 false를 반환합니다.</returns>
        public static bool IsRegisteredForStartup()
        {
            try
            {
                // 'Run' 레지스트리 키를 읽기 모드로 엽니다.
                using (RegistryKey? startupKey = Registry.CurrentUser.OpenSubKey(StartupRegistryKeyPath, false))
                {
                    if (startupKey == null) return false;

                    // 지정된 이름의 값이 있고, 그 값이 현재 실행 파일 경로와 일치하는지 확인합니다.
                    object? value = startupKey.GetValue(AppName);
                    return value != null && value.ToString().Equals($"\"{ExecutablePath}\"", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while checking startup registration: {ex.Message}");
                Debug.WriteLine(ex);
                return false;
            }
        }
    }
}

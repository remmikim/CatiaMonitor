using System;
using System.Runtime.InteropServices;

namespace CatiaMonitor.Client
{
    /// <summary>
    /// P/Invoke를 사용하여 콘솔 창의 동작을 제어하는 유틸리티 클래스입니다.
    /// WinExe 형식에서도 필요 시 콘솔을 생성하는 기능을 포함합니다.
    /// </summary>
    public static class ConsoleManager
    {
        // Windows API 함수를 가져옵니다.
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole(); // ★★★ 새로 추가된 부분 ★★★

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate? HandlerRoutine, bool Add);

        // ShowWindow 함수의 인자값
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        // 콘솔 제어 이벤트 유형
        private const int CTRL_CLOSE_EVENT = 2;

        // 콘솔 제어 이벤트를 처리할 델리게이트 정의
        private delegate bool ConsoleCtrlDelegate(int sig);

        /// <summary>
        /// ★★★ 새로 추가된 메서드 ★★★
        /// 애플리케이션에 콘솔 창을 할당하여 표시합니다.
        /// </summary>
        public static void Show()
        {
            if (GetConsoleWindow() == IntPtr.Zero)
            {
                AllocConsole();
            }
        }

        /// <summary>
        /// 콘솔 창을 숨깁니다.
        /// </summary>
        public static void Hide()
        {
            IntPtr consoleHandle = GetConsoleWindow();
            if (consoleHandle != IntPtr.Zero)
            {
                ShowWindow(consoleHandle, SW_HIDE);
            }
        }

        /// <summary>
        /// 사용자가 콘솔 창의 닫기 버튼을 눌렀을 때의 동작을 설정합니다.
        /// </summary>
        public static void SetupCloseHandler()
        {
            // 닫기 이벤트를 가로채서 창을 숨기는 핸들러를 등록합니다.
            SetConsoleCtrlHandler(new ConsoleCtrlDelegate(ConsoleCtrlCheck), true);
        }

        /// <summary>
        /// 콘솔 제어 이벤트 콜백 함수입니다.
        /// </summary>
        private static bool ConsoleCtrlCheck(int sig)
        {
            // 닫기 버튼 이벤트(CTRL_CLOSE_EVENT)가 발생했을 때
            if (sig == CTRL_CLOSE_EVENT)
            {
                Console.WriteLine("[Info] Close button clicked. Hiding window to run in the background.");
                Hide(); // 콘솔 창을 숨깁니다.
                return true; // 이벤트를 처리했으므로 시스템이 프로그램을 종료하지 않습니다.
            }
            return false; // 다른 이벤트는 기본 처리되도록 합니다.
        }
    }
}
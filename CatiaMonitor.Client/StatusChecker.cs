using System;
using System.Diagnostics;

namespace CatiaMonitor.Client
{
    /// <summary>
    /// Provides functionality to check the status of specific applications, like CATIA.
    /// CATIA와 같은 특정 애플리케이션의 상태를 확인하는 기능을 제공합니다.
    /// </summary>
    public static class StatusChecker
    {
        // 확인할 CATIA V5의 프로세스 이름입니다.
        // 작업 관리자에서 실행되는 실제 프로세스 이름은 'CNEXT.exe'입니다.
        private const string CatiaProcessName = "CNEXT";

        /// <summary>
        /// Checks if the CATIA V5 process is currently running on the local machine.
        /// CATIA V5 프로세스가 현재 로컬 컴퓨터에서 실행 중인지 확인합니다.
        /// </summary>
        /// <returns>
        /// True if at least one CATIA process is running; otherwise, false.
        /// CATIA 프로세스가 하나 이상 실행 중이면 true, 아니면 false를 반환합니다.
        /// </returns>
        public static bool IsCatiaRunning()
        {
            try
            {
                // 시스템에서 지정된 이름과 일치하는 모든 프로세스의 배열을 가져옵니다.
                // GetProcessesByName 메서드는 '.exe' 확장자를 제외한 프로세스 이름을 사용합니다.
                Process[] processes = Process.GetProcessesByName(CatiaProcessName);

                // 해당 이름을 가진 프로세스가 하나라도 존재하면(배열의 길이가 0보다 크면),
                // CATIA가 실행 중인 것으로 간주합니다.
                if (processes.Length > 0)
                {
                    // 디버깅을 위해 찾은 프로세스 수를 출력할 수 있습니다.
                    // Console.WriteLine($"{processes.Length} instance(s) of {CatiaProcessName} found.");
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                // 프로세스 목록을 가져오는 동안 예외가 발생할 수 있습니다 (예: 접근 권한 문제).
                // 이 경우, 콘솔에 오류를 기록하고 안전하게 '실행되지 않음'으로 처리합니다.
                Console.WriteLine($"An error occurred while checking for the CATIA process: {ex.Message}");
                return false;
            }
        }
    }
}

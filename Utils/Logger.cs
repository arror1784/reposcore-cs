using System;

namespace RepoScore.Utils
{
    public static class Logger
    {
        /// <summary>
        /// 진행 상황 및 정보 로그를 stderr로 출력합니다.
        /// </summary>
        public static void LogInfo(string message)
        {
            Console.Error.WriteLine(message);
        }

        /// <summary>
        /// 오류 로그를 stderr로 출력합니다.
        /// </summary>
        public static void LogError(string message)
        {
            Console.Error.WriteLine(message);
        }

        /// <summary>
        /// 분석 결과 데이터를 stdout으로 출력합니다.
        /// </summary>
        public static void WriteOutput(string data)
        {
            Console.WriteLine(data);
        }
    }
}

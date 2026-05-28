using System.Collections.Generic;
using System.Linq;

namespace RepoScore.Data
{
    /// <summary>
    /// 리포트 데이터의 정렬 기준을 나타내는 열거형입니다.
    /// </summary>
    public enum SortBy { Score, Id }

    /// <summary>
    /// 리포트 데이터의 정렬 방향을 나타내는 열거형입니다.
    /// </summary>
    public enum SortOrder { Asc, Desc }

    /// <summary>
    /// 기여도 분석 리포트 데이터를 지정된 기준과 방향으로 정렬하는 클래스입니다.
    /// </summary>
    public static class ReportSorter
    {
        /// <summary>
        /// 리포트 데이터를 지정된 정렬 기준과 정렬 방향에 따라 정렬하여 반환합니다.
        /// </summary>
        /// <param name="data">정렬할 유저별 기여 수치 및 점수 데이터 목록</param>
        /// <param name="sortBy">정렬 기준 (점수 또는 아이디)</param>
        /// <param name="sortOrder">정렬 방향 (오름차순 또는 내림차순)</param>
        /// <returns>정렬이 완료된 리포트 데이터 목록</returns>
        public static List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)>
            SortReportData(
            List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)> data,
            SortBy sortBy,
            SortOrder sortOrder)
        {
            return sortBy switch
            {
                SortBy.Id => sortOrder == SortOrder.Asc
                    ? data.OrderBy(x => x.Id).ToList()
                    : data.OrderByDescending(x => x.Id).ToList(),
                _ => sortOrder == SortOrder.Asc
                    ? data.OrderBy(x => x.Score).ToList()
                    : data.OrderByDescending(x => x.Score).ToList()
            };
        }
    }
}

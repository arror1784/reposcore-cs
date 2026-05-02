using System.Collections.Generic;
using System.Linq;

namespace RepoScore.Data
{
    public static class ReportSorter
    {
        public static List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)>
        SortReportData(
            List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)> data,
            string sortBy,
            string sortOrder)
        {
            return sortBy.ToLower() switch
            {
                "score" => sortOrder.ToLower() == "asc"
                    ? data.OrderBy(x => x.Score).ToList()
                    : data.OrderByDescending(x => x.Score).ToList(),
                "id" => sortOrder.ToLower() == "asc"
                    ? data.OrderBy(x => x.Id).ToList()
                    : data.OrderByDescending(x => x.Id).ToList(),
                _ => sortOrder.ToLower() == "asc"
                    ? data.OrderBy(x => x.Score).ToList()
                    : data.OrderByDescending(x => x.Score).ToList()
            };
        }
    }
}

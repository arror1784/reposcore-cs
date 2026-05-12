using RepoScore.Data;
using RepoScore.Services;
using Xunit;

namespace RepoScore.Tests;

public class ReportFormatterTests
{
    [Fact]
    public void BuildClaimsReport_IncludesLinkedPrNumber_WhenIssueHasLinkedPr()
    {
        var data = new ClaimsData
        {
            ClaimedMap = new Dictionary<string, List<IssueRecord>>
            {
                ["arror1784"] = new List<IssueRecord>
                {
                    new IssueRecord
                    {
                        Number = 392,
                        Url = "https://github.com/oss2026hnu/reposcore-cs/issues/392",
                        HasPr = true,
                        LinkedPullRequests = new List<PRRecord>
                        {
                            new PRRecord { Number = 393, Url = "https://github.com/oss2026hnu/reposcore-cs/pull/393", Title = "Fix issue 392" }
                        }
                    }
                }
            }
        };

        string report = ReportFormatter.BuildClaimsReport(data, "repo");

        Assert.Contains("PR 생성됨 - #393", report);
    }
}

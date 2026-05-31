using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RepoScore.Services;
using System.Text.Json;
using Spectre.Console;

namespace RepoScore.Data
{
    /// <summary>
    /// 분석 결과 리포트의 출력 형식을 나타내는 열거형입니다.
    /// </summary>
    public enum OutputFormat { Csv, Txt, Html }

    /// <summary>
    /// 이슈 선점 현황 조회 시 출력 기준을 나타내는 열거형입니다.
    /// </summary>
    public enum ClaimsMode { Issue, User }

    /// <summary>
    /// 분석 결과를 다양한 형식(TXT, HTML, Claims)으로 포맷팅하여 문자열로 반환하는 클래스입니다.
    /// </summary>
    public static class ReportFormatter
    {
        /// <summary>
        /// 기여도 분석 결과를 사람이 읽기 쉬운 텍스트 표 형식으로 빌드합니다.
        /// 미인정 항목이 존재하는 경우 추가 제안 메시지를 포함합니다.
        /// </summary>
        /// <param name="repo">분석 대상 저장소 이름 (예: owner/repo)</param>
        /// <param name="reportData">유저별 기여 수치 및 점수 데이터 목록</param>
        /// <returns>포맷팅된 텍스트 리포트 문자열</returns>
        public static string BuildTextReport(
            string repo,
            List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)> reportData)
        {
            var rows = reportData.Select(r => new
            {
                Id = r.Id,
                Issues = $"{r.docIssues + r.featBugIssues} ({r.docIssues}/{r.featBugIssues})",
                PullRequests = $"{r.docPrs + r.featBugPrs + r.typoPrs} ({r.docPrs}/{r.featBugPrs}/{r.typoPrs})",
                Score = r.Score.ToString(),
                Raw = r
            }).ToList();

            using var sw = new StringWriter();
            var anConsole = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(sw),
                Ansi = AnsiSupport.No
            });
            
            // 텍스트 출력 시 콘솔 기본 80자 제한으로 인한 강제 줄바꿈 및 레이아웃 깨짐 방지
            anConsole.Profile.Width = 1024;

            var rejections = new List<(string userId, StringBuilder suggestions)>();

            anConsole.WriteLine($"=== {repo} 오픈소스 기여도 분석 리포트 ===");
            anConsole.WriteLine($"분석 일시: {DateTime.Now:yyyy-MM-dd HH:mm}");
            anConsole.WriteLine();

            var table = new Table()
                .Border(TableBorder.Markdown) // 영어를 사용하여 정렬 문제가 해결되었으므로 마크다운 표 테두리(|) 사용
                .AddColumn(new TableColumn("User").NoWrap()) // 텍스트 길이 초과 시 줄바꿈 방지
                .AddColumn(new TableColumn("Issues (Docs/Features)").RightAligned().NoWrap())
                .AddColumn(new TableColumn("PR (Docs/Features/Typo)").RightAligned().NoWrap())
                .AddColumn(new TableColumn("Score").RightAligned().NoWrap());

            foreach (var row in rows)
            {
                table.AddRow(row.Id, row.Issues, row.PullRequests, row.Score);

                var r = row.Raw;

                int maxAdditionalPr = 3 * Math.Max(r.featBugPrs, 1);
                int totalDocTypoPr = r.docPrs + r.typoPrs;
                int rejectedPr = Math.Max(0, totalDocTypoPr - maxAdditionalPr);

                int validPrCount = r.featBugPrs + Math.Min(totalDocTypoPr, maxAdditionalPr);
                int maxIssueCount = 4 * validPrCount;
                int totalIssues = r.featBugIssues + r.docIssues;
                int rejectedIssue = Math.Max(0, totalIssues - maxIssueCount);

                if (rejectedPr > 0 || rejectedIssue > 0)
                {
                    var userRejections = new StringBuilder();
                    userRejections.AppendLine($"{row.Id}:");
                    userRejections.AppendLine($"   [미인정 항목] 문서/오타 PR {rejectedPr}개 초과(한도 {maxAdditionalPr}개) / 이슈 {rejectedIssue}개 초과(한도 {maxIssueCount}개)");

                    if (rejectedPr > 0)
                    {
                        int docSuggestionCount = (rejectedPr + 2) / 3;
                        userRejections.AppendLine($"   [추가 제안] 기능/버그 PR {docSuggestionCount}개 추가 시 문서PR 인정 한도 +{docSuggestionCount * 3}");
                    }

                    if (rejectedIssue > 0)
                    {
                        int issueSuggestionCount = (rejectedIssue + 3) / 4;
                        if (totalDocTypoPr < maxAdditionalPr)
                        {
                            userRejections.AppendLine($"   [추가 제안] 문서 PR {issueSuggestionCount}개 추가 혹은 기능/버그 PR {issueSuggestionCount}개 추가시 이슈 인정한도 +{issueSuggestionCount * 4}");
                        }
                        else
                        {
                            userRejections.AppendLine($"   [추가 제안] 기능/버그 PR {issueSuggestionCount}개 추가시 이슈 인정한도 +{issueSuggestionCount * 4}");
                        }
                    }

                    rejections.Add((row.Id, userRejections));
                }
            }

            anConsole.Write(table);

            // 미인정 항목이 있는 경우 최하단에 표시
            if (rejections.Count > 0)
            {
                anConsole.WriteLine();
                anConsole.WriteLine("=== 미인정 항목 및 추가 제안 ===");
                anConsole.WriteLine();

                foreach (var (userId, suggestions) in rejections)
                {
                    anConsole.Write(suggestions.ToString());
                    anConsole.WriteLine();
                }
            }

            return sw.ToString();
        }

        /// <summary>
        /// 기여도 분석 결과를 Chart.js 기반의 인터랙티브 막대 차트가 포함된 HTML 리포트로 빌드합니다.
        /// </summary>
        /// <param name="repoName">분석 대상 저장소 이름 (예: owner/repo)</param>
        /// <param name="reportData">유저별 기여 수치 및 점수 데이터 목록</param>
        /// <returns>포맷팅된 HTML 리포트 문자열</returns>
        public static string BuildHtmlReport(string repoName, List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)> reportData)
        {
            var labels = JsonSerializer.Serialize(reportData.Select(r => $"{r.Id} (점수: {r.Score})"));
            var featBugPrData = JsonSerializer.Serialize(reportData.Select(r => r.featBugPrs));
            var docPrData = JsonSerializer.Serialize(reportData.Select(r => r.docPrs));
            var typoPrData = JsonSerializer.Serialize(reportData.Select(r => r.typoPrs));
            var featBugIssueData = JsonSerializer.Serialize(reportData.Select(r => r.featBugIssues));
            var docIssueData = JsonSerializer.Serialize(reportData.Select(r => r.docIssues));

            int chartHeight = Math.Max(400, reportData.Count * 30);

            return $@"
<!DOCTYPE html>
<html lang=""ko"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>RepoScore Report - {repoName}</title>
    <script src=""https://cdn.jsdelivr.net/npm/chart.js""></script>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Helvetica, Arial, sans-serif, ""Apple Color Emoji"", ""Segoe UI Emoji""; margin: 2em; background-color: #f6f8fa; color: #24292e; }}
        h1 {{ border-bottom: 1px solid #e1e4e8; padding-bottom: 0.3em; }}
        .chart-container {{ position: relative; height: {chartHeight}px; width: 90vw; margin-top: 2em; background-color: #fff; border: 1px solid #e1e4e8; border-radius: 6px; padding: 1em; }}
    </style>
</head>
<body>
    <h1>RepoScore Report: {repoName}</h1>
    <div class=""chart-container"">
        <canvas id=""contributionChart""></canvas>
    </div>
    <script>
        const ctx = document.getElementById('contributionChart');
        new Chart(ctx, {{
            type: 'bar',
            data: {{
                labels: {labels},
                datasets: [
                    {{ label: '문서 이슈', data: {docIssueData}, backgroundColor: '#a2eeef' }},
                    {{ label: '기능/버그 이슈', data: {featBugIssueData}, backgroundColor: '#28a745' }},
                    {{ label: '오타 PR', data: {typoPrData}, backgroundColor: '#fbca04' }},
                    {{ label: '문서 PR', data: {docPrData}, backgroundColor: '#0366d6' }},
                    {{ label: '기능/버그 PR', data: {featBugPrData}, backgroundColor: '#d73a49' }}
                ]
            }},
            options: {{
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                scales: {{ x: {{ stacked: true }}, y: {{ stacked: true }} }},
                plugins: {{
                    title: {{ display: true, text: '사용자별 기여 항목 분포' }},
                    legend: {{ position: 'top' }}
                }}
            }}
        }});
    </script>
</body>
</html>
";
        }

        /// <summary>
        /// 이슈 선점 현황 데이터를 지정된 출력 모드에 따라 텍스트 리포트로 빌드합니다.
        /// </summary>
        /// <param name="data">선점된 이슈 맵과 미선점 이슈 URL 목록을 포함하는 선점 현황 데이터</param>
        /// <param name="mode">출력 기준 모드 (이슈별 또는 유저별)</param>
        /// <returns>포맷팅된 선점 현황 리포트 문자열</returns>
        public static string BuildClaimsReport(ClaimsData data, ClaimsMode mode)
        {
            var sb = new StringBuilder();

            if (data.ClaimedMap.Count == 0 && data.UnclaimedUrls.Count == 0)
            {
                return "최근 48시간 내 선점된 이슈가 없습니다.\n";
            }

            if (mode == ClaimsMode.User)
            {
                if (data.UnclaimedUrls.Count > 0)
                {
                    sb.AppendLine("미선점 이슈");
                    foreach (var url in data.UnclaimedUrls) sb.AppendLine($" - {url}");
                }

                if (data.ClaimedMap.Count > 0)
                {
                    sb.AppendLine("\n선점된 이슈");
                    foreach (var (login, claims) in data.ClaimedMap)
                    {
                        sb.AppendLine($"{login}");
                        foreach (var claim in claims)
                        {
                            sb.AppendLine($" - {claim.Url}");
                            if (claim.Labels.Count > 0) sb.AppendLine($"   라벨: {string.Join(", ", claim.Labels)}");
                            sb.AppendLine(FormatClaimStatus(claim));
                        }
                    }
                }
            }
            else
            {
                var claimedIssues = data.ClaimedMap.SelectMany(kv => kv.Value.Select(c => (Login: kv.Key, Claim: c)))
                                                  .OrderBy(x => x.Claim.Number).ToList();

                if (claimedIssues.Count > 0)
                {
                    sb.AppendLine("선점된 이슈");
                    foreach (var (login, claim) in claimedIssues)
                    {
                        sb.AppendLine($" #{claim.Number} {claim.Url}");
                        sb.AppendLine($"   선점자: {login}");
                        if (claim.Labels.Count > 0) sb.AppendLine($"   라벨: {string.Join(", ", claim.Labels)}");
                        sb.AppendLine(FormatClaimStatus(claim));
                    }
                }

                if (data.UnclaimedUrls.Count > 0)
                {
                    sb.AppendLine("\n미선점 이슈");
                    foreach (var url in data.UnclaimedUrls) sb.AppendLine($" - {url}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 선점된 이슈의 현재 상태(PR 연동 여부 또는 남은 시간)를 나타내는 문자열을 반환합니다.
        /// </summary>
        /// <param name="claim">상태를 포맷팅할 대상 이슈 기록</param>
        /// <returns>PR 연동 정보 또는 선점 만료까지 남은 시간 문자열</returns>
        public static string FormatClaimStatus(IssueRecord claim)
        {
            if (!claim.HasPr)
            {
                return FormatRemainingTime(claim.Remaining);
            }

            if (claim.LinkedPullRequests != null && claim.LinkedPullRequests.Count > 0)
            {
                var prNumbers = string.Join(", ", claim.LinkedPullRequests.Select(pr => $"#{pr.Number}"));
                return $"   PR 생성됨 - {prNumbers}";
            }

            return "   PR 생성됨";
        }

        /// <summary>
        /// 선점 만료까지 남은 시간을 "HH:MM:SS" 형식의 문자열로 반환합니다.
        /// 만료 기한이 지난 경우 "기한 초과" 문자열을 반환합니다.
        /// </summary>
        /// <param name="remaining">선점 만료까지 남은 시간</param>
        /// <returns>남은 시간 문자열 또는 기한 초과 메시지</returns>
        public static string FormatRemainingTime(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero) return "   기한 초과";
            return $"   남은 시간: {(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }
    }
}

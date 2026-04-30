using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cocona;
using RepoScore.Data;
using RepoScore.Services;
using Spectre.Console; // 라이브러리 추가

CoconaApp.Run((
    [Option('t', Description = "GitHub Token (미입력시 GITHUB_TOKEN 사용)")] string? token,
    [Option(Description = "최근 이슈 선점 현황 조회 (issue|user)")] string? claims,
    [Option('f', Description = "출력 형식 (csv, txt)")] string? format,
    [Option('o', Description = "출력 디렉토리 경로")] string? output,
    [Option(Description = "정렬 기준 (score | id)")] string? sortBy,
    [Option(Description = "정렬 방법 (asc | desc)")] string? sortOrder,
    [Option(Description = "이슈 선점 키워드 (쉼표 구분, 미입력시 기본값 사용)")] string? keywords,
    [Argument(Description = "대상 저장소 목록 (예: owner/repo1 owner/repo2)")] string[] repos
) =>
{
    token ??= Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    if (string.IsNullOrEmpty(token)) { Console.Error.WriteLine("오류: GitHub 토큰이 필요합니다."); return; }

    format ??= "csv";
    output ??= "./results";
    sortBy ??= "score";
    sortOrder ??= "desc";

    var allowedFormats = new[] { "csv", "txt" };
    if (!allowedFormats.Contains(format.ToLowerInvariant()))
    {
        Console.Error.WriteLine($"오류: 지원하지 않는 형식입니다. csv 또는 txt를 입력해 주세요. (입력값: {format})");
        return;
    }

    string[]? parsedKeywords = keywords != null
        ? keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        : null;

    foreach (var repo in repos)
    {
        var parts = repo.Split('/');
        if (parts.Length != 2) { Console.Error.WriteLine($"오류: '{repo}'는 'owner/repo' 형식이 아닙니다. 건너뜁니다."); continue; }

        string ownerName = parts[0];
        string repoName = parts[1];

        string repoOutput = repos.Length > 1
            ? Path.Combine(output, $"{ownerName}_{repoName}")
            : output;

        var service = new GitHubService(ownerName, repoName, token, parsedKeywords);

        try
        {
            if (claims != null)
            {
                AnsiConsole.MarkupLine($"[[[blue]{ownerName}/{repoName}[/]]] 최근 이슈 선점 현황을 조회합니다...\n");
                var mode = string.IsNullOrEmpty(claims) ? "issue" : claims;

                var claimsData = service.GetRecentClaimsData();
                var report = BuildClaimsReport(claimsData, mode);
                Console.Write(report);
                continue;
            }

            AnsiConsole.MarkupLine($"[yellow]{repo}[/] 기여자 데이터 수집 및 분석 중...");

            if (!Directory.Exists(repoOutput)) Directory.CreateDirectory(repoOutput);
            string cachePath = Path.Combine(repoOutput, "cache.json");
            var cache = CacheManager.LoadCache(cachePath, repo);

            DateTimeOffset? since = cache.LastAnalyzedAt > DateTimeOffset.MinValue ? cache.LastAnalyzedAt : null;

            if (since.HasValue)
            {
                Console.Error.WriteLine($"기존 캐시 존재: {since.Value.ToLocalTime():yyyy-MM-dd HH:mm}");
            }
            else
            {
                Console.Error.WriteLine("기존 캐시 없음: 전체 데이터를 수집합니다.");
            }

            List<string> contributors = service.GetAllContributors();
            if (contributors.Count == 0) { Console.Error.WriteLine("조회된 기여자가 없습니다."); continue; }

            var reportData = new List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)>();

            foreach (var user in contributors)
            {
                var newClaims = service.GetClaims(user, since);
                var newPrs = service.GetPullRequests(user, since);

                if (!cache.UserClaims.ContainsKey(user)) cache.UserClaims[user] = new List<ClaimRecord>();
                if (!cache.UserPullRequests.ContainsKey(user)) cache.UserPullRequests[user] = new List<PRRecord>();

                foreach (var nc in newClaims)
                {
                    int index = cache.UserClaims[user].FindIndex(c => c.Number == nc.Number);
                    if (index >= 0) cache.UserClaims[user][index] = nc;
                    else cache.UserClaims[user].Add(nc);
                }

                foreach (var npr in newPrs)
                {
                    int index = cache.UserPullRequests[user].FindIndex(p => p.Number == npr.Number);
                    if (index >= 0) cache.UserPullRequests[user][index] = npr;
                    else cache.UserPullRequests[user].Add(npr);
                }

                var userClaimsToCalc = cache.UserClaims[user]
                    .Where(c => c.ClosedReason != IssueClosedStateReason.NotPlanned && c.ClosedReason != IssueClosedStateReason.Duplicate)
                    .ToList();

                var prsToCalc = cache.UserPullRequests[user]
                    .Where(p => p.IsMerged)
                    .ToList();

                var featureBugPrs = prsToCalc.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Bug) || p.Labels.Contains(GitHubIssuePrLabel.Enhancement)).ToList();
                var docPrs = prsToCalc.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Documentation)).ToList();
                var typoPrs = prsToCalc.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Typo)).ToList();
                var featureBugIssues = userClaimsToCalc.Where(c => c.Labels.Contains(GitHubIssuePrLabel.Bug) || c.Labels.Contains(GitHubIssuePrLabel.Enhancement)).ToList();
                var docIssues = userClaimsToCalc.Where(c => c.Labels.Contains(GitHubIssuePrLabel.Documentation)).ToList();

                int finalScore
                    = ScoreCalculator.CalculateFinalScore(featureBugPrs.Count, docPrs.Count, typoPrs.Count, featureBugIssues.Count, docIssues.Count);

                reportData.Add((user, docIssues.Count, featureBugIssues.Count, typoPrs.Count, docPrs.Count, featureBugPrs.Count, finalScore));
            }

            CacheManager.SaveCache(cachePath, cache);
            Console.Error.WriteLine($"캐시 갱신 및 저장 완료: {cachePath}");

            reportData = SortReportData(reportData, sortBy, sortOrder);

            // CSV 데이터 파일 생성
            var csv = new StringBuilder();
            csv.AppendLine("아이디, 문서이슈, 버그/기능이슈, 오타PR, 문서PR, 버그/기능PR, 총점");
            foreach (var r in reportData) csv.AppendLine($"{r.Id}, {r.docIssues}, {r.featBugIssues}, {r.typoPrs}, {r.docPrs}, {r.featBugPrs}, {r.Score}");

            string csvPath = Path.Combine(repoOutput, "results.csv");
            File.WriteAllText(csvPath, csv.ToString(), Encoding.UTF8);
            Console.Error.WriteLine($"기본 데이터(CSV) 저장 완료: {csvPath}");

            // TXT 리포트 생성 및 화면 출력
            if (format.ToLower() == "txt")
            {
                string txtPath = Path.Combine(repoOutput, "results.txt");
                string txtContent = BuildTextReport(repo, reportData);

                File.WriteAllText(txtPath, txtContent, Encoding.UTF8);
                Console.Error.WriteLine($"가독성 리포트(TXT) 추가 저장 완료: {txtPath}");

                // 화면에도 Spectre.Console을 사용하여 예쁘게 출력
                PrintSpectreTable(repo, reportData);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
        }
    }
});

static List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)>
SortReportData(List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)> data,
                string sortBy, string sortOrder)
{
    var sorted = sortBy.ToLower() switch
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

    return sorted;
}

// Spectre.Console을 사용하여 터미널에 직접 출력하는 함수
static void PrintSpectreTable(string repo, List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)> reportData)
{
    var table = new Table();
    table.Title($"[bold blue]=== {repo} 기여도 리포트 ===[/]");
    table.Border(TableBorder.Rounded);

    table.AddColumn(new TableColumn("[yellow]유저[/]").Centered());
    table.AddColumn(new TableColumn("[yellow]이슈/PR[/]").Centered());
    table.AddColumn(new TableColumn("[yellow]점수[/]").Centered());

    foreach (var r in reportData)
    {
        table.AddRow(
            r.Id,
            $"{r.docIssues + r.featBugIssues}/{r.typoPrs + r.docPrs + r.featBugPrs}",
            $"[green]{r.Score}[/]"
        );
    }

    AnsiConsole.Write(table);
}

static string BuildTextReport(
    string repo,
    List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)> reportData)
{
    var console = AnsiConsole.Create(new AnsiConsoleSettings
    {
        Ansi = AnsiSupport.No,
        Interactive = InteractionSupport.No
    });

    var recorder = new Recorder(console);

    var table = new Table();
    table.Title($"=== {repo} 오픈소스 기여도 분석 리포트 ===");
    table.Caption($"분석 일시: {DateTime.Now:yyyy-MM-dd HH:mm}");

    table.AddColumn("유저");
    table.AddColumn("이슈/PR");
    table.AddColumn("점수");

    foreach (var r in reportData)
    {
        table.AddRow(r.Id, $"{r.docIssues + r.featBugIssues}/{r.typoPrs + r.docPrs + r.featBugPrs}", r.Score.ToString());
    }

    recorder.Write(table);
    return recorder.ExportText();
}

static string BuildClaimsReport(ClaimsData data, string mode)
{
    var sb = new StringBuilder();

    if (data.ClaimedMap.Count == 0 && data.UnclaimedUrls.Count == 0)
    {
        return "최근 48시간 내 선점된 이슈가 없습니다.\n";
    }

    if (mode == "user")
    {
        if (data.UnclaimedUrls.Count > 0)
        {
            sb.AppendLine("[yellow]미선점 이슈[/]");
            foreach (var url in data.UnclaimedUrls) sb.AppendLine($" - {url}");
        }

        if (data.ClaimedMap.Count > 0)
        {
            sb.AppendLine("\n[green]선점된 이슈[/]");
            foreach (var (login, claims) in data.ClaimedMap)
            {
                sb.AppendLine($"[bold]{login}[/]");
                foreach (var claim in claims)
                {
                    sb.AppendLine($" - {claim.Url}");
                    if (claim.Labels.Count > 0) sb.AppendLine($"   라벨: {string.Join(", ", claim.Labels)}");
                    sb.AppendLine(claim.HasPr ? "   PR 생성됨" : FormatRemainingTime(claim.Remaining));
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
            sb.AppendLine("[green]선점된 이슈[/]");
            foreach (var (login, claim) in claimedIssues)
            {
                sb.AppendLine($" #{claim.Number} {claim.Url}");
                sb.AppendLine($"   선점자: {login}");
                if (claim.Labels.Count > 0) sb.AppendLine($"   라벨: {string.Join(", ", claim.Labels)}");
                sb.AppendLine(claim.HasPr ? "   PR 생성됨" : FormatRemainingTime(claim.Remaining));
            }
        }

        if (data.UnclaimedUrls.Count > 0)
        {
            sb.AppendLine("\n[yellow]미선점 이슈[/]");
            foreach (var url in data.UnclaimedUrls) sb.AppendLine($" - {url}");
        }
    }

    return sb.ToString();
}

static string FormatRemainingTime(TimeSpan remaining)
{
    if (remaining <= TimeSpan.Zero) return "   기한 초과";
    return $"   남은 시간: {(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
}

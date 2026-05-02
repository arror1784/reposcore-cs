using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cocona;
using RepoScore.Data;
using RepoScore.Services;
using Spectre.Console;
using System.Globalization;

CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

CoconaApp.Run((
[Argument(Description = "대상 저장소 목록 (예: owner/repo1 owner/repo2)")] string[] repos,
[Option('t', Description = "GitHub Token (미입력시 GITHUB_TOKEN 사용)")] string? token = null,
[Option(Description = "최근 이슈 선점 현황 조회 (issue|user)")] string? claims = null,
[Option('f', Description = "출력 형식 (csv, txt)")] string? format = null,
[Option('o', Description = "출력 디렉토리 경로")] string? output = null,
[Option(Description = "정렬 기준 (score | id)")] string? sortBy = null,
[Option(Description = "정렬 방법 (asc | desc)")] string? sortOrder= null,
[Option(Description = "이슈 선점 키워드 (쉼표 구분, 미입력시 기본값 사용)")] string? keywords = null,
[Option(Description = "캐시를 무시하고 전체 데이터를 다시 수집할지 여부")] bool noCache = false
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
        if (!Directory.Exists(repoOutput)) Directory.CreateDirectory(repoOutput);
        string cachePath = Path.Combine(repoOutput, "cache.json");
        var cache = CacheManager.LoadCache(cachePath, repo, noCache);

        var service = new GitHubService(ownerName, repoName, token, parsedKeywords);

        try
        {
            if (claims != null)
            {
                AnsiConsole.MarkupLine($"[[[blue]{ownerName}/{repoName}[/]]] 최근 이슈 선점 현황을 조회합니다...\n");
                var mode = string.IsNullOrEmpty(claims) ? "issue" : claims;

                var claimsData = service.GetRecentClaimsData();
                var report = ReportFormatter.BuildClaimsReport(claimsData, mode);
                Console.Write(report);
                continue;
            }

            AnsiConsole.MarkupLine($"[yellow]{repo}[/] 기여자 데이터 수집 및 분석 중...");

            if (!Directory.Exists(repoOutput)) Directory.CreateDirectory(repoOutput);

            if (!CacheManager.HasSameKeywords(cache, parsedKeywords))
            {
                Console.Error.WriteLine("키워드 옵션이 이전 실행과 달라 캐시를 무효화합니다.");

                cache = new RepoCache
                {
                    Repository = repo,
                    Keywords = parsedKeywords
                };
            }

            DateTimeOffset? since = cache.LastAnalyzedAt == DateTimeOffset.MinValue
                ? null
                : cache.LastAnalyzedAt;

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

                var userClaimsToCalc = cache.UserClaims[user];
                var prsToCalc = cache.UserPullRequests[user];

                var featureBugPrs = prsToCalc.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Bug) || p.Labels.Contains(GitHubIssuePrLabel.Enhancement)).ToList();
                var docPrs = prsToCalc.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Documentation)).ToList();
                var typoPrs = prsToCalc.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Typo)).ToList();
                var featureBugIssues = userClaimsToCalc.Where(c => c.Labels.Contains(GitHubIssuePrLabel.Bug) || c.Labels.Contains(GitHubIssuePrLabel.Enhancement)).ToList();
                var docIssues = userClaimsToCalc.Where(c => c.Labels.Contains(GitHubIssuePrLabel.Documentation)).ToList();

                int finalScore
                    = ScoreCalculator.CalculateFinalScore(featureBugPrs.Count, docPrs.Count, typoPrs.Count, featureBugIssues.Count, docIssues.Count);

                reportData.Add((user, docIssues.Count, featureBugIssues.Count, typoPrs.Count, docPrs.Count, featureBugPrs.Count, finalScore));
            }

            CacheManager.SaveCache(cachePath, cache, parsedKeywords);
            Console.Error.WriteLine($"캐시 갱신 및 저장 완료: {cachePath}");

            reportData = ReportSorter.SortReportData(reportData, sortBy, sortOrder);

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
                string txtContent = ReportFormatter.BuildTextReport(repo, reportData);

                File.WriteAllText(txtPath, txtContent, Encoding.UTF8);
                Console.Error.WriteLine($"가독성 리포트(TXT) 추가 저장 완료: {txtPath}");

                PrintSpectreTable(repo, reportData);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
        }
    }
});

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
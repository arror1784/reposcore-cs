using System.Text;
using Cocona;
using RepoScore.Data;
using RepoScore.Services;
using Spectre.Console;
using System.Globalization;
using Serilog;
using Serilog.Events;

CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

await CoconaApp.RunAsync(async (
[Argument(Description = "대상 저장소 목록 (예: owner/repo1 owner/repo2)")] string[] repos,
[Option('t', Description = "GitHub Token (미입력시 GITHUB_TOKEN 사용)", ValueName = "TOKEN")] string? token = null,
[Option(Description = "최근 이슈 선점 현황 조회")] ClaimsMode? claims = null,
[Option('f', Description = "출력 형식 (쉼표 구분, 예: csv,txt 허용값: csv,txt,html)")] string format = "csv",
[Option('o', Description = "출력 디렉토리 경로", ValueName = "DIR")] string output = "./results",
[Option(Description = "정렬 기준")] SortBy sortBy = SortBy.Score,
[Option(Description = "정렬 방법")] SortOrder sortOrder = SortOrder.Desc,
[Option(Description = "이슈 선점 키워드 (쉼표 구분, 미입력시 기본값 사용)", ValueName = "KEYWORDS")] string? keywords = null,
[Option(Description = "캐시를 무시하고 전체 데이터를 다시 수집할지 여부")] bool noCache = false,
[Option(Description = "로그 상세 수준 (-1=경고/에러만, 0=기본/진행 정보, 1=디버그, 2=상세 디버그)")] int verbose = 0
) =>
{
    var minimumLevel = verbose switch
    {
        < 0 => LogEventLevel.Warning,
        0 => LogEventLevel.Information,
        1 => LogEventLevel.Debug,
        _ => LogEventLevel.Verbose,
    };

    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Is(minimumLevel)
        .WriteTo.Console(
            standardErrorFromLevel: LogEventLevel.Verbose,
            outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

    // --format 옵션 파싱 및 유효성 검사
    var activeFormats = new HashSet<OutputFormat>();
    var invalidFormats = new List<string>();
    var allowedFormats = Enum.GetNames<OutputFormat>().Select(e => e.ToLowerInvariant());

    foreach (var f in format.Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
        var trimmedFormat = f.Trim();
        if (string.IsNullOrEmpty(trimmedFormat)) continue;

        if (Enum.TryParse<OutputFormat>(trimmedFormat, true, out var parsedFormat))
        {
            activeFormats.Add(parsedFormat);
        }
        else
        {
            invalidFormats.Add(trimmedFormat);
        }
    }

    if (invalidFormats.Any())
    {
        foreach (var invalid in invalidFormats)
            Log.Error("오류: '{Format}'은(는) 유효하지 않은 출력 형식입니다. 허용값: {Allowed}", invalid, string.Join(", ", allowedFormats));
        Log.Error("도움말을 보려면 --help 옵션을 사용하세요.");
        throw new CommandExitedException(1);
    }

    if (activeFormats.Count == 0) activeFormats.Add(OutputFormat.Csv);

    // 토큰 확인 단계를 위로 이동 (저장소 존재 여부 네트워크 검증에 필수적이기 때문)
    token ??= Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    if (string.IsNullOrEmpty(token))
    {
        Log.Error("오류: GitHub 토큰이 필요합니다.");
        Log.Error("도움말을 보려면 --help 옵션을 사용하세요.");
        throw new CommandExitedException(1);
    }

    // [리팩토링 포인트] 1단계&2단계: 저장소 유효성(형식 + 실제 존재 여부) 일괄 사전 검증
    var (malformed, notFound) = await GitHubService.ValidateRepositoriesAsync(repos, token);

    if (malformed.Count > 0)
    {
        foreach (var r in malformed)
            Log.Error("오류: '{Repo}'는 'owner/repo' 형식이 아닙니다.", r);
    }

    if (notFound.Count > 0)
    {
        foreach (var r in notFound)
            Log.Error("오류: '{Repo}' 저장소가 존재하지 않거나 접근할 수 없습니다. 이름 오타가 아닌지 확인해 주세요.", r);
    }

    if (malformed.Count > 0 || notFound.Count > 0)
    {
        Log.Error("도움말을 보려면 --help 옵션을 사용하세요.");
        throw new CommandExitedException(1);
    }

    string[]? parsedKeywords = keywords != null
        ? keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        : null;

    var repoResults = new System.Collections.Concurrent.ConcurrentDictionary<string, (Dictionary<string, List<IssueRecord>> UserIssues, Dictionary<string, List<PRRecord>> UserPrs)>();
    var repoFailures = new System.Collections.Concurrent.ConcurrentBag<string>();

    using var semaphore = new System.Threading.SemaphoreSlim(8);

    var repoTasks = repos.Select(async repo =>
    {
        await semaphore.WaitAsync();
        try
        {
            var parts = repo.Split('/');
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
                    Log.Information("[{Repo}] 최근 이슈 선점 현황을 조회합니다.", repo);

                    DateTimeOffset? claimsSince = (!noCache && cache.LastClaimsAnalyzedAt != DateTimeOffset.MinValue)
                        ? cache.LastClaimsAnalyzedAt
                        : (DateTimeOffset?)null;

                    if (claimsSince.HasValue)
                        Log.Information("[{Repo}] Claims 캐시 존재: {ClaimsSince:yyyy-MM-dd HH:mm} — 이후 변경분만 재조회합니다.", repo, claimsSince.Value.ToLocalTime());
                    else
                        Log.Information("[{Repo}] Claims 캐시 없음: 전체 데이터를 수집합니다.", repo);

                    var cachedOpenIssues = cache.CachedOpenIssues.Count > 0 ? cache.CachedOpenIssues : null;
                    var cachedOpenPrs = cache.CachedOpenPrs.Count > 0 ? cache.CachedOpenPrs : null;

                    var (claimsData, updatedOpenIssues, updatedOpenPrs) = await service.GetRecentClaimsDataAsync(
                        cachedOpenIssues, cachedOpenPrs, claimsSince);

                    var report = ReportFormatter.BuildClaimsReport(claimsData, (ClaimsMode)claims);
                    Console.Write($"=== {repo} 이슈 선점 현황 ===\n{report}\n");

                    CacheManager.SaveClaimsCache(cachePath, cache, updatedOpenIssues, updatedOpenPrs);
                    Log.Information("[{Repo}] Claims 캐시 갱신 완료: {CachePath}", repo, cachePath);

                    return;
                }

                Log.Information("[{Repo}] 기여자 데이터 수집 및 분석 중...", repo);

                if (!Directory.Exists(repoOutput)) Directory.CreateDirectory(repoOutput);

                if (!CacheManager.HasSameKeywords(cache, parsedKeywords))
                {
                    Log.Information("[{Repo}] 키워드 옵션이 이전 실행과 달라 캐시를 무효화합니다.", repo);

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
                    Log.Information("[{Repo}] 기존 캐시 존재: {AnalyzedAt:yyyy-MM-dd HH:mm}", repo, since.Value.ToLocalTime());
                else
                    Log.Information("[{Repo}] 기존 캐시 없음: 전체 데이터를 수집합니다.", repo);

                var prsTask = service.GetPullRequestsAsync(since);
                var issuesTask = service.GetIssuesAsync(since);
                await Task.WhenAll(prsTask, issuesTask);
                var allNewPrs = prsTask.Result;
                var allNewIssues = issuesTask.Result;
                Log.Debug("[{Repo}] PR {PrCount}개, Issue {IssueCount}개 조회", repo, allNewPrs.Count, allNewIssues.Count);

                List<string> contributors = allNewPrs.Select(p => p.AuthorLogin)
                    .Concat(allNewIssues.Select(i => i.AuthorLogin))
                    .Concat(cache.UserIssues.Keys)
                    .Concat(cache.UserPullRequests.Keys)
                    .Where(login => !string.IsNullOrEmpty(login))
                    .Distinct()
                    .ToList();

                if (contributors.Count == 0)
                {
                    Log.Warning("[{Repo}] 조회된 기여자가 없습니다.", repo);
                    return;
                }

                var reportData = new List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)>();
                var repoUserIssues = new Dictionary<string, List<IssueRecord>>();
                var repoUserPrs = new Dictionary<string, List<PRRecord>>();

                foreach (var user in contributors)
                {
                    var newIssues = allNewIssues.Where(i => i.AuthorLogin == user).ToList();
                    var newPrs = allNewPrs.Where(p => p.AuthorLogin == user).ToList();

                    if (!cache.UserIssues.ContainsKey(user)) cache.UserIssues[user] = new List<IssueRecord>();
                    if (!cache.UserPullRequests.ContainsKey(user)) cache.UserPullRequests[user] = new List<PRRecord>();

                    foreach (var ni in newIssues)
                    {
                        int index = cache.UserIssues[user].FindIndex(c => c.Number == ni.Number);
                        if (index >= 0) cache.UserIssues[user][index] = ni;
                        else cache.UserIssues[user].Add(ni);
                    }

                    foreach (var npr in newPrs)
                    {
                        int index = cache.UserPullRequests[user].FindIndex(p => p.Number == npr.Number);
                        if (index >= 0) cache.UserPullRequests[user][index] = npr;
                        else cache.UserPullRequests[user].Add(npr);
                    }

                    var userIssuesToCalc = cache.UserIssues[user];
                    var prsToCalc = cache.UserPullRequests[user];

                    var featureBugPrs = prsToCalc.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Bug) || p.Labels.Contains(GitHubIssuePrLabel.Enhancement)).ToList();
                    var docPrs = prsToCalc.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Documentation)).ToList();
                    var typoPrs = prsToCalc.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Typo)).ToList();
                    var featureBugIssues = userIssuesToCalc.Where(c => c.Labels.Contains(GitHubIssuePrLabel.Bug) || c.Labels.Contains(GitHubIssuePrLabel.Enhancement)).ToList();
                    var docIssues = userIssuesToCalc.Where(c => c.Labels.Contains(GitHubIssuePrLabel.Documentation)).ToList();

                    int finalScore = ScoreCalculator.CalculateFinalScore(featureBugPrs.Count, docPrs.Count, typoPrs.Count, featureBugIssues.Count, docIssues.Count);

                    reportData.Add((user, docIssues.Count, featureBugIssues.Count, typoPrs.Count, docPrs.Count, featureBugPrs.Count, finalScore));

                    if (repos.Length > 1)
                    {
                        repoUserIssues[user] = cache.UserIssues[user];
                        repoUserPrs[user] = cache.UserPullRequests[user];
                    }
                }

                CacheManager.SaveCache(cachePath, cache, parsedKeywords);
                Log.Information("[{Repo}] 캐시 갱신 및 저장 완료: {CachePath}", repo, cachePath);

                reportData = ReportSorter.SortReportData(reportData, sortBy, sortOrder);

                // ── 개별 저장소 리포트 출력 ──
                ExportReports(repo, reportData, activeFormats, repoOutput);

                if (repos.Length > 1)
                {
                    repoResults[repo] = (repoUserIssues, repoUserPrs);
                }
            }
            catch (Exception ex)
            {
                // [리팩토링 포인트] 수집 단단으로 존재 검증을 옮겼으므로, 강제 중단(Environment.Exit) 분기를 지우고 일관되게 repoFailures 처리
                Log.Error(ex, "[{Repo}] 처리 중 예외가 발생했습니다.", repo);
                repoFailures.Add(repo);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }).ToList();

    await Task.WhenAll(repoTasks);

    bool hasRepoFailure = !repoFailures.IsEmpty;

    if (repos.Length > 1 && repoResults.Count > 0)
    {
        try
        {
            Log.Information("전체 저장소 합산 리포트 생성 중...");

            var totalUserIssues = new Dictionary<string, List<IssueRecord>>();
            var totalUserPullRequests = new Dictionary<string, List<PRRecord>>();

            foreach (var (_, (userIssues, userPrs)) in repoResults)
            {
                MergeUserRecords(totalUserIssues, userIssues);
                MergeUserRecords(totalUserPullRequests, userPrs);
            }

            var totalReportData = new List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)>();
            var allUsers = totalUserIssues.Keys.Union(totalUserPullRequests.Keys).ToList();

            foreach (var user in allUsers)
            {
                var allIssues = totalUserIssues.TryGetValue(user, out var issues) ? issues : new List<IssueRecord>();
                var allPrs = totalUserPullRequests.TryGetValue(user, out var prs) ? prs : new List<PRRecord>();

                var featureBugPrs = allPrs.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Bug) || p.Labels.Contains(GitHubIssuePrLabel.Enhancement)).ToList();
                var docPrs = allPrs.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Documentation)).ToList();
                var typoPrs = allPrs.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Typo)).ToList();
                var featureBugIssues = allIssues.Where(c => c.Labels.Contains(GitHubIssuePrLabel.Bug) || c.Labels.Contains(GitHubIssuePrLabel.Enhancement)).ToList();
                var docIssues = allIssues.Where(c => c.Labels.Contains(GitHubIssuePrLabel.Documentation)).ToList();

                int finalScore = ScoreCalculator.CalculateFinalScore(featureBugPrs.Count, docPrs.Count, typoPrs.Count, featureBugIssues.Count, docIssues.Count);

                totalReportData.Add((user, docIssues.Count, featureBugIssues.Count, typoPrs.Count, docPrs.Count, featureBugPrs.Count, finalScore));
            }

            totalReportData = ReportSorter.SortReportData(totalReportData, sortBy, sortOrder);

            string totalLabel = string.Join(" + ", repos);
            ExportReports(totalLabel, totalReportData, activeFormats, output);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "합산 리포트 생성 중 예외가 발생했습니다.");
            throw new CommandExitedException(1);
        }
    }

    if (hasRepoFailure)
    {
        var failedRepos = string.Join(", ", repoFailures);
        Log.Error("다음 저장소 처리 중 오류가 발생했습니다: {FailedRepos}", failedRepos);
        throw new CommandExitedException(1);
    }
});


/// <summary>
/// CSV, TXT, HTML 서식에 맞춰 리포트 파일을 물리 디렉토리에 내보내는 공통 메서드
/// </summary>
static void ExportReports(
    string label,
    List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)> reportData,
    HashSet<OutputFormat> activeFormats,
    string outputDir)
{
    if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

    if (activeFormats.Contains(OutputFormat.Csv))
    {
        var csv = new StringBuilder();
        csv.AppendLine("아이디, 문서이슈, 버그/기능이슈, 오타PR, 문서PR, 버그/기능PR, 총점");
        foreach (var r in reportData)
            csv.AppendLine($"{r.Id}, {r.docIssues}, {r.featBugIssues}, {r.typoPrs}, {r.docPrs}, {r.featBugPrs}, {r.Score}");

        string csvPath = Path.Combine(outputDir, "results.csv");
        File.WriteAllText(csvPath, csv.ToString(), Encoding.UTF8);
        Log.Information("[{Label}] 데이터(CSV) 저장 완료: {CsvPath}", label, csvPath);
    }

    if (activeFormats.Contains(OutputFormat.Txt))
    {
        string txtPath = Path.Combine(outputDir, "results.txt");
        string txtContent = ReportFormatter.BuildTextReport(label, reportData);
        File.WriteAllText(txtPath, txtContent, Encoding.UTF8);
        Log.Information("[{Label}] 가독성 리포트(TXT) 저장 완료: {TxtPath}", label, txtPath);
    }

    if (activeFormats.Contains(OutputFormat.Html))
    {
        string htmlPath = Path.Combine(outputDir, "results.html");
        string htmlContent = ReportFormatter.BuildHtmlReport(label, reportData);
        File.WriteAllText(htmlPath, htmlContent, Encoding.UTF8);
        Log.Information("[{Label}] HTML 리포트 저장 완료: {HtmlPath}", label, htmlPath);
    }
}

static void MergeUserRecords<T>(
    Dictionary<string, List<T>> target,
    Dictionary<string, List<T>> source)
{
    foreach (var (user, items) in source)
    {
        if (!target.TryGetValue(user, out var targetList))
        {
            targetList = new List<T>();
            target[user] = targetList;
        }

        foreach (var item in items)
        {
            bool isDuplicate = false;

            if (item is IssueRecord issue)
            {
                isDuplicate = string.IsNullOrEmpty(issue.Url)
                    ? targetList.Any(t => t is IssueRecord i && string.IsNullOrEmpty(i.Url) && i.Number == issue.Number)
                    : targetList.Any(t => t is IssueRecord i && i.Url == issue.Url);
            }
            else if (item is PRRecord pr)
            {
                isDuplicate = string.IsNullOrEmpty(pr.Url)
                    ? targetList.Any(t => t is PRRecord p && string.IsNullOrEmpty(p.Url) && p.Number == pr.Number)
                    : targetList.Any(t => t is PRRecord p && p.Url == pr.Url);
            }

            if (!isDuplicate)
            {
                targetList.Add(item);
            }
        }
    }
}

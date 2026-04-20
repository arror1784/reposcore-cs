using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cocona;
using RepoScore.Data;
using RepoScore.Services;

var app = CoconaApp.Create();

app.AddCommand((
    [Argument(Description = "대상 저장소 (예: owner/repo)")] string repo,
    [Option('t', Description = "GitHub Token (미입력시 GITHUB_TOKEN 사용)")] string? token = null,
    [Option("show-claims", Description = "최근 이슈 선점 현황 조회 (issue|user)")] string? showClaims = null,
    [Option('f', Description = "출력 형식 (csv, txt)")] string format = "csv",
    [Option("output-dir", ['o'], Description = "출력 디렉토리 경로")] string outputDir = "./results"
) =>
{
    // 1. 토큰 및 저장소 검증
    token ??= Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    if (string.IsNullOrEmpty(token)) { Console.WriteLine("오류: GitHub 토큰이 필요합니다."); return; }

    var parts = repo.Split('/');
    if (parts.Length != 2) { Console.WriteLine("오류: 저장소 이름은 'owner/repo' 형식이어야 합니다."); return; }

    string ownerName = parts[0];
    string repoName = parts[1];
    var service = new GitHubService(ownerName, repoName, token);

    try
    {
        // 2. 이슈 선점 현황 조회 모드 (출력 전용 메서드로 전달)
        if (showClaims != null)
        {
            Console.WriteLine($"[{ownerName}/{repoName}] 최근 이슈 선점 현황을 조회합니다...\n");
            var mode = string.IsNullOrEmpty(showClaims) ? "issue" : showClaims;

            var claimsData = service.GetRecentClaimsData();
            PrintClaimsReport(claimsData, mode);
            return;
        }

        // 3. 전체 기여자 점수 산출
        Console.WriteLine($"🔍 {repo} 기여자 데이터 수집 및 분석 중...");
        List<string> contributors = service.GetAllContributors();
        if (contributors.Count == 0) { Console.WriteLine("조회된 기여자가 없습니다."); return; }

        var reportData = new List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)>();

        foreach (var user in contributors)
        {
            var claims = service.GetClaims(user);
            var prs = service.GetPullRequests(user);
            
            var featureBugPrs = prs.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Bug) || p.Labels.Contains(GitHubIssuePrLabel.Enhancement)).ToList();
            var docPrs = prs.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Documentation)).ToList();
            var typoPrs = prs.Where(p => p.Labels.Contains(GitHubIssuePrLabel.Typo)).ToList();
            var featureBugIssues = claims.Where(c => c.Labels.Contains(GitHubIssuePrLabel.Bug) || c.Labels.Contains(GitHubIssuePrLabel.Enhancement)).ToList();
            var docIssues = claims.Where(c => c.Labels.Contains(GitHubIssuePrLabel.Documentation)).ToList();

            int finalScore 
                = ScoreCalculator.CalculateFinalScore(featureBugPrs.Count, docPrs.Count, typoPrs.Count, featureBugIssues.Count, docIssues.Count);

            reportData.Add((user, docIssues.Count, featureBugIssues.Count, typoPrs.Count, docPrs.Count, featureBugPrs.Count, finalScore));
        }

        // 4. 출력 방식 분기 처리 및 파일 저장
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

        // CSV 데이터 파일 생성
        var csv = new StringBuilder();
        csv.AppendLine("아이디, 문서이슈, 버그/기능이슈, 오타PR, 문서PR, 버그/기능PR, 총점");
        foreach (var r in reportData) csv.AppendLine($"{r.Id}, {r.docIssues}, {r.featBugIssues}, {r.typoPrs}, {r.docPrs}, {r.featBugPrs}, {r.Score}");

        string csvPath = Path.Combine(outputDir, "results.csv");
        File.WriteAllText(csvPath, csv.ToString(), Encoding.UTF8);
        Console.WriteLine($"✅ 기본 데이터(CSV) 저장 완료: {csvPath}");

        // txt 파일 생성
        if (format.ToLower() == "txt")
        {
            var txt = new StringBuilder();
            txt.AppendLine($"=== {repo} 오픈소스 기여도 분석 리포트 ===");
            txt.AppendLine($"분석 일시: {DateTime.Now:yyyy-MM-dd HH:mm}");
            txt.AppendLine(new string('-', 50));
            foreach (var r in reportData)
            {
                txt.AppendLine($"👤 유저: {r.Id}");
                txt.AppendLine($"   - 이슈 처리: {r.docIssues + r.featBugIssues}회 / PR 제출: {r.typoPrs + r.docPrs + r.featBugPrs}회");
                txt.AppendLine($"   - 🏆 최종 기여 점수: {r.Score}점");
                txt.AppendLine(new string('-', 50));
            }

            string txtPath = Path.Combine(outputDir, "results.txt");
            File.WriteAllText(txtPath, txt.ToString(), Encoding.UTF8);
            Console.WriteLine($"✅ 가독성 리포트(TXT) 추가 저장 완료: {txtPath}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"데이터 처리 중 오류 발생: {ex.Message}");
    }
});

app.Run();

// 출력 전용 로직을 분리한 메서드
static void PrintClaimsReport(ClaimsData data, string mode)
{
    if (data.ClaimedMap.Count == 0)
    {
        Console.WriteLine("최근 48시간 내 선점된 이슈가 없습니다.");
        return;
    }

    if (mode == "user")
    {
        foreach (var (login, claims) in data.ClaimedMap)
        {
            Console.WriteLine($"👤 {login}");
            foreach (var claim in claims)
            {
                Console.WriteLine($" - {claim.Url}");
                if (claim.Labels.Count > 0)
                {
                    Console.WriteLine($"   🏷️ 라벨: {string.Join(", ", claim.Labels)}");
                }
                Console.WriteLine(claim.HasPr ? "   ✅ PR 생성됨" : FormatRemainingTime(claim.Remaining));
            }
        }
    }
    else
    {
        Console.WriteLine("📋 미선점 이슈");
        foreach (var url in data.UnclaimedUrls) Console.WriteLine($" - {url}");
        Console.WriteLine("\n📌 선점된 이슈");
        foreach (var (login, claims) in data.ClaimedMap)
        {
            Console.WriteLine($"👤 {login}");
            foreach (var claim in claims)
            {
                Console.WriteLine($" - {claim.Url}");
                if (claim.Labels.Count > 0)
                {
                    Console.WriteLine($"   🏷️ 라벨: {string.Join(", ", claim.Labels)}");
                }
                Console.WriteLine(claim.HasPr ? "   ✅ PR 생성됨" : FormatRemainingTime(claim.Remaining));
            }
        }
    }
}

static string FormatRemainingTime(TimeSpan remaining)
{
    if (remaining <= TimeSpan.Zero) return "   ⌛ 기한 초과";
    return $"   ⏳ 남은 시간: {(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
}

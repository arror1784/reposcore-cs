using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cocona;
using RepoScore.Data;
using RepoScore.Services;

var app = CoconaApp.Create();

app.AddCommand ((
    [Argument(Description = "대상 저장소 (예: owner/repo)")] string repo,
    [Option('t', Description = "GitHub Personal Access Token (미입력 시 환경변수 GITHUB_TOKEN 사용)")] string? token = null,
    [Option("show-claims", Description = "최근 이슈 선점 현황 조회 (issue|user, 기본값: issue)")] string? showClaims = null
) =>
{
    // 1. 토큰 처리
    if (string.IsNullOrEmpty(token))
        token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

    if (string.IsNullOrEmpty(token))
    {
        Console.WriteLine("오류: GitHub 토큰이 필요합니다. -t 옵션을 사용하거나 GITHUB_TOKEN 환경 변수를 설정해주세요.");
        return;
    }

    // 2. 저장소 이름 파싱
    var parts = repo.Split('/');
    if (parts.Length != 2)
    {
        Console.WriteLine("오류: 저장소 이름은 'owner/repo' 형식이어야 합니다.");
        return;
    }

    string ownerName = parts[0];
    string repoName = parts[1];
    var service = new GitHubService(ownerName, repoName, token);

    // 3. 이슈 선점 현황 조회 모드 (--show-claims)
    if (showClaims != null)
    {
        Console.WriteLine($"[{ownerName}/{repoName}] 최근 이슈 선점 현황을 조회합니다...\n");
        var mode = string.IsNullOrEmpty(showClaims) ? "issue" : showClaims;

        service.ShowRecentClaims(mode);
        return;
    }

    // 4. 전체 기여자 점수 산출 모드
    Console.WriteLine($"저장소: {repo}");
    Console.WriteLine($"토큰 인증 사용 중 (토큰: {token[..Math.Min(4, token.Length)]}***)");
    Console.WriteLine("모든 기여자의 데이터를 조회 중입니다. 시간이 조금 걸릴 수 있습니다...\n");

    try
    {
        // 전체 기여자 목록 조회
        List<string> contributors = service.GetAllContributors();

        if (contributors.Count == 0)
        {
            Console.WriteLine("조회된 기여자가 없습니다.");
            return;
        }

        Console.WriteLine("아이디, 문서이슈, 버그/기능이슈, 오타PR, 문서PR, 버그/기능PR, 총점");

        foreach (var user in contributors)
        {
            int totalPrs = service.GetPullRequestCount(user);
            int totalIssues = service.GetIssueCount(user);

            int finalScore = ScoreCalculator.CalculateFinalScore(
                featureBugPrCount: totalPrs,
                docPrCount: 0,
                typoPrCount: 0,
                featureBugIssueCount: totalIssues,
                docIssueCount: 0
            );

            Console.WriteLine($"{user}, 0, {totalIssues}, 0, 0, {totalPrs}, {finalScore}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"데이터 조회 중 오류가 발생했습니다: {ex.Message}");
    }
});

app.Run();

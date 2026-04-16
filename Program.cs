using System;
using System.Threading.Tasks;
using Cocona;
using RepoScore.Data;
using RepoScore.Services;

var app = CoconaApp.Create();

app.AddCommand(async (
    [Argument(Description = "대상 저장소 (예: owner/repo)")] string repo,
    [Argument(Description = "분석할 학생의 GitHub ID (show-claims 사용 시 생략 가능)")] string? userId = null,
    [Option('t', Description = "GitHub Personal Access Token (미입력 시 환경변수 GITHUB_TOKEN 사용)")] string? token = null,
    [Option("show-claims", Description = "최근 이슈 선점 현황 조회")] bool showClaims = false
) =>
{
    // 1. 토큰 처리 (환경 변수 Fallback)
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
    if (showClaims)
    {
        Console.WriteLine($"[{ownerName}/{repoName}] 최근 이슈 선점 현황을 조회합니다...\n");
        await service.ShowRecentClaimsAsync();
        return;
    }

    // 4. 기여도 산출 모드 (기본 동작)
    if (string.IsNullOrEmpty(userId))
    {
        Console.WriteLine("오류: 점수를 산출할 학생의 GitHub ID를 입력해주세요.");
        return;
    }

    Console.WriteLine($"저장소: {repo}");
    Console.WriteLine($"토큰 인증 사용 중 (토큰: {token[..Math.Min(4, token.Length)]}***)");
    Console.WriteLine();

    try
    {
        // GitHub API를 통해 실제 데이터 조회
        int totalPrs = await service.GetPullRequestCountAsync(userId);
        int totalIssues = await service.GetIssueCountAsync(userId);

        int finalScore = ScoreCalculator.CalculateFinalScore(
            featureBugPrCount: totalPrs,
            docPrCount: 0,
            typoPrCount: 0,
            featureBugIssueCount: totalIssues,
            docIssueCount: 0
        );

        Console.WriteLine("아이디, 문서이슈, 버그/기능이슈, 오타PR, 문서PR, 버그/기능PR, 총점");
        Console.WriteLine($"{userId}, 0, {totalIssues}, 0, 0, {totalPrs}, {finalScore}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"데이터 조회 중 오류가 발생했습니다: {ex.Message}");
    }
});

app.Run();

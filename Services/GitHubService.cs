using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;

namespace RepoScore.Services
{
    public class GitHubService
    {
        private readonly Octokit.GraphQL.Connection _connection;
        private readonly string _owner;
        private readonly string _repo;
        private readonly string _token;
        private readonly string[] _claimKeywords;

        private static readonly HttpClient s_httpClient = new()
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        private static readonly string[] s_defaultClaimKeywords =
            ["제가 하겠습니다", "진행하겠습니다", "할게요", "I'll take this"];

        // 작업 유형별 기한 (이슈 제목 키워드 기반 추론)
        private static readonly string[] s_docKeywords =
            ["doc", "docs", "문서", "readme", "guide", "typo", "오타"];

        static GitHubService()
        {
            s_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("reposcore-cs");
            s_httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        public GitHubService(string owner, string repo, string token,
            string[]? claimKeywords = null)
        {
            _owner = owner;
            _repo  = repo;
            _token = token ?? throw new ArgumentNullException(nameof(token));
            _claimKeywords = (claimKeywords is { Length: > 0 })
                ? claimKeywords
                : s_defaultClaimKeywords;

            _connection = new Octokit.GraphQL.Connection(
                new Octokit.GraphQL.ProductHeaderValue("reposcore-cs"),
                token
            );
        }

        // PR 개수
        public async Task<int> GetPullRequestCountAsync(string authorLogin)
        {
            var query =
                new Query()
                .Search(
                    query: $"repo:{_owner}/{_repo} is:pr author:{authorLogin}",
                    type: SearchType.Issue,
                    first: 1)
                .Select(x => x.IssueCount);

            return await _connection.Run(query);
        }

        // Issue 개수
        public async Task<int> GetIssueCountAsync(string authorLogin)
        {
            var query =
                new Query()
                .Search(
                    query: $"repo:{_owner}/{_repo} is:issue author:{authorLogin}",
                    type: SearchType.Issue,
                    first: 1)
                .Select(x => x.IssueCount);

            return await _connection.Run(query);
        }

        // PR 댓글
        public async Task<List<string>> GetPullRequestCommentsAsync(int prNumber)
        {
            var query =
                new Query()
                .Repository(_owner, _repo)
                .PullRequest(prNumber)
                .Comments(first: 50)
                .Nodes
                .Select(c => c.Body);

            var result = await _connection.Run(query);

            return new List<string>(result);
        }

        // 이슈 번호로 연결된 오픈 PR 존재 여부 확인
        private async Task<bool> HasLinkedPullRequestAsync(int issueNumber)
        {
            var url = $"repos/{_owner}/{_repo}/issues/{issueNumber}/timeline";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            request.Headers.Accept.ParseAdd("application/vnd.github.mockingbird-preview+json");

            HttpResponseMessage response;
            try
            {
                response = await s_httpClient.SendAsync(request);
            }
            catch
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
                return false;

            var body = await response.Content.ReadAsStringAsync();
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                return false;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Array)
                    return false;

                foreach (var evt in root.EnumerateArray())
                {
                    if (!evt.TryGetProperty("event", out var evtType))
                        continue;

                    if (evtType.GetString() == "cross-referenced"
                        && evt.TryGetProperty("source", out var source)
                        && source.TryGetProperty("type", out var sourceType)
                        && sourceType.GetString() == "issue"
                        && source.TryGetProperty("issue", out var linkedIssue)
                        && linkedIssue.TryGetProperty("pull_request", out _))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // 이슈 제목으로 문서/오타 작업인지 추론 (기한 결정용)
        private static bool IsDocumentTask(string issueTitle)
        {
            var lower = issueTitle.ToLowerInvariant();
            return s_docKeywords.Any(k => lower.Contains(k));
        }

        // 남은 시간 문자열 생성
        private static string FormatRemainingTime(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero)
                return "⌛ 기한 초과";

            return $"⏳ 남은 시간: {(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }

        // 최근 이슈 선점 현황 조회
        public async Task ShowRecentClaimsAsync()
        {
            // 각 키워드를 큰따옴표로 감싸 OR 연결 → GitHub Search의 문자열 LIKE 역할
            var keywordFilter = string.Join(" OR ",
                _claimKeywords.Select(k => $"\"{k}\""));

            // in:comments 로 댓글 본문까지 서버 측에서 검색하여 후보 이슈를 좁힘
            var searchQuery =
                $"repo:{_owner}/{_repo} is:issue is:open in:comments {keywordFilter}";

            const string graphQL = @"
                query($searchQuery: String!) {
                  search(query: $searchQuery, type: ISSUE, first: 20) {
                    nodes {
                      ... on Issue {
                        number
                        title
                        url
                        comments(first: 10) {
                          nodes {
                            body
                            createdAt
                            author {
                              login
                            }
                          }
                        }
                      }
                    }
                  }
                }
            ";

            var requestBody = new
            {
                query = graphQL,
                variables = new { searchQuery }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, "graphql")
            {
                Content = content
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

            HttpResponseMessage response;
            try
            {
                response = await s_httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GitHub 요청 실패: {ex.Message}");
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"GitHub API 요청 실패: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                if (!string.IsNullOrWhiteSpace(errorText))
                {
                    Console.WriteLine("응답 본문:");
                    Console.WriteLine(errorText);
                }
                return;
            }

            var body = await response.Content.ReadAsStringAsync();
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(body);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"응답 JSON 파싱 실패: {ex.Message}");
                return;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                {
                    Console.WriteLine("GraphQL 오류가 발생했습니다:");
                    foreach (var error in errors.EnumerateArray())
                    {
                        if (error.TryGetProperty("message", out var errorMessage) && errorMessage.ValueKind == JsonValueKind.String)
                        {
                            Console.WriteLine($" - {errorMessage.GetString()}");
                        }
                        else
                        {
                            Console.WriteLine($" - {error}");
                        }
                    }
                    return;
                }

                if (!root.TryGetProperty("data", out var data))
                {
                    Console.WriteLine("GitHub 응답에 data 필드가 없습니다.");
                    return;
                }

                if (!data.TryGetProperty("search", out var search))
                {
                    Console.WriteLine("GitHub 응답에 search 필드가 없습니다.");
                    return;
                }

                if (!search.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
                {
                    Console.WriteLine("GitHub 응답에 search.nodes 필드가 없습니다.");
                    return;
                }

                var now = DateTimeOffset.UtcNow;
                Console.WriteLine("📌 최근 이슈 선점 현황\n");

                bool foundAny = false;

                foreach (var issue in nodes.EnumerateArray())
                {
                    if (!issue.TryGetProperty("url", out var urlProperty) || urlProperty.ValueKind != JsonValueKind.String)
                        continue;

                    var issueUrl = urlProperty.GetString() ?? string.Empty;

                    var issueNumber = issue.TryGetProperty("number", out var numProp)
                        ? numProp.GetInt32()
                        : 0;

                    var issueTitle = issue.TryGetProperty("title", out var titleProp)
                        && titleProp.ValueKind == JsonValueKind.String
                        ? titleProp.GetString() ?? string.Empty
                        : string.Empty;

                    if (!issue.TryGetProperty("comments", out var comments) || comments.ValueKind != JsonValueKind.Object)
                        continue;

                    if (!comments.TryGetProperty("nodes", out var commentNodes) || commentNodes.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var comment in commentNodes.EnumerateArray())
                    {
                        if (!comment.TryGetProperty("body", out var bodyProperty) || bodyProperty.ValueKind != JsonValueKind.String)
                            continue;

                        var commentBody = bodyProperty.GetString() ?? string.Empty;

                        if (!comment.TryGetProperty("createdAt", out var createdAtProperty) || createdAtProperty.ValueKind != JsonValueKind.String)
                            continue;

                        if (!DateTimeOffset.TryParse(createdAtProperty.GetString(), out var claimedAt))
                            continue;

                        if ((now - claimedAt).TotalHours > 48)
                            continue;

                        if (!comment.TryGetProperty("author", out var author) || author.ValueKind != JsonValueKind.Object)
                            continue;

                        var login = author.TryGetProperty("login", out var loginProperty) && loginProperty.ValueKind == JsonValueKind.String
                            ? loginProperty.GetString()
                            : "unknown";

                        // 클라이언트 측 정확한 키워드 매칭 (서버 검색의 오탐 방지)
                        if (!_claimKeywords.Any(k => commentBody.Contains(k, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        foundAny = true;

                        bool isDoc = IsDocumentTask(issueTitle);
                        double deadlineHours = isDoc ? 24.0 : 48.0;
                        var deadline = claimedAt.AddHours(deadlineHours);
                        var remaining = deadline - now;

                        bool hasPr = issueNumber > 0 && await HasLinkedPullRequestAsync(issueNumber);

                        Console.WriteLine($"👤 {login}");
                        Console.WriteLine($" - {issueUrl}");

                        if (hasPr)
                        {
                            Console.WriteLine($" - ✅ PR 생성됨");
                        }
                        else
                        {
                            Console.WriteLine($" - {FormatRemainingTime(remaining)}");
                        }

                        Console.WriteLine();
                        break;
                    }
                }

                if (!foundAny)
                {
                    Console.WriteLine("최근 48시간 내 선점된 이슈가 없습니다.");
                }
            }
        }
    }
}

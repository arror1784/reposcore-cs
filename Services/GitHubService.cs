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

        private static readonly HttpClient s_httpClient = new()
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        private static readonly string[] s_claimKeywords =
            ["제가 하겠습니다", "진행하겠습니다", "할게요", "I'll take this"];


        // 작업 유형별 기한 (이슈 제목 키워드 기반 추론)
        private static readonly string[] s_docKeywords =
            ["doc", "docs", "문서", "readme", "guide", "typo", "오타"];

        static GitHubService()
        {
            s_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("reposcore-cs");
            s_httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        public GitHubService(string owner, string repo, string token)
        {
            _owner = owner;
            _repo = repo;
            _token = token ?? throw new ArgumentNullException(nameof(token));

            _connection = new Octokit.GraphQL.Connection(
                new Octokit.GraphQL.ProductHeaderValue("reposcore-cs"),
                token
            );
        }

        // PR 개수
        public int GetPullRequestCount(string authorLogin)
        {
            var query =
                new Query()
                .Search(
                    query: $"repo:{_owner}/{_repo} is:pr author:{authorLogin}",
                    type: SearchType.Issue,
                    first: 1)
                .Select(x => x.IssueCount);

            return _connection.Run(query).Result;
        }

        // Issue 개수
        public int GetIssueCount(string authorLogin)
        {
            var query =
                new Query()
                .Search(
                    query: $"repo:{_owner}/{_repo} is:issue author:{authorLogin}",
                    type: SearchType.Issue,
                    first: 1)
                .Select(x => x.IssueCount);

            return _connection.Run(query).Result;
        }

        // PR 댓글
        public List<string> GetPullRequestComments(int prNumber)
        {
            var query =
                new Query()
                .Repository(_owner, _repo)
                .PullRequest(prNumber)
                .Comments(first: 50)
                .Nodes
                .Select(c => c.Body);

            // 동기 실행
            var result = _connection.Run(query).Result;

            return new List<string>(result);
        }

        // 이슈 번호로 연결된 오픈 PR 존재 여부 확인
        private bool HasLinkedPullRequest(int issueNumber)
        {
            // GitHub REST API: 이슈에 연결된 타임라인 이벤트에서 cross-referenced PR 확인
            // 또는 Search API로 해당 이슈를 닫는 PR 조회
            var url = $"repos/{_owner}/{_repo}/issues/{issueNumber}/timeline";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            // timeline API에는 preview 헤더 필요
            request.Headers.Accept.ParseAdd("application/vnd.github.mockingbird-preview+json");

            HttpResponseMessage response;
            try
            {
                // 동기 HTTP 요청
                response = s_httpClient.Send(request);
            }
            catch
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
                return false;

            // 동기 문자열 읽기 - Stream 사용
            using var stream = response.Content.ReadAsStream();
            using var reader = new StreamReader(stream);
            var body = reader.ReadToEnd();

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

                    // "cross-referenced" 이벤트 중 PR이 이슈를 closes 하는 경우
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

        // 최근 이슈 선점 현황 조회 (동기 버전)
        public void ShowRecentClaims(string mode = "issue")
        {
            const string graphQL = @"
                query($owner: String!, $name: String!) {
                  repository(owner: $owner, name: $name) {
                    issues(first: 20, states: OPEN, orderBy: { field: CREATED_AT, direction: DESC }) {
                      nodes {
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
                variables = new { owner = _owner, name = _repo }
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
                response = s_httpClient.Send(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GitHub 요청 실패: {ex.Message}");
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                using var errorStream = response.Content.ReadAsStream();
                using var errorReader = new StreamReader(errorStream);
                var errorText = errorReader.ReadToEnd();
                
                Console.WriteLine($"GitHub API 요청 실패: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                
                if (!string.IsNullOrWhiteSpace(errorText))
                {
                    Console.WriteLine("응답 본문:");
                    Console.WriteLine(errorText);
                }
                return;
            }

            using var stream = response.Content.ReadAsStream();
            using var reader = new StreamReader(stream);
            var body = reader.ReadToEnd();

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

                if (!data.TryGetProperty("repository", out var repository))
                {
                    Console.WriteLine("GitHub 응답에 repository 필드가 없습니다.");
                    return;
                }

                if (!repository.TryGetProperty("issues", out var issues))
                {
                    Console.WriteLine("GitHub 응답에 issues 필드가 없습니다.");
                    return;
                }

                if (!issues.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
                {
                    Console.WriteLine("GitHub 응답에 issues.nodes 필드가 없습니다.");
                    return;
                }

                var now = DateTimeOffset.UtcNow;
                Console.WriteLine("📌 최근 이슈 선점 현황\n");

                var claimMap = new Dictionary<string, List<(string Url, bool HasPr, TimeSpan Remaining)>>();

                foreach (var issue in nodes.EnumerateArray())
                {
                    if (!issue.TryGetProperty("url", out var urlProperty) || urlProperty.ValueKind != JsonValueKind.String)
                        continue;

                    var issueUrl = urlProperty.GetString() ?? string.Empty;

                    // 이슈 번호 및 제목 추출
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
                            ? loginProperty.GetString() ?? "unknown"
                            : "unknown";

                        if (!s_claimKeywords.Any(k => commentBody.Contains(k, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        // 작업 유형에 따른 기한 결정
                        bool isDoc = IsDocumentTask(issueTitle);
                        double deadlineHours = isDoc ? 24.0 : 48.0;
                        var deadline = claimedAt.AddHours(deadlineHours);
                        var remaining = deadline - now;

                        // PR 연결 여부 확인
                        bool hasPr = issueNumber > 0 && HasLinkedPullRequest(issueNumber);

                        // 유저별 딕셔너리에 수집
                        if (!claimMap.ContainsKey(login))
                            claimMap[login] = new List<(string, bool, TimeSpan)>();
                        claimMap[login].Add((issueUrl, hasPr, remaining));
                        break;
                    }
                }

                // 출력
                if (claimMap.Count == 0)
                {
                    Console.WriteLine("최근 48시간 내 선점된 이슈가 없습니다.");
                }
                else if (mode == "user")
                {
                    foreach (var (login, claims) in claimMap)
                    {
                        Console.WriteLine($"👤 {login}");
                        foreach (var (url, hasPr, remaining) in claims)
                        {
                            Console.WriteLine($" - {url}");
                            if (hasPr)
                                Console.WriteLine($"   ✅ PR 생성됨");
                            else
                                Console.WriteLine($"   {FormatRemainingTime(remaining)}");
                        }
                    }
                }
                else
                {
                    var claimedUrls = new HashSet<string>(
                        claimMap.Values.SelectMany(c => c.Select(x => x.Url)));

                    var unclaimedIssues = new List<string>();
                    foreach (var issue in nodes.EnumerateArray())
                    {
                        if (!issue.TryGetProperty("url", out var u) || u.ValueKind != JsonValueKind.String)
                            continue;
                        var url = u.GetString() ?? "";
                        if (!claimedUrls.Contains(url))
                            unclaimedIssues.Add(url);
                    }

                    Console.WriteLine("📋 미선점 이슈");
                    foreach (var url in unclaimedIssues)
                        Console.WriteLine($" - {url}");
                    Console.WriteLine();

                    Console.WriteLine("📌 선점된 이슈");
                    foreach (var (login, claims) in claimMap)
                    {
                        Console.WriteLine($"👤 {login}");
                        foreach (var (url, hasPr, remaining) in claims)
                        {
                            Console.WriteLine($" - {url}");
                            if (hasPr)
                                Console.WriteLine($"   ✅ PR 생성됨");
                            else
                                Console.WriteLine($"   {FormatRemainingTime(remaining)}");
                        }
                    }
                }
            }
        }

        // 모든 기여자의 GitHub ID 목록을 가져오는 메서드
        public List<string> GetAllContributors()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{_owner}/{_repo}/contributors");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

            var response = s_httpClient.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"기여자 목록 조회 실패: HTTP {(int)response.StatusCode}");
                return new List<string>();
            }

            Stream stream = response.Content.ReadAsStream();
            StreamReader reader = new StreamReader(stream);
            string json = reader.ReadToEnd();

            using var document = JsonDocument.Parse(json);

            var contributors = new List<string>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("login", out var loginProp))
                {
                    contributors.Add(loginProp.GetString() ?? string.Empty);
                }
            }

            return contributors;
        }
    }
}

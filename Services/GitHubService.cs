using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;

namespace RepoScore.Services
{
    public enum GitHubIssuePrLabel
    {
        None,           // No labels
        Bug,
        Documentation,
        Duplicate,
        Enhancement,
        GoodFirstIssue, // good first issue
        HelpWanted,     // help wanted
        Invalid,
        Pinned,
        Question,
        Typo,
        Wontfix
    }

    // 구조화된 반환을 위한 데이터 모델
    public class ClaimRecord
    {
        public int Number { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool HasPr { get; set; }
        public TimeSpan Remaining { get; set; }
        public List<GitHubIssuePrLabel> Labels { get; set; } = new List<GitHubIssuePrLabel>();
    }

    public class ClaimsData
    {
        public Dictionary<string, List<ClaimRecord>> ClaimedMap { get; set; } = new();
        public List<string> UnclaimedUrls { get; set; } = new();
    }

    public class PRRecord
    {
        public int Number { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<GitHubIssuePrLabel> Labels { get; set; } = new List<GitHubIssuePrLabel>();
    }

    public class PRData
    {
        public Dictionary<string, List<PRRecord>> PullRequestsByAuthor { get; set; } = new();
        public List<string> AllUrls { get; set; } = new();
    }

    public class GitHubService
    {
        private readonly Connection _connection;
        private readonly string _owner;
        private readonly string _repo;
        private readonly string _token;

        private static readonly HttpClient s_httpClient = new()
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        private static readonly string[] s_claimKeywords = ["제가 하겠습니다", "진행하겠습니다", "할게요", "I'll take this"];

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

            _connection = new Connection(new ProductHeaderValue("reposcore-cs"), token);
        }

        public List<PRRecord> GetPullRequests(string authorLogin)
        {
            const string graphQL = @"
                query($query: String!) {
                  search(query: $query, type: ISSUE, first: 50) {
                    nodes {
                      ... on PullRequest {
                        number
                        title
                        url
                        labels(first: 10) {
                          nodes { name }
                        }
                      }
                    }
                  }
                }";

            var requestBody = new { query = graphQL, variables = new { query = $"repo:{_owner}/{_repo} is:pr author:{authorLogin}" } };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, "graphql") { Content = content };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

            using var response = s_httpClient.Send(request);
            if (!response.IsSuccessStatusCode) throw new Exception($"API 요청 실패: {response.StatusCode}");

            using var stream = response.Content.ReadAsStream();
            using var reader = new StreamReader(stream);
            using var document = JsonDocument.Parse(reader.ReadToEnd());

            var root = document.RootElement;
            if (root.TryGetProperty("errors", out var errors)) throw new Exception("GraphQL 오류가 발생했습니다.");

            var nodes = root.GetProperty("data").GetProperty("search").GetProperty("nodes");
            var prRecords = new List<PRRecord>();

            foreach (var node in nodes.EnumerateArray())
            {
                if (!node.TryGetProperty("number", out var numberProp)) continue;
                var prNumber = numberProp.GetInt32();
                var prTitle = node.GetProperty("title").GetString() ?? string.Empty;
                var prUrl = node.GetProperty("url").GetString() ?? string.Empty;
                var prLabels = new List<GitHubIssuePrLabel>();

                if (node.TryGetProperty("labels", out var labelsProp) && labelsProp.TryGetProperty("nodes", out var labelNodes))
                {
                    foreach (var labelNode in labelNodes.EnumerateArray())
                    {
                        if (labelNode.TryGetProperty("name", out var nameProp))
                        {
                            var label = ParseGitHubLabel(nameProp.GetString() ?? string.Empty);
                            if (label != GitHubIssuePrLabel.None) prLabels.Add(label);
                        }
                    }
                }

                prRecords.Add(new PRRecord
                {
                    Number = prNumber,
                    Title = prTitle,
                    Url = prUrl,
                    Labels = prLabels
                });
            }

            return prRecords;
        }

        public List<ClaimRecord> GetClaims(string authorLogin)
        {
            const string graphQL = @"
                query($query: String!) {
                  search(query: $query, type: ISSUE, first: 50) {
                    nodes {
                      ... on Issue {
                        number
                        title
                        url
                        labels(first: 10) {
                          nodes { name }
                        }
                      }
                    }
                  }
                }";

            var requestBody = new { query = graphQL, variables = new { query = $"repo:{_owner}/{_repo} is:issue author:{authorLogin}" } };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, "graphql") { Content = content };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

            using var response = s_httpClient.Send(request);
            if (!response.IsSuccessStatusCode) throw new Exception($"API 요청 실패: {response.StatusCode}");

            using var stream = response.Content.ReadAsStream();
            using var reader = new StreamReader(stream);
            using var document = JsonDocument.Parse(reader.ReadToEnd());

            var root = document.RootElement;
            if (root.TryGetProperty("errors", out var errors)) throw new Exception("GraphQL 오류가 발생했습니다.");

            var nodes = root.GetProperty("data").GetProperty("search").GetProperty("nodes");
            var claimRecords = new List<ClaimRecord>();

            foreach (var node in nodes.EnumerateArray())
            {
                if (!node.TryGetProperty("number", out var numberProp)) continue;
                var claimNumber = numberProp.GetInt32();
                var claimTitle = node.GetProperty("title").GetString() ?? string.Empty;
                var claimUrl = node.GetProperty("url").GetString() ?? string.Empty;
                var claimLabels = new List<GitHubIssuePrLabel>();

                if (node.TryGetProperty("labels", out var labelsProp) && labelsProp.TryGetProperty("nodes", out var labelNodes))
                {
                    foreach (var labelNode in labelNodes.EnumerateArray())
                    {
                        if (labelNode.TryGetProperty("name", out var nameProp))
                        {
                            var label = ParseGitHubLabel(nameProp.GetString() ?? string.Empty);
                            if (label != GitHubIssuePrLabel.None) claimLabels.Add(label);
                        }
                    }
                }

                claimRecords.Add(new ClaimRecord
                {
                    Number = claimNumber,
                    Title = claimTitle,
                    Url = claimUrl,
                    Labels = claimLabels
                });
            }

            return claimRecords;
        }

        public List<string> GetPullRequestComments(int prNumber)
        {
            var query = new Query().Repository(_owner, _repo).PullRequest(prNumber).Comments(first: 50).Nodes.Select(c => c.Body);
            return new List<string>(_connection.Run(query).Result);
        }

        private bool HasLinkedPullRequest(int issueNumber)
        {
            var url = $"repos/{_owner}/{_repo}/issues/{issueNumber}/timeline";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            request.Headers.Accept.ParseAdd("application/vnd.github.mockingbird-preview+json");

            try
            {
                using var response = s_httpClient.Send(request);
                if (!response.IsSuccessStatusCode) return false;

                using var stream = response.Content.ReadAsStream();
                using var reader = new StreamReader(stream);
                var body = reader.ReadToEnd();

                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Array) return false;

                foreach (var evt in root.EnumerateArray())
                {
                    if (evt.TryGetProperty("event", out var evtType) && evtType.GetString() == "cross-referenced"
                        && evt.TryGetProperty("source", out var source)
                        && source.TryGetProperty("type", out var sourceType) && sourceType.GetString() == "issue"
                        && source.TryGetProperty("issue", out var linkedIssue)
                        && linkedIssue.TryGetProperty("pull_request", out _))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        private static bool IsDocumentTask(List<GitHubIssuePrLabel> issueLabels)
        {
            return issueLabels.Contains(GitHubIssuePrLabel.Documentation) || issueLabels.Contains(GitHubIssuePrLabel.Typo);
        }

        private static GitHubIssuePrLabel ParseGitHubLabel(string labelName)
        {
            if (string.IsNullOrEmpty(labelName)) return GitHubIssuePrLabel.None;

            var normalized = labelName.ToLowerInvariant().Replace(" ", "").Replace("-", "");
            switch (normalized)
            {
                case "bug": return GitHubIssuePrLabel.Bug;
                case "documentation": return GitHubIssuePrLabel.Documentation;
                case "duplicate": return GitHubIssuePrLabel.Duplicate;
                case "enhancement": return GitHubIssuePrLabel.Enhancement;
                case "good first issue": return GitHubIssuePrLabel.GoodFirstIssue;
                case "help wanted": return GitHubIssuePrLabel.HelpWanted;
                case "invalid": return GitHubIssuePrLabel.Invalid;
                case "pinned": return GitHubIssuePrLabel.Pinned;
                case "question": return GitHubIssuePrLabel.Question;
                case "typo": return GitHubIssuePrLabel.Typo;
                case "wontfix": return GitHubIssuePrLabel.Wontfix;
                case "nolabels": return GitHubIssuePrLabel.None;
                default: return GitHubIssuePrLabel.None;
            }
        }

        // 콘솔 출력 없이 구조화된 ClaimsData를 반환하도록 변경
        public ClaimsData GetRecentClaimsData()
        {
            const string graphQL = @"
                query($owner: String!, $name: String!) {
                  repository(owner: $owner, name: $name) {
                    issues(first: 20, states: OPEN, orderBy: { field: CREATED_AT, direction: DESC }) {
                      nodes { number, title, url, labels(first: 10) { nodes { name } }, comments(first: 10) { nodes { body, createdAt, author { login } } } }
                    }
                  }
                }";

            var requestBody = new { query = graphQL, variables = new { owner = _owner, name = _repo } };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, "graphql") { Content = content };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

            using var response = s_httpClient.Send(request);
            if (!response.IsSuccessStatusCode) throw new Exception($"API 요청 실패: {response.StatusCode}");

            using var stream = response.Content.ReadAsStream();
            using var reader = new StreamReader(stream);
            using var document = JsonDocument.Parse(reader.ReadToEnd());

            var root = document.RootElement;
            if (root.TryGetProperty("errors", out var errors)) throw new Exception("GraphQL 오류가 발생했습니다.");

            var nodes = root.GetProperty("data").GetProperty("repository").GetProperty("issues").GetProperty("nodes");
            var now = DateTimeOffset.UtcNow;
            var claimsData = new ClaimsData();

            foreach (var issue in nodes.EnumerateArray())
            {
                var issueUrl = issue.GetProperty("url").GetString() ?? "";
                var issueNumber = issue.GetProperty("number").GetInt32();
                var issueLabels = new List<GitHubIssuePrLabel>();
                var isClaimed = false;

                // 라벨 정보 추출
                if (issue.TryGetProperty("labels", out var labelsProp) && labelsProp.TryGetProperty("nodes", out var labelNodes))
                {
                    foreach (var labelNode in labelNodes.EnumerateArray())
                    {
                        if (labelNode.TryGetProperty("name", out var nameProp))
                        {
                            var labelName = nameProp.GetString() ?? "";
                            var label = ParseGitHubLabel(labelName);
                            if (label != GitHubIssuePrLabel.None)
                            {
                                issueLabels.Add(label);
                            }
                        }
                    }
                }

                foreach (var comment in issue.GetProperty("comments").GetProperty("nodes").EnumerateArray())
                {
                    var commentBody = comment.GetProperty("body").GetString() ?? "";
                    if (!DateTimeOffset.TryParse(comment.GetProperty("createdAt").GetString(), out var claimedAt)) continue;
                    if ((now - claimedAt).TotalHours > 48) continue;

                    var login = comment.GetProperty("author").TryGetProperty("login", out var lp) ? lp.GetString() ?? "unknown" : "unknown";

                    if (s_claimKeywords.Any(k => commentBody.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    {
                        var deadlineHours = IsDocumentTask(issueLabels) ? 24.0 : 48.0;
                        var remaining = claimedAt.AddHours(deadlineHours) - now;
                        var hasPr = issueNumber > 0 && HasLinkedPullRequest(issueNumber);

                        if (!claimsData.ClaimedMap.ContainsKey(login)) claimsData.ClaimedMap[login] = new List<ClaimRecord>();
                        claimsData.ClaimedMap[login].Add(new ClaimRecord { Url = issueUrl, HasPr = hasPr, Remaining = remaining, Labels = issueLabels });
                        isClaimed = true;
                        break;
                    }
                }

                if (!isClaimed) claimsData.UnclaimedUrls.Add(issueUrl);
            }

            return claimsData;
        }

        public List<string> GetAllContributors()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{_owner}/{_repo}/contributors");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

            using var response = s_httpClient.Send(request);
            if (!response.IsSuccessStatusCode) return new List<string>();

            using var stream = response.Content.ReadAsStream();
            using var reader = new StreamReader(stream);
            using var document = JsonDocument.Parse(reader.ReadToEnd());

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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;

namespace RepoScore.Services
{
    public enum GitHubIssuePrLabel
    {
        None, Bug, Documentation, Duplicate, Enhancement, GoodFirstIssue,
        HelpWanted, Invalid, Pinned, Question, Typo, Wontfix
    }

    public enum IssueClosedStateReason
    {
        None,
        Completed,
        Duplicate,
        NotPlanned
    }

    public class IssueRecord
    {
        public int Number { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool HasPr { get; set; }
        public IssueClosedStateReason ClosedReason { get; set; } = IssueClosedStateReason.None;
        public TimeSpan Remaining { get; set; }
        public List<GitHubIssuePrLabel> Labels { get; set; } = new();
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class ClaimsData
    {
        public Dictionary<string, List<IssueRecord>> ClaimedMap { get; set; } = new();
        public List<string> UnclaimedUrls { get; set; } = new();
    }

    public class PRRecord
    {
        public int Number { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool IsMerged { get; set; } = false;
        public List<GitHubIssuePrLabel> Labels { get; set; } = new();
        public DateTimeOffset UpdatedAt { get; set; }
    }

    // GitHub REST/GraphQL API를 통해 저장소 데이터를 조회하는 서비스 클래스.
    // PR 조회, 이슈 조회, 기여자 목록 조회, 이슈 선점 현황 조회 기능을 담당.
    public class GitHubService
    {
        private readonly Octokit.GraphQL.Connection _graphQLConnection;
        private readonly string _owner;
        private readonly string _repo;

        private static readonly string[] s_defaultClaimKeywords = ["제가 하겠습니다", "진행하겠습니다", "할게요", "I'll take this"];
        private readonly string[] _claimKeywords;

        public GitHubService(string owner, string repo, string token, string[]? keywords = null)
        {
            _owner = owner;
            _repo = repo;
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));

            _claimKeywords = keywords ?? s_defaultClaimKeywords;

            _graphQLConnection = new Octokit.GraphQL.Connection(
                new Octokit.GraphQL.ProductHeaderValue("reposcore-cs"), token);
        }

        // 특정 사용자가 작성하고 머지된 PR 목록을 GraphQL로 조회.
        // since가 지정된 경우 해당 시각 이후 업데이트된 PR만 가져옴.
        public List<PRRecord> GetPullRequests(string authorLogin, DateTimeOffset? since = null)
        {
            string searchString = $"repo:{_owner}/{_repo} is:pr is:merged author:{authorLogin}";
            if (since.HasValue)
            {
                searchString += $" updated:>={since.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}";
            }

            var prRecords = new List<PRRecord>();
            string? cursor = null;
            bool hasNextPage = true;

            while (hasNextPage)
            {
                var query = new Octokit.GraphQL.Query()
                    .Search(query: searchString, type: SearchType.Issue, first: 100, after: cursor)
                    .Select(s => new
                    {
                        s.PageInfo.HasNextPage,
                        s.PageInfo.EndCursor,
                        Items = s.Nodes.OfType<Octokit.GraphQL.Model.PullRequest>().Select(pr => new
                        {
                            pr.Number,
                            pr.Title,
                            pr.Url,
                            pr.Merged,
                            pr.UpdatedAt,
                            Labels = pr.Labels(10, null, null, null, null).Nodes.Select(l => l.Name).ToList()
                        }).ToList()
                    });

                var result = _graphQLConnection.Run(query).Result;

                foreach (var pr in result.Items)
                {
                    prRecords.Add(new PRRecord
                    {
                        Number = pr.Number,
                        Title = pr.Title,
                        Url = pr.Url,
                        IsMerged = pr.Merged,
                        UpdatedAt = pr.UpdatedAt,
                        Labels = pr.Labels.Select(ParseGitHubLabel).Where(l => l != GitHubIssuePrLabel.None).ToList()
                    });
                }

                hasNextPage = result.HasNextPage;
                cursor = result.EndCursor;
            }

            return prRecords;
        }

        // 특정 사용자가 작성한 이슈 목록을 GraphQL로 조회.
        // "not planned", "duplicate" 사유로 닫힌 이슈는 제외.
        // since가 지정된 경우 해당 시각 이후 업데이트된 이슈만 가져옴.
        public List<IssueRecord> GetIssues(string authorLogin, DateTimeOffset? since = null)
        {
            const string rawGraphQl = @"
            query($searchQuery: String!, $after: String) {
                search(query: $searchQuery, type: ISSUE, first: 100, after: $after) {
                    pageInfo {
                        hasNextPage
                        endCursor
                    }
                    nodes {
                        ... on Issue {
                            number
                            title
                            url
                            stateReason
                            updatedAt
                            labels(first: 10) {
                                nodes {
                                    name
                                }
                            }
                        }
                    }
                }
            }";

            string searchString = $"repo:{_owner}/{_repo} is:issue author:{authorLogin} -reason:\"not planned\" -reason:\"duplicate\"";
            if (since.HasValue)
            {
                searchString += $" updated:>={since.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}";
            }

            var issueRecords = new List<IssueRecord>();
            string? cursor = null;
            bool hasNextPage = true;

            while (hasNextPage)
            {
                var requestPayload = BuildRawQueryPayload(
                    rawGraphQl,
                    new Dictionary<string, object>
                    {
                        ["searchQuery"] = searchString,
                        ["after"] = cursor!
                    });

                var rawResponse = _graphQLConnection.Run(requestPayload).Result;
                using var document = JsonDocument.Parse(rawResponse);

                if (!document.RootElement.TryGetProperty("data", out var dataElement) ||
                    !dataElement.TryGetProperty("search", out var searchElement))
                {
                    break;
                }

                var pageInfo = searchElement.GetProperty("pageInfo");
                hasNextPage = pageInfo.GetProperty("hasNextPage").GetBoolean();
                cursor = pageInfo.GetProperty("endCursor").GetString();

                if (searchElement.TryGetProperty("nodes", out var nodesElement) && nodesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var node in nodesElement.EnumerateArray())
                    {
                        if (node.ValueKind != JsonValueKind.Object) continue;

                        var labelNames = new List<string>();
                        if (node.TryGetProperty("labels", out var labelsElement) &&
                            labelsElement.TryGetProperty("nodes", out var labelNodesElement))
                        {
                            foreach (var labelNode in labelNodesElement.EnumerateArray())
                            {
                                if (labelNode.TryGetProperty("name", out var labelNameElement))
                                    labelNames.Add(labelNameElement.GetString() ?? "");
                            }
                        }

                        var updatedAt = node.TryGetProperty("updatedAt", out var updatedElement)
                            ? DateTimeOffset.Parse(updatedElement.GetString()!) : DateTimeOffset.MinValue;

                        issueRecords.Add(new IssueRecord
                        {
                            Number = node.TryGetProperty("number", out var numEl) ? numEl.GetInt32() : 0,
                            Title = node.TryGetProperty("title", out var titEl) ? titEl.GetString() ?? "" : "",
                            Url = node.TryGetProperty("url", out var urlEl) ? urlEl.GetString() ?? "" : "",
                            ClosedReason = ParseIssueClosedStateReason(node),
                            Labels = labelNames.Select(ParseGitHubLabel).Where(l => l != GitHubIssuePrLabel.None).ToList(),
                            UpdatedAt = updatedAt
                        });
                    }
                }
            }

            return issueRecords;
        }

        // 저장소의 열린 이슈를 대상으로 최근 48시간 내 선점 현황을 조회.
        // 이슈별로 선점 댓글 작성자, 남은 기한, 연결된 PR 유무를 파악하여 반환.
        public ClaimsData GetRecentClaimsData()
        {
            var claimsData = new ClaimsData();
            string? cursor = null;
            bool hasNextPage = true;
            var now = DateTimeOffset.UtcNow;

            while (hasNextPage)
            {
                var query = new Octokit.GraphQL.Query()
                    .Repository(_repo, _owner)
                    .Issues(first: 100, after: cursor, states: new[] { IssueState.Open }, orderBy: new IssueOrder { Field = IssueOrderField.CreatedAt, Direction = OrderDirection.Desc })
                    .Select(s => new
                    {
                        s.PageInfo.HasNextPage,
                        s.PageInfo.EndCursor,
                        Items = s.Nodes.Select(issue => new
                        {
                            issue.Number,
                            issue.Url,
                            Labels = issue.Labels(10, null, null, null, null).Nodes.Select(l => l.Name).ToList(),
                            Comments = issue.Comments(10, null, null, null, null).Nodes.Select(c => new
                            {
                                c.Body,
                                c.CreatedAt,
                                AuthorLogin = c.Author.Login
                            }).ToList()
                        }).ToList()
                    });

                var result = _graphQLConnection.Run(query).Result;

                foreach (var issue in result.Items)
                {
                    var issueLabels = issue.Labels.Select(ParseGitHubLabel).Where(l => l != GitHubIssuePrLabel.None).ToList();
                    var isClaimed = false;

                    foreach (var comment in issue.Comments)
                    {
                        if ((now - comment.CreatedAt).TotalHours > 48) continue;

                        var login = comment.AuthorLogin ?? "unknown";

                        if (_claimKeywords.Any(k => comment.Body.Contains(k, StringComparison.OrdinalIgnoreCase)))
                        {
                            var deadlineHours = IsDocumentTask(issueLabels) ? 24.0 : 48.0;
                            var remaining = comment.CreatedAt.AddHours(deadlineHours) - now;
                            var hasPr = issue.Number > 0 && HasLinkedPullRequest(issue.Number);

                            if (!claimsData.ClaimedMap.ContainsKey(login))
                                claimsData.ClaimedMap[login] = new List<IssueRecord>();

                            claimsData.ClaimedMap[login].Add(new IssueRecord
                            {
                                Number = issue.Number,
                                Url = issue.Url,
                                HasPr = hasPr,
                                Remaining = remaining,
                                Labels = issueLabels
                            });
                            isClaimed = true;
                            break;
                        }
                    }

                    if (!isClaimed) claimsData.UnclaimedUrls.Add(issue.Url);
                }

                hasNextPage = result.HasNextPage;
                cursor = result.EndCursor;
            }

            return claimsData;
        }

        public List<string> GetPullRequestComments(int prNumber)
        {
            var query = new Octokit.GraphQL.Query()
                .Repository(_repo, _owner)
                .PullRequest(prNumber)
                .Comments(first: 50)
                .Nodes.Select(c => c.Body);

            return _graphQLConnection.Run(query).Result.ToList();
        }

        private bool HasLinkedPullRequest(int issueNumber)
        {
            try
            {
                var query = new Octokit.GraphQL.Query()
                    .Repository(_repo, _owner)
                    .Issue(issueNumber)
                    .TimelineItems(first: 50)
                    .Nodes
                    .OfType<CrossReferencedEvent>()
                    .Select(e => e.Url);

                var timelineUrls = _graphQLConnection.Run(query).Result;

                return timelineUrls.Any(url => !string.IsNullOrEmpty(url) && url.Contains("/pull/"));
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsDocumentTask(List<GitHubIssuePrLabel> issueLabels)
        {
            return issueLabels.Contains(GitHubIssuePrLabel.Documentation) || issueLabels.Contains(GitHubIssuePrLabel.Typo);
        }

        // GitHub 라벨 이름 문자열을 GitHubIssuePrLabel 열거형 값으로 변환.
        // 대소문자 및 공백/하이픈을 정규화한 뒤 매핑. 알 수 없는 라벨은 None 반환.
        internal static GitHubIssuePrLabel ParseGitHubLabel(string labelName)
        {
            if (string.IsNullOrEmpty(labelName)) return GitHubIssuePrLabel.None;

            var normalized = labelName.ToLowerInvariant().Replace(" ", "").Replace("-", "");
            return normalized switch
            {
                "bug" => GitHubIssuePrLabel.Bug,
                "documentation" => GitHubIssuePrLabel.Documentation,
                "duplicate" => GitHubIssuePrLabel.Duplicate,
                "enhancement" => GitHubIssuePrLabel.Enhancement,
                "goodfirstissue" => GitHubIssuePrLabel.GoodFirstIssue,
                "helpwanted" => GitHubIssuePrLabel.HelpWanted,
                "invalid" => GitHubIssuePrLabel.Invalid,
                "pinned" => GitHubIssuePrLabel.Pinned,
                "question" => GitHubIssuePrLabel.Question,
                "typo" => GitHubIssuePrLabel.Typo,
                "wontfix" => GitHubIssuePrLabel.Wontfix,
                _ => GitHubIssuePrLabel.None,
            };
        }

        private static string BuildRawQueryPayload(string query, Dictionary<string, object> variables)
        {
            return JsonSerializer.Serialize(new
            {
                query,
                variables
            });
        }

        // GraphQL 응답의 이슈 노드에서 닫힌 사유(stateReason)를 파싱하여 열거형으로 반환.
        internal static IssueClosedStateReason ParseIssueClosedStateReason(JsonElement issueNode)
        {
            if (!issueNode.TryGetProperty("stateReason", out var stateReasonElement) ||
                stateReasonElement.ValueKind == JsonValueKind.Null)
            {
                return IssueClosedStateReason.None;
            }

            var reason = stateReasonElement.GetString()?.ToUpperInvariant();
            return reason switch
            {
                "COMPLETED" => IssueClosedStateReason.Completed,
                "DUPLICATE" => IssueClosedStateReason.Duplicate,
                "NOT_PLANNED" or "NOTPLANNED" => IssueClosedStateReason.NotPlanned,
                _ => IssueClosedStateReason.None
            };
        }

        // GraphQL API를 통해 저장소의 전체 기여자 로그인 ID 목록을 조회.
        // 조회 실패 시 빈 목록을 반환.
        public List<string> GetAllContributors()
        {
            try
            {
                var contributors = new List<string>();
                string? cursor = null;
                bool hasNextPage = true;

                while (hasNextPage)
                {
                    var query = new Octokit.GraphQL.Query()
                        .Repository(_repo, _owner)
                        .DefaultBranchRef
                        .Target
                        .Cast<Octokit.GraphQL.Model.Commit>()
                        .History(first: 100, after: cursor)
                        .Select(h => new
                        {
                            h.PageInfo.HasNextPage,
                            h.PageInfo.EndCursor,
                            Items = h.Nodes.Select(c => new
                            {
                                AuthorLogin = c.Author.User.Login
                            }).ToList()
                        });

                    var result = _graphQLConnection.Run(query).Result;

                    contributors.AddRange(
                        result.Items
                            .Where(c => !string.IsNullOrEmpty(c.AuthorLogin))
                            .Select(c => c.AuthorLogin)
                    );

                    hasNextPage = result.HasNextPage;
                    cursor = result.EndCursor;
                }

                return contributors.Distinct().ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"기여자 목록 조회 실패: {ex.Message}");
                return new List<string>();
            }
        }
    }
}

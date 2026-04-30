using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Octokit;
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

    public class ClaimRecord
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
        public Dictionary<string, List<ClaimRecord>> ClaimedMap { get; set; } = new();
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

    public class GitHubService
    {
        private readonly Octokit.GraphQL.Connection _graphQLConnection;
        private readonly Octokit.GitHubClient _restClient;
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

            _restClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("reposcore-cs"))
            {
                Credentials = new Octokit.Credentials(token)
            };
        }

        public List<PRRecord> GetPullRequests(string authorLogin, DateTimeOffset? since = null)
        {
            string searchString = $"repo:{_owner}/{_repo} is:pr author:{authorLogin}";
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

        public List<ClaimRecord> GetClaims(string authorLogin, DateTimeOffset? since = null)
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

            string searchString = $"repo:{_owner}/{_repo} is:issue author:{authorLogin}";
            if (since.HasValue)
            {
                searchString += $" updated:>={since.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}";
            }

            var claimRecords = new List<ClaimRecord>();
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

                        claimRecords.Add(new ClaimRecord
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

            return claimRecords;
        }

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
                                claimsData.ClaimedMap[login] = new List<ClaimRecord>();

                            claimsData.ClaimedMap[login].Add(new ClaimRecord
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

        private static bool IsDocumentTask(List<GitHubIssuePrLabel> issueLabels)
        {
            return issueLabels.Contains(GitHubIssuePrLabel.Documentation) || issueLabels.Contains(GitHubIssuePrLabel.Typo);
        }

        private static GitHubIssuePrLabel ParseGitHubLabel(string labelName)
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

        private static IssueClosedStateReason ParseIssueClosedStateReason(JsonElement issueNode)
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

        public List<string> GetAllContributors()
        {
            try
            {
                var contributors = _restClient.Repository.GetAllContributors(_owner, _repo).Result;
                return contributors.Select(c => c.Login).ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"기여자 목록 조회 실패: {ex.Message}");
                return new List<string>();
            }
        }
    }
}

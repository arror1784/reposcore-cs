using System;
using System.Collections.Generic;
using System.Linq;
using Octokit; 
using RepoScore.Utils;
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

    // 구조화된 반환을 위한 데이터 모델
    public class ClaimRecord
    {
        public int Number { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool HasPr { get; set; }
        public IssueClosedStateReason ClosedReason { get; set; } = IssueClosedStateReason.None;
        public TimeSpan Remaining { get; set; }
        public List<GitHubIssuePrLabel> Labels { get; set; } = new();
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
    }

    public class PRData
    {
        public Dictionary<string, List<PRRecord>> PullRequestsByAuthor { get; set; } = new();
        public List<string> AllUrls { get; set; } = new();
    }

    public class GitHubService
    {
        private readonly Octokit.GraphQL.Connection _graphQLConnection;
        private readonly Octokit.GitHubClient _restClient;
        private readonly string _owner;
        private readonly string _repo;

        private static readonly string[] s_claimKeywords = ["제가 하겠습니다", "진행하겠습니다", "할게요", "I'll take this"];

        public GitHubService(string owner, string repo, string token)
        {
            _owner = owner;
            _repo = repo;
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));

            // 1. GraphQL 커넥션 초기화
            _graphQLConnection = new Octokit.GraphQL.Connection(
                new Octokit.GraphQL.ProductHeaderValue("reposcore-cs"), token);

            // 2. REST API 클라이언트 초기화
            _restClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("reposcore-cs"))
            {
                Credentials = new Octokit.Credentials(token)
            };
        }

        public List<PRRecord> GetPullRequests(string authorLogin)
        {
            var query = new Octokit.GraphQL.Query()
                .Search(query: $"repo:{_owner}/{_repo} is:pr author:{authorLogin}", type: SearchType.Issue, first: 50)
                .Nodes
                .OfType<Octokit.GraphQL.Model.PullRequest>()
                .Select(pr => new
                {
                    pr.Number,
                    pr.Title,
                    pr.Url,
                    pr.Merged, // main 브랜치의 IsMerged 요구사항 통합
                    Labels = pr.Labels(10, null, null, null, null).Nodes.Select(l => l.Name).ToList()
                });

            var result = _graphQLConnection.Run(query).Result;
            var prRecords = new List<PRRecord>();

            foreach (var pr in result)
            {
                prRecords.Add(new PRRecord
                {
                    Number = pr.Number,
                    Title = pr.Title,
                    Url = pr.Url,
                    IsMerged = pr.Merged,
                    Labels = pr.Labels.Select(ParseGitHubLabel).Where(l => l != GitHubIssuePrLabel.None).ToList()
                });
            }

            return prRecords;
        }

        public List<ClaimRecord> GetClaims(string authorLogin)
        {
            var query = new Octokit.GraphQL.Query()
                .Search(query: $"repo:{_owner}/{_repo} is:issue author:{authorLogin}", type: SearchType.Issue, first: 50)
                .Nodes
                .OfType<Octokit.GraphQL.Model.Issue>()
                .Select(issue => new
                {
                    issue.Number,
                    issue.Title,
                    issue.Url,
                    issue.StateReason, // main 브랜치의 closedReason 요구사항 통합
                    Labels = issue.Labels(10, null, null, null, null).Nodes.Select(l => l.Name).ToList()
                });

            var result = _graphQLConnection.Run(query).Result;
            var claimRecords = new List<ClaimRecord>();

            foreach (var issue in result)
            {
                var claimClosedReason = IssueClosedStateReason.None;
                
                // GraphQL 열거형 데이터를 내부 열거형 모델로 매핑
                if (issue.StateReason.HasValue)
                {
                    var reasonStr = issue.StateReason.Value.ToString().ToUpperInvariant();
                    claimClosedReason = reasonStr switch
                    {
                        "COMPLETED" => IssueClosedStateReason.Completed,
                        "DUPLICATE" => IssueClosedStateReason.Duplicate,
                        "NOTPLANNED" or "NOT_PLANNED" => IssueClosedStateReason.NotPlanned,
                        _ => IssueClosedStateReason.None
                    };
                }

                claimRecords.Add(new ClaimRecord
                {
                    Number = issue.Number,
                    Title = issue.Title,
                    Url = issue.Url,
                    ClosedReason = claimClosedReason,
                    Labels = issue.Labels.Select(ParseGitHubLabel).Where(l => l != GitHubIssuePrLabel.None).ToList()
                });
            }

            return claimRecords;
        }

        public List<string> GetPullRequestComments(int prNumber)
        {
            var query = new Octokit.GraphQL.Query()
                .Repository(_owner, _repo)
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
                    .Repository(_owner, _repo)
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

        public ClaimsData GetRecentClaimsData()
        {
            var query = new Octokit.GraphQL.Query()
                .Repository(_owner, _repo)
                .Issues(first: 20, states: new[] { IssueState.Open }, orderBy: new IssueOrder { Field = IssueOrderField.CreatedAt, Direction = OrderDirection.Desc })
                .Nodes.Select(issue => new
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
                });

            var result = _graphQLConnection.Run(query).Result;
            var now = DateTimeOffset.UtcNow;
            var claimsData = new ClaimsData();

            foreach (var issue in result)
            {
                var issueLabels = issue.Labels.Select(ParseGitHubLabel).Where(l => l != GitHubIssuePrLabel.None).ToList();
                var isClaimed = false;

                foreach (var comment in issue.Comments)
                {
                    if ((now - comment.CreatedAt).TotalHours > 48) continue;

                    var login = comment.AuthorLogin ?? "unknown";

                    if (s_claimKeywords.Any(k => comment.Body.Contains(k, StringComparison.OrdinalIgnoreCase)))
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

            return claimsData;
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
                Logger.LogError($"기여자 목록 조회 실패: {ex.Message}");
                return new List<string>();
            }
        }
    }
}
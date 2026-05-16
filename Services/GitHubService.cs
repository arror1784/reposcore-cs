using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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

    // 선점 댓글 정보를 캐시하기 위한 레코드
    public class ClaimComment
    {
        public string AuthorLogin { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class IssueRecord
    {
        public int Number { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string AuthorLogin { get; set; } = string.Empty;
        public bool HasPr { get; set; }
        public List<PRRecord> LinkedPullRequests { get; set; } = new();
        public IssueClosedStateReason ClosedReason { get; set; } = IssueClosedStateReason.None;
        public TimeSpan Remaining { get; set; }
        public List<GitHubIssuePrLabel> Labels { get; set; } = new();
        public DateTimeOffset UpdatedAt { get; set; }

        // 캐시용: 최근 48시간 내 선점 키워드가 포함된 댓글 목록.
        // null이면 캐시 미보유, 빈 리스트면 선점 댓글 없음을 의미.
        // null일 때 직렬화 생략 → 기여도 분석 캐시(UserIssues)의 크기에 영향 없음.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ClaimComment>? CachedClaimComments { get; set; } = null;
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
        public string AuthorLogin { get; set; } = string.Empty;
        public bool IsMerged { get; set; } = false;
        public List<GitHubIssuePrLabel> Labels { get; set; } = new();
        public DateTimeOffset UpdatedAt { get; set; }

        // Claims 캐시용: PR에 연결된 이슈 번호 목록.
        // 빈 리스트일 때 직렬화 생략 → 기여도 분석 캐시(UserPullRequests)의 크기에 영향 없음.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<int> LinkedIssueNumbers { get; set; } = new();
    }

    public class PRWithLinkedIssues
    {
        public PRRecord Pr { get; set; } = new();
        public List<int> LinkedIssueNumbers { get; set; } = new();
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

        // 저장소의 머지된 전체 PR 목록을 GraphQL로 조회.
        // since가 지정된 경우 해당 시각 이후 업데이트된 PR만 가져옴.
        public List<PRRecord> GetPullRequests(DateTimeOffset? since = null)
        {
            string searchString = $"repo:{_owner}/{_repo} is:pr is:merged";
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
                            AuthorLogin = pr.Author.Login,
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
                        AuthorLogin = pr.AuthorLogin ?? "",
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

        // 저장소의 전체 이슈 목록을 GraphQL로 조회.
        // "not planned", "duplicate" 사유로 닫힌 이슈는 제외.
        // since가 지정된 경우 해당 시각 이후 업데이트된 이슈만 가져옴.
        public List<IssueRecord> GetIssues(DateTimeOffset? since = null)
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
                            author {
                                login
                            }
                            labels(first: 10) {
                                nodes {
                                    name
                                }
                            }
                        }
                    }
                }
            }";

            string searchString = $"repo:{_owner}/{_repo} is:issue -reason:\"not planned\" -reason:\"duplicate\"";
            if (since.HasValue)
            {
                searchString += $" updated:>={since.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}";
            }

            var issueRecords = new List<IssueRecord>();
            string? cursor = null;
            bool hasNextPage = true;

            while (hasNextPage)
            {
                var requestPayload = JsonSerializer.Serialize(new
                {
                    query = rawGraphQl,
                    variables = new Dictionary<string, object>
                    {
                        ["searchQuery"] = searchString,
                        ["after"] = cursor!
                    }
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

                        string authorLogin = "";
                        if (node.TryGetProperty("author", out var authorElement) && authorElement.ValueKind == JsonValueKind.Object)
                        {
                            if (authorElement.TryGetProperty("login", out var loginElement))
                            {
                                authorLogin = loginElement.GetString() ?? "";
                            }
                        }

                        issueRecords.Add(new IssueRecord
                        {
                            Number = node.TryGetProperty("number", out var numEl) ? numEl.GetInt32() : 0,
                            Title = node.TryGetProperty("title", out var titEl) ? titEl.GetString() ?? "" : "",
                            Url = node.TryGetProperty("url", out var urlEl) ? urlEl.GetString() ?? "" : "",
                            AuthorLogin = authorLogin,
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
        //
        // cachedOpenIssues: 이전 실행에서 캐시된 열린 이슈 목록 (CachedClaimComments 포함).
        // cachedOpenPrs:    이전 실행에서 캐시된 열린 PR 목록 (LinkedIssueNumbers 포함).
        // since: 마지막 Claims 캐시 갱신 시각. 이 시각 이후 업데이트된 항목만 재조회.
        //
        // API 호출을 최소화하기 위해 갱신된 이슈/PR을 함께 반환.
        // 호출 측에서 반환값을 SaveClaimsCache에 그대로 전달하면 추가 API 호출 없이 캐시 저장 가능.
        public (ClaimsData claimsData, List<IssueRecord> updatedOpenIssues, List<PRRecord> updatedOpenPrs)
            GetRecentClaimsData(
                List<IssueRecord>? cachedOpenIssues = null,
                List<PRRecord>? cachedOpenPrs = null,
                DateTimeOffset? since = null)
        {
            var now = DateTimeOffset.UtcNow;
            bool isFullRefresh = since == null || (now - since.Value).TotalHours > 48;

            // ── 1. 열린 PR 갱신 (API 호출 1회) ───────────────────────────────────
            // 전체 재조회면 since=null로 전체를 가져오고, 증분이면 since 이후만 가져옴.
            var freshOpenPrs = GetOpenPullRequestsWithLinkedIssues(isFullRefresh ? null : since);

            List<PRRecord> updatedOpenPrs;
            if (isFullRefresh || cachedOpenPrs == null)
            {
                updatedOpenPrs = freshOpenPrs.Select(p =>
                {
                    p.Pr.LinkedIssueNumbers = p.LinkedIssueNumbers;
                    return p.Pr;
                }).ToList();
            }
            else
            {
                // 증분: 캐시에 fresh 결과를 병합
                updatedOpenPrs = new List<PRRecord>(cachedOpenPrs);
                foreach (var freshPrWithLinks in freshOpenPrs)
                {
                    var freshPr = freshPrWithLinks.Pr;
                    freshPr.LinkedIssueNumbers = freshPrWithLinks.LinkedIssueNumbers;
                    int idx = updatedOpenPrs.FindIndex(p => p.Number == freshPr.Number);
                    if (idx >= 0)
                        updatedOpenPrs[idx] = freshPr;
                    else
                        updatedOpenPrs.Add(freshPr);
                }
            }

            // ── 2. 열린 이슈 갱신 (API 호출 1회) ─────────────────────────────────
            var (freshIssues, closedIssueNumbers) = FetchOpenIssuesWithClaimComments(
                isFullRefresh ? null : since);

            List<IssueRecord> updatedOpenIssues;
            if (isFullRefresh || cachedOpenIssues == null)
            {
                // 전체 재조회: fresh 결과가 현재 열린 이슈 전체
                updatedOpenIssues = freshIssues;
            }
            else
            {
                // 증분: 캐시에 병합하고, since 이후 닫힌 이슈는 제거
                var openIssueDict = cachedOpenIssues.ToDictionary(i => i.Number);
                foreach (var freshIssue in freshIssues)
                    openIssueDict[freshIssue.Number] = freshIssue;
                foreach (var closedNumber in closedIssueNumbers)
                    openIssueDict.Remove(closedNumber);
                updatedOpenIssues = openIssueDict.Values.ToList();
            }

            // ── 3. Claims 판단 ────────────────────────────────────────────────────
            var claimsData = new ClaimsData();

            foreach (var issue in updatedOpenIssues)
            {
                var issueLabels = issue.Labels;
                var comments = issue.CachedClaimComments ?? new List<ClaimComment>();
                bool isClaimed = false;

                foreach (var comment in comments)
                {
                    if ((now - comment.CreatedAt).TotalHours > 48) continue;

                    var login = comment.AuthorLogin;
                    var deadlineHours = IsDocumentTask(issueLabels) ? 24.0 : 48.0;
                    var remaining = comment.CreatedAt.AddHours(deadlineHours) - now;

                    var linkedPrs = updatedOpenPrs
                        .Where(pr => pr.LinkedIssueNumbers.Contains(issue.Number))
                        .ToList();

                    if (!claimsData.ClaimedMap.ContainsKey(login))
                        claimsData.ClaimedMap[login] = new List<IssueRecord>();

                    claimsData.ClaimedMap[login].Add(new IssueRecord
                    {
                        Number = issue.Number,
                        Url = issue.Url,
                        HasPr = linkedPrs.Count > 0,
                        LinkedPullRequests = linkedPrs,
                        Remaining = remaining,
                        Labels = issueLabels
                    });
                    isClaimed = true;
                    break;
                }

                if (!isClaimed)
                    claimsData.UnclaimedUrls.Add(issue.Url);
            }

            return (claimsData, updatedOpenIssues, updatedOpenPrs);
        }

        // 열린 이슈와 선점 댓글을 함께 조회.
        // since가 있으면 해당 시각 이후 업데이트된 이슈만 가져옴.
        //
        // 반환: (열린 이슈 목록, since 이후 닫힌 이슈 번호 집합)
        // 닫힌 이슈 번호 집합: since 이후 업데이트된 이슈를 별도 쿼리로 조회하여
        //                      열린 이슈 목록에 없는 번호를 닫힌 것으로 판단.
        private (List<IssueRecord> openIssues, HashSet<int> closedIssueNumbers)
            FetchOpenIssuesWithClaimComments(DateTimeOffset? since = null)
        {
            var openIssues = new List<IssueRecord>();
            var closedIssueNumbers = new HashSet<int>();
            string? cursor = null;
            bool hasNextPage = true;
            var now = DateTimeOffset.UtcNow;

            // since 이후 업데이트된 이슈 번호 전체 (열린 것 + 닫힌 것) 수집용
            // → 열린 이슈 조회 결과와 비교해 닫힌 이슈를 판별
            var updatedIssueNumbers = new HashSet<int>();
            if (since.HasValue)
            {
                // 검색 API로 since 이후 업데이트된 모든 이슈 번호 수집 (열림/닫힘 구분 없음)
                const string allIssuesQuery = @"
                query($searchQuery: String!, $after: String) {
                    search(query: $searchQuery, type: ISSUE, first: 100, after: $after) {
                        pageInfo { hasNextPage endCursor }
                        nodes {
                            ... on Issue { number }
                        }
                    }
                }";

                string searchString = $"repo:{_owner}/{_repo} is:issue updated:>={since.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}";
                string? searchCursor = null;
                bool searchHasNextPage = true;

                while (searchHasNextPage)
                {
                    var payload = JsonSerializer.Serialize(new
                    {
                        query = allIssuesQuery,
                        variables = new Dictionary<string, object>
                        {
                            ["searchQuery"] = searchString,
                            ["after"] = searchCursor!
                        }
                    });

                    var rawResponse = _graphQLConnection.Run(payload).Result;
                    using var doc = JsonDocument.Parse(rawResponse);

                    if (!doc.RootElement.TryGetProperty("data", out var dataEl) ||
                        !dataEl.TryGetProperty("search", out var searchEl))
                        break;

                    var pageInfo = searchEl.GetProperty("pageInfo");
                    searchHasNextPage = pageInfo.GetProperty("hasNextPage").GetBoolean();
                    searchCursor = pageInfo.GetProperty("endCursor").GetString();

                    if (searchEl.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var node in nodes.EnumerateArray())
                        {
                            if (node.TryGetProperty("number", out var numEl))
                                updatedIssueNumbers.Add(numEl.GetInt32());
                        }
                    }
                }
            }

            // 열린 이슈 + 댓글 조회
            while (hasNextPage)
            {
                var query = new Octokit.GraphQL.Query()
                    .Repository(_repo, _owner)
                    .Issues(
                        first: 100,
                        after: cursor,
                        states: new[] { IssueState.Open },
                        orderBy: new IssueOrder { Field = IssueOrderField.UpdatedAt, Direction = OrderDirection.Desc })
                    .Select(s => new
                    {
                        s.PageInfo.HasNextPage,
                        s.PageInfo.EndCursor,
                        Items = s.Nodes.Select(issue => new
                        {
                            issue.Number,
                            issue.Url,
                            issue.UpdatedAt,
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
                    // UpdatedAt 내림차순이므로 since 이전 항목이 나오면 조기 종료
                    if (since.HasValue && issue.UpdatedAt < since.Value)
                    {
                        hasNextPage = false;
                        break;
                    }

                    var issueLabels = issue.Labels
                        .Select(ParseGitHubLabel)
                        .Where(l => l != GitHubIssuePrLabel.None)
                        .ToList();

                    var claimComments = issue.Comments
                        .Where(c => (now - c.CreatedAt).TotalHours <= 48
                            && _claimKeywords.Any(k => c.Body.Contains(k, StringComparison.OrdinalIgnoreCase)))
                        .Select(c => new ClaimComment
                        {
                            AuthorLogin = c.AuthorLogin ?? "unknown",
                            CreatedAt = c.CreatedAt
                        })
                        .ToList();

                    openIssues.Add(new IssueRecord
                    {
                        Number = issue.Number,
                        Url = issue.Url,
                        Labels = issueLabels,
                        UpdatedAt = issue.UpdatedAt,
                        CachedClaimComments = claimComments
                    });
                }

                if (!hasNextPage) break;
                hasNextPage = result.HasNextPage;
                cursor = result.EndCursor;
            }

            // since 이후 업데이트됐지만 열린 이슈 목록에 없는 것 = 닫힌 이슈
            if (since.HasValue)
            {
                var openNumbers = openIssues.Select(i => i.Number).ToHashSet();
                foreach (var num in updatedIssueNumbers)
                {
                    if (!openNumbers.Contains(num))
                        closedIssueNumbers.Add(num);
                }
            }

            return (openIssues, closedIssueNumbers);
        }

        // since 이후 업데이트된 열린 PR과 본문에서 파싱한 연결 이슈 번호 목록을 반환.
        // since가 null이면 전체 열린 PR을 조회.
        public List<PRWithLinkedIssues> GetOpenPullRequestsWithLinkedIssues(DateTimeOffset? since = null)
        {
            var prsWithIssues = new List<PRWithLinkedIssues>();
            string? cursor = null;
            bool hasNextPage = true;

            var regex = new Regex(@"(?<!\w)#(\d+)\b");

            while (hasNextPage)
            {
                var query = new Octokit.GraphQL.Query()
                    .Repository(_repo, _owner)
                    .PullRequests(first: 100, states: new[] { PullRequestState.Open }, after: cursor)
                    .Select(s => new
                    {
                        s.PageInfo.HasNextPage,
                        s.PageInfo.EndCursor,
                        Items = s.Nodes.Select(pr => new
                        {
                            pr.Number,
                            pr.Title,
                            pr.Url,
                            pr.Body,
                            pr.UpdatedAt,
                            AuthorLogin = pr.Author.Login,
                            Labels = pr.Labels(10, null, null, null, null).Nodes.Select(l => l.Name).ToList()
                        }).ToList()
                    });

                var result = _graphQLConnection.Run(query).Result;

                foreach (var pr in result.Items)
                {
                    if (since.HasValue && pr.UpdatedAt < since.Value)
                        continue;

                    var linkedIssueNumbers = new HashSet<int>();

                    if (!string.IsNullOrWhiteSpace(pr.Body))
                    {
                        var matches = regex.Matches(pr.Body);
                        foreach (Match match in matches)
                        {
                            if (match.Groups.Count > 1 && int.TryParse(match.Groups[1].Value, out int issueNum))
                                linkedIssueNumbers.Add(issueNum);
                        }
                    }

                    prsWithIssues.Add(new PRWithLinkedIssues
                    {
                        Pr = new PRRecord
                        {
                            Number = pr.Number,
                            Title = pr.Title,
                            Url = pr.Url,
                            AuthorLogin = pr.AuthorLogin ?? "",
                            IsMerged = false,
                            UpdatedAt = pr.UpdatedAt,
                            Labels = pr.Labels.Select(ParseGitHubLabel).Where(l => l != GitHubIssuePrLabel.None).ToList()
                        },
                        LinkedIssueNumbers = linkedIssueNumbers.ToList()
                    });
                }

                hasNextPage = result.HasNextPage;
                cursor = result.EndCursor;
            }

            return prsWithIssues;
        }

        internal static bool IsDocumentTask(List<GitHubIssuePrLabel> issueLabels)
        {
            return issueLabels.Contains(GitHubIssuePrLabel.Documentation) || issueLabels.Contains(GitHubIssuePrLabel.Typo);
        }

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
    }
}

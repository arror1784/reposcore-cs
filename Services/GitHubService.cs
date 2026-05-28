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
    /// <summary>
    /// GitHub 이슈 및 Pull Request에 부여된 레이블의 종류를 나타내는 열거형입니다.
    /// </summary>
    public enum GitHubIssuePrLabel
    {
        None, Bug, Documentation, Duplicate, Enhancement, GoodFirstIssue,
        HelpWanted, Invalid, Pinned, Question, Typo, Wontfix
    }

    /// <summary>
    /// 이슈가 닫힌 구체적인 사유를 나타내는 열거형입니다.
    /// </summary>
    public enum IssueClosedStateReason
    {
        None,
        Completed,
        Duplicate,
        NotPlanned
    }

    /// <summary>
    /// 특정 이슈에 대한 선점 댓글 정보를 저장하고 캐싱하기 위한 클래스입니다.
    /// </summary>
    public class ClaimComment
    {
        /// <summary>
        /// 댓글 작성자의 GitHub 로그인 ID를 가져오거나 설정합니다.
        /// </summary>
        public string AuthorLogin { get; set; } = string.Empty;

        /// <summary>
        /// 댓글이 작성된 일시를 가져오거나 설정합니다.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }
    }

    /// <summary>
    /// GitHub 이슈의 상세 기여 정보를 담는 레코드 클래스입니다.
    /// </summary>
    public class IssueRecord
    {
        /// <summary>
        /// 이슈 번호를 가져오거나 설정합니다.
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// 이슈의 GitHub 상세 URL 주소를 가져오거나 설정합니다.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 이슈의 제목을 가져오거나 설정합니다.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 이슈 작성자의 GitHub 로그인 ID를 가져오거나 설정합니다.
        /// </summary>
        public string AuthorLogin { get; set; } = string.Empty;

        /// <summary>
        /// 해당 이슈와 연결된 Pull Request가 존재하는지 여부를 가져오거나 설정합니다.
        /// </summary>
        public bool HasPr { get; set; }

        /// <summary>
        /// 이 이슈와 연동된 Pull Request 기록 목록을 가져오거나 설정합니다.
        /// </summary>
        public List<PRRecord> LinkedPullRequests { get; set; } = new();

        /// <summary>
        /// 이슈가 종료된 사유를 가져오거나 설정합니다.
        /// </summary>
        public IssueClosedStateReason ClosedReason { get; set; } = IssueClosedStateReason.None;

        /// <summary>
        /// 선점 만료까지 남은 잔여 시간을 가져오거나 설정합니다.
        /// </summary>
        public TimeSpan Remaining { get; set; }

        /// <summary>
        /// 이슈에 부착된 유효 레이블 목록을 가져오거나 설정합니다.
        /// </summary>
        public List<GitHubIssuePrLabel> Labels { get; set; } = new();

        /// <summary>
        /// 이슈가 최종 업데이트된 일시를 가져오거나 설정합니다.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// 캐싱된 이슈 선점 댓글 목록을 가져오거나 설정합니다. 값이 null일 경우 JSON 직렬화에서 제외됩니다.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ClaimComment>? CachedClaimComments { get; set; } = null;
    }

    /// <summary>
    /// 저장소 이슈들의 최근 선점 및 미선점 현황 데이터를 관리하는 클래스입니다.
    /// </summary>
    public class ClaimsData
    {
        /// <summary>
        /// 사용자별로 선점한 이슈 목록을 매핑한 딕셔너리를 가져오거나 설정합니다.
        /// </summary>
        public Dictionary<string, List<IssueRecord>> ClaimedMap { get; set; } = new();

        /// <summary>
        /// 아직 아무도 선점하지 않은 열린 이슈들의 URL 목록을 가져오거나 설정합니다.
        /// </summary>
        public List<string> UnclaimedUrls { get; set; } = new();
    }

    /// <summary>
    /// GitHub Pull Request(PR)의 상세 기여 정보를 담는 레코드 클래스입니다.
    /// </summary>
    public class PRRecord
    {
        /// <summary>
        /// Pull Request 번호를 가져오거나 설정합니다.
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// Pull Request의 GitHub 상세 URL 주소를 가져오거나 설정합니다.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Pull Request의 제목을 가져오거나 설정합니다.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Pull Request 작성자의 GitHub 로그인 ID를 가져오거나 설정합니다.
        /// </summary>
        public string AuthorLogin { get; set; } = string.Empty;

        /// <summary>
        /// 해당 Pull Request가 본문에 최종 병합(Merged)되었는지 여부를 가져오거나 설정합니다.
        /// </summary>
        public bool IsMerged { get; set; } = false;

        /// <summary>
        /// Pull Request에 부착된 유효 레이블 목록을 가져오거나 설정합니다.
        /// </summary>
        public List<GitHubIssuePrLabel> Labels { get; set; } = new();

        /// <summary>
        /// Pull Request가 최종 업데이트된 일시를 가져오거나 설정합니다.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// 본문 분석을 통해 연동이 확인된 이슈 번호 목록을 가져오거나 설정합니다. 기본값일 경우 JSON 직렬화에서 제외됩니다.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<int> LinkedIssueNumbers { get; set; } = new();
    }

    /// <summary>
    /// Pull Request 정보와 해당 PR 본문에서 참조 중인 이슈 번호 목록을 쌍으로 묶어 관리하는 데이터 구조 클래스입니다.
    /// </summary>
    public class PRWithLinkedIssues
    {
        /// <summary>
        /// 대상 Pull Request 기록 객체를 가져오거나 설정합니다.
        /// </summary>
        public PRRecord Pr { get; set; } = new();

        /// <summary>
        /// 이 Pull Request와 연결된 이슈 번호 목록을 가져오거나 설정합니다.
        /// </summary>
        public List<int> LinkedIssueNumbers { get; set; } = new();
    }

    /// <summary>
    /// GitHub GraphQL API를 사용하여 특정 저장소의 Pull Request, 이슈, 선점 댓글 현황을 비동기 조회하는 서비스 클래스입니다.
    /// </summary>
    public class GitHubService
    {
        private readonly Octokit.GraphQL.Connection _graphQLConnection;
        private readonly string _owner;
        private readonly string _repo;

        private static readonly string[] s_defaultClaimKeywords = ["제가 하겠습니다", "진행하겠습니다", "할게요", "I'll take this"];
        private readonly string[] _claimKeywords;

        /// <summary>
        /// 지정된 저장소 정보 및 인증 토큰을 사용하여 <see cref="GitHubService"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="owner">저장소 소유자 계정명 (예: 조직명 또는 개인 ID)</param>
        /// <param name="repo">대상 저장소 이름</param>
        /// <param name="token">GitHub API 호출용 Personal Access Token</param>
        /// <param name="keywords">이슈 선점 판단을 위한 사용자 정의 키워드 배열 (미입력 시 기본 키워드 사용)</param>
        /// <exception cref="ArgumentNullException">토큰 인자값이 누락되었거나 빈 문자열일 때 발생합니다.</exception>
        public GitHubService(string owner, string repo, string token, string[]? keywords = null)
        {
            _owner = owner;
            _repo = repo;
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));

            _claimKeywords = keywords ?? s_defaultClaimKeywords;

            _graphQLConnection = new Octokit.GraphQL.Connection(
                new Octokit.GraphQL.ProductHeaderValue("reposcore-cs"), token);
        }

        /// <summary>
        /// 저장소 내에서 메인 브랜치에 병합(Merged)이 완료된 전체 Pull Request 목록을 GraphQL로 비동기 조회합니다.
        /// </summary>
        /// <param name="since">지정된 경우, 해당 일시 이후에 최종 업데이트된 Pull Request 데이터만 필터링하여 수집합니다.</param>
        /// <returns>조회 및 파싱이 완료된 병합된 <see cref="PRRecord"/>의 리스트</returns>
        public async System.Threading.Tasks.Task<List<PRRecord>> GetPullRequestsAsync(DateTimeOffset? since = null)
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

                var result = await _graphQLConnection.Run(query);

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

        /// <summary>
        /// 저장소의 전체 이슈 목록을 GraphQL로 비동기 조회합니다. 
        /// 단, "not planned" 및 "duplicate" 사유로 거절/닫힌 이슈는 수집 대상에서 자동으로 제외됩니다.
        /// </summary>
        /// <param name="since">지정된 경우, 해당 일시 이후에 최종 업데이트된 이슈 데이터만 필터링하여 수집합니다.</param>
        /// <returns>수집 조건에 부합하는 <see cref="IssueRecord"/>의 리스트</returns>
        /// <exception cref="InvalidOperationException">저장소가 존재하지 않거나 GitHub API 측에서 오류 응답을 반환할 때 발생합니다.</exception>
        public async System.Threading.Tasks.Task<List<IssueRecord>> GetIssuesAsync(DateTimeOffset? since = null)
        {
            const string rawGraphQl = @"
            query($owner: String!, $repoName: String!, $searchQuery: String!, $after: String) {
                repository(owner: $owner, name: $repoName) {
                    id
                }
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
                    variables = new Dictionary<string, object?>
                    {
                        ["owner"] = _owner,
                        ["repoName"] = _repo,
                        ["searchQuery"] = searchString,
                        ["after"] = cursor
                    }
                });

                var rawResponse = await _graphQLConnection.Run(requestPayload);
                using var document = JsonDocument.Parse(rawResponse);

                if (document.RootElement.TryGetProperty("errors", out var errorsElement))
                {
                    var firstError = errorsElement.EnumerateArray().FirstOrDefault();
                    var message = firstError.TryGetProperty("message", out var msgEl)
                        ? msgEl.GetString() ?? "알 수 없는 오류"
                        : "알 수 없는 오류";
                    throw new InvalidOperationException(message);
                }

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

        /// <summary>
        /// 저장소의 열린 이슈들을 대상으로 최근 48시간 이내에 발생한 선점 현황 데이터와 점검이 완료된 오픈 데이터들을 비동기 조회합니다.
        /// </summary>
        /// <param name="cachedOpenIssues">기존에 임시 보관 중이던 열린 이슈 목록 데이터</param>
        /// <param name="cachedOpenPrs">기존에 임시 보관 중이던 열린 Pull Request 목록 데이터</param>
        /// <param name="since">캐시 갱신 판단 지점이 되는 특정 기준 일시</param>
        /// <returns>선점 통계 데이터 맵 및 최신으로 동기화된 오픈 이슈/PR 목록 결과 튜플</returns>
        public async System.Threading.Tasks.Task<(ClaimsData claimsData, List<IssueRecord> updatedOpenIssues, List<PRRecord> updatedOpenPrs)>
            GetRecentClaimsDataAsync(
                List<IssueRecord>? cachedOpenIssues = null,
                List<PRRecord>? cachedOpenPrs = null,
                DateTimeOffset? since = null)
        {
            var now = DateTimeOffset.UtcNow;
            bool isFullRefresh = since == null || (now - since.Value).TotalHours > 48;

            var freshOpenPrs = await GetOpenPullRequestsWithLinkedIssuesAsync(isFullRefresh ? null : since);

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

            var (freshIssues, closedIssueNumbers) = await FetchOpenIssuesWithClaimCommentsAsync(
                isFullRefresh ? null : since);

            List<IssueRecord> updatedOpenIssues;
            if (isFullRefresh || cachedOpenIssues == null)
            {
                updatedOpenIssues = freshIssues;
            }
            else
            {
                var openIssueDict = cachedOpenIssues.ToDictionary(i => i.Number);
                foreach (var freshIssue in freshIssues)
                    openIssueDict[freshIssue.Number] = freshIssue;
                foreach (var closedNumber in closedIssueNumbers)
                    openIssueDict.Remove(closedNumber);
                updatedOpenIssues = openIssueDict.Values.ToList();
            }

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

        /// <summary>
        /// 현재 열려 있는 상태의 이슈 목록과 그에 포함된 특정 선점 키워드 댓글 기록을 결합하여 함께 비동기 조회합니다.
        /// </summary>
        /// <param name="since">수집 필터링 기준이 될 최종 변경 일시</param>
        /// <returns>열린 상태의 이슈 목록 리스트와 닫힌 이슈 번호들을 추적한 해시셋 결과 튜플</returns>
        private async System.Threading.Tasks.Task<(List<IssueRecord> openIssues, HashSet<int> closedIssueNumbers)>
            FetchOpenIssuesWithClaimCommentsAsync(DateTimeOffset? since = null)
        {
            var openIssues = new List<IssueRecord>();
            var closedIssueNumbers = new HashSet<int>();
            string? cursor = null;
            bool hasNextPage = true;
            var now = DateTimeOffset.UtcNow;

            var updatedIssueNumbers = new HashSet<int>();
            if (since.HasValue)
            {
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

                    var rawResponse = await _graphQLConnection.Run(payload);
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

                var result = await _graphQLConnection.Run(query);

                foreach (var issue in result.Items)
                {
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

        /// <summary>
        /// 현재 오픈 상태인 Pull Request 목록을 조회하고, 정규표현식을 매칭하여 PR 본문 내에서 교차 참조 중인 연결 이슈 번호 리스트를 함께 구합니다.
        /// </summary>
        /// <param name="since">필터링을 적용할 변경점 기록 일시 기준</param>
        /// <returns>연동 관계 파싱 데이터가 포함된 <see cref="PRWithLinkedIssues"/>의 리스트</returns>
        public async System.Threading.Tasks.Task<List<PRWithLinkedIssues>> GetOpenPullRequestsWithLinkedIssuesAsync(DateTimeOffset? since = null)
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

                var result = await _graphQLConnection.Run(query);

                foreach (var pr in result.Items)
                {

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

        /// <summary>
        /// 이슈 레이블 목록을 기준으로 현재 작업이 단순 문서화 관련(Documentation 또는 Typo) 성격의 태스크인지 검사합니다.
        /// </summary>
        /// <param name="issueLabels">검증을 진행할 이슈 레이블 목록</param>
        /// <returns>문서화 기여 작업에 해당되면 true, 그렇지 않으면 false를 반환합니다.</returns>
        internal static bool IsDocumentTask(List<GitHubIssuePrLabel> issueLabels)
        {
            return issueLabels.Contains(GitHubIssuePrLabel.Documentation) || issueLabels.Contains(GitHubIssuePrLabel.Typo);
        }

        /// <summary>
        /// 문자열 형식의 GitHub 레이블 이름을 시스템 내부 열거형인 <see cref="GitHubIssuePrLabel"/>로 변환 및 파싱합니다.
        /// </summary>
        /// <param name="labelName">GitHub 저장소에서 수집된 레이블 텍스트</param>
        /// <returns>매칭이 완료된 시스템 레이블 열거형 값 (미일치 시 None 반환)</returns>
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

        /// <summary>
        /// Json 응답 요소 내에 표기된 이슈 종료 사유 필드(stateReason)를 시스템 열거형 구조로 추출 및 파싱합니다.
        /// </summary>
        /// <param name="issueNode">파싱 대상이 되는 단일 이슈 노드의 Json 요소 객체</param>
        /// <returns>추출된 이슈 완료 사유 상태값 (<see cref="IssueClosedStateReason"/>)</returns>
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

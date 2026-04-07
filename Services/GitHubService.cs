using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;

namespace RepoScore.Services
{
    public class GitHubService
    {
        private readonly GitHubClient _client;
        private readonly string _owner;
        private readonly string _repo;

        public GitHubService(string owner, string repo, string? token = null)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            _client = new GitHubClient(new ProductHeaderValue("reposcore-cs"));

            if (!string.IsNullOrWhiteSpace(token))
            {
                _client.Credentials = new Credentials(token);
            }
        }

        /// <summary>
        /// 레포지토리의 이슈 목록을 조회합니다. (Pull Request 제외)
        /// </summary>
        public async Task<IReadOnlyList<Issue>> GetIssuesAsync(ItemStateFilter state = ItemStateFilter.All)
        {
            try
            {
                var request = new RepositoryIssueRequest
                {
                    State = state,
                    Filter = IssueFilter.All
                };

                var allItems = await _client.Issue.GetAllForRepository(_owner, _repo, request);

                // GitHub API는 PR도 Issue로 반환하므로 순수 Issue만 필터링
                var issues = new List<Issue>();
                foreach (var item in allItems)
                {
                    if (item.PullRequest == null)
                        issues.Add(item);
                }

                return issues.AsReadOnly();
            }
            catch (AuthorizationException ex)
            {
                throw new InvalidOperationException("GitHub 인증에 실패했습니다. 토큰을 확인해주세요.", ex);
            }
            catch (NotFoundException ex)
            {
                throw new InvalidOperationException($"레포지토리 '{_owner}/{_repo}'를 찾을 수 없습니다.", ex);
            }
            catch (RateLimitExceededException ex)
            {
                throw new InvalidOperationException(
                    $"GitHub API 요청 한도를 초과했습니다. 재시도 가능 시각: {ex.Reset.ToLocalTime()}", ex);
            }
            catch (ApiException ex)
            {
                throw new InvalidOperationException($"GitHub API 오류가 발생했습니다: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 레포지토리의 Pull Request 목록을 조회합니다.
        /// </summary>
        public async Task<IReadOnlyList<PullRequest>> GetPullRequestsAsync(ItemStateFilter state = ItemStateFilter.All)
        {
            try
            {
                var request = new PullRequestRequest
                {
                    State = state
                };

                return await _client.PullRequest.GetAllForRepository(_owner, _repo, request);
            }
            catch (AuthorizationException ex)
            {
                throw new InvalidOperationException("GitHub 인증에 실패했습니다. 토큰을 확인해주세요.", ex);
            }
            catch (NotFoundException ex)
            {
                throw new InvalidOperationException($"레포지토리 '{_owner}/{_repo}'를 찾을 수 없습니다.", ex);
            }
            catch (RateLimitExceededException ex)
            {
                throw new InvalidOperationException(
                    $"GitHub API 요청 한도를 초과했습니다. 재시도 가능 시각: {ex.Reset.ToLocalTime()}", ex);
            }
            catch (ApiException ex)
            {
                throw new InvalidOperationException($"GitHub API 오류가 발생했습니다: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 레포지토리의 커밋 목록을 조회합니다.
        /// </summary>
        public async Task<IReadOnlyList<GitHubCommit>> GetCommitsAsync(string? branch = null, DateTimeOffset? since = null, DateTimeOffset? until = null)
        {
            try
            {
                var request = new CommitRequest();

                if (!string.IsNullOrWhiteSpace(branch))
                    request.Sha = branch;
                if (since.HasValue)
                    request.Since = since;
                if (until.HasValue)
                    request.Until = until;

                return await _client.Repository.Commit.GetAll(_owner, _repo, request);
            }
            catch (AuthorizationException ex)
            {
                throw new InvalidOperationException("GitHub 인증에 실패했습니다. 토큰을 확인해주세요.", ex);
            }
            catch (NotFoundException ex)
            {
                throw new InvalidOperationException($"레포지토리 '{_owner}/{_repo}'를 찾을 수 없습니다.", ex);
            }
            catch (RateLimitExceededException ex)
            {
                throw new InvalidOperationException(
                    $"GitHub API 요청 한도를 초과했습니다. 재시도 가능 시각: {ex.Reset.ToLocalTime()}", ex);
            }
            catch (ApiException ex)
            {
                throw new InvalidOperationException($"GitHub API 오류가 발생했습니다: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 현재 인증된 사용자의 GitHub API 요청 한도 정보를 반환합니다.
        /// </summary>
        public async Task<MiscellaneousRateLimit> GetRateLimitAsync()
        {
            try
            {
                return await _client.RateLimit.GetRateLimits();
            }
            catch (ApiException ex)
            {
                throw new InvalidOperationException($"Rate limit 정보를 가져오는 데 실패했습니다: {ex.Message}", ex);
            }
        }
    }
}

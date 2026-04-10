using System;
using System.Collections.Generic;
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

        public GitHubService(string owner, string repo, string token)
        {
            _owner = owner;
            _repo = repo;

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
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RepoScore.Services;

namespace RepoScore.Data
{

    public class RepoCache
    {
        public string Repository { get; set; } = string.Empty;
        public DateTimeOffset LastAnalyzedAt { get; set; } = DateTimeOffset.MinValue;

        public string[]? Keywords { get; set; }

        public Dictionary<string, List<ClaimRecord>> UserClaims { get; set; } = new();

        public Dictionary<string, List<PRRecord>> UserPullRequests { get; set; } = new();
    }

    public static class CacheManager
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static RepoCache LoadCache(string cacheFilePath, string repoName, bool noCache = false)
        {
            if (noCache)
            {
                Console.Error.WriteLine("ℹ️  캐시를 무시하고 전체 데이터를 다시 수집합니다.");
                return new RepoCache { Repository = repoName };
            }

            if (!File.Exists(cacheFilePath))
            {
                return new RepoCache { Repository = repoName };
            }

            try
            {
                string json = File.ReadAllText(cacheFilePath);
                var cache = JsonSerializer.Deserialize<RepoCache>(json, s_jsonOptions);

                if (cache == null || cache.Repository != repoName)
                {
                    return new RepoCache { Repository = repoName };
                }
                return cache;
            }
            catch
            {
                Console.Error.WriteLine("⚠️ 기존 캐시 파일이 손상되어 새로 수집을 시작합니다.");
                return new RepoCache { Repository = repoName };
            }
        }
        public static void SaveCache(string cacheFilePath, RepoCache cacheData, string[]? keywords)
        {
            var dir = Path.GetDirectoryName(cacheFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            cacheData.LastAnalyzedAt = DateTimeOffset.UtcNow;
            cacheData.Keywords = keywords;

            string json = JsonSerializer.Serialize(cacheData, s_jsonOptions);
            File.WriteAllText(cacheFilePath, json);
        }
        public static bool HasSameKeywords(RepoCache cacheData, string[]? currentKeywords)
        {
            var cachedKeywords = cacheData.Keywords;

            if (cachedKeywords == null && currentKeywords == null)
                return true;

            if (cachedKeywords == null || currentKeywords == null)
                return false;

            return cachedKeywords
                .OrderBy(x => x)
                .SequenceEqual(currentKeywords.OrderBy(x => x));
        }
    }
}

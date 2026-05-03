using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RepoScore.Services;

namespace RepoScore.Data
{
    // 저장소별 분석 캐시 데이터를 표현하는 클래스. JSON 파일로 직렬화되어 저장됨.
    public class RepoCache
    {
        public string Repository { get; set; } = string.Empty;
        public DateTimeOffset LastAnalyzedAt { get; set; } = DateTimeOffset.MinValue;

        public string[]? Keywords { get; set; }

        public Dictionary<string, List<IssueRecord>> UserIssues { get; set; } = new();

        public Dictionary<string, List<PRRecord>> UserPullRequests { get; set; } = new();
    }

    // 캐시 파일의 로드 및 저장을 담당하는 클래스.
    // 이전 분석 결과를 JSON 파일로 유지하여 불필요한 API 요청을 줄임.
    public static class CacheManager
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        // 기존 캐시 파일을 읽어 RepoCache 객체를 반환.
        // 파일이 없거나 손상된 경우, 또는 noCache=true이면 빈 캐시를 반환.
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

        // 분석 결과 캐시를 JSON 파일로 저장. 저장 시각과 키워드를 함께 기록.
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

        // 캐시에 저장된 키워드와 현재 실행 키워드가 동일한지 비교.
        // 키워드가 달라진 경우 캐시 무효화 여부 판단에 사용됨.
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

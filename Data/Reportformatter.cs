using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RepoScore.Services;

namespace RepoScore.Data
{
    public static class ReportFormatter
    {
        public static string BuildTextReport(
            string repo,
            List<(string Id, int docIssues, int featBugIssues, int typoPrs, int docPrs, int featBugPrs, int Score)> reportData)
        {
            var rows = reportData.Select(r => new
            {
                Id = r.Id,
                IssuePr = $"{r.docIssues + r.featBugIssues}/{r.typoPrs + r.docPrs + r.featBugPrs}",
                Score = r.Score.ToString()
            }).ToList();

            string userHeader = "유저";
            string issuePrHeader = "이슈/PR";
            string scoreHeader = "점수";

            int userWidth = Math.Max(userHeader.Length, rows.Any() ? rows.Max(x => GetDisplayWidth(x.Id)) : 0);
            int issuePrWidth = Math.Max(issuePrHeader.Length, rows.Any() ? rows.Max(x => x.IssuePr.Length) : 0);
            int scoreWidth = Math.Max(scoreHeader.Length, rows.Any() ? rows.Max(x => x.Score.Length) : 0);

            string separator =
                new string('-', userWidth) + "-+-" +
                new string('-', issuePrWidth) + "-+-" +
                new string('-', scoreWidth);

            var sb = new StringBuilder();
            sb.AppendLine($"=== {repo} 오픈소스 기여도 분석 리포트 ===");
            sb.AppendLine($"분석 일시: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();

            sb.AppendLine(
                PadRightKorean(userHeader, userWidth) + " | " +
                PadLeft(issuePrHeader, issuePrWidth) + " | " +
                PadLeft(scoreHeader, scoreWidth));

            sb.AppendLine(separator);

            foreach (var row in rows)
            {
                sb.AppendLine(
                    PadRightKorean(row.Id, userWidth) + " | " +
                    PadLeft(row.IssuePr, issuePrWidth) + " | " +
                    PadLeft(row.Score, scoreWidth));
            }

            return sb.ToString();
        }

        public static string BuildClaimsReport(ClaimsData data, string mode)
        {
            var sb = new StringBuilder();

            if (data.ClaimedMap.Count == 0 && data.UnclaimedUrls.Count == 0)
            {
                return "최근 48시간 내 선점된 이슈가 없습니다.\n";
            }

            if (mode == "user")
            {
                if (data.UnclaimedUrls.Count > 0)
                {
                    sb.AppendLine("미선점 이슈");
                    foreach (var url in data.UnclaimedUrls) sb.AppendLine($" - {url}");
                }

                if (data.ClaimedMap.Count > 0)
                {
                    sb.AppendLine("\n선점된 이슈");
                    foreach (var (login, claims) in data.ClaimedMap)
                    {
                        sb.AppendLine($"{login}");
                        foreach (var claim in claims)
                        {
                            sb.AppendLine($" - {claim.Url}");
                            if (claim.Labels.Count > 0) sb.AppendLine($"   라벨: {string.Join(", ", claim.Labels)}");
                            sb.AppendLine(claim.HasPr ? "   PR 생성됨" : FormatRemainingTime(claim.Remaining));
                        }
                    }
                }
            }
            else
            {
                var claimedIssues = data.ClaimedMap.SelectMany(kv => kv.Value.Select(c => (Login: kv.Key, Claim: c)))
                                                  .OrderBy(x => x.Claim.Number).ToList();

                if (claimedIssues.Count > 0)
                {
                    sb.AppendLine("선점된 이슈");
                    foreach (var (login, claim) in claimedIssues)
                    {
                        sb.AppendLine($" #{claim.Number} {claim.Url}");
                        sb.AppendLine($"   선점자: {login}");
                        if (claim.Labels.Count > 0) sb.AppendLine($"   라벨: {string.Join(", ", claim.Labels)}");
                        sb.AppendLine(claim.HasPr ? "   PR 생성됨" : FormatRemainingTime(claim.Remaining));
                    }
                }

                if (data.UnclaimedUrls.Count > 0)
                {
                    sb.AppendLine("\n미선점 이슈");
                    foreach (var url in data.UnclaimedUrls) sb.AppendLine($" - {url}");
                }
            }

            return sb.ToString();
        }

        public static string FormatRemainingTime(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero) return "   기한 초과";
            return $"   남은 시간: {(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }

        public static string PadLeft(string text, int width)
        {
            return text.PadLeft(width);
        }

        public static string PadRightKorean(string text, int width)
        {
            int textWidth = GetDisplayWidth(text);
            if (textWidth >= width) return text;

            return text + new string(' ', width - textWidth);
        }

        public static int GetDisplayWidth(string text)
        {
            int width = 0;

            foreach (char c in text)
            {
                width += c > 127 ? 2 : 1;
            }

            return width;
        }
    }
}

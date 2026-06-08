using System;

namespace RepoScore.Data
{
    /// <summary>
    /// 한도 계산 공식의 결과 데이터를 보관하는 구조체 클래스입니다.
    /// </summary>
    public class ContributionLimitResult
    {
        public int MaxDocTypoPrCount { get; set; }
        public int ValidPrCount { get; set; }
        public int MaxIssueCount { get; set; }
        public int ValidIssueCount { get; set; }
        public int RejectedPrCount { get; set; }
        public int RejectedIssueCount { get; set; }
    }

    /// <summary>
    /// 오픈소스 기여 점수를 계산하는 클래스입니다.
    /// PR 및 이슈 개수 비율에 따른 유효 개수 제한(Validity Limits)을 적용하여 최종 점수를 산출합니다.
    /// </summary>
    public static class ScoreCalculator
    {
        public const int MaxDocTypoPrMultiplier = 3;
        public const int MaxIssuePerValidPrMultiplier = 4;

        private const int s_scorePrFeatureBug = 3;
        private const int s_scorePrDoc = 2;
        private const int s_scorePrTypo = 1;

        private const int s_scoreIssueFeatureBug = 2;
        private const int s_scoreIssueDoc = 1;

        public static ContributionLimitResult CalculateLimits(
            int featureBugPrCount,
            int docPrCount,
            int typoPrCount,
            int featureBugIssueCount,
            int docIssueCount)
        {
            int maxDocTypoPrCount = MaxDocTypoPrMultiplier * Math.Max(featureBugPrCount, 1);
            int validPrCount = featureBugPrCount + Math.Min(docPrCount + typoPrCount, maxDocTypoPrCount);
            int maxIssueCount = MaxIssuePerValidPrMultiplier * validPrCount;
            int validIssueCount = Math.Min(featureBugIssueCount + docIssueCount, maxIssueCount);

            // ReportFormatter에서 사용할 문서/오타 PR 및 이슈의 초과분(Rejected) 계산
            int rejectedPrCount = Math.Max(0, (docPrCount + typoPrCount) - maxDocTypoPrCount);
            int rejectedIssueCount = Math.Max(0, (featureBugIssueCount + docIssueCount) - maxIssueCount);

            return new ContributionLimitResult
            {
                MaxDocTypoPrCount = maxDocTypoPrCount,
                ValidPrCount = validPrCount,
                MaxIssueCount = maxIssueCount,
                ValidIssueCount = validIssueCount,
                RejectedPrCount = rejectedPrCount,
                RejectedIssueCount = rejectedIssueCount
            };
        }

        /// <summary>
        /// 학생의 기여 내역을 바탕으로 최종 점수를 계산합니다.
        /// </summary>
        public static int CalculateFinalScore(
            int featureBugPrCount,
            int docPrCount,
            int typoPrCount,
            int featureBugIssueCount,
            int docIssueCount)
        {
            // 1단계 & 2단계: 공용 한도 계산 메서드 호출로 대체
            var limits = CalculateLimits(featureBugPrCount, docPrCount, typoPrCount, featureBugIssueCount, docIssueCount);
            int validPrCount = limits.ValidPrCount;
            int validIssueCount = limits.ValidIssueCount;

            // 3단계: PR 최적화 계산 (배점이 높은 기능/버그 -> 문서 -> 오타 순으로 채움)
            int optimizedFeatureBugPrCount = Math.Min(featureBugPrCount, validPrCount);

            int remainingPrSlots = validPrCount - optimizedFeatureBugPrCount;
            int optimizedDocPrCount = Math.Min(docPrCount, remainingPrSlots);

            int optimizedTypoPrCount = validPrCount - optimizedFeatureBugPrCount - optimizedDocPrCount;

            // 4단계: 이슈 최적화 계산 (배점이 높은 기능/버그 -> 문서 순으로 채움)
            int optimizedFeatureBugIssueCount = Math.Min(featureBugIssueCount, validIssueCount);
            int optimizedDocIssueCount = validIssueCount - optimizedFeatureBugIssueCount;

            // 5단계: 최종 점수 합산
            int finalScore = (optimizedFeatureBugPrCount * s_scorePrFeatureBug)
                           + (optimizedDocPrCount * s_scorePrDoc)
                           + (optimizedTypoPrCount * s_scorePrTypo)
                           + (optimizedFeatureBugIssueCount * s_scoreIssueFeatureBug)
                           + (optimizedDocIssueCount * s_scoreIssueDoc);

            return finalScore;
        }
    }
}

using UnityEngine;

// 보스전 점수 계산 결과. 항목별 점수 · 합계 · 등급
public readonly struct ScoreResult
{
    public ScoreResult(float totalTimeScore, float retryScore, float bossScore, string grade)
    {
        TotalTimeScore = totalTimeScore;
        RetryScore = retryScore;
        BossScore = bossScore;
        Total = totalTimeScore + retryScore + bossScore;
        Grade = grade;
    }

    public float TotalTimeScore { get; }
    public float RetryScore { get; }
    public float BossScore { get; }
    public float Total { get; }
    public string Grade { get; }
}

// 보스전 기록으로 점수 · 등급을 계산한다.
// 점수로 S 를 받았을 때만 보스 처치 · 재시도 없음마다 S 를 하나씩 더 붙여 최대 SSS 다.
public static class ScoreCalculator
{
    private const string BONUS_GRADE = "S";

    public static ScoreResult Calculate(ScoreSettings settings, float totalTime, int retryCount, float bossDamageRatio, GameFlow.BossOutcome outcome)
    {
        // 만점 기준 시간에서 0 점 기준 시간까지 1 → 0 으로 줄어드는 비율
        float timeRate = 1f - Mathf.InverseLerp(settings.FullScoreTime, settings.ZeroScoreTime, totalTime);
        float totalTimeScore = settings.TotalTimeMaxScore * timeRate;
        int penalizedRetryCount = Mathf.Max(0, retryCount - settings.FreeRetryCount);
        float retryScore = Mathf.Max(0f, settings.RetryMaxScore - settings.RetryPenalty * penalizedRetryCount);
        float bossScore = settings.BossMaxScore * Mathf.Clamp01(bossDamageRatio);

        if(outcome == GameFlow.BossOutcome.Wiped) bossScore *= settings.WipedBossScoreRate;

        float total = totalTimeScore + retryScore + bossScore;

        return new ScoreResult(totalTimeScore, retryScore, bossScore, GetGrade(settings, total, retryCount, outcome));
    }

    private static string GetGrade(ScoreSettings settings, float total, int retryCount, GameFlow.BossOutcome outcome)
    {
        string grade = settings.GetGrade(total);

        if(!settings.IsTopGrade(total)) return grade;

        if(outcome == GameFlow.BossOutcome.Cleared) grade += BONUS_GRADE;
        if(retryCount == 0) grade += BONUS_GRADE;

        return grade;
    }
}

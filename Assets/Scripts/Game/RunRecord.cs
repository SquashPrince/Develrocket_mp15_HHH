using UnityEngine;

// 한 판의 기록. 씬을 다시 불러와도 남고 게임을 끄면 사라진다(파일에 저장하지 않는다).
// 다시 하기를 씬 다시 불러오기로 처리해, 기능마다 되돌리는 코드 없이 어디서 시작할지와 지금까지의 기록만 여기에 둔다.
public static class RunRecord
{
    // 0 이면 처음 시작 지점, 아니면 마지막으로 정리한 구역의 다시 시작 지점에서 시작한다.
    public static int ClearedZoneCount { get; private set; }
    // 재시도까지 누적한다.
    public static float TotalTime { get; private set; }
    // 보스 처치 후 재도전할 때 되돌릴 값
    public static float BossStartTotalTime { get; private set; }
    public static int RetryCount { get; private set; }
    // 마지막 보스전 시도의 값
    public static float BossTime { get; private set; }
    public static float BossDamageRatio { get; private set; }

    public static void AddTime(float deltaTime) => TotalTime += deltaTime;

    public static void BeginBossPhase() => BossStartTotalTime = TotalTime;

    // 정리한 구역 수는 줄어들지 않는다.
    public static void ClearZone(int clearedCount) => ClearedZoneCount = Mathf.Max(ClearedZoneCount, clearedCount);

    public static void EndBossPhase(float bossTime, float damageRatio)
    {
        BossTime = bossTime;
        BossDamageRatio = damageRatio;
    }

    public static void ResetRun()
    {
        ClearedZoneCount = 0;
        TotalTime = 0f;
        BossStartTotalTime = 0f;
        RetryCount = 0;
        BossTime = 0f;
        BossDamageRatio = 0f;
    }

    public static void Retry() => RetryCount++;

    // 이전 처치 기록과 같은 조건에서 겨루도록 전체 시간을 보스전 시작 값으로 되돌린다.
    public static void RetryBossAfterClear() => TotalTime = BossStartTotalTime;

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 기록이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad() => ResetRun();
}

using UnityEngine;

// 한 판의 기록. 씬을 다시 불러와도(재시작 · 재도전) 남고, 게임을 끄면 사라진다.
// 재시작 · 재도전을 씬 다시 불러오기로 처리하기 때문에, 다시 불러온 씬이 어디서 시작할지와 지금까지의 기록을 여기에 둔다.
//   씬을 다시 불러오면 파티 · 보스 · 화면 효과가 모두 처음 값으로 돌아가므로, 기능마다 되돌리는 코드를 따로 두지 않아도 된다.
// 보스전 앞 스폰 포인트는 저장하지 않는다는 규칙대로 파일에 쓰지 않는다.
// 전체 시간 : 보스전 재도전까지 누적한다. 웨이브 중 전멸 후 재시작하면 0 부터 다시 센다.
// 보스 시간 · 깎은 체력 비율 : 마지막 보스전 시도의 값이다. 결과 화면(점수)에서 쓴다.
public static class RunRecord
{
    public enum StartPoint
    {
        // 첫 웨이브부터
        StageStart,
        // 보스전 앞 스폰 포인트에서 보스전 웨이브부터
        BossPhase
    }

    public static StartPoint StartFrom { get; private set; }
    public static float TotalTime { get; private set; }
    // 이번 보스전이 시작된 순간의 전체 시간. 보스 처치 후 재도전할 때 되돌릴 값이다. (결과 화면 작업)
    public static float BossStartTotalTime { get; private set; }
    public static int RetryCount { get; private set; }
    public static float BossTime { get; private set; }
    // 깎은 보스 체력 비율(0 ~ 1). 처치했으면 1 이다.
    public static float BossDamageRatio { get; private set; }

    public static void AddTime(float deltaTime) => TotalTime += deltaTime;

    public static void BeginBossPhase() => BossStartTotalTime = TotalTime;

    public static void EndBossPhase(float bossTime, float damageRatio)
    {
        BossTime = bossTime;
        BossDamageRatio = damageRatio;
    }

    // 처음부터 다시 한다. 기록을 모두 지운다. 웨이브 전멸 후 재시작 · 타이틀로 갈 때 부른다.
    public static void ResetRun()
    {
        StartFrom = StartPoint.StageStart;
        TotalTime = 0f;
        BossStartTotalTime = 0f;
        RetryCount = 0;
        BossTime = 0f;
        BossDamageRatio = 0f;
    }

    // 보스전 실패 후 재도전한다. 재시도 횟수가 오르고, 전체 시간은 그대로 이어서 센다.
    public static void RetryBoss()
    {
        StartFrom = StartPoint.BossPhase;
        RetryCount++;
    }

    // 보스 처치 후 더 빠른 처치에 다시 도전한다. 재시도 횟수는 그대로 두고, 전체 시간을 이번 보스전이 시작된 순간의 값으로 되돌린다.
    // 이전 처치 기록과 같은 조건에서 겨루게 해, 더 빨리 잡으면 점수가 오를 수 있게 하기 위해서다.
    public static void RetryBossAfterClear()
    {
        StartFrom = StartPoint.BossPhase;
        TotalTime = BossStartTotalTime;
    }

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 기록이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad() => ResetRun();
}

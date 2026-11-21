using UnityEngine;

// 한 판의 기록. 씬을 다시 불러와도(재시작 · 재도전) 남고, 게임을 끄면 사라진다.
// 재시작 · 재도전을 씬 다시 불러오기로 처리하기 때문에, 다시 불러온 씬이 어디서 시작할지와 지금까지의 기록을 여기에 둔다.
//   씬을 다시 불러오면 파티 · 보스 · 화면 효과가 모두 처음 값으로 돌아가므로, 기능마다 되돌리는 코드를 따로 두지 않아도 된다.
// 다시 시작 지점은 저장하지 않는다는 규칙대로 파일에 쓰지 않는다.
// 시작 지점 : 정리한 전투 구역 수로 정한다. 0 이면 처음 시작 지점, 아니면 마지막으로 정리한 구역의 다시 시작 지점이다.
// 전체 시간 : 재시도(구역 전투 전멸 · 보스전 실패)까지 누적한다. 처음부터 다시 하면 0 부터 다시 센다.
// 재시도 횟수 : 구역 전투 전멸 · 보스전 실패 뒤 다시 할 때마다 오른다. 어디서 실패했든 같은 규칙이다.
// 보스 시간 · 깎은 체력 비율 : 마지막 보스전 시도의 값이다. 결과 화면(점수)에서 쓴다.
public static class RunRecord
{
    // 정리한 전투 구역 수. 다시 불러온 씬은 이 수만큼의 구역을 열어 두고 그 뒤에서 시작한다.
    public static int ClearedZoneCount { get; private set; }
    public static float TotalTime { get; private set; }
    // 이번 보스전이 시작된 순간의 전체 시간. 보스 처치 후 재도전할 때 되돌릴 값이다. (결과 화면 작업)
    public static float BossStartTotalTime { get; private set; }
    public static int RetryCount { get; private set; }
    public static float BossTime { get; private set; }
    // 깎은 보스 체력 비율(0 ~ 1). 처치했으면 1 이다.
    public static float BossDamageRatio { get; private set; }

    public static void AddTime(float deltaTime) => TotalTime += deltaTime;

    public static void BeginBossPhase() => BossStartTotalTime = TotalTime;

    // 구역 하나를 정리했다. 정리한 구역 수는 줄어들지 않는다.
    public static void ClearZone(int clearedCount) => ClearedZoneCount = Mathf.Max(ClearedZoneCount, clearedCount);

    public static void EndBossPhase(float bossTime, float damageRatio)
    {
        BossTime = bossTime;
        BossDamageRatio = damageRatio;
    }

    // 처음부터 다시 한다. 기록을 모두 지운다. 처음부터 재시작 · 타이틀로 갈 때 부른다.
    public static void ResetRun()
    {
        ClearedZoneCount = 0;
        TotalTime = 0f;
        BossStartTotalTime = 0f;
        RetryCount = 0;
        BossTime = 0f;
        BossDamageRatio = 0f;
    }

    // 구역 전투 전멸 · 보스전 실패 후 마지막으로 정리한 구역 뒤에서 다시 한다. 재시도 횟수가 오르고, 전체 시간은 그대로 이어서 센다.
    public static void Retry() => RetryCount++;

    // 보스 처치 후 더 빠른 처치에 다시 도전한다. 재시도 횟수는 그대로 두고, 전체 시간을 이번 보스전이 시작된 순간의 값으로 되돌린다.
    // 이전 처치 기록과 같은 조건에서 겨루게 해, 더 빨리 잡으면 점수가 오를 수 있게 하기 위해서다.
    public static void RetryBossAfterClear() => TotalTime = BossStartTotalTime;

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 기록이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad() => ResetRun();
}

using UnityEngine;

// 모든 적이 함께 쓰는 적 전용 시간 배율. 완벽 회피 슬로모션 · 필살기 연출처럼 적만 느려져야 할 때 낮춘다.
// 플레이어는 원래 속도로 움직여야 하므로 Time.timeScale 은 건드리지 않는다. 적 쪽 기능은 EnemyClock 을 거쳐 읽는다.
public static class EnemyTime
{
    public static float Scale { get; set; } = 1f;

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 배율이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetScale() => Scale = 1f;
}

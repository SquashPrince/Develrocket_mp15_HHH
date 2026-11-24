using UnityEngine;

// 모든 적이 함께 쓰는 적 전용 시간 배율. 완벽 회피 슬로모션 · 필살기 연출처럼 적만 느려지거나 멈춰야 할 때 이 배율을 낮춘다.
// 플레이어는 원래 속도로 움직여야 하므로 Unity 전체 시간(Time.timeScale)은 건드리지 않는다.
// 적 하나만 멈추는 히트스톱은 각 적의 시간(EnemyClock)이 맡는다. 적 · 적 애니메이션 · 적 행동 노드는 이 배율을 직접 쓰지 않고,
// 이 배율에 자기 히트스톱을 더한 자기 시간의 DeltaTime 을 쓴다.
public static class EnemyTime
{
    // 1이면 원래 속도, 0.5면 절반 속도
    public static float Scale { get; set; } = 1f;

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 배율이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetScale() => Scale = 1f;
}

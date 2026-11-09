using UnityEngine;

// 적 전용 시간 배율. 완벽 회피 슬로모션처럼 적만 느려져야 할 때 이 배율을 낮춘다.
// 플레이어는 원래 속도로 움직여야 하므로 Unity 전체 시간(Time.timeScale)은 건드리지 않는다.
// 적 · 적 애니메이션 · 적 이펙트는 프레임 시간으로 Time.deltaTime 대신 DeltaTime 을 써야 슬로모션이 적용된다.
public static class EnemyTime
{
    // 1이면 원래 속도, 0.5면 절반 속도
    public static float Scale { get; set; } = 1f;

    public static float DeltaTime => Time.deltaTime * Scale;

    // 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 플레이의 배율이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetScale() => Scale = 1f;
}

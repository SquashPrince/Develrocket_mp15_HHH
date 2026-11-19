using UnityEngine;

// 적 하나의 시간. 모든 적이 함께 쓰는 적 전용 배율(EnemyTime)에 이 적만의 정지(히트스톱)를 더한다.
// 완벽 회피 슬로모션 · 필살기 연출처럼 모든 적에게 거는 배율은 EnemyTime 이 맡고, 맞은 적 하나만 멈추는 것은 이 시간이 맡는다.
// 적 · 적 애니메이션 · 적 행동 노드는 프레임 시간으로 EnemyTime 대신 자기 시간의 DeltaTime 을 써야 두 효과가 모두 적용된다.
// 히트스톱 남은 시간은 게임 시간으로 줄인다. 플레이어의 히트렉과 같은 시간으로 재야 둘이 함께 멈췄다 함께 풀리고, 일시정지 중에는 함께 멈추기 때문이다.
// 적 상태(EnemyState)가 하나씩 가지고 매 프레임 갱신한다.
public class EnemyClock
{
    private float _hitStopTimer;

    // 히트스톱으로 멈춰 있는지
    public bool IsHitStopped => _hitStopTimer > 0f;
    // 이 적의 시간 배율. 멈춰 있으면 0, 아니면 적 전용 배율이다.
    public float Scale => IsHitStopped ? 0f : EnemyTime.Scale;
    public float DeltaTime => Time.deltaTime * Scale;

    // duration(초) 동안 이 적을 멈춘다. 멈춘 중에 다시 걸면 남은 시간과 새 시간 중 긴 쪽으로 이어 간다.
    public void BeginHitStop(float duration)
    {
        if(duration <= 0f) return;

        _hitStopTimer = Mathf.Max(_hitStopTimer, duration);
    }

    // 매 프레임 히트스톱 남은 시간을 줄인다.
    public void Tick()
    {
        if(_hitStopTimer > 0f) _hitStopTimer -= Time.deltaTime;
    }
}

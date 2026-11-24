using UnityEngine;

// 적 하나의 시간. 모든 적에게 거는 배율(EnemyTime)에 이 적만의 히트스톱을 더한다.
// 적 쪽 기능 · 행동 노드는 EnemyTime 대신 이 DeltaTime 을 써야 두 효과가 모두 적용된다.
public class EnemyClock
{
    private float _hitStopTimer;

    public bool IsHitStopped => _hitStopTimer > 0f;
    public float Scale => IsHitStopped ? 0f : EnemyTime.Scale;
    public float DeltaTime => Time.deltaTime * Scale;

    // 멈춘 중에 다시 걸면 남은 시간과 새 시간 중 긴 쪽으로 이어 간다.
    public void BeginHitStop(float duration)
    {
        if(duration <= 0f) return;

        _hitStopTimer = Mathf.Max(_hitStopTimer, duration);
    }

    // 플레이어 히트렉과 같은 게임 시간으로 재야 둘이 함께 멈췄다 함께 풀리고, 일시정지 중에는 함께 멈춘다.
    public void Tick()
    {
        if(_hitStopTimer > 0f) _hitStopTimer -= Time.deltaTime;
    }
}

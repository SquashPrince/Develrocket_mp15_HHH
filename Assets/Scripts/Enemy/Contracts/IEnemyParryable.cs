using UnityEngine;

// 패링 가능 시간을 여는 적. 패링 공격을 하는 적이 구현한다.
// 플레이어가 패링 대상을 고르고, 패링 대상 앞으로 순간 이동하고, 맞았을 때 넉백 방향을 정할 때 쓴다.
public interface IEnemyParryable
{
    // 패링 대상 앞으로 순간 이동하거나 넉백 방향을 정할 때 위치 · 정면을 쓴다.
    Transform transform { get; }

    bool IsParryWindowOpen { get; }
}

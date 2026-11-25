using UnityEngine;

// 플레이어가 공격 · 록온 · 순간 이동 대상으로 고를 수 있는 적.
public interface IEnemyTargetable
{
    // MonoBehaviour 가 이미 가진 transform 을 그대로 쓴다.
    Transform transform { get; }

    bool IsAlive { get; }

    // 몸의 수평 반지름. 순간 이동 위치 · 공격 정지 거리를 몸 표면 기준으로 정할 때 쓴다.
    float Radius { get; }

    // 없으면 null 이고, 기본 높이에 띄운다.
    Transform LockOnPoint { get; }
}

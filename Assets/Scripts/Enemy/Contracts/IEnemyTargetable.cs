using UnityEngine;

// 플레이어가 공격 · 록온 · 순간 이동 대상으로 고를 수 있는 적.
// 타겟팅 · 록온 카메라 · 공격 접근 거리 · 필살기 · 콤보 스킬이 적의 위치 · 생존 여부 · 몸 반지름 · 록온 표시 위치를 볼 때 쓴다.
public interface IEnemyTargetable
{
    // MonoBehaviour 가 이미 가진 transform 을 그대로 쓴다. 구현하는 컴포넌트가 따로 만들 필요는 없다.
    Transform transform { get; }

    // 처치된 적은 대상으로 고르지 않고, 록온 중이었으면 록온이 풀린다.
    bool IsAlive { get; }

    // 몸의 수평 반지름. 순간 이동 위치 · 공격 정지 거리를 몸 표면 기준으로 정할 때 쓴다.
    float Radius { get; }

    // 록온 표시를 띄울 위치(머리 · 가슴 등). 없으면 null 이고, 타겟팅 기능이 기본 높이에 띄운다.
    Transform LockOnPoint { get; }
}

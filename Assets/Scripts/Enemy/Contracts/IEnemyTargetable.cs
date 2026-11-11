using UnityEngine;

// 플레이어가 공격 · 록온 · 순간 이동 대상으로 고를 수 있는 적.
// 타겟팅 · 록온 카메라 · 공격 접근 거리 · 필살기 · 콤보 스킬이 적의 위치와 생존 여부를 볼 때 쓴다.
public interface IEnemyTargetable
{
    // MonoBehaviour 가 이미 가진 transform 을 그대로 쓴다. 구현하는 컴포넌트가 따로 만들 필요는 없다.
    Transform transform { get; }

    // 처치된 적은 대상으로 고르지 않고, 록온 중이었으면 록온이 풀린다.
    bool IsAlive { get; }
}

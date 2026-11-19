using UnityEngine;

// 플레이어 공격에 맞아 체력이 줄어드는 적.
// 플레이어 타격 판정이 맞은 적에게 위력과 강타 여부를 넘길 때 쓴다.
public interface IEnemyDamageable
{
    // 맞은 적의 경직 · 그로기 기능을 같은 오브젝트에서 찾을 때 쓴다.
    Transform transform { get; }

    // 이 타격으로 처치된 적은 경직 · 콤보 스킬 대상에서 빠진다.
    bool IsAlive { get; }

    // power : 공격 위력. 체력에서 뺀다.
    // isImpact : 강타인지. 체력이 0 인 적은 강타에 맞아야만 처치된다.
    void TakeDamage(float power, bool isImpact);

    // duration(초) 동안 이 적 하나만 멈춘다(히트스톱). 다른 적과 플레이어는 그대로 움직인다.
    void BeginHitStop(float duration);
}

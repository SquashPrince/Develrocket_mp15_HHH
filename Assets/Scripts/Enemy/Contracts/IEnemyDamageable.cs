using UnityEngine;

// 플레이어 공격에 맞아 체력이 줄어드는 적.
public interface IEnemyDamageable
{
    // 맞은 적의 경직 · 그로기 기능을 같은 오브젝트에서 찾을 때 쓴다.
    Transform transform { get; }

    bool IsAlive { get; }

    // 실제로 받은 대미지를 돌려준다. 체력이 0 인 적은 강타(isImpact)에 맞아야 처치된다.
    float TakeDamage(float power, bool isImpact);

    // 이 적 하나만 멈춘다.
    void BeginHitStop(float duration);
}

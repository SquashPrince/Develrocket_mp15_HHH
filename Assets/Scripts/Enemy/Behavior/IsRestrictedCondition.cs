using System;
using Unity.Behavior;
using Unity.Properties;

// [행동 그래프 조건] 적이 행동할 수 없는 상태(경직 · 그로기 · 처치)이면 참이다.
// Abort 모디파이어의 조건으로 써서, 하던 이동 · 공격을 바로 끊는 데 쓴다.
[Serializable, GeneratePropertyBag]
[Condition(
    name: "Is Restricted",
    category: "Enemy",
    story: "Self is restricted",
    id: "be2c42cd95884df4ae61a443c2141f04")]
public partial class IsRestrictedCondition : Condition
{
    private EnemyHealth _health;
    private EnemyReaction _reaction;

    public override void OnStart()
    {
        _health = GameObject.GetComponent<EnemyHealth>();
        _reaction = GameObject.GetComponent<EnemyReaction>();
    }

    public override bool IsTrue() => !_health.IsAlive || _reaction.IsRestricted;
}

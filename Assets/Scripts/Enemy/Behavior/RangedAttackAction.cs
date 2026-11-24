using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

// [행동 그래프 행동] 대상이 공격 사거리 밖이면, 사거리 밖에서 쓸 수 있는 공격(돌진 등)을 다가가지 않고 바로 한다.
// Try In Order 로 Attack Target 보다 앞에 두어, 실패하면 Attack Target 이 다가가서 공격하게 한다.
// 공격이 경직 · 그로기 · 처치로 끊기면 실패한다.
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Ranged Attack",
    description: "대상이 공격 사거리 밖이면 사거리 밖에서 쓸 수 있는 공격을 바로 한다. 쓸 수 없으면 바로 실패한다.",
    story: "Ranged attack [Target]",
    category: "Enemy",
    id: "0f0f3a3582304bc2a6a1f2c652e62c3f")]
public partial class RangedAttackAction : Action
{
    [SerializeReference] public BlackboardVariable<Transform> Target;

    private EnemyAttacker _attacker;

    protected override Status OnStart()
    {
        if(Target.Value == null) return Status.Failure;

        _attacker = GameObject.GetComponent<EnemyAttacker>();
        EnemyData data = GameObject.GetComponent<EnemyController>().Data;

        Vector3 toTarget = HorizontalVector.Between(GameObject.transform.position, Target.Value.position);

        if(toTarget.sqrMagnitude <= data.AttackRange * data.AttackRange) return Status.Failure;
        if(!_attacker.TryStartAttack(Target.Value, true)) return Status.Failure;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if(_attacker.IsAttacking) return Status.Running;

        return _attacker.WasLastAttackInterrupted ? Status.Failure : Status.Success;
    }
}

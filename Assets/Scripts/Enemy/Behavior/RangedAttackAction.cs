using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

// [행동 그래프 행동] 대상이 공격 사거리 밖일 때, 사거리 밖에서 쓸 수 있는 공격(추격 · 돌진 등)을 다가가지 않고 바로 한다.
// 대상이 사거리 안이거나, 쓸 수 있는 공격이 없으면(쿨타임 · 사용 최소 거리) 바로 실패한다.
// 그래프의 공격 자리에서 Try In Order 로 Attack Target 노드보다 앞에 둔다. 실패하면 Attack Target 이 다가가서 공격한다.
// 그래서 원거리 조건은 공격을 고르는 순간에만 보고, 적은 기존 주기(추적 → 거리 유지 → 공격 → 대기)를 그대로 따른다.
// 공격이 끝나면 성공하고, 경직 · 그로기 · 처치로 끊기면 실패한다. 어떤 공격을 할지는 적 공격 기능이 고른다.
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

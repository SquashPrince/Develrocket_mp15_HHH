using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

// [행동 그래프 행동] 공격 사거리까지 대상에게 다가간 뒤 공격 하나를 하고, 공격이 끝나면 성공한다.
// 쓸 수 있는 공격이 없거나(모두 쿨타임) 대상이 없으면 실패한다. 공격이 경직 · 그로기 · 처치로 끊겨도 실패한다.
// 어떤 공격을 할지는 적 공격 기능이 고른다.
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Attack Target",
    description: "공격 사거리까지 다가가 공격 하나를 한다.",
    story: "Attack [Target]",
    category: "Enemy",
    id: "461c140b1d764dc49f3532efb3071cf4")]
public partial class AttackTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<Transform> Target;

    private EnemyMovement _movement;
    private EnemyBehaviorSetting _setting;
    private EnemyAttacker _attacker;
    private bool _hasStartedAttack;

    protected override Status OnStart()
    {
        if(Target.Value == null) return Status.Failure;

        _movement = GameObject.GetComponent<EnemyMovement>();
        _setting = GameObject.GetComponent<EnemyBehaviorSetting>();
        _attacker = GameObject.GetComponent<EnemyAttacker>();
        _hasStartedAttack = false;

        if(!_attacker.CanAttack()) return Status.Failure;

        return OnUpdate();
    }

    protected override Status OnUpdate()
    {
        if(_hasStartedAttack)
        {
            if(_attacker.IsAttacking) return Status.Running;

            return _attacker.WasLastAttackInterrupted ? Status.Failure : Status.Success;
        }

        Vector3 toTarget = Target.Value.position - GameObject.transform.position;
        toTarget.y = 0f;

        if(toTarget.sqrMagnitude > _setting.AttackRange * _setting.AttackRange)
        {
            _movement.MoveTo(Target.Value.position, _movement.MoveSpeed);
            return Status.Running;
        }

        _movement.Stop();

        if(!_attacker.TryStartAttack(Target.Value)) return Status.Failure;

        _hasStartedAttack = true;
        return Status.Running;
    }

    // 다가가는 중에 끝나면 이동을 멈춘다. 공격 중 끊기는 경우는 적 공격 기능이 경직 · 그로기 · 처치 알림으로 정리한다.
    protected override void OnEnd()
    {
        if(_movement != null) _movement.Stop();
    }
}

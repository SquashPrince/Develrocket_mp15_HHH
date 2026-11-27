using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

// [행동 그래프 행동] 배틀 매니저에게 공격 권한을 받아 공격 사거리까지 다가간 뒤 공격 하나를 한다.
// 권한이 없으면 유지 거리에서 대상을 바라보며 기다린다. 다가가는 시간이 상한을 넘으면 사거리 밖 공격(추격 공격)을 하고, 없으면 실패한다.
// 공격이 경직 · 그로기 · 처치로 끊기면 실패한다. 어느 경우든 노드가 끝나면 권한을 반납한다.
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Attack Target",
    description: "공격 권한을 받아 공격 사거리까지 다가가 공격 하나를 한다.",
    story: "Attack [Target]",
    category: "Enemy",
    id: "461c140b1d764dc49f3532efb3071cf4")]
public partial class AttackTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<Transform> Target;

    private EnemyController _controller;
    private EnemyMovement _movement;
    private EnemyData _data;
    private EnemyAttacker _attacker;
    private EnemyClock _clock;
    // 권한을 받은 뒤 다가간 시간 (적 전용 시간). 기다린 시간은 넣지 않는다.
    private float _approachTime;
    private bool _hasAttackRight;
    private bool _hasStartedAttack;

    protected override Status OnStart()
    {
        if(Target.Value == null) return Status.Failure;

        _controller = GameObject.GetComponent<EnemyController>();
        _movement = GameObject.GetComponent<EnemyMovement>();
        _data = _controller.Data;
        _attacker = GameObject.GetComponent<EnemyAttacker>();
        _clock = _controller.State.Clock;
        _approachTime = 0f;
        _hasAttackRight = false;
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

        // 다가가는 도중 대상이 사라지면(씬 전환 등) 실패한다. 시작한 공격은 공격 기능이 저장한 대상으로 끝까지 진행한다.
        if(Target.Value == null) return Status.Failure;

        if(!_hasAttackRight) _hasAttackRight = _controller.Battle.TryAcquireAttack(_controller);

        if(!_hasAttackRight)
        {
            WaitForAttackRight();
            return Status.Running;
        }

        Vector3 toTarget = HorizontalVector.Between(GameObject.transform.position, Target.Value.position);

        if(toTarget.sqrMagnitude > _data.AttackRange * _data.AttackRange) return Approach();

        _movement.Stop();
        return StartAttack(false);
    }

    // 다가가는 중에 끝나면 이동을 멈춘다. 공격 중 끊기는 경우는 적 공격 기능이 경직 · 그로기 · 처치 알림으로 정리한다.
    // 성공 · 실패 · 중단(Abort) 모두 여기를 지나므로 권한 반납을 한 곳에서 한다.
    protected override void OnEnd()
    {
        if(_movement != null) _movement.Stop();
        if(_controller != null) _controller.Battle.ReleaseAttack(_controller);
    }

    // 상한이 0 이면 끝까지 다가간다. 넘기면 멈춰 서서 사거리 밖 공격을 고르고, 고를 공격이 없으면 실패해 권한을 내놓는다.
    private Status Approach()
    {
        _approachTime += _clock.DeltaTime;

        if(_data.ApproachTimeLimit > 0f && _approachTime >= _data.ApproachTimeLimit)
        {
            _movement.Stop();
            return StartAttack(true);
        }

        _movement.MoveTo(Target.Value.position, _movement.MoveSpeed * _data.RunSpeedRate);
        return Status.Running;
    }

    // rangedOnly : 사거리 밖에서 쓸 수 있는 공격만 고른다.
    private Status StartAttack(bool rangedOnly)
    {
        if(!_attacker.TryStartAttack(Target.Value, rangedOnly)) return Status.Failure;

        _hasStartedAttack = true;
        return Status.Running;
    }

    // 지금 방향 그대로 유지 거리 원 위의 지점으로 가며 대상을 바라본다. 거리가 맞으면 제자리에서 몸만 돌린다.
    // 허용 범위보다 많이 멀어지면 추적 속도로, 그 밖에는 도는 속도로 거리를 맞춘다. (거리 유지 노드의 좌 · 우 돌기와 같은 기준)
    private void WaitForAttackRight()
    {
        Vector3 targetPosition = Target.Value.position;
        Vector3 fromTarget = HorizontalVector.Between(targetPosition, GameObject.transform.position);
        Vector3 direction = fromTarget == Vector3.zero ? Target.Value.forward : fromTarget.normalized;
        bool isTooFar = fromTarget.magnitude > _data.KeepDistance + _data.KeepDistanceTolerance;
        float speed = isTooFar ? _movement.MoveSpeed : _data.StrafeSpeed;

        _movement.MoveTo(targetPosition + direction * _data.KeepDistance, speed, Target.Value);
    }
}

using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

// [행동 그래프 행동] 대상에게 유지 거리까지 다가간다.
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Chase Target",
    description: "대상에게 유지 거리까지 다가간다.",
    story: "Chase [Target]",
    category: "Enemy",
    id: "7bfbc232858d471fb971b0daa21f03df")]
public partial class ChaseTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<Transform> Target;

    private EnemyMovement _movement;
    private EnemyData _data;

    protected override Status OnStart()
    {
        if(Target.Value == null) return Status.Failure;

        _movement = GameObject.GetComponent<EnemyMovement>();
        _data = GameObject.GetComponent<EnemyController>().Data;

        return OnUpdate();
    }

    protected override Status OnUpdate()
    {
        // 실행 중 대상이 사라지면(씬 전환 등) 실패한다.
        if(Target.Value == null) return Status.Failure;

        Vector3 toTarget = HorizontalVector.Between(GameObject.transform.position, Target.Value.position);

        if(toTarget.sqrMagnitude <= _data.KeepDistance * _data.KeepDistance) return Status.Success;

        _movement.MoveTo(Target.Value.position, _movement.MoveSpeed * _data.RunSpeedRate);
        return Status.Running;
    }

    protected override void OnEnd()
    {
        if(_movement != null) _movement.Stop();
    }
}

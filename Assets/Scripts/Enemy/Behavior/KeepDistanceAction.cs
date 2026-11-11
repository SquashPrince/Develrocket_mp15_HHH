using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Random = UnityEngine.Random;

// [행동 그래프 행동] 정해진 시간 동안 대상을 바라보며 대상 둘레의 유지 거리 위를 돈다. 시간이 지나면 멈추고 성공한다.
// 목적지는 대상을 중심으로 유지 거리만큼 떨어진 원 위에서, 지금 위치보다 도는 방향으로 조금 앞선 지점이다.
// 그래서 거리가 맞으면 옆으로 돌고, 멀어지거나 가까워지면 원 쪽으로 다가가거나 물러나며 거리를 맞춘다.
// 유지 거리보다 허용 범위 이상 멀어지면 추적 속도로, 그 밖에는 도는 속도로 움직인다.
// 도는 방향(왼쪽 · 오른쪽)은 시작할 때마다 무작위로 정한다. 대상이 없으면 실패한다.
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Keep Distance",
    description: "정해진 시간 동안 대상과 거리를 유지하며 둘레를 돈다.",
    story: "Keep distance from [Target]",
    category: "Enemy",
    id: "3ca178fc3d414765856c9e041f198c94")]
public partial class KeepDistanceAction : Action
{
    // 목적지를 지금 위치보다 도는 방향으로 이 각도만큼 앞선 지점으로 잡는다.
    private const float STRAFE_LEAD_ANGLE = 30f;

    [SerializeReference] public BlackboardVariable<Transform> Target;

    private EnemyMovement _movement;
    private EnemyBehaviorSetting _setting;
    private float _timer;
    // 1 이면 위에서 볼 때 시계 방향, -1 이면 반시계 방향으로 돈다.
    private float _strafeSign;

    protected override Status OnStart()
    {
        if(Target.Value == null) return Status.Failure;

        _movement = GameObject.GetComponent<EnemyMovement>();
        _setting = GameObject.GetComponent<EnemyBehaviorSetting>();
        _timer = _setting.PickKeepDistanceTime();
        _strafeSign = Random.value < 0.5f ? 1f : -1f;

        return OnUpdate();
    }

    protected override Status OnUpdate()
    {
        _timer -= EnemyTime.DeltaTime;

        if(_timer <= 0f) return Status.Success;

        Vector3 targetPosition = Target.Value.position;
        Vector3 fromTarget = GameObject.transform.position - targetPosition;
        fromTarget.y = 0f;

        // 대상과 위치가 겹치면 방향을 정할 수 없으므로 대상 앞쪽을 기준으로 삼는다.
        if(fromTarget == Vector3.zero) fromTarget = Target.Value.forward;

        Vector3 circleDirection = Quaternion.Euler(0f, STRAFE_LEAD_ANGLE * _strafeSign, 0f) * fromTarget.normalized;
        Vector3 destination = targetPosition + circleDirection * _setting.KeepDistance;
        bool isTooFar = fromTarget.magnitude > _setting.KeepDistance + _setting.KeepDistanceTolerance;
        float speed = isTooFar ? _movement.MoveSpeed : _setting.StrafeSpeed;

        _movement.MoveTo(destination, speed, Target.Value);
        return Status.Running;
    }

    // 성공 · 실패 · 중단 어느 경우든 이동을 멈춘다.
    protected override void OnEnd()
    {
        if(_movement != null) _movement.Stop();
    }
}

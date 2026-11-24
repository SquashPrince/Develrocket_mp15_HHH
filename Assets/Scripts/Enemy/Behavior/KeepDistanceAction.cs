using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Random = UnityEngine.Random;

// [행동 그래프 행동] 대상과 거리를 유지한다. 시작할 때 확률을 한 번 뽑아 백스텝 · 제자리 대기 · 좌 · 우 돌기 중 하나를 한다.
//   뽑은 값이 백스텝 확률 안이면 백스텝, 그다음 대기 확률 구간이면 제자리 대기, 나머지는 좌 · 우 돌기다.
// 백스텝 : 대상을 바라본 채 BackStep 모션을 재생하며 대상 반대쪽으로 물러난다. 모션이 끝나면 멈추고 성공한다.
//          Animator 에 BackStep 상태가 없으면 좌 · 우 돌기로 대신한다.
// 제자리 대기 : 거리 유지 시간 동안 움직이지 않고 대상 쪽으로 몸만 돌린다. 그동안 대상이 다가오거나 멀어져도 거리를 맞추지 않는다.
// 좌 · 우 돌기 : 정해진 시간 동안 대상을 바라보며 대상 둘레의 유지 거리 위를 돈다. 시간이 지나면 멈추고 성공한다.
//   목적지는 대상을 중심으로 유지 거리만큼 떨어진 원 위에서, 지금 위치보다 도는 방향으로 조금 앞선 지점이다.
//   그래서 거리가 맞으면 옆으로 돌고, 멀어지거나 가까워지면 원 쪽으로 다가가거나 물러나며 거리를 맞춘다.
//   유지 거리보다 허용 범위 이상 멀어지면 추적 속도로, 그 밖에는 도는 속도로 움직인다.
//   도는 방향(왼쪽 · 오른쪽)은 시작할 때마다 무작위로 정한다.
// 대상이 없으면 실패한다.
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Keep Distance",
    description: "대상과 거리를 유지한다. 확률에 따라 백스텝하거나, 정해진 시간 동안 제자리에서 기다리거나 둘레를 돈다.",
    story: "Keep distance from [Target]",
    category: "Enemy",
    id: "3ca178fc3d414765856c9e041f198c94")]
public partial class KeepDistanceAction : Action
{
    private enum Mode
    {
        Strafe,
        BackStep,
        Idle
    }

    // 목적지를 지금 위치보다 도는 방향으로 이 각도만큼 앞선 지점으로 잡는다.
    private const float STRAFE_LEAD_ANGLE = 30f;
    // 백스텝 목적지를 지금 위치에서 이 거리만큼 뒤로 잡는다. 실제로 물러나는 거리는 속도 × 모션 길이가 정한다.
    private const float BACK_STEP_LOOK_AHEAD = 6f;

    [SerializeReference] public BlackboardVariable<Transform> Target;

    private EnemyMovement _movement;
    private EnemyAnimation _animation;
    private EnemyData _data;
    private EnemyClock _clock;
    private float _timer;
    // 1 이면 위에서 볼 때 시계 방향, -1 이면 반시계 방향으로 돈다.
    private float _strafeSign;
    private Mode _mode;

    protected override Status OnStart()
    {
        if(Target.Value == null) return Status.Failure;

        _movement = GameObject.GetComponent<EnemyMovement>();
        _animation = GameObject.GetComponent<EnemyAnimation>();
        _data = GameObject.GetComponent<EnemyController>().Data;
        _clock = GameObject.GetComponent<EnemyState>().Clock;
        _timer = _data.PickKeepDistanceTime();
        _strafeSign = Random.value < 0.5f ? 1f : -1f;
        _mode = PickMode();

        return OnUpdate();
    }

    protected override Status OnUpdate()
    {
        // 실행 중 대상이 사라지면(씬 전환 등) 실패한다.
        if(Target.Value == null) return Status.Failure;

        switch(_mode)
        {
            case Mode.BackStep: return UpdateBackStep();
            case Mode.Idle: return UpdateIdle();
            default: return UpdateStrafe();
        }
    }

    // 성공 · 실패 · 중단 어느 경우든 이동을 멈춘다.
    protected override void OnEnd()
    {
        if(_movement != null) _movement.Stop();
    }

    // 확률을 한 번 뽑아 [0, 백스텝 확률) 은 백스텝, [백스텝 확률, 백스텝 + 대기 확률) 은 제자리 대기, 나머지는 좌 · 우 돌기로 정한다.
    // 백스텝 구간이 뽑혔지만 백스텝 모션을 재생할 수 없으면 좌 · 우 돌기로 대신한다.
    private Mode PickMode()
    {
        float roll = Random.value;

        if(roll < _data.BackStepChance) return _animation.TryPlayBackStep() ? Mode.BackStep : Mode.Strafe;
        if(roll < _data.BackStepChance + _data.IdleChance) return Mode.Idle;

        return Mode.Strafe;
    }

    // 속도 0 으로 지금 위치를 목적지로 넘겨, 움직이지 않고 대상 쪽으로 몸만 돌린다. 방향 모션은 (0, 0) 이라 제자리 모션이 나온다.
    private Status UpdateIdle()
    {
        _timer -= _clock.DeltaTime;

        if(_timer <= 0f) return Status.Success;

        _movement.MoveTo(GameObject.transform.position, 0f, Target.Value);
        return Status.Running;
    }

    private Status UpdateBackStep()
    {
        if(_animation.IsBackStepFinished()) return Status.Success;

        Vector3 awayDirection = GetDirectionFromTarget();
        Vector3 destination = GameObject.transform.position + awayDirection * BACK_STEP_LOOK_AHEAD;
        float speed = _movement.MoveSpeed * _data.BackStepSpeedRate;

        _movement.MoveTo(destination, speed, Target.Value, false);
        return Status.Running;
    }

    private Status UpdateStrafe()
    {
        _timer -= _clock.DeltaTime;

        if(_timer <= 0f) return Status.Success;

        Vector3 targetPosition = Target.Value.position;
        Vector3 fromTarget = HorizontalVector.Between(targetPosition, GameObject.transform.position);

        Vector3 circleDirection = Quaternion.Euler(0f, STRAFE_LEAD_ANGLE * _strafeSign, 0f) * GetDirectionFromTarget();
        Vector3 destination = targetPosition + circleDirection * _data.KeepDistance;
        bool isTooFar = fromTarget.magnitude > _data.KeepDistance + _data.KeepDistanceTolerance;
        float speed = isTooFar ? _movement.MoveSpeed : _data.StrafeSpeed;

        _movement.MoveTo(destination, speed, Target.Value);
        return Status.Running;
    }

    // 대상에서 이 적 쪽으로 향하는 수평 방향. 대상과 위치가 겹치면 방향을 정할 수 없으므로 대상 앞쪽을 기준으로 삼는다.
    private Vector3 GetDirectionFromTarget()
    {
        Vector3 fromTarget = HorizontalVector.Between(Target.Value.position, GameObject.transform.position);

        return fromTarget == Vector3.zero ? Target.Value.forward : fromTarget.normalized;
    }
}

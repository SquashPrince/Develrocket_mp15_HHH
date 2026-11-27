using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 적 이동. NavMeshAgent 는 경로 계산에만 쓰고 이동 · 회전은 직접 한다.
// Agent 가 스스로 움직이면 가속도 설정의 영향을 받고 적 전용 시간(슬로모션 · 필살기 연출)을 따르지 않기 때문이다.
// 적끼리 피하는 기능(Agent 회피)은 쓰지 않고, 한 걸음 옮기기 전에 다른 적의 몸 반지름과 겹치지 않게 직접 밀어낸다.
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyState))]
[RequireComponent(typeof(EnemyAnimation))]
public class EnemyMovement : MonoBehaviour
{
    // 이보다 덜 움직였으면 제자리로 보고 방향 모션을 (0, 0) 으로 둔다.
    private const float MIN_MOVED_DISTANCE = 0.0001f;

    private EnemyController _controller;
    private EnemyData _data;
    private NavMeshAgent _agent;
    private EnemyState _state;
    private EnemyAnimation _animation;
    // 없으면 이동 방향을 본다.
    private Transform _lookTarget;
    private Vector3 _destination;
    private float _speed;
    private bool _isMoving;

    public float MoveSpeed => _data.MoveSpeed;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update() => UpdateMove();

    // 매 프레임 불러도 목적지가 조금 바뀐 정도면 경로를 다시 계산하지 않는다.
    // lookTarget : 넘기면 바라보는 방향과 이동 방향이 달라지므로 방향 모션(Move)을 재생한다.
    // playsMoveAnimation : 백스텝처럼 다른 기능이 모션을 정할 때 false
    public void MoveTo(Vector3 destination, float speed, Transform lookTarget = null, bool playsMoveAnimation = true)
    {
        _speed = speed;
        _lookTarget = lookTarget;

        if(!_isMoving || (destination - _destination).sqrMagnitude > _data.RepathDistance * _data.RepathDistance)
        {
            _destination = destination;
            _agent.SetDestination(destination);
        }

        if(_isMoving) return;

        _isMoving = true;

        if(playsMoveAnimation) _animation.SetMoving(true, lookTarget != null);
    }

    // playsIdle : 공격 중 대상 따라가기처럼 다른 기능이 모션을 재생하는 중이면 false. 대기 모션으로 바꾸면 공격 모션이 끊겨 공격이 끝나지 않는다.
    public void Stop(bool playsIdle = true)
    {
        if(!_isMoving) return;

        _isMoving = false;

        // 에디터 정지 · 씬 전환으로 적이 사라질 때 Agent 가 먼저 꺼진 뒤 행동 그래프가 끝나며 부를 수 있다. 꺼진 Agent 는 경로를 지울 수 없다.
        if(_agent.isOnNavMesh) _agent.ResetPath();

        if(playsIdle) _animation.SetMoving(false);
    }

    private void UpdateMove()
    {
        if(!_isMoving || _agent.pathPending) return;
        // 행동 그래프가 중단되기 전 프레임에도 움직이지 않게 막는다.
        if(!_state.IsAlive || _state.IsRestricted) return;

        // 꺾이는 지점을 지나치지 않게 남은 거리보다 멀리 가지 않는다.
        Vector3 toCorner = HorizontalVector.Between(transform.position, _agent.steeringTarget);

        float step = Mathf.Min(_speed * _state.Clock.DeltaTime, toCorner.magnitude);
        Vector3 moveDirection = toCorner.normalized;

        Vector3 previousPosition = transform.position;

        // Agent 의 위치를 먼저 옮기면 NavMesh 위로 맞춰지므로, 맞춰진 위치를 실제 위치로 쓴다.
        _agent.nextPosition = KeepApartFromEnemies(previousPosition + moveDirection * step);
        transform.position = _agent.nextPosition;

        // 방향 모션용. 다른 적에 밀려 옆으로 미끄러질 수 있어 실제로 움직인 방향을 쓴다. 움직이지 않았으면 (0, 0) 이라 제자리 모션이 나온다.
        Vector3 moved = HorizontalVector.Between(previousPosition, transform.position);
        Vector3 localDirection = moved.sqrMagnitude > MIN_MOVED_DISTANCE * MIN_MOVED_DISTANCE ? transform.InverseTransformDirection(moved.normalized) : Vector3.zero;
        _animation.SetMoveDirection(localDirection);

        Vector3 lookDirection = _lookTarget != null ? _lookTarget.position - transform.position : moveDirection;
        TurnTowards(lookDirection, _data.TurnSpeed);
    }

    // 높이는 무시하고 적 전용 시간으로 돈다. 이동 중에는 이동 기능이, 공격 중에는 공격 기능이 공격별 회전 속도로 부른다.
    // turnSpeed : 초당 회전 각도
    public void TurnTowards(Vector3 direction, float turnSpeed)
    {
        direction = HorizontalVector.Flatten(direction);

        if(direction == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * _state.Clock.DeltaTime);
    }

    // 다른 적의 몸에 들어간 만큼 수평으로 밀어낸다. 다가가던 방향의 겹친 성분만 빠지므로 다른 적을 옆으로 미끄러지며 돌아간다.
    // 서 있는 적은 밀지 않고 움직이는 쪽만 비켜 간다. 시체 · 꺼진 적은 막지 않는다. 완전히 같은 자리면 밀 방향이 없어 그대로 둔다.
    private Vector3 KeepApartFromEnemies(Vector3 position)
    {
        IReadOnlyList<EnemyController> enemies = _controller.Battle.Enemies;

        for(int i = 0; i < enemies.Count; i++)
        {
            EnemyController other = enemies[i];

            if(other == _controller || !other.IsAlive || !other.gameObject.activeInHierarchy) continue;

            Vector3 otherPosition = other.transform.position;
            Vector3 fromOther = HorizontalVector.Between(otherPosition, position);
            float minDistance = _controller.Radius + other.Radius;

            if(fromOther == Vector3.zero || fromOther.sqrMagnitude >= minDistance * minDistance) continue;

            Vector3 pushed = otherPosition + fromOther.normalized * minDistance;
            position.x = pushed.x;
            position.z = pushed.z;
        }

        return position;
    }

    private void CacheComponent()
    {
        _controller = GetComponent<EnemyController>();
        _data = _controller.Data;
        _agent = GetComponent<NavMeshAgent>();
        _state = GetComponent<EnemyState>();
        _animation = GetComponent<EnemyAnimation>();
    }

    // Agent 가 위치 · 회전을 직접 바꾸지 않게 하고 경로 계산에만 쓴다.
    private void Init()
    {
        _agent.updatePosition = false;
        _agent.updateRotation = false;
    }
}

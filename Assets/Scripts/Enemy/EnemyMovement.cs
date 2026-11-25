using UnityEngine;
using UnityEngine.AI;

// 적 이동. NavMeshAgent 는 경로 계산에만 쓰고 이동 · 회전은 직접 한다.
// Agent 가 스스로 움직이면 가속도 설정의 영향을 받고 적 전용 시간(슬로모션 · 필살기 연출)을 따르지 않기 때문이다.
// 적끼리 피하는 기능(Agent 회피)은 쓰지 않는다.
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyState))]
[RequireComponent(typeof(EnemyAnimation))]
public class EnemyMovement : MonoBehaviour
{
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

        // Agent 의 위치를 먼저 옮기면 NavMesh 위로 맞춰지므로, 맞춰진 위치를 실제 위치로 쓴다.
        _agent.nextPosition = transform.position + moveDirection * step;
        transform.position = _agent.nextPosition;

        // 방향 모션용. 움직이지 않았으면 (0, 0) 이라 제자리 모션이 나온다.
        Vector3 localDirection = step > 0f ? transform.InverseTransformDirection(moveDirection) : Vector3.zero;
        _animation.SetMoveDirection(localDirection);

        Vector3 lookDirection = _lookTarget != null ? _lookTarget.position - transform.position : moveDirection;
        TurnTowards(lookDirection);
    }

    private void TurnTowards(Vector3 direction)
    {
        direction = HorizontalVector.Flatten(direction);

        if(direction == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _data.TurnSpeed * _state.Clock.DeltaTime);
    }

    private void CacheComponent()
    {
        _data = GetComponent<EnemyController>().Data;
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

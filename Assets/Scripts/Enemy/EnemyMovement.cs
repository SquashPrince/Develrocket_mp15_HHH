using UnityEngine;
using UnityEngine.AI;

// 적 이동. NavMeshAgent 는 경로 계산에만 쓰고, 실제 이동 · 회전은 이 기능이 직접 한다.
// Agent 가 스스로 움직이면 가속도 설정의 영향을 받고 적 전용 시간(슬로모션 · 필살기 연출)을 따르지 않기 때문이다.
// 매 프레임 경로의 다음 꺾이는 지점 쪽으로 정해진 속도 × 적 전용 시간만큼 이동한다. 가속 없이 바로 그 속도로 움직인다.
// 경직 · 그로기 · 처치 중에는 이동을 멈춘다. 행동 그래프가 중단되기 전 프레임에도 움직이지 않게 하기 위해서다.
// 적끼리 피하는 기능(Agent 회피)은 쓰지 않는다. 여러 마리가 나오는 웨이브 단계에서 다시 검토한다.
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth), typeof(EnemyReaction))]
[RequireComponent(typeof(EnemyAnimation))]
public class EnemyMovement : MonoBehaviour
{
    [Tooltip("추적할 때의 이동 속도(초당 거리)")]
    [SerializeField, Min(0f)] private float _moveSpeed = 3f;
    [Tooltip("초당 회전 각도")]
    [SerializeField, Min(0f)] private float _turnSpeed = 540f;
    [Tooltip("목적지가 이 거리보다 적게 바뀌면 경로를 다시 계산하지 않는다")]
    [SerializeField, Min(0f)] private float _repathDistance = 0.3f;

    private NavMeshAgent _agent;
    private EnemyHealth _health;
    private EnemyReaction _reaction;
    private EnemyAnimation _animation;
    // 이동하는 동안 바라볼 대상. 없으면 이동 방향을 본다.
    private Transform _lookTarget;
    private Vector3 _destination;
    private float _speed;
    private bool _isMoving;

    public float MoveSpeed => _moveSpeed;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update() => UpdateMove();

    // destination 쪽으로 speed 로 이동한다. 매 프레임 불러도 목적지가 조금 바뀐 정도면 경로를 다시 계산하지 않는다.
    // lookTarget : 이동하는 동안 바라볼 대상. 대상 둘레를 돌 때처럼 옆으로 움직이며 대상을 봐야 할 때 넘긴다.
    public void MoveTo(Vector3 destination, float speed, Transform lookTarget = null)
    {
        _speed = speed;
        _lookTarget = lookTarget;

        if(!_isMoving || (destination - _destination).sqrMagnitude > _repathDistance * _repathDistance)
        {
            _destination = destination;
            _agent.SetDestination(destination);
        }

        if(_isMoving) return;

        _isMoving = true;
        _animation.SetMoving(true);
    }

    public void Stop()
    {
        if(!_isMoving) return;

        _isMoving = false;
        _agent.ResetPath();
        _animation.SetMoving(false);
    }

    private void UpdateMove()
    {
        if(!_isMoving || _agent.pathPending) return;
        if(!_health.IsAlive || _reaction.IsRestricted) return;

        // 경로의 다음 꺾이는 지점 쪽으로 이번 프레임 이동 거리만큼 간다. 지점을 지나치지 않게 남은 거리보다 멀리 가지 않는다.
        Vector3 toCorner = _agent.steeringTarget - transform.position;
        toCorner.y = 0f;

        float step = Mathf.Min(_speed * EnemyTime.DeltaTime, toCorner.magnitude);
        Vector3 moveDirection = toCorner.normalized;

        // Agent 의 위치를 먼저 옮기면 NavMesh 위로 맞춰지므로, 맞춰진 위치를 실제 위치로 쓴다.
        _agent.nextPosition = transform.position + moveDirection * step;
        transform.position = _agent.nextPosition;

        Vector3 lookDirection = _lookTarget != null ? _lookTarget.position - transform.position : moveDirection;
        TurnTowards(lookDirection);
    }

    private void TurnTowards(Vector3 direction)
    {
        direction.y = 0f;

        if(direction == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _turnSpeed * EnemyTime.DeltaTime);
    }

    private void CacheComponent()
    {
        _agent = GetComponent<NavMeshAgent>();
        _health = GetComponent<EnemyHealth>();
        _reaction = GetComponent<EnemyReaction>();
        _animation = GetComponent<EnemyAnimation>();
    }

    // Agent 가 위치 · 회전을 직접 바꾸지 않게 하고 경로 계산에만 쓴다.
    private void Init()
    {
        _agent.updatePosition = false;
        _agent.updateRotation = false;
    }
}

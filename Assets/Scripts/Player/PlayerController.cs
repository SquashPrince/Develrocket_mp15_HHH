using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어의 걷기 · 달리기 · 대시 · 회피(백스텝)를 CharacterController 로 처리한다.
// 이동 방향은 매 프레임 카메라가 보는 수평 방향을 기준으로 한다.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private enum MoveState
    {
        Idle,
        Walk,
        Run,
        Dash,
        Dodge
    }

    private const int MAX_DASH_STACK = 2;
    // 땅에 붙어 있도록 누르는 작은 아래 방향 속도 (0 이면 경사에서 isGrounded 가 흔들린다)
    private const float GROUNDED_VERTICAL_SPEED = -2f;

    [Header("입력")]
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private InputActionReference _dashAction;

    [Header("참조")]
    [Tooltip("이동 방향의 기준이 되는 카메라 (Main Camera)")]
    [SerializeField] private Transform _cameraTransform;

    [Header("이동")]
    [SerializeField, Min(0f)] private float _walkSpeed = 4f;
    [SerializeField, Min(0f)] private float _runSpeed = 7f;
    [Tooltip("목표 방향으로 몸을 돌리는 데 걸리는 시간(초). 이동은 캐릭터가 보는 방향으로 하므로 곡선을 그리며 꺾는다")]
    [SerializeField, Min(0f)] private float _turnTime = 0.3f;
    [Tooltip("이동 입력을 이 시간 이상 유지하면 달리기로 전환")]
    [SerializeField, Min(0f)] private float _autoRunDelay = 1.5f;
    [SerializeField] private float _gravity = -20f;

    [Header("대시 · 회피")]
    [SerializeField, Min(0f)] private float _dashDistance = 4f;
    [SerializeField, Min(0.01f)] private float _dashDuration = 0.2f;
    [SerializeField, Min(0f)] private float _dodgeDistance = 3f;
    [SerializeField, Min(0.01f)] private float _dodgeDuration = 0.2f;
    [Tooltip("대시 스택 1개가 차는 시간")]
    [SerializeField, Min(0f)] private float _dashCooldown = 1f;
    [Tooltip("대시 · 회피 시작 후 다음 대시를 쓸 수 있기까지의 시간. 지속시간보다 길게 설정하면 지속시간을 기준으로 한다")]
    [SerializeField, Min(0f)] private float _dashChainInterval = 0.2f;

    private CharacterController _characterController;
    private MoveState _state;
    private Vector3 _dashDirection;
    private float _dashSpeed;
    private float _dashActionDuration;
    private float _dashActionTimer;
    private float _dashCooldownTimer;
    private float _moveHoldTime;
    private float _verticalSpeed;
    private float _turnVelocity;
    private int _dashStack;

    private bool IsDashing => _state == MoveState.Dash || _state == MoveState.Dodge;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update()
    {
        RechargeDashStack();
        HandleMovement();
    }

    private void HandleMovement()
    {
        Vector3 moveDirection = ReadMoveDirection();

        if(_dashAction.action.WasPressedThisFrame()) TryStartDash(moveDirection);

        Vector3 horizontalVelocity = IsDashing ? UpdateDash(moveDirection) : UpdateMove(moveDirection);

        _characterController.Move((horizontalVelocity + Vector3.up * UpdateVerticalSpeed()) * Time.deltaTime);
    }

    // 입력을 카메라 기준의 월드 수평 방향으로 바꾼다. 스틱을 살짝 기울이면 길이가 1보다 작다.
    // 이동 중 카메라가 캐릭터 뒤로 자동 복귀하면 기준이 바뀌어 원을 그리므로, 이동 중 복귀는 CameraRecenterBlocker 가 막는다.
    private Vector3 ReadMoveDirection()
    {
        Vector2 input = _moveAction.action.ReadValue<Vector2>();
        Vector3 localDirection = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);

        return Quaternion.Euler(0f, _cameraTransform.eulerAngles.y, 0f) * localDirection;
    }

    private Vector3 UpdateMove(Vector3 moveDirection)
    {
        if(moveDirection == Vector3.zero)
        {
            _state = MoveState.Idle;
            _moveHoldTime = 0f;
            return Vector3.zero;
        }

        _moveHoldTime += Time.deltaTime;

        if(_state != MoveState.Run && _moveHoldTime >= _autoRunDelay) _state = MoveState.Run;
        else if(_state == MoveState.Idle) _state = MoveState.Walk;

        TurnTowards(moveDirection);

        float speed = _state == MoveState.Run ? _runSpeed : _walkSpeed;

        // 목표 방향이 아닌 현재 바라보는 방향으로 이동해 곡선으로 꺾는다. 스틱을 덜 기울이면 느려진다.
        return transform.forward * (moveDirection.magnitude * speed);
    }

    private void TurnTowards(Vector3 direction)
    {
        float targetYaw = Quaternion.LookRotation(direction).eulerAngles.y;
        float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref _turnVelocity, _turnTime);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void TryStartDash(Vector3 moveDirection)
    {
        if(_dashStack <= 0) return;

        // 대시 · 회피 중에는 연속 사용 간격이 지나야 다음 대시를 쓸 수 있다.
        if(IsDashing && _dashActionTimer < Mathf.Min(_dashChainInterval, _dashActionDuration)) return;

        if(moveDirection != Vector3.zero)
        {
            // 방향 입력이 있으면 그 방향으로 즉시 돌아서 대시
            _dashDirection = moveDirection.normalized;
            transform.rotation = Quaternion.LookRotation(_dashDirection);
            _turnVelocity = 0f;
            StartDashAction(MoveState.Dash, _dashDistance, _dashDuration);
        }
        else
        {
            // 방향 입력이 없으면 바라보는 방향을 유지한 채 뒤로 회피
            _dashDirection = -transform.forward;
            StartDashAction(MoveState.Dodge, _dodgeDistance, _dodgeDuration);
        }

        _dashStack--;
    }

    private void StartDashAction(MoveState state, float distance, float duration)
    {
        _state = state;
        _dashActionDuration = duration;
        _dashSpeed = distance / duration;
        _dashActionTimer = 0f;
    }

    private Vector3 UpdateDash(Vector3 moveDirection)
    {
        _dashActionTimer += Time.deltaTime;

        if(_dashActionTimer < _dashActionDuration) return _dashDirection * _dashSpeed;

        EndDash(moveDirection);

        return UpdateMove(moveDirection);
    }

    // 끝날 때 이동 입력이 있으면 달리기로 이어가고, 없으면 멈춘다.
    private void EndDash(Vector3 moveDirection)
    {
        _moveHoldTime = 0f;
        _state = moveDirection == Vector3.zero ? MoveState.Idle : MoveState.Run;
    }

    // 스택이 최대치보다 적으면 대시 중에도 쿨타임이 계속 흐른다.
    private void RechargeDashStack()
    {
        if(_dashStack >= MAX_DASH_STACK) return;

        _dashCooldownTimer += Time.deltaTime;

        if(_dashCooldownTimer < _dashCooldown) return;

        _dashCooldownTimer -= _dashCooldown;
        _dashStack++;

        if(_dashStack >= MAX_DASH_STACK) _dashCooldownTimer = 0f;
    }

    private float UpdateVerticalSpeed()
    {
        if(_characterController.isGrounded && _verticalSpeed < 0f) _verticalSpeed = GROUNDED_VERTICAL_SPEED;
        else _verticalSpeed += _gravity * Time.deltaTime;

        return _verticalSpeed;
    }

    private void CacheComponent()
    {
        _characterController = GetComponent<CharacterController>();
    }

    private void Init()
    {
        _state = MoveState.Idle;
        _dashStack = MAX_DASH_STACK;
    }
}

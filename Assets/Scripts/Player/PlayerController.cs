using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어의 공용 데이터(입력 · 수치 · 대시 스택 · 중력)와 상태 전환을 맡는다.
// 실제 동작은 상태 클래스(이동 · 대시)가 처리하고, 이 클래스는 매 프레임 상태를 실행한 뒤 이동을 반영한다.
// 이동 방향은 매 프레임 카메라가 보는 수평 방향을 기준으로 한다.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private static readonly int SPEED_PARAMETER = Animator.StringToHash("Speed");

    private const int MAX_DASH_STACK = 2;
    // 애니메이션을 바꿀 때 이전 모션과 섞는 시간(초)
    private const float ANIMATION_FADE_TIME = 0.1f;
    // 이동 속도 파라미터가 목표값까지 부드럽게 따라가는 시간(초)
    private const float SPEED_DAMP_TIME = 0.1f;
    // 땅에 붙어 있도록 누르는 작은 아래 방향 속도 (0 이면 경사에서 isGrounded 가 흔들린다)
    private const float GROUNDED_VERTICAL_SPEED = -2f;

    [Header("입력")]
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private InputActionReference _dashAction;

    [Header("참조")]
    [Tooltip("이동 방향의 기준이 되는 카메라 (Main Camera)")]
    [SerializeField] private Transform _cameraTransform;
    [Tooltip("캐릭터 모델의 Animator (Visual 아래 캐릭터 모델)")]
    [SerializeField] private Animator _animator;

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
    private PlayerStateMachine _stateMachine;
    private PlayerLocomotionState _locomotionState;
    private PlayerDashState _dashState;
    private float _dashCooldownTimer;
    private float _verticalSpeed;
    private float _turnVelocity;
    private int _dashStack;

    // 상태가 이번 프레임에 정한 수평 속도. 매 프레임 상태 실행 뒤 중력과 함께 반영한다.
    public Vector3 HorizontalVelocity { get; set; }
    public bool WasDashPressed => _dashAction.action.WasPressedThisFrame();
    public float WalkSpeed => _walkSpeed;
    public float RunSpeed => _runSpeed;
    public float AutoRunDelay => _autoRunDelay;
    public float DashChainInterval => _dashChainInterval;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update()
    {
        RechargeDashStack();
        _stateMachine.Tick();
        ApplyMovement();
    }

    // 입력을 카메라 기준의 월드 수평 방향으로 바꾼다. 스틱을 살짝 기울이면 길이가 1보다 작다.
    // 이동 중 카메라가 캐릭터 뒤로 자동 복귀하면 기준이 바뀌어 원을 그리므로, 이동 중 복귀는 CameraRecenterBlocker 가 막는다.
    public Vector3 ReadMoveDirection()
    {
        Vector2 input = _moveAction.action.ReadValue<Vector2>();
        Vector3 localDirection = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);

        return Quaternion.Euler(0f, _cameraTransform.eulerAngles.y, 0f) * localDirection;
    }

    public void TurnTowards(Vector3 direction)
    {
        float targetYaw = Quaternion.LookRotation(direction).eulerAngles.y;
        float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref _turnVelocity, _turnTime);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // 스택이 있으면 대시(방향 입력 있음) 또는 회피(방향 입력 없음)를 시작한다.
    public bool TryStartDash(Vector3 moveDirection)
    {
        if(_dashStack <= 0) return false;

        if(moveDirection != Vector3.zero)
        {
            // 방향 입력이 있으면 그 방향으로 즉시 돌아서 대시
            Vector3 direction = moveDirection.normalized;
            transform.rotation = Quaternion.LookRotation(direction);
            _turnVelocity = 0f;
            _dashState.Setup(direction, _dashDistance, _dashDuration, false);
        }
        else
        {
            // 방향 입력이 없으면 바라보는 방향을 유지한 채 뒤로 회피
            _dashState.Setup(-transform.forward, _dodgeDistance, _dodgeDuration, true);
        }

        _dashStack--;
        _stateMachine.ChangeState(_dashState);

        return true;
    }

    // Animator 의 상태를 이름(해시)으로 직접 재생한다. 전이선 대신 코드가 어떤 모션을 재생할지 정한다.
    public void PlayAnimation(int stateHash)
    {
        _animator.CrossFadeInFixedTime(stateHash, ANIMATION_FADE_TIME);
    }

    // 실제 수평 이동 속도를 넘겨 Blend Tree 가 대기 · 걷기 · 달리기를 섞게 한다.
    public void SetMoveSpeedParameter(float speed)
    {
        _animator.SetFloat(SPEED_PARAMETER, speed, SPEED_DAMP_TIME, Time.deltaTime);
    }

    public void EnterLocomotion(bool startRunning)
    {
        _locomotionState.SetStartRunning(startRunning);
        _stateMachine.ChangeState(_locomotionState);
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

    private void ApplyMovement()
    {
        if(_characterController.isGrounded && _verticalSpeed < 0f) _verticalSpeed = GROUNDED_VERTICAL_SPEED;
        else _verticalSpeed += _gravity * Time.deltaTime;

        _characterController.Move((HorizontalVelocity + Vector3.up * _verticalSpeed) * Time.deltaTime);
    }

    private void CacheComponent()
    {
        _characterController = GetComponent<CharacterController>();
    }

    private void Init()
    {
        _stateMachine = new PlayerStateMachine();
        _locomotionState = new PlayerLocomotionState(this);
        _dashState = new PlayerDashState(this);
        _dashStack = MAX_DASH_STACK;

        EnterLocomotion(false);
    }
}

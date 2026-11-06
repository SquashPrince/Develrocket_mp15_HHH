using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어의 공용 데이터(입력 · 수치 · 대시 스택 · 콤보 · 차지 · 스킬 · 중력)와 상태 전환을 맡는다.
// 실제 동작은 상태 클래스(이동 · 대시 · 공격)가 처리하고, 이 클래스는 매 프레임 상태를 실행한 뒤 이동을 반영한다.
// 이동 방향은 매 프레임 카메라가 보는 수평 방향을 기준으로 한다.
[RequireComponent(typeof(CharacterController), typeof(PlayerEnergy))]
public class PlayerController : MonoBehaviour
{
    private static readonly int SPEED_PARAMETER = Animator.StringToHash("Speed");

    private const int MAX_DASH_STACK = 2;
    // 한 번의 타격 판정에서 검사할 수 있는 최대 콜라이더 수
    private const int MAX_HIT_COLLIDERS = 16;
    // 애니메이션을 바꿀 때 이전 모션과 섞는 시간(초)
    private const float ANIMATION_FADE_TIME = 0.1f;
    // 이동 속도 파라미터가 목표값까지 부드럽게 따라가는 시간(초)
    private const float SPEED_DAMP_TIME = 0.1f;
    // 땅에 붙어 있도록 누르는 작은 아래 방향 속도 (0 이면 경사에서 isGrounded 가 흔들린다)
    private const float GROUNDED_VERTICAL_SPEED = -2f;

    [Header("입력")]
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private InputActionReference _dashAction;
    [SerializeField] private InputActionReference _attackAction;
    [SerializeField] private InputActionReference _skillAction;

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

    [Header("공격")]
    [Tooltip("콤보 단계별 설정. 배열 크기가 콤보 단수이며, Animator 에 \"BasicAttack 1\" 부터 단수만큼 상태가 있어야 한다. 마지막 단계는 마무리 공격으로, 후딜이 끝날 때까지 공격 입력을 받지 않는다")]
    [SerializeField] private PlayerAttackSetting[] _comboSteps = { new PlayerAttackSetting(), new PlayerAttackSetting(), new PlayerAttackSetting(), new PlayerAttackSetting() };
    [SerializeField] private PlayerAttackSetting _heavyAttack = new PlayerAttackSetting();
    [Tooltip("공격 키를 누르고 있으면 이 시간 뒤 강공격이 나간다")]
    [SerializeField, Min(0.01f)] private float _chargeTime = 0.6f;
    [Tooltip("타격 판정 상자의 중심 (캐릭터 기준 위치)")]
    [SerializeField] private Vector3 _hitBoxCenter = new Vector3(0f, 1f, 1f);
    [SerializeField] private Vector3 _hitBoxSize = new Vector3(1.5f, 1.5f, 2f);

    [Header("스킬")]
    [SerializeField] private PlayerAttackSetting _skill = new PlayerAttackSetting();
    [SerializeField] private PlayerAttackSetting _specialSkill = new PlayerAttackSetting();
    [Tooltip("특수스킬에 필요한 에너지. 스킬 입력 시 에너지가 이보다 적으면 일반 스킬이 나간다")]
    [SerializeField, Min(0f)] private float _specialSkillEnergyCost = 60f;

    private readonly HashSet<DummyEnemy> _hitTargets = new HashSet<DummyEnemy>();
    private readonly Collider[] _hitColliders = new Collider[MAX_HIT_COLLIDERS];
    private CharacterController _characterController;
    private PlayerEnergy _energy;
    private PlayerStateMachine _stateMachine;
    private PlayerLocomotionState _locomotionState;
    private PlayerDashState _dashState;
    private PlayerAttackState _attackState;
    private float _dashCooldownTimer;
    private float _verticalSpeed;
    private float _turnVelocity;
    private float _comboExpireTime;
    private float _chargeTimer;
    private int _dashStack;
    private int _nextComboStep;
    private bool _isCharging;

    // 상태가 이번 프레임에 정한 수평 속도. 매 프레임 상태 실행 뒤 중력과 함께 반영한다.
    public Vector3 HorizontalVelocity { get; set; }
    public bool WasDashPressed => _dashAction.action.WasPressedThisFrame();
    public bool WasAttackPressed => _attackAction.action.WasPressedThisFrame();
    public bool WasSkillPressed => _skillAction.action.WasPressedThisFrame();
    public int ComboStepCount => _comboSteps.Length;
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
    private void OnDrawGizmosSelected() => DrawHitBox();

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

    // 스택이 있으면 대시(방향 입력 있음) 또는 회피(방향 입력 없음)를 시작한다. 진행 중이던 차지는 끊는다.
    public bool TryStartDash(Vector3 moveDirection)
    {
        if(_dashStack <= 0) return false;

        if(moveDirection != Vector3.zero)
        {
            // 방향 입력이 있으면 그 방향으로 즉시 돌아서 대시
            FaceImmediately(moveDirection);
            _dashState.Setup(transform.forward, _dashDistance, _dashDuration, false);
        }
        else
        {
            // 방향 입력이 없으면 바라보는 방향을 유지한 채 뒤로 회피
            _dashState.Setup(-transform.forward, _dodgeDistance, _dodgeDuration, true);
        }

        _dashStack--;
        _isCharging = false;
        _stateMachine.ChangeState(_dashState);

        return true;
    }

    // 입력 버퍼 시간 안이면 다음 콤보 단계, 지났으면 1단부터 시작한다. 방향을 바꾸지 않고 바라보는 방향으로 공격한다.
    public void StartBasicAttack()
    {
        int step = Time.time <= _comboExpireTime ? _nextComboStep : 0;

        _comboExpireTime = 0f;
        _attackState.Setup(PlayerAttackState.AttackType.Basic, step, _comboSteps[step]);
        _stateMachine.ChangeState(_attackState);
    }

    // 공격 동작이 끝난 시점에 다음 단계와, 다음 단계로 이어갈 수 있는 마감 시각을 정한다. 마지막 단계 다음은 1단이다.
    public void FinishComboStep(int step)
    {
        _nextComboStep = (step + 1) % _comboSteps.Length;
        _comboExpireTime = Time.time + _comboSteps[step].InputBuffer;
    }

    // 에너지가 특수스킬 요구량 이상이면 소모하고 특수스킬, 모자라면 일반 스킬을 시작한다.
    // 진행 중이던 차지는 끊고, 스킬 뒤의 콤보는 1단부터 다시 시작한다.
    public void StartSkill()
    {
        bool isSpecial = _energy.TryConsume(_specialSkillEnergyCost);

#if UNITY_EDITOR
        if(isSpecial) Debug.Log($"[특수스킬] 에너지 -{_specialSkillEnergyCost} → {_energy.CurrentEnergy:F1}");
#endif

        _isCharging = false;
        _comboExpireTime = 0f;

        if(isSpecial) _attackState.Setup(PlayerAttackState.AttackType.SpecialSkill, 0, _specialSkill);
        else _attackState.Setup(PlayerAttackState.AttackType.Skill, 0, _skill);

        _stateMachine.ChangeState(_attackState);
    }

    public void BeginCharge()
    {
        _isCharging = true;
        _chargeTimer = 0f;
    }

    // 공격 키를 누르고 있는 동안 차지 시간을 재고, 차지 시간에 도달하면 진행 중인 공격을 끊고 강공격을 시작한다.
    // 강공격을 시작했으면 true 를 돌려준다.
    public bool UpdateCharge()
    {
        if(!_isCharging) return false;

        if(!_attackAction.action.IsPressed())
        {
            _isCharging = false;
            return false;
        }

        _chargeTimer += Time.deltaTime;

        if(_chargeTimer < _chargeTime) return false;

        StartHeavyAttack();
        return true;
    }

    // 캐릭터 앞의 판정 상자 안에 있는 적에게 한 번씩 적중시키고, 적중한 적마다 공격 설정의 에너지를 얻는다.
    public void PerformHit(PlayerAttackState.AttackType type, PlayerAttackSetting setting)
    {
        Vector3 center = transform.TransformPoint(_hitBoxCenter);
        int count = Physics.OverlapBoxNonAlloc(center, _hitBoxSize * 0.5f, _hitColliders, transform.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);

        // 콜라이더가 여러 개인 적도 한 번만 맞도록 이번 판정에서 맞은 적을 기억한다.
        _hitTargets.Clear();

        for(int i = 0; i < count; i++)
        {
            DummyEnemy enemy = _hitColliders[i].GetComponentInParent<DummyEnemy>();

            if(enemy == null || !_hitTargets.Add(enemy)) continue;

            enemy.TakeHit();
            _energy.Add(setting.EnergyGain);

#if UNITY_EDITOR
            Debug.Log($"[적중] {type} 위력 {setting.Power} / 에너지 +{setting.EnergyGain} → {_energy.CurrentEnergy:F1}");
#endif
        }
    }

    public void HandleAnimationEvent(PlayerAnimationEventType type, int stateHash)
    {
        _stateMachine.CurrentState.OnAnimationEvent(type, stateHash);
    }

    // Animator 의 상태를 이름(해시)으로 직접 재생한다. 전이선 대신 코드가 어떤 모션을 재생할지 정한다.
    // fadeTime : 이전 모션과 섞는 시간(초). 공격은 공격 설정의 값을 넘기고, 그 외에는 기본값을 쓴다.
    public void PlayAnimation(int stateHash, float fadeTime = ANIMATION_FADE_TIME)
    {
        _animator.CrossFadeInFixedTime(stateHash, fadeTime);
    }

    // 실제 수평 이동 속도를 넘겨 Blend Tree 가 걷기 · 달리기를 섞게 한다.
    public void SetMoveSpeedParameter(float speed)
    {
        _animator.SetFloat(SPEED_PARAMETER, speed, SPEED_DAMP_TIME, Time.deltaTime);
    }

    // 이동 상태로 돌아가면 진행 중이던 차지는 끊는다.
    public void EnterLocomotion(bool startRunning)
    {
        _isCharging = false;
        _locomotionState.SetStartRunning(startRunning);
        _stateMachine.ChangeState(_locomotionState);
    }

    // 강공격 뒤에는 콤보가 1단부터 다시 시작한다.
    private void StartHeavyAttack()
    {
        _isCharging = false;
        _comboExpireTime = 0f;
        _attackState.Setup(PlayerAttackState.AttackType.Heavy, 0, _heavyAttack);
        _stateMachine.ChangeState(_attackState);
    }

    private void FaceImmediately(Vector3 direction)
    {
        if(direction == Vector3.zero) return;

        transform.rotation = Quaternion.LookRotation(direction.normalized);
        _turnVelocity = 0f;
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

    // Scene 창에서 플레이어를 선택하면 타격 판정 범위를 빨간 상자로 보여준다.
    private void DrawHitBox()
    {
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(_hitBoxCenter), transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, _hitBoxSize);
    }

    private void CacheComponent()
    {
        _characterController = GetComponent<CharacterController>();
        _energy = GetComponent<PlayerEnergy>();
    }

    private void Init()
    {
        _stateMachine = new PlayerStateMachine();
        _locomotionState = new PlayerLocomotionState(this);
        _dashState = new PlayerDashState(this);
        _attackState = new PlayerAttackState(this);
        _dashStack = MAX_DASH_STACK;

        EnterLocomotion(false);
    }
}

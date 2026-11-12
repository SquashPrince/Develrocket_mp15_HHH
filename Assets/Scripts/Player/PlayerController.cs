using UnityEngine;

// 플레이어의 공용 데이터(수치 · 대시 스택 · 콤보 · 차지 · 스킬 · 중력)와 상태 전환을 맡는다.
// 입력은 입력 기능, 타격 판정은 판정 기능, Animator 제어는 애니메이션 기능이 맡고, 상태 클래스는 이 클래스를 통해 그 기능들을 꺼내 쓴다.
// 실제 동작은 상태 클래스(이동 · 대시 · 공격 · 피격)가 처리하고, 이 클래스는 매 프레임 상태를 실행한 뒤 이동을 반영한다.
// 이동 방향은 매 프레임 카메라가 보는 수평 방향을 기준으로 한다.
[RequireComponent(typeof(CharacterController), typeof(PlayerEnergy), typeof(PlayerTargeting))]
[RequireComponent(typeof(PlayerHealth), typeof(PlayerSlowMotion), typeof(PlayerParry))]
[RequireComponent(typeof(PlayerUltimate), typeof(PlayerComboSkill), typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerAttackJudge), typeof(PlayerAnimation))]
public class PlayerController : MonoBehaviour
{
    private const int MAX_DASH_STACK = 2;
    // 땅에 붙어 있도록 누르는 작은 아래 방향 속도 (0 이면 경사에서 isGrounded 가 흔들린다)
    private const float GROUNDED_VERTICAL_SPEED = -2f;

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
    [Tooltip("대시 · 회피 시작부터 적 공격에 맞지 않는 시간(초). 이 시간 안에 닿은 공격은 완벽 회피가 된다. 대시와 회피가 같은 값을 쓴다")]
    [SerializeField, Min(0f)] private float _dashInvincibleTime = 0.15f;

    [Header("공격")]
    [Tooltip("콤보 단계별 설정. 배열 크기가 콤보 단수이며, Animator 에 \"BasicAttack 1\" 부터 단수만큼 상태가 있어야 한다. 마지막 단계는 마무리 공격으로, 후딜이 끝날 때까지 공격 입력을 받지 않는다")]
    [SerializeField] private PlayerAttackSetting[] _comboSteps = { new PlayerAttackSetting(), new PlayerAttackSetting(), new PlayerAttackSetting(), new PlayerAttackSetting() };
    [SerializeField] private PlayerAttackSetting _heavyAttack = new PlayerAttackSetting();
    [Tooltip("공격 키를 누르고 있으면 이 시간 뒤 강공격이 나간다")]
    [SerializeField, Min(0.01f)] private float _chargeTime = 0.6f;
    [Tooltip("공격 대상에게 다가갈 때 대상 중심에서 이 거리만큼 앞에서 멈춘다")]
    [SerializeField, Min(0f)] private float _attackStopDistance = 1.2f;

    [Header("대시 공격 · 회피 반격")]
    [Tooltip("대시 · 회피 도중 공격 입력으로 나가는 공격")]
    [SerializeField] private PlayerAttackSetting _dashAttack = new PlayerAttackSetting();
    [Tooltip("완벽 회피 슬로모션 중 공격 입력으로 나가는 반격")]
    [SerializeField] private PlayerAttackSetting _dodgeCounter = new PlayerAttackSetting();

    [Header("스킬")]
    [SerializeField] private PlayerAttackSetting _skill = new PlayerAttackSetting();
    [SerializeField] private PlayerAttackSetting _specialSkill = new PlayerAttackSetting();
    [Tooltip("특수스킬에 필요한 에너지. 스킬 입력 시 에너지가 이보다 적으면 일반 스킬이 나간다")]
    [SerializeField, Min(0f)] private float _specialSkillEnergyCost = 60f;

    private CharacterController _characterController;
    private PlayerInputReader _input;
    private PlayerEnergy _energy;
    private PlayerTargeting _targeting;
    private PlayerSlowMotion _slowMotion;
    private PlayerStateMachine _stateMachine;
    private PlayerLocomotionState _locomotionState;
    private PlayerDashState _dashState;
    private PlayerAttackState _attackState;
    private PlayerHitState _hitState;
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
    public PlayerHealth Health { get; private set; }
    public PlayerAttackJudge AttackJudge { get; private set; }
    public PlayerAnimation Animation { get; private set; }
    public bool WasDashPressed => _input.WasDashPressed;
    public bool WasAttackPressed => _input.WasAttackPressed;
    public bool WasSkillPressed => _input.WasSkillPressed;
    public int ComboStepCount => _comboSteps.Length;
    public float WalkSpeed => _walkSpeed;
    public float RunSpeed => _runSpeed;
    public float AutoRunDelay => _autoRunDelay;
    public float DashChainInterval => _dashChainInterval;
    public float DashInvincibleTime => _dashInvincibleTime;
    // 완벽 회피 후 슬로모션 중인지. 이 동안의 공격 입력은 대시 공격보다 회피 반격이 우선한다.
    public bool IsPerfectDodge => _slowMotion.IsActive;
    // 현재 상태에서 필살기를 쓸 수 있는지. 상태마다 스스로 정한다.
    public bool CanUseUltimate => _stateMachine.CurrentState.CanUseUltimate;

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
        Vector2 input = _input.MoveInput;
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

    // 입력 버퍼 시간 안이면 다음 콤보 단계, 지났으면 1단부터 시작한다. 공격 방향은 공격 상태에 들어갈 때 AimAttack 이 정한다.
    public void StartBasicAttack()
    {
        int step = Time.time <= _comboExpireTime ? _nextComboStep : 0;

        _comboExpireTime = 0f;
        _attackState.Setup(PlayerAttackState.AttackType.Basic, step, _comboSteps[step]);
        _stateMachine.ChangeState(_attackState);
    }

    // 기본 공격 외의 공격(강공격 · 스킬 · 특수스킬 · 대시 공격 · 회피 반격 · 패링 추격)을 시작한다.
    // 진행 중이던 차지는 끊고, 이 공격들 뒤의 콤보는 1단부터 다시 시작한다.
    // priorityTarget : 록온 중이 아닐 때 자동 조준보다 먼저 노릴 대상. 패링 추격은 패링한 적을 넘긴다.
    public void StartAttack(PlayerAttackState.AttackType type, PlayerAttackSetting setting, IEnemyTargetable priorityTarget = null)
    {
        _isCharging = false;
        _comboExpireTime = 0f;
        _attackState.Setup(type, 0, setting, priorityTarget);
        _stateMachine.ChangeState(_attackState);
    }

    // 다른 기능이 가진 상태(패링 등)로 넘긴다. 진행 중이던 차지는 끊고, 이후 콤보는 1단부터 다시 시작한다.
    public void EnterState(PlayerState state)
    {
        _isCharging = false;
        _comboExpireTime = 0f;
        _stateMachine.ChangeState(state);
    }

    // 대시 · 회피 도중 공격 입력으로 시작한다.
    public void StartDashAttack() => StartAttack(PlayerAttackState.AttackType.DashAttack, _dashAttack);

    // 완벽 회피 슬로모션 중 공격 입력으로 회피 반격을 시작한다. 슬로모션은 공격 상태에 들어가면서 끝난다.
    public void StartDodgeCounter() => StartAttack(PlayerAttackState.AttackType.DodgeCounter, _dodgeCounter);

    // 적 공격에 맞으면 어떤 상태에서든 피격 상태로 넘어간다. 진행 중이던 공격 · 차지는 끊고, 이후 콤보는 1단부터 시작한다.
    public void StartHit(EnemyAttackSetting attack, Vector3 attackerPosition)
    {
        _isCharging = false;
        _comboExpireTime = 0f;
        _hitState.Setup(attack, attackerPosition);
        _stateMachine.ChangeState(_hitState);
    }

    // 공격 동작이 끝난 시점에 다음 단계와, 다음 단계로 이어갈 수 있는 마감 시각을 정한다. 마지막 단계 다음은 1단이다.
    public void FinishComboStep(int step)
    {
        _nextComboStep = (step + 1) % _comboSteps.Length;
        _comboExpireTime = Time.time + _comboSteps[step].InputBuffer;
    }

    // 에너지가 특수스킬 요구량 이상이면 소모하고 특수스킬, 모자라면 일반 스킬을 시작한다.
    public void StartSkill()
    {
        bool isSpecial = _energy.TryConsume(_specialSkillEnergyCost);

#if UNITY_EDITOR
        if(isSpecial) Debug.Log($"[특수스킬] 에너지 -{_specialSkillEnergyCost} → {_energy.CurrentEnergy:F1}");
#endif

        if(isSpecial) StartAttack(PlayerAttackState.AttackType.SpecialSkill, _specialSkill);
        else StartAttack(PlayerAttackState.AttackType.Skill, _skill);
    }

    // 공격을 시작할 때 몸을 돌릴 방향과 전진 거리를 정하고, 전진 거리를 돌려준다.
    // 대상은 록온 대상 → 우선 대상 → 공격 사거리 안의 자동 조준 대상 순서로 고른다.
    // 대상이 있으면 대상 쪽으로 즉시 돌고, 대상 앞 정지 거리까지 다가간다. 다가가는 거리는 공격 사거리를 넘지 않는다.
    // 그래서 록온 대상이 공격 사거리 밖에 있으면 공격 사거리만큼만 다가가고, 공격이 닿지 않을 수 있다.
    // 대상이 없으면 canTurnToInput 일 때만 이동 입력 방향으로 돌고(입력이 없으면 그대로), 공격 설정의 전진 거리를 쓴다.
    // priorityTarget : 록온 중이 아닐 때 자동 조준보다 먼저 쓸 대상. 패링 추격은 패링한 적을 넘긴다.
    public float AimAttack(PlayerAttackSetting setting, bool canTurnToInput, IEnemyTargetable priorityTarget = null)
    {
        IEnemyTargetable target = _targeting.FindAttackTarget(_targeting.AttackRange, priorityTarget);

        if(target == null)
        {
            if(canTurnToInput) FaceImmediately(ReadMoveDirection());
            return setting.AdvanceDistance;
        }

        return FaceAndGetApproachDistance(target);
    }

    // 발동 때 정한 대상을 끝까지 노리는 공격(필살기 · 콤보 스킬)의 방향과 전진 거리를 정한다.
    // 도중에 록온을 바꾸거나 풀어도 이 대상 쪽으로 돌아 다가간다. 카메라는 바뀐 록온 대상을 따라간다.
    // 발동 때 대상이 없었으면(null) 도중에 생긴 대상도 노리지 않고, 바라보는 방향 그대로 공격 설정의 전진 거리를 쓴다.
    public float AimFixedTarget(PlayerAttackSetting setting, IEnemyTargetable fixedTarget)
    {
        if(fixedTarget == null) return setting.AdvanceDistance;

        return FaceAndGetApproachDistance(fixedTarget);
    }

    // 완벽 회피 슬로모션이 남아 있으면 끝낸다. 어떤 공격이든 시작하면 슬로모션은 끝난다.
    public void EndSlowMotion() => _slowMotion.End();

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

        if(!_input.IsAttackHeld)
        {
            _isCharging = false;
            return false;
        }

        _chargeTimer += Time.deltaTime;

        if(_chargeTimer < _chargeTime) return false;

        StartHeavyAttack();
        return true;
    }

    public void HandleAnimationEvent(PlayerAnimationEventType type, int stateHash)
    {
        _stateMachine.CurrentState.OnAnimationEvent(type, stateHash);
    }

    // 이동 상태로 돌아가면 진행 중이던 차지는 끊는다.
    public void EnterLocomotion(bool startRunning)
    {
        _isCharging = false;
        _locomotionState.SetStartRunning(startRunning);
        _stateMachine.ChangeState(_locomotionState);
    }

    // 대상 쪽으로 즉시 돌고, 대상 앞 정지 거리까지의 전진 거리를 돌려준다. 다가가는 거리는 공격 사거리를 넘지 않는다.
    private float FaceAndGetApproachDistance(IEnemyTargetable target)
    {
        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        FaceImmediately(toTarget);

        return Mathf.Clamp(toTarget.magnitude - _attackStopDistance, 0f, _targeting.AttackRange);
    }

    // 강공격 뒤에는 콤보가 1단부터 다시 시작한다.
    private void StartHeavyAttack() => StartAttack(PlayerAttackState.AttackType.Heavy, _heavyAttack);

    public void FaceImmediately(Vector3 direction)
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

    private void CacheComponent()
    {
        _characterController = GetComponent<CharacterController>();
        _input = GetComponent<PlayerInputReader>();
        _energy = GetComponent<PlayerEnergy>();
        _targeting = GetComponent<PlayerTargeting>();
        _slowMotion = GetComponent<PlayerSlowMotion>();
        AttackJudge = GetComponent<PlayerAttackJudge>();
        Animation = GetComponent<PlayerAnimation>();
        Health = GetComponent<PlayerHealth>();
    }

    private void Init()
    {
        _stateMachine = new PlayerStateMachine();
        _locomotionState = new PlayerLocomotionState(this);
        _dashState = new PlayerDashState(this);
        _attackState = new PlayerAttackState(this);
        _hitState = new PlayerHitState(this);
        _dashStack = MAX_DASH_STACK;

        EnterLocomotion(false);
    }
}

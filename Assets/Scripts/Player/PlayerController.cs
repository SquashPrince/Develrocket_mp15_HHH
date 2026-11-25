using UnityEngine;

// 캐릭터의 상태 전환과 대시 스택 · 콤보 · 차지 · 스킬 분기 · 중력을 맡고, 매 프레임 상태를 실행한 뒤 이동을 반영한다.
// 상태 클래스 · 다른 기능은 이 클래스를 통해 캐릭터 데이터 · 공통 설정 · 각 기능을 꺼내 쓴다.
// 자세한 구조 : Docs/Technical/Player/PlayerStructure.md
[RequireComponent(typeof(CharacterController), typeof(PlayerTargeting))]
[RequireComponent(typeof(PlayerStatus), typeof(PlayerActionTrigger))]
[RequireComponent(typeof(PlayerInputReader), typeof(PlayerAttackJudge), typeof(PlayerAnimation))]
public class PlayerController : MonoBehaviour
{
    private const int MAX_DASH_STACK = 2;
    // 땅에 붙어 있도록 누르는 작은 아래 방향 속도 (0 이면 경사에서 isGrounded 가 흔들린다)
    private const float GROUNDED_VERTICAL_SPEED = -2f;

    [Header("캐릭터")]
    [Tooltip("이 캐릭터의 능력치 · 이동 · 대시 수치와 액션. 파티 전투에서는 캐릭터마다 다른 에셋을 넣는다")]
    [SerializeField] private CharacterData _character;

    private CharacterController _characterController;
    private PlayerInputReader _input;
    private PlayerTargeting _targeting;
    private PlayerStateMachine _stateMachine;
    private PlayerLocomotionState _locomotionState;
    private PlayerDashState _dashState;
    private PlayerAttackState _attackState;
    private PlayerHitState _hitState;
    private PlayerSwapInState _swapInState;
    private PlayerSwapOutState _swapOutState;
    private PlayerDownedState _downedState;
    // 파티가 Awake 에서 알려 준다.
    private PlayerParty _party;
    private float _dashCooldownTimer;
    private float _verticalSpeed;
    private float _turnVelocity;
    private float _comboExpireTime;
    private float _chargeTimer;
    private int _dashStack;
    private int _nextComboStep;
    private bool _isCharging;
    // 하던 동작이 남아 퇴장을 미뤄 둔 상태
    private bool _isSwapOutReserved;
    // 이 동작이 끝나면 반복 중인 동작 이펙트를 멈춘다.
    private ActionStep _motionEffectStep;

    // 상태가 정하고, 상태 실행 뒤 중력과 함께 반영한다.
    public Vector3 HorizontalVelocity { get; set; }
    public PlayerStatus Status { get; private set; }
    public PlayerAttackJudge AttackJudge { get; private set; }
    public PlayerAnimation Animation { get; private set; }
    public PlayerInputReader Input => _input;
    public PlayerTargeting Targeting => _targeting;
    public PlayerActionTrigger ActionTrigger { get; private set; }
    public CharacterController Body => _characterController;
    // 히트렉처럼 이 캐릭터만 멈추는 효과가 반영된 시간. 상태 · 기능의 타이머 · 이동은 이 값을 쓴다.
    public float DeltaTime => Status.DeltaTime;
    public bool WasDashPressed => _input.WasDashPressed;
    public bool WasAttackPressed => _input.WasAttackPressed;
    public bool WasSkillPressed => _input.WasSkillPressed;
    public CharacterData Character => _character;
    public PlayerParty Party => _party;
    public CommonCharacterSettings Common => _party.Common;
    public int ComboStepCount => _character.ComboStepCount;
    public float WalkSpeed => _character.WalkSpeed;
    public float RunSpeed => _character.RunSpeed;
    public float AutoRunDelay => _character.AutoRunDelay;
    public float DashChainInterval => _character.DashChainInterval;
    public float DashInvincibleTime => _character.DashInvincibleTime;
    public bool HasDashStack => _dashStack > 0;
    // 다음 대시 횟수가 찰 때까지 남은 비율(1 → 0)
    public float DashCooldownRatio => _dashStack >= MAX_DASH_STACK || _character.DashCooldown <= 0f ? 0f : 1f - _dashCooldownTimer / _character.DashCooldown;
    // 이 동안의 공격 입력은 대시 공격보다 회피 반격이 우선한다.
    public bool IsPerfectDodge => _party.SlowMotion.IsActive;
    public bool CanUseUltimate => _stateMachine.CurrentState.CanUseUltimate;
    public bool CanSwap => _stateMachine.CurrentState.CanSwap;
    public ActionData CurrentAction => _stateMachine.CurrentState.CurrentAction;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update()
    {
        RechargeDashStack();
        _stateMachine.Tick();
        StopMotionEffectsOnStepChange();

        // 퇴장이 끝나 이번 프레임에 캐릭터가 꺼졌으면 꺼진 CharacterController 를 움직이지 않는다.
        if(!gameObject.activeSelf) return;

        ApplyMovement();
    }

    public void JoinParty(PlayerParty party) => _party = party;

    public void SwapIn() => EnterState(_swapInState);

    // 모든 동작은 끝나면 이동 상태로 돌아가므로, 동작 중이면 이동 상태로 돌아가는 순간 퇴장한다.
    public void ReserveSwapOut()
    {
        if(_stateMachine.CurrentState == _locomotionState) EnterState(_swapOutState);
        else _isSwapOutReserved = true;
    }

    // 필드에 남아 있던 이 캐릭터를 다시 조작할 때 부른다. 이미 퇴장 중이면 이동 상태로 되돌린다.
    public void CancelSwapOut()
    {
        _isSwapOutReserved = false;

        if(_stateMachine.CurrentState == _swapOutState) EnterLocomotion(false);
    }

    // 꺼진 캐릭터는 적에게 맞거나 대상으로 잡히지 않는다.
    public void Standby() => gameObject.SetActive(false);

    // 카메라 기준의 수평 방향. 스틱을 살짝 기울이면 길이가 1보다 작다.
    // 이동 중 카메라가 자동 복귀하면 기준이 바뀌어 원을 그리므로, 복귀는 CameraRecenterBlocker 가 막는다.
    public Vector3 ReadMoveDirection()
    {
        Vector2 input = _input.MoveInput;
        Vector3 localDirection = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);

        return Quaternion.Euler(0f, _party.MainCamera.transform.eulerAngles.y, 0f) * localDirection;
    }

    public void TurnTowards(Vector3 direction)
    {
        float targetYaw = Quaternion.LookRotation(direction).eulerAngles.y;
        float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref _turnVelocity, _character.TurnTime);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // 방향 입력이 있으면 그 방향으로 대시, 없으면 바라보는 방향을 유지한 채 뒤로 회피한다.
    public bool TryStartDash(Vector3 moveDirection)
    {
        if(_dashStack <= 0) return false;

        if(moveDirection != Vector3.zero)
        {
            FaceImmediately(moveDirection);
            _dashState.Setup(transform.forward, _character.DashDistance, _character.DashDuration, false);
        }
        else
        {
            _dashState.Setup(-transform.forward, _character.DodgeDistance, _character.DodgeDuration, true);
        }

        _dashStack--;
        _isCharging = false;
        _stateMachine.ChangeState(_dashState);

        return true;
    }

    // 입력 버퍼 시간이 지났으면 1단부터 시작한다.
    public void StartBasicAttack()
    {
        int step = Status.LocalTime <= _comboExpireTime ? _nextComboStep : 0;

        _comboExpireTime = 0f;
        _attackState.Setup(PlayerAttackState.AttackType.Basic, step, _character.GetComboStep(step));
        _stateMachine.ChangeState(_attackState);
    }

    // 기본 공격 외의 공격 뒤 콤보는 1단부터 다시 시작한다.
    // priorityTarget : 록온 중이 아닐 때 자동 조준보다 먼저 노릴 대상 (패링 추격 · 빠른 지원)
    public void StartAttack(PlayerAttackState.AttackType type, ActionData setting, IEnemyTargetable priorityTarget = null)
    {
        _isCharging = false;
        _comboExpireTime = 0f;
        _attackState.Setup(type, 0, setting, priorityTarget);
        _stateMachine.ChangeState(_attackState);
    }

    public void EnterState(PlayerState state)
    {
        _isCharging = false;
        _comboExpireTime = 0f;
        _stateMachine.ChangeState(state);
    }

    public void StartDashAttack() => StartAttack(PlayerAttackState.AttackType.DashAttack, _character.DashAttack);

    // 슬로모션은 공격 상태에 들어가면서 끝난다.
    public void StartDodgeCounter() => StartAttack(PlayerAttackState.AttackType.DodgeCounter, _character.DodgeCounter);

    // 순간 이동할 자리가 막혀 있으면 제자리에서 쓴다. 액션이 없으면 false 라 파티가 보통 입장으로 교체한다.
    public bool TryStartQuickSupport(Transform target)
    {
        ActionData action = _character.QuickSupport;

        if(action == null || target == null) return false;

        TeleportManager.TryTeleportTo(this, target, action.Teleport);
        target.TryGetComponent(out IEnemyTargetable priorityTarget);
        StartAttack(PlayerAttackState.AttackType.QuickSupport, action, priorityTarget);
        return true;
    }

    // reaction : 연타의 막타는 공격의 반응과 다를 수 있어 따로 받는다.
    public void StartHit(EnemyAttackData attack, EnemyAttackData.HitReaction reaction, Transform attacker)
    {
        _isCharging = false;
        _comboExpireTime = 0f;
        _hitState.Setup(attack, reaction, attacker);
        _stateMachine.ChangeState(_hitState);
    }

    public void StartDowned()
    {
        _isCharging = false;
        _comboExpireTime = 0f;
        _isSwapOutReserved = false;
        _stateMachine.ChangeState(_downedState);
    }

    public void FinishComboStep(int step)
    {
        _nextComboStep = (step + 1) % _character.ComboStepCount;
        _comboExpireTime = Status.LocalTime + _character.GetComboStep(step).InputBuffer;
    }

    // 에너지가 모자라면 일반 스킬을 쓴다.
    public void StartSkill()
    {
        float cost = _character.SpecialSkill.EnergyCost;
        bool isSpecial = Status.TryConsumeEnergy(cost);

        if(isSpecial) StartAttack(PlayerAttackState.AttackType.SpecialSkill, _character.SpecialSkill);
        else StartAttack(PlayerAttackState.AttackType.Skill, _character.Skill);
    }

    // 대상 우선순위 : 록온 대상 → 우선 대상 → 공격 사거리 안의 자동 조준 대상. 전진 거리를 돌려준다.
    // 다가가는 거리는 공격 사거리를 넘지 않아, 사거리 밖의 록온 대상에게는 공격이 닿지 않을 수 있다.
    public float AimAttack(ActionStep step, bool canTurnToInput, IEnemyTargetable priorityTarget = null)
    {
        IEnemyTargetable target = _targeting.FindAttackTarget(_targeting.AttackRange, priorityTarget);

        if(target == null)
        {
            if(canTurnToInput) FaceImmediately(ReadMoveDirection());
            return step.AdvanceDistance;
        }

        return FaceAndGetApproachDistance(target);
    }

    // 필살기 · 콤보 스킬은 도중에 록온을 바꿔도 발동 때 정한 대상을 끝까지 노린다. 대상이 없었으면 도중에 생겨도 노리지 않는다.
    public float AimFixedTarget(ActionStep step, IEnemyTargetable fixedTarget)
    {
        if(fixedTarget == null) return step.AdvanceDistance;

        return FaceAndGetApproachDistance(fixedTarget);
    }

    public void EndSlowMotion() => _party.SlowMotion.End();

    public void BeginCharge()
    {
        _isCharging = true;
        _chargeTimer = 0f;
    }

    // 차지 시간에 도달하면 진행 중인 공격을 끊고 강공격을 시작하고 true 를 돌려준다.
    public bool UpdateCharge()
    {
        if(!_isCharging) return false;

        if(!_input.IsAttackHeld)
        {
            _isCharging = false;
            return false;
        }

        _chargeTimer += DeltaTime;

        if(_chargeTimer < _character.HeavyAttack.ChargeTime) return false;

        StartHeavyAttack();
        return true;
    }

    public void HandleAnimationEvent(PlayerAnimationEventType type, int stateHash)
    {
        _stateMachine.CurrentState.OnAnimationEvent(type, stateHash);
    }

    // 이전 클립과 섞이는 동안 이전 클립의 이벤트도 오므로, 지금 동작의 클립에서 온 이벤트만 처리한다.
    public void PlayMotionEffect(int index, int stateHash)
    {
        ActionStep step = _stateMachine.CurrentState.CurrentStep;

        if(step == null || step.AnimationHash != stateHash) return;

        MotionEffectSetting effect = step.GetMotionEffect(index);

        if(effect == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[{name}] 동작 '{step.AnimationState}' 에 {index} 번 동작 이펙트가 없습니다. 클립 이벤트의 Int 값과 액션 데이터의 이펙트 목록을 확인하세요.", this);
#endif
            return;
        }

        BattleEffect.PlayAttached(effect.Prefab, transform, effect.Position, effect.Rotation, effect.Scale);
        _motionEffectStep = step;
    }

    // 이전 클립의 이벤트가 지금 동작의 이펙트를 멈추지 않게 거른다.
    public void StopLoopingMotionEffects(int stateHash)
    {
        ActionStep step = _stateMachine.CurrentState.CurrentStep;

        if(step == null || step.AnimationHash != stateHash) return;

        StopLoopingMotionEffects();
    }

    private void StopLoopingMotionEffects()
    {
        BattleEffect.StopLoop(transform);
        _motionEffectStep = null;
    }

    // 피격 · 회피로 동작이 끊겨 판정 끝 이벤트가 오지 않아도 반복 이펙트가 남지 않게 한다.
    private void StopMotionEffectsOnStepChange()
    {
        if(_motionEffectStep != null && _stateMachine.CurrentState.CurrentStep != _motionEffectStep) StopLoopingMotionEffects();
    }

    // 퇴장이 예약되어 있으면 이동 상태 대신 퇴장한다.
    public void EnterLocomotion(bool startRunning)
    {
        if(_isSwapOutReserved)
        {
            _isSwapOutReserved = false;
            EnterState(_swapOutState);
            return;
        }

        _isCharging = false;
        _locomotionState.SetStartRunning(startRunning);
        _stateMachine.ChangeState(_locomotionState);
    }

    // 정지 거리는 몸 표면 기준이다. 중심 기준이면 큰 적(보스)일수록 몸 안까지 파고든다.
    private float FaceAndGetApproachDistance(IEnemyTargetable target)
    {
        Vector3 toTarget = HorizontalVector.Between(transform.position, target.transform.position);
        FaceImmediately(toTarget);

        float stopDistance = BodyRadius.GetHorizontal(target.transform) + _characterController.radius + Common.AttackableDistance;

        return Mathf.Clamp(toTarget.magnitude - stopDistance, 0f, _targeting.AttackRange);
    }

    private void StartHeavyAttack() => StartAttack(PlayerAttackState.AttackType.Heavy, _character.HeavyAttack);

    public void FaceImmediately(Vector3 direction)
    {
        if(direction == Vector3.zero) return;

        transform.rotation = Quaternion.LookRotation(direction.normalized);
        _turnVelocity = 0f;
    }

    // 대시 중에도 쿨타임이 계속 흐른다.
    private void RechargeDashStack()
    {
        if(_dashStack >= MAX_DASH_STACK) return;

        _dashCooldownTimer += DeltaTime;

        if(_dashCooldownTimer < _character.DashCooldown) return;

        _dashCooldownTimer -= _character.DashCooldown;
        _dashStack++;

        if(_dashStack >= MAX_DASH_STACK) _dashCooldownTimer = 0f;
    }

    private void ApplyMovement()
    {
        if(_characterController.isGrounded && _verticalSpeed < 0f) _verticalSpeed = GROUNDED_VERTICAL_SPEED;
        else _verticalSpeed += Common.Gravity * DeltaTime;

        _characterController.Move((HorizontalVelocity + Vector3.up * _verticalSpeed) * DeltaTime);
    }

    private void CacheComponent()
    {
        _characterController = GetComponent<CharacterController>();
        _input = GetComponent<PlayerInputReader>();
        _targeting = GetComponent<PlayerTargeting>();
        AttackJudge = GetComponent<PlayerAttackJudge>();
        ActionTrigger = GetComponent<PlayerActionTrigger>();
        Animation = GetComponent<PlayerAnimation>();
        Status = GetComponent<PlayerStatus>();
    }

    private void Init()
    {
        _stateMachine = new PlayerStateMachine();
        _locomotionState = new PlayerLocomotionState(this);
        _dashState = new PlayerDashState(this);
        _attackState = new PlayerAttackState(this);
        _hitState = new PlayerHitState(this);
        _swapInState = new PlayerSwapInState(this);
        _swapOutState = new PlayerSwapOutState(this);
        _downedState = new PlayerDownedState(this);
        _dashStack = MAX_DASH_STACK;

        EnterLocomotion(false);
    }
}

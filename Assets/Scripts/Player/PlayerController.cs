using UnityEngine;

// 플레이어의 상태 전환과 이동 · 대시 스택 · 콤보 · 차지 · 스킬 분기 · 중력을 맡는다. 캐릭터별 수치 · 액션은 캐릭터 데이터(CharacterData)에서 읽고, 다른 기능도 이 클래스를 통해 같은 캐릭터 데이터를 꺼내 쓴다.
// 모든 캐릭터가 같이 쓰는 규칙 값 · 입력은 공통 설정(CommonCharacterSettings)에 있다. 공통 설정은 파티(PlayerParty)가 들고, 다른 기능은 이 클래스를 통해 꺼내 쓴다.
// 입력은 입력 기능, 타격 판정은 판정 기능, Animator 제어는 애니메이션 기능, 순간 이동은 순간 이동 규칙(TeleportManager)이 맡고, 상태 클래스는 이 클래스를 통해 그 기능들을 꺼내 쓴다.
// 실제 동작은 상태 클래스(이동 · 대시 · 공격 · 피격)가 처리하고, 이 클래스는 매 프레임 상태를 실행한 뒤 이동을 반영한다.
// 이동 방향은 매 프레임 카메라(파티의 메인 카메라)가 보는 수평 방향을 기준으로 한다.
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
    // 이 캐릭터가 속한 파티. 파티가 Awake 에서 알려 준다.
    private PlayerParty _party;
    private float _dashCooldownTimer;
    private float _verticalSpeed;
    private float _turnVelocity;
    private float _comboExpireTime;
    private float _chargeTimer;
    private int _dashStack;
    private int _nextComboStep;
    private bool _isCharging;
    // 교체로 나가야 하지만 하던 동작이 남아 있어 퇴장을 미뤄 둔 상태인지
    private bool _isSwapOutReserved;

    // 상태가 이번 프레임에 정한 수평 속도. 매 프레임 상태 실행 뒤 중력과 함께 반영한다.
    public Vector3 HorizontalVelocity { get; set; }
    public PlayerStatus Status { get; private set; }
    public PlayerAttackJudge AttackJudge { get; private set; }
    public PlayerAnimation Animation { get; private set; }
    public PlayerInputReader Input => _input;
    public PlayerTargeting Targeting => _targeting;
    public PlayerActionTrigger ActionTrigger { get; private set; }
    // 캐릭터 몸(CharacterController). 순간 이동 규칙이 몸 크기를 재고 위치를 옮길 때 쓴다.
    public CharacterController Body => _characterController;
    // 이번 프레임의 캐릭터 시간(초). 히트렉처럼 이 캐릭터만 멈추는 효과가 반영된다. 상태 · 기능의 타이머 · 이동은 이 값을 쓴다.
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
    // 대시 · 회피를 쓸 수 있는 남은 횟수가 있는지. 전투 화면이 회피 칸의 쿨타임 표시를 켤지 정할 때 쓴다.
    public bool HasDashStack => _dashStack > 0;
    // 다음 대시 횟수가 찰 때까지 남은 쿨타임의 비율(1 → 0). 횟수가 가득 차 있으면 0 이다.
    public float DashCooldownRatio => _dashStack >= MAX_DASH_STACK || _character.DashCooldown <= 0f ? 0f : 1f - _dashCooldownTimer / _character.DashCooldown;
    // 완벽 회피 후 슬로모션 중인지. 이 동안의 공격 입력은 대시 공격보다 회피 반격이 우선한다.
    public bool IsPerfectDodge => _party.SlowMotion.IsActive;
    // 현재 상태에서 필살기를 쓸 수 있는지. 상태마다 스스로 정한다.
    public bool CanUseUltimate => _stateMachine.CurrentState.CanUseUltimate;
    // 현재 상태에서 교체할 수 있는지. 상태마다 스스로 정한다.
    public bool CanSwap => _stateMachine.CurrentState.CanSwap;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update()
    {
        RechargeDashStack();
        _stateMachine.Tick();

        // 퇴장이 끝나 이번 프레임에 캐릭터가 꺼졌으면 꺼진 CharacterController 를 움직이지 않는다.
        if(!gameObject.activeSelf) return;

        ApplyMovement();
    }

    // 파티가 시작할 때 부른다. 이후 이 캐릭터의 기능은 파티의 공통 설정을 쓴다.
    public void JoinParty(PlayerParty party) => _party = party;

    // 교체로 필드에 들어온다. 파티가 이전 캐릭터 자리로 옮기고 켠 뒤 부른다.
    public void SwapIn() => EnterState(_swapInState);

    // 교체로 필드에서 나간다. 이동 상태(하던 동작이 없음)면 바로 퇴장하고,
    // 공격 · 피격 같은 동작 중이면 그 동작이 끝나 이동 상태로 돌아가는 순간 퇴장한다.
    // 모든 동작은 끝나면 이동 상태로 돌아가므로, 퇴장 시점을 이동 상태로 돌아가는 한곳에서 정한다.
    public void ReserveSwapOut()
    {
        if(_stateMachine.CurrentState == _locomotionState) EnterState(_swapOutState);
        else _isSwapOutReserved = true;
    }

    // 퇴장이 끝나면 캐릭터를 꺼 대기 상태로 만든다. 꺼진 캐릭터는 적에게 맞거나 대상으로 잡히지 않는다.
    public void Standby() => gameObject.SetActive(false);

    // 입력을 카메라 기준의 월드 수평 방향으로 바꾼다. 스틱을 살짝 기울이면 길이가 1보다 작다.
    // 이동 중 카메라가 캐릭터 뒤로 자동 복귀하면 기준이 바뀌어 원을 그리므로, 이동 중 복귀는 CameraRecenterBlocker 가 막는다.
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

    // 스택이 있으면 대시(방향 입력 있음) 또는 회피(방향 입력 없음)를 시작한다. 진행 중이던 차지는 끊는다.
    public bool TryStartDash(Vector3 moveDirection)
    {
        if(_dashStack <= 0) return false;

        if(moveDirection != Vector3.zero)
        {
            // 방향 입력이 있으면 그 방향으로 즉시 돌아서 대시
            FaceImmediately(moveDirection);
            _dashState.Setup(transform.forward, _character.DashDistance, _character.DashDuration, false);
        }
        else
        {
            // 방향 입력이 없으면 바라보는 방향을 유지한 채 뒤로 회피
            _dashState.Setup(-transform.forward, _character.DodgeDistance, _character.DodgeDuration, true);
        }

        _dashStack--;
        _isCharging = false;
        _stateMachine.ChangeState(_dashState);

        return true;
    }

    // 입력 버퍼 시간 안이면 다음 콤보 단계, 지났으면 1단부터 시작한다. 공격 방향은 공격 상태에 들어갈 때 AimAttack 이 정한다.
    public void StartBasicAttack()
    {
        int step = Status.LocalTime <= _comboExpireTime ? _nextComboStep : 0;

        _comboExpireTime = 0f;
        _attackState.Setup(PlayerAttackState.AttackType.Basic, step, _character.GetComboStep(step));
        _stateMachine.ChangeState(_attackState);
    }

    // 기본 공격 외의 공격(강공격 · 스킬 · 특수스킬 · 대시 공격 · 회피 반격 · 패링 추격)을 시작한다.
    // 진행 중이던 차지는 끊고, 이 공격들 뒤의 콤보는 1단부터 다시 시작한다.
    // priorityTarget : 록온 중이 아닐 때 자동 조준보다 먼저 노릴 대상. 패링 추격은 패링한 적을 넘긴다.
    public void StartAttack(PlayerAttackState.AttackType type, ActionData setting, IEnemyTargetable priorityTarget = null)
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
    public void StartDashAttack() => StartAttack(PlayerAttackState.AttackType.DashAttack, _character.DashAttack);

    // 완벽 회피 슬로모션 중 공격 입력으로 회피 반격을 시작한다. 슬로모션은 공격 상태에 들어가면서 끝난다.
    public void StartDodgeCounter() => StartAttack(PlayerAttackState.AttackType.DodgeCounter, _character.DodgeCounter);

    // 빠른 지원 : 교체로 막 들어온 이 캐릭터가 이전 캐릭터를 넉백시킨 적 앞으로 순간 이동해 빠른 지원 액션을 쓴다. 파티가 교체할 때 부른다.
    // 서는 방향 · 옆 각도는 빠른 지원 액션의 순간 이동 옵션이 정하고, 순간 이동할 자리가 막혀 있으면 제자리에서 쓴다.
    // 공격은 그 적을 우선 대상으로 노린다. 빠른 지원 액션이 없으면 false 를 돌려주고, 파티는 보통 입장으로 교체한다.
    public bool TryStartQuickSupport(Transform target)
    {
        ActionData action = _character.QuickSupport;

        if(action == null || target == null) return false;

        TeleportManager.TryTeleportTo(this, target, action.Teleport);
        target.TryGetComponent(out IEnemyTargetable priorityTarget);
        StartAttack(PlayerAttackState.AttackType.QuickSupport, action, priorityTarget);
        return true;
    }

    // 적 공격에 맞으면 어떤 상태에서든 피격 상태로 넘어간다. 진행 중이던 공격 · 차지는 끊고, 이후 콤보는 1단부터 시작한다.
    public void StartHit(EnemyAttackData attack, Transform attacker)
    {
        _isCharging = false;
        _comboExpireTime = 0f;
        _hitState.Setup(attack, attacker);
        _stateMachine.ChangeState(_hitState);
    }

    // 공격 동작이 끝난 시점에 다음 단계와, 다음 단계로 이어갈 수 있는 마감 시각을 정한다. 마지막 단계 다음은 1단이다.
    public void FinishComboStep(int step)
    {
        _nextComboStep = (step + 1) % _character.ComboStepCount;
        _comboExpireTime = Status.LocalTime + _character.GetComboStep(step).InputBuffer;
    }

    // 에너지가 특수스킬 액션의 에너지 비용 이상이면 소모하고 특수스킬, 모자라면 일반 스킬을 시작한다.
    public void StartSkill()
    {
        float cost = _character.SpecialSkill.EnergyCost;
        bool isSpecial = Status.TryConsumeEnergy(cost);

        if(isSpecial) StartAttack(PlayerAttackState.AttackType.SpecialSkill, _character.SpecialSkill);
        else StartAttack(PlayerAttackState.AttackType.Skill, _character.Skill);
    }

    // 공격을 시작할 때 몸을 돌릴 방향과 전진 거리를 정하고, 전진 거리를 돌려준다.
    // 대상은 록온 대상 → 우선 대상 → 공격 사거리 안의 자동 조준 대상 순서로 고른다.
    // 대상이 있으면 대상 쪽으로 즉시 돌고, 대상 앞 정지 거리까지 다가간다. 다가가는 거리는 공격 사거리를 넘지 않는다.
    // 그래서 록온 대상이 공격 사거리 밖에 있으면 공격 사거리만큼만 다가가고, 공격이 닿지 않을 수 있다.
    // 대상이 없으면 canTurnToInput 일 때만 이동 입력 방향으로 돌고(입력이 없으면 그대로), 동작의 전진 거리를 쓴다.
    // priorityTarget : 록온 중이 아닐 때 자동 조준보다 먼저 쓸 대상. 패링 추격은 패링한 적을 넘긴다.
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

    // 발동 때 정한 대상을 끝까지 노리는 공격(필살기 · 콤보 스킬)의 방향과 전진 거리를 정한다.
    // 도중에 록온을 바꾸거나 풀어도 이 대상 쪽으로 돌아 다가간다. 카메라는 바뀐 록온 대상을 따라간다.
    // 발동 때 대상이 없었으면(null) 도중에 생긴 대상도 노리지 않고, 바라보는 방향 그대로 동작의 전진 거리를 쓴다.
    public float AimFixedTarget(ActionStep step, IEnemyTargetable fixedTarget)
    {
        if(fixedTarget == null) return step.AdvanceDistance;

        return FaceAndGetApproachDistance(fixedTarget);
    }

    // 완벽 회피 슬로모션이 남아 있으면 끝낸다. 어떤 공격이든 시작하면 슬로모션은 끝난다.
    public void EndSlowMotion() => _party.SlowMotion.End();

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

        _chargeTimer += DeltaTime;

        if(_chargeTimer < _character.HeavyAttack.ChargeTime) return false;

        StartHeavyAttack();
        return true;
    }

    public void HandleAnimationEvent(PlayerAnimationEventType type, int stateHash)
    {
        _stateMachine.CurrentState.OnAnimationEvent(type, stateHash);
    }

    // 이동 상태로 돌아가면 진행 중이던 차지는 끊는다. 퇴장이 예약되어 있으면 이동 상태 대신 퇴장한다.
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

    // 대상 쪽으로 즉시 돌고, 대상 앞 정지 거리까지의 전진 거리를 돌려준다. 다가가는 거리는 공격 사거리를 넘지 않는다.
    // 정지 거리는 순간 이동과 같은 몸 표면 기준이다. 대상 반지름 + 플레이어 반지름 + 공격 간격만큼 떨어져 멈춘다.
    //   중심 기준으로 정하면 큰 적(보스)일수록 몸 안까지 파고들기 때문이다.
    private float FaceAndGetApproachDistance(IEnemyTargetable target)
    {
        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        FaceImmediately(toTarget);

        float stopDistance = BodyRadius.GetHorizontal(target.transform) + _characterController.radius + Common.AttackableDistance;

        return Mathf.Clamp(toTarget.magnitude - stopDistance, 0f, _targeting.AttackRange);
    }

    // 강공격 뒤에는 콤보가 1단부터 다시 시작한다.
    private void StartHeavyAttack() => StartAttack(PlayerAttackState.AttackType.Heavy, _character.HeavyAttack);

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
        _dashStack = MAX_DASH_STACK;

        EnterLocomotion(false);
    }
}

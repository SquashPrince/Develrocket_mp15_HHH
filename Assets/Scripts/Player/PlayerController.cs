using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어의 공용 데이터(입력 · 수치 · 대시 스택 · 콤보 · 차지 · 스킬 · 중력)와 상태 전환을 맡는다.
// 실제 동작은 상태 클래스(이동 · 대시 · 공격 · 피격)가 처리하고, 이 클래스는 매 프레임 상태를 실행한 뒤 이동을 반영한다.
// 이동 방향은 매 프레임 카메라가 보는 수평 방향을 기준으로 한다.
[RequireComponent(typeof(CharacterController), typeof(PlayerEnergy), typeof(PlayerTargeting))]
[RequireComponent(typeof(PlayerHealth), typeof(PlayerSlowMotion), typeof(PlayerParry))]
[RequireComponent(typeof(PlayerUltimate), typeof(PlayerComboSkill))]
public class PlayerController : MonoBehaviour
{
    private static readonly int SPEED_PARAMETER = Animator.StringToHash("Speed");
    private static readonly int LEG_RUN_ANIMATION = Animator.StringToHash("LegRun");

    // Animator Controller 의 하체 달리기 레이어 이름과 같아야 한다. 하체만 덮는 Avatar Mask 를 씌운 레이어다.
    private const string LEG_RUN_LAYER_NAME = "LegRun";

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
    [Tooltip("대시 · 회피 시작부터 적 공격에 맞지 않는 시간(초). 이 시간 안에 닿은 공격은 완벽 회피가 된다. 대시와 회피가 같은 값을 쓴다")]
    [SerializeField, Min(0f)] private float _dashInvincibleTime = 0.15f;

    [Header("공격")]
    [Tooltip("콤보 단계별 설정. 배열 크기가 콤보 단수이며, Animator 에 \"BasicAttack 1\" 부터 단수만큼 상태가 있어야 한다. 마지막 단계는 마무리 공격으로, 후딜이 끝날 때까지 공격 입력을 받지 않는다")]
    [SerializeField] private PlayerAttackSetting[] _comboSteps = { new PlayerAttackSetting(), new PlayerAttackSetting(), new PlayerAttackSetting(), new PlayerAttackSetting() };
    [SerializeField] private PlayerAttackSetting _heavyAttack = new PlayerAttackSetting();
    [Tooltip("공격 키를 누르고 있으면 이 시간 뒤 강공격이 나간다")]
    [SerializeField, Min(0.01f)] private float _chargeTime = 0.6f;
    [Tooltip("타격 판정 상자의 중심 (캐릭터 기준 위치)")]
    [SerializeField] private Vector3 _hitBoxCenter = new Vector3(0f, 1f, 1f);
    [SerializeField] private Vector3 _hitBoxSize = new Vector3(1.5f, 1.5f, 2f);
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

    private readonly HashSet<IEnemyDamageable> _hitTargets = new HashSet<IEnemyDamageable>();
    private readonly Collider[] _hitColliders = new Collider[MAX_HIT_COLLIDERS];
    private CharacterController _characterController;
    private PlayerEnergy _energy;
    private PlayerTargeting _targeting;
    private PlayerSlowMotion _slowMotion;
    private PlayerUltimate _ultimate;
    private PlayerComboSkill _comboSkill;
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
    private float _legRunTargetWeight;
    private int _legRunLayer;
    private int _dashStack;
    private int _nextComboStep;
    private bool _isCharging;

    // 상태가 이번 프레임에 정한 수평 속도. 매 프레임 상태 실행 뒤 중력과 함께 반영한다.
    public Vector3 HorizontalVelocity { get; set; }
    public PlayerHealth Health { get; private set; }
    public bool WasDashPressed => _dashAction.action.WasPressedThisFrame();
    public bool WasAttackPressed => _attackAction.action.WasPressedThisFrame();
    public bool WasSkillPressed => _skillAction.action.WasPressedThisFrame();
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
        UpdateLegRunWeight();
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
        IEnemyTargetable target = _targeting.LockTarget != null ? _targeting.LockTarget : priorityTarget;

        if(target == null) target = _targeting.FindTarget(_targeting.AttackRange);

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

    // 캐릭터 앞의 판정 상자 안에 있는 적에게 한 번씩 적중시키고, 적중한 적마다 공격 설정의 에너지와 필살기 게이지를 얻는다.
    // 맞은 적의 체력은 공격 설정의 위력만큼 줄어든다.
    // 공격 설정의 경직 시간이 0보다 크면, 맞은 적 중 경직 저항 레벨이 공격의 경직 레벨 이하인 적을 경직시킨다.
    // isImpact : 강타 신호로 판정했는지. 판정은 일반 타격과 같고, 그로기 적에게 맞으면 판정이 끝난 뒤 콤보 스킬을 발동한다.
    public void PerformHit(PlayerAttackState.AttackType type, PlayerAttackSetting setting, bool isImpact = false)
    {
        Vector3 center = transform.TransformPoint(_hitBoxCenter);
        int count = Physics.OverlapBoxNonAlloc(center, _hitBoxSize * 0.5f, _hitColliders, transform.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        IEnemyTargetable comboSkillTarget = null;

        // 콜라이더가 여러 개인 적도 한 번만 맞도록 이번 판정에서 맞은 적을 기억한다.
        _hitTargets.Clear();

        for(int i = 0; i < count; i++)
        {
            IEnemyDamageable enemy = _hitColliders[i].GetComponentInParent<IEnemyDamageable>();

            if(enemy == null || !_hitTargets.Add(enemy)) continue;

            enemy.TakeDamage(setting.Power, isImpact);
            _energy.Add(setting.EnergyGain);

#if UNITY_EDITOR
            Debug.Log($"[{(isImpact ? "강타" : "적중")}] {type} 위력 {setting.Power} / 에너지 +{setting.EnergyGain} → {_energy.CurrentEnergy:F1}");
#endif

            _ultimate.AddGauge(setting.UltimateGaugeGain);

            // 이 타격으로 처치된 적은 경직 · 콤보 스킬 대상이 아니다. 그로기 적이 강타로 처치되면 콤보 스킬보다 처치가 먼저다.
            if(!enemy.IsAlive) continue;

            // 경직 · 그로기 기능이 없는 적(공격하지 않는 더미 적)은 경직 · 그로기 · 콤보 스킬 대상이 아니다.
            if(setting.StaggerTime > 0f && enemy.transform.TryGetComponent(out IEnemyStaggerable staggerable)) staggerable.Stagger(setting.StaggerTime, setting.StaggerLevel);

            // 한 번의 판정에서 콤보 스킬은 하나의 그로기 적에게만 발동한다. 발동 기회는 실제로 발동할 적에게서만 소모한다.
            if(!isImpact || comboSkillTarget != null || !_comboSkill.CanStart) continue;

            if(!enemy.transform.TryGetComponent(out IEnemyGroggyable groggyable) || !groggyable.TryConsumeComboSkillChance()) continue;

            enemy.transform.TryGetComponent(out comboSkillTarget);
        }

        // 판정을 모두 마친 뒤 상태를 바꿔, 같은 판정의 다른 적도 에너지 · 게이지 · 경직을 받게 한다.
        if(comboSkillTarget != null) _comboSkill.Begin(comboSkillTarget);
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

    // 공격 대상에게 멀리 다가가는 동안 하체에 달리기 모션을 덮는다. 상체는 공격 모션을 그대로 재생한다.
    // 레이어 비중은 즉시 바꾸지 않고 UpdateLegRunWeight 가 섞는 시간에 걸쳐 따라가게 해 모션이 튀지 않게 한다.
    public void SetLegRun(bool isRunning)
    {
        // 꺼져 있다가 켜질 때만 달리기를 처음부터 재생한다. 콤보가 이어지며 다시 켜질 때 발이 끊기지 않게 하기 위해서다.
        if(isRunning && _legRunTargetWeight == 0f) _animator.Play(LEG_RUN_ANIMATION, _legRunLayer, 0f);

        _legRunTargetWeight = isRunning ? 1f : 0f;
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

    private void UpdateLegRunWeight()
    {
        float weight = Mathf.MoveTowards(_animator.GetLayerWeight(_legRunLayer), _legRunTargetWeight, Time.deltaTime / ANIMATION_FADE_TIME);

        _animator.SetLayerWeight(_legRunLayer, weight);
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
        _targeting = GetComponent<PlayerTargeting>();
        _slowMotion = GetComponent<PlayerSlowMotion>();
        _ultimate = GetComponent<PlayerUltimate>();
        _comboSkill = GetComponent<PlayerComboSkill>();
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
        _legRunLayer = _animator.GetLayerIndex(LEG_RUN_LAYER_NAME);

        EnterLocomotion(false);
    }
}

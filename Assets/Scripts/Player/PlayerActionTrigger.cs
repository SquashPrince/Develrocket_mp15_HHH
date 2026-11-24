using UnityEngine;

// 지금 상태와 상관없이 끼어드는 특수 행동(패링 · 필살기 · 콤보 스킬)의 시작을 판단하고, 각 행동의 상태를 들고 있다.
// 패링은 현재 캐릭터가 직접 하지 않고, 교체로 나온 캐릭터가 한다(지원 공격). 콤보 스킬은 파티의 콤보 스킬 선택이 시작시킨다.
[RequireComponent(typeof(PlayerController), typeof(PlayerTargeting), typeof(PlayerInputReader))]
public class PlayerActionTrigger : MonoBehaviour
{
    private PlayerController _controller;
    private PlayerTargeting _targeting;
    private PlayerInputReader _input;
    private PlayerParryState _parryState;
    private PlayerUltimateState _ultimateState;
    private PlayerComboSkillState _comboSkillState;
    // 패링 상태에 들어가며 이 적 앞으로 순간 이동한다.
    private IEnemyParryable _selectedParryAttacker;

    public ActionData ParryAction => _controller.Character.Parry;
    public ActionData ParryChaseAction => _controller.Character.ParryChase;
    public ActionData UltimateAction => _controller.Character.Ultimate;
    public ActionData ComboSkillAction => _controller.Character.ComboSkill;
    public float ParryReadyTime => _controller.Common.ParryReadyTime;
    public float ParryReactDistance => _controller.Common.ParryReactDistance;
    public float ParryReactTime => _controller.Common.ParryReactTime;
    // 적은 예고를 시작할 때 플레이어 쪽으로 돌기 때문에, 이 적의 정면은 플레이어가 있던 쪽이다.
    public IEnemyParryable SelectedParryAttacker => _selectedParryAttacker;
    // 콤보 스킬 중에는 다른 그로기 적에게 강타가 맞아도 다시 발동하지 않는다.
    public bool CanStartComboSkill => !_comboSkillState.IsActive;
    // 교체로 나오는 캐릭터는 이 캐릭터 옆에 나오므로, 이 캐릭터 기준으로 지원 패링이 되는지 본다.
    public bool HasParryTarget => SelectParryAttacker() != null;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update() => TryStartUltimate();

    public bool CanParry(EnemyAttackStep step) => _parryState.IsActive && step.IsParryable;

    // 경직할지 다음 동작으로 이어갈지는 적이 지금 동작 설정으로 정한다.
    public bool TryParry(EnemyAttackData attack, EnemyAttackStep step, IEnemyParryable attacker)
    {
        if(!CanParry(step)) return false;

#if UNITY_EDITOR
        Debug.Log($"[패링 성공] {attack.name}");
#endif

        attacker.OnParried();

        if(attacker.transform.TryGetComponent(out IEnemyGroggyable groggyable) && groggyable.AddGroggy(ParryAction.GroggyGain)) _controller.Party.ComboSelector.OnGroggyEntered(groggyable);

        _controller.Status.AddParryGauge();
        attacker.transform.TryGetComponent(out IEnemyTargetable parriedTarget);
        _parryState.OnParrySuccess(parriedTarget);
        return true;
    }

    public IEnemyTargetable FindUltimateTarget() => _targeting.FindAttackTarget(_controller.Common.UltimateTargetSearchRange);

    public void BeginComboSkill(IEnemyTargetable target)
    {
#if UNITY_EDITOR
        Debug.Log($"[콤보 스킬] {_controller.Character.name} → {target.transform.name}");
#endif

        _comboSkillState.Setup(target);
        _controller.EnterState(_comboSkillState);
    }

    public void OnComboSkillImpactHit(IEnemyDamageable enemy) => _controller.Party.ComboSelector.RequestNextChain(_controller, enemy.transform);

    public bool AllowsSwap(ActionStep step) => step.Allows(ActionStep.Interrupt.Swap) || (step.Allows(ActionStep.Interrupt.ParrySwap) && HasParryTarget);

    // 패링 대상이 없으면 false 라 파티가 보통 입장으로 교체한다.
    public bool TryStartSupportParry()
    {
        if(_parryState.IsActive || _controller.Status.IsAttackInvincible) return false;

        _selectedParryAttacker = SelectParryAttacker();

        if(_selectedParryAttacker == null) return false;

        // 준비 · 반응 동작 없이 들어가면 패링 판정만 켜져 준비 → 반응 → 추격 순서가 깨진다.
        if(ParryAction == null || ParryAction.StepCount <= PlayerParryState.REACT_STEP)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[패링] {_controller.Character.name} 의 패링 액션이 비어 있거나 준비 · 반응 동작이 없어 패링하지 않는다");
#endif
            return false;
        }

        // 같은 예고에 다른 캐릭터가 겹쳐 패링하지 않게 기록한다.
        _controller.Party.ClaimParry(_selectedParryAttacker);
        _controller.EnterState(_parryState);
        return true;
    }

    private void TryStartUltimate()
    {
        if(!_input.WasUltimatePressed) return;
        if(!_controller.Status.IsUltimateGaugeFull || !_controller.CanUseUltimate) return;

        _controller.Status.ConsumeUltimateGauge();
        _controller.EnterState(_ultimateState);
    }

    // 우선순위 : 록온 대상 → 자동 조준 대상 → 록온 사거리 안에서 가장 최근에 예고를 시작한 적.
    // 후보마다 패링 가능 시간을 열었는지 보고 다음 후보로 넘어가야 해서 공통 공격 대상 고르기를 쓰지 않는다.
    private IEnemyParryable SelectParryAttacker()
    {
        IEnemyParryable lockAttacker = GetOpenParryAttacker(_targeting.LockTarget);

        if(lockAttacker != null) return lockAttacker;

        IEnemyParryable aimAttacker = GetOpenParryAttacker(_targeting.FindTarget(_targeting.AttackRange));

        return aimAttacker != null ? aimAttacker : ParryWindow.FindLatestWithin(transform.position, _targeting.LockOnRange, _controller.Party.CanStartParry);
    }

    private IEnemyParryable GetOpenParryAttacker(IEnemyTargetable target)
    {
        if(target == null) return null;

        if(!target.transform.TryGetComponent(out IEnemyParryable attacker) || !attacker.IsParryWindowOpen) return null;
        if(!_controller.Party.CanStartParry(attacker)) return null;

        float lockOnRange = _targeting.LockOnRange;

        return (attacker.transform.position - transform.position).sqrMagnitude <= lockOnRange * lockOnRange ? attacker : null;
    }

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _targeting = GetComponent<PlayerTargeting>();
        _input = GetComponent<PlayerInputReader>();
    }

    private void Init()
    {
        _parryState = new PlayerParryState(_controller, this);
        _ultimateState = new PlayerUltimateState(_controller, this);
        _comboSkillState = new PlayerComboSkillState(_controller, this);
    }
}

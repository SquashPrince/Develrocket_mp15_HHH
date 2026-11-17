using UnityEngine;

// 특수 행동(패링 · 필살기 · 콤보 스킬)을 언제 시작할지 판단하고, 각 행동의 상태를 들고 있다.
// 일반 공격 · 스킬과 달리 이 행동들은 지금 상태와 상관없이 끼어들거나(패링 · 필살기), 판정 기능이 밖에서 시작시킨다(콤보 스킬).
// 각 행동의 액션은 캐릭터 데이터에서, 패링 수치 · 필살기 대상 찾는 거리는 공통 설정에서 읽는다.
//
// 패링 : 현재 캐릭터가 직접 하지 않는다. 적이 패링 가능 시간을 열었을 때 교체 키를 누르면, 교체로 나온 캐릭터가 패링 상태로 들어간다(지원 공격, 파티가 시작시킨다).
//        이미 패링 중이거나, 시전 중 무적인 공격(패링 추격 · 필살기 포함) 중이면 시작하지 않는다.
//        록온 사거리 안에 패링 가능 시간을 연 적이 없으면 시작하지 않는다.
//        패링 공격을 하는 적이 여럿이면 공격 대상 기준(록온 대상 → 자동 조준 대상)을 따르고, 둘 다 아니면 록온 사거리 안에서 가장 최근에 예고를 시작한 적을 고른다.
// 필살기 : 게이지가 가득 차 있고 현재 상태가 필살기를 허용할 때 필살기 키를 누르면 게이지를 전부 소모하고 필살기 상태로 넘긴다.
//          패링(준비 · 반응)과 추격 중에는 상태가 필살기를 허용하지 않아 쓸 수 없다.
// 콤보 스킬 : 언제 누가 쓸지는 파티의 콤보 스킬 선택이 정하고(전투 공통 규칙), 이 기능은 고른 캐릭터의 콤보 스킬 상태를 시작한다.
//             한 번의 콤보 스킬은 단계(동작) 여러 개를 순서대로 이어 쓴다. 단계 수와 연계 수는 관계없다.
//             콤보 스킬 중 강타가 맞으면 콤보 스킬 선택에 알려, 연계가 남았으면 다음 캐릭터를 고르게 한다.
[RequireComponent(typeof(PlayerController), typeof(PlayerTargeting), typeof(PlayerInputReader))]
public class PlayerActionTrigger : MonoBehaviour
{
    private PlayerController _controller;
    private PlayerTargeting _targeting;
    private PlayerInputReader _input;
    private PlayerParryState _parryState;
    private PlayerUltimateState _ultimateState;
    private PlayerComboSkillState _comboSkillState;
    // 패링 키를 누른 순간 고른 패링 대상. 패링 상태에 들어가며 이 적 앞으로 순간 이동한다.
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

    private void Awake() => CacheComponent();
    private void Start() => Init();
    // 패링은 현재 캐릭터가 직접 하지 않는다. 파티 전투에서 교체 키로 나온 캐릭터가 패링한다(지원 공격).
    private void Update() => TryStartUltimate();

    // 패링 준비 · 반응 중 노란 동작(패링할 수 있는 동작)이 닿으면 패링 성공으로 처리하고 true 를 돌려준다. 상태 기능이 맞았을 때 부른다.
    // 패링한 적은 경직되고 패링 액션의 그로기 값만큼 그로기가 쌓이며, 패링 추격은 이 적을 노린다.
    public bool TryParry(EnemyAttackData attack, EnemyAttackStep step, IEnemyParryable attacker)
    {
        if(!_parryState.IsActive || !step.IsParryable) return false;

#if UNITY_EDITOR
        Debug.Log($"[패링 성공] {attack.name}");
#endif

        // 패링 성공 경직은 패링당한 공격의 저항 레벨을 경직 레벨로 쓴다. 레벨이 같으면 경직되므로 패링 성공은 항상 경직을 건다.
        if(attacker.transform.TryGetComponent(out IEnemyStaggerable staggerable)) staggerable.Stagger(step.ParryStaggerTime, attack.StaggerResistLevel);

        // 이 패링으로 그로기에 들어갔으면 콤보 스킬 선택에 알려 그로기 정지 시간을 걸게 한다.
        if(attacker.transform.TryGetComponent(out IEnemyGroggyable groggyable) && groggyable.AddGroggy(ParryAction.GroggyGain)) _controller.Party.ComboSelector.OnGroggyEntered(groggyable);

        _controller.Status.AddParryGauge();
        attacker.transform.TryGetComponent(out IEnemyTargetable parriedTarget);
        _parryState.OnParrySuccess(parriedTarget);
        return true;
    }

    // 공격 대상 기준(록온 대상 → 공통 설정의 찾는 거리 안의 자동 조준 대상)으로 필살기 대상을 고른다. 없으면 null 이다.
    public IEnemyTargetable FindUltimateTarget() => _targeting.FindAttackTarget(_controller.Common.UltimateTargetSearchRange);

    // 그로기 적을 대상으로 콤보 스킬 상태로 넘긴다. 콤보 스킬 선택에서 고른 캐릭터에게 파티가 부른다.
    public void BeginComboSkill(IEnemyTargetable target)
    {
#if UNITY_EDITOR
        Debug.Log($"[콤보 스킬] {_controller.Character.name} → {target.transform.name}");
#endif

        _comboSkillState.Setup(target);
        _controller.EnterState(_comboSkillState);
    }

    // 콤보 스킬 중 강타가 맞은 적을 콤보 스킬 선택에 알린다. 연계 대상이고 연계가 남았으면 다음 캐릭터를 고르는 단계가 열린다.
    public void OnComboSkillImpactHit(IEnemyDamageable enemy) => _controller.Party.ComboSelector.RequestNextChain(_controller, enemy.transform);

    // 지원 공격 : 교체로 막 들어온 이 캐릭터가 패링을 시작한다. 파티가 교체할 때 부르고, 패링을 시작했으면 true 를 돌려준다.
    // 록온 사거리 안에 패링 가능 시간을 연 적이 없으면 시작하지 않고 false 를 돌려준다. 이때 파티는 보통 입장으로 교체한다.
    public bool TryStartSupportParry()
    {
        if(_parryState.IsActive || _controller.Status.IsAttackInvincible) return false;

        _selectedParryAttacker = SelectParryAttacker();

        if(_selectedParryAttacker == null) return false;

        // 패링 액션이 연결되지 않았으면 패링을 시작하지 않는다. 상태에 들어간 뒤 멈추면 준비 모션 없이 패링 판정만 켜져,
        // 준비 성공 → 반응 → 추격 순서가 깨지기 때문이다. 패링 액션에 준비 · 반응 두 동작이 없을 때도 같은 이유로 막는다.
        if(ParryAction == null || ParryAction.StepCount <= PlayerParryState.REACT_STEP)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[패링] {_controller.Character.name} 의 패링 액션이 비어 있거나 준비 · 반응 동작이 없어 패링하지 않는다");
#endif
            return false;
        }

        _controller.EnterState(_parryState);
        return true;
    }

    // 진행 중이던 강공격 차지는 상태를 넘길 때 끊긴다.
    private void TryStartUltimate()
    {
        if(!_input.WasUltimatePressed) return;
        if(!_controller.Status.IsUltimateGaugeFull || !_controller.CanUseUltimate) return;

        _controller.Status.ConsumeUltimateGauge();
        _controller.EnterState(_ultimateState);
    }

    // 록온 사거리 안에서 패링 가능 시간을 연 적 중 공격 대상 기준으로 고른다. 록온 대상 → 자동 조준 대상 → 가장 최근에 예고를 시작한 적 순서다.
    // 록온 사거리는 록온이 풀리는 거리와 같아, 록온할 수 있는 거리의 적만 패링할 수 있다. 없으면 null 이다.
    // 공통 타겟팅 클래스(PlayerTargeting 의 공격 대상 고르기)를 쓰지 않고 패링만의 로직을 사용한다.
    // 이유는 후보마다 패링 가능 시간을 열었는지 확인하고, 열지 않았으면 다음 후보로 넘어가야 하기 때문이다.
    private IEnemyParryable SelectParryAttacker()
    {
        IEnemyParryable lockAttacker = GetOpenParryAttacker(_targeting.LockTarget);

        if(lockAttacker != null) return lockAttacker;

        IEnemyParryable aimAttacker = GetOpenParryAttacker(_targeting.FindTarget(_targeting.AttackRange));

        return aimAttacker != null ? aimAttacker : ParryWindow.FindLatestWithin(transform.position, _targeting.LockOnRange);
    }

    // 대상이 록온 사거리 안에서 패링 가능 시간을 연 적이면 그 적을, 아니면 null 을 돌려준다.
    private IEnemyParryable GetOpenParryAttacker(IEnemyTargetable target)
    {
        if(target == null) return null;

        if(!target.transform.TryGetComponent(out IEnemyParryable attacker) || !attacker.IsParryWindowOpen) return null;

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

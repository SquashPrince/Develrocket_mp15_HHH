using UnityEngine;

// 패링 입력과 수치. 록온 사거리 안의 적이 패링 가능 시간을 열었을 때 패링 키를 누르면 어느 상태에서든 패링 상태로 넘긴다.
// 코어 전투에서는 이전 · 다음 캐릭터 키 모두 현재 캐릭터가 직접 패링한다. 교체 방향 구분은 파티 전투 단계에서 한다.
// 이미 패링 중이거나, 시전 중 무적인 공격(패링 추격 · 필살기 포함) 중이면 입력을 무시한다.
// 패링 가능 시간 밖이거나 록온 사거리 밖의 적만 연 경우의 입력도 무시한다. 파티 전투에서는 이 입력이 그냥 교체가 된다.
// 패링을 시작하면 패링 액션의 순간 이동 옵션대로 패링 공격을 하는 적 앞으로 순간 이동한다. 실제로 이동하면 벽 등에 걸리기 때문이다. 도착 지점이 막혀 있으면 제자리에서 패링한다.
// 패링 공격을 하는 적이 여럿이면 공격 대상 기준(록온 대상 → 자동 조준 대상)을 따르고, 둘 다 아니면 록온 사거리 안에서 가장 최근에 예고를 시작한 적을 고른다.
[RequireComponent(typeof(PlayerController), typeof(PlayerTargeting), typeof(PlayerInputReader))]
public class PlayerParry : MonoBehaviour
{
    [Header("패링")]
    [Tooltip("패링 준비 자세를 유지하는 시간(초). 적 공격이 끊겨도 끝까지 유지한다")]
    [SerializeField, Min(0.01f)] private float _readyTime = 1f;
    [Tooltip("패링 성공 후 공격 반대 방향으로 밀려나는 거리")]
    [SerializeField, Min(0f)] private float _reactDistance = 2f;
    [Tooltip("패링 성공 후 반응 시간(초). 이 시간에 걸쳐 점점 느려지며 밀려나고, 이 동안 닿은 노란 공격은 자동으로 패링된다. 이 동안 공격 키를 누르면 끝난 뒤 추격 공격이 나간다")]
    [SerializeField, Min(0.01f)] private float _reactTime = 1f;

    private PlayerController _controller;
    private PlayerTargeting _targeting;
    private PlayerInputReader _input;
    private PlayerParryState _parryState;
    // 패링 키를 누른 순간 고른 패링 대상. 패링 상태에 들어가며 이 적 앞으로 순간 이동한다.
    private IEnemyParryable _selectedAttacker;

    public float ReadyTime => _readyTime;
    public float ReactDistance => _reactDistance;
    public float ReactTime => _reactTime;
    public ActionData Action => _controller.Character.Parry;
    public ActionData ParryChase => _controller.Character.ParryChase;
    // 패링 키를 누른 순간 고른 패링 대상. 적은 예고를 시작할 때 플레이어 쪽으로 돌기 때문에, 이 적의 정면은 플레이어가 있던 쪽이다.
    public IEnemyParryable SelectedAttacker => _selectedAttacker;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update() => TryStartParry();

    // 패링 준비 · 반응 중 노란 공격이 닿으면 패링 성공으로 처리하고 true 를 돌려준다. 체력 기능이 맞았을 때 부른다.
    // 패링한 적은 경직되고, 패링 추격은 이 적을 노린다.
    public bool TryParry(EnemyAttackSetting attack, IEnemyParryable attacker)
    {
        if(!_parryState.IsActive || !attack.IsParryable) return false;

#if UNITY_EDITOR
        Debug.Log($"[패링 성공] {attack.Name}");
#endif

        // 패링 성공 경직은 패링당한 공격의 저항 레벨을 경직 레벨로 쓴다. 레벨이 같으면 경직되므로 패링 성공은 항상 경직을 건다.
        if(attacker.transform.TryGetComponent(out IEnemyStaggerable staggerable)) staggerable.Stagger(attack.ParryStaggerTime, attack.StaggerResistLevel);

        _controller.Status.AddParryGauge();
        attacker.transform.TryGetComponent(out IEnemyTargetable parriedTarget);
        _parryState.OnParrySuccess(parriedTarget);
        return true;
    }

    // 록온 사거리 안에서 패링 가능 시간을 연 적 중 공격 대상 기준으로 고른다. 록온 대상 → 자동 조준 대상 → 가장 최근에 예고를 시작한 적 순서다.
    // 록온 사거리는 록온이 풀리는 거리와 같아, 록온할 수 있는 거리의 적만 패링할 수 있다. 없으면 null 이다.
    // 공통 타겟팅 클래스(PlayerTargeting 의 공격 대상 고르기)를 쓰지 않고 패링만의 로직을 사용한다.
    // 이유는 후보마다 패링 가능 시간을 열었는지 확인하고, 열지 않았으면 다음 후보로 넘어가야 하기 때문이다.
    private IEnemyParryable SelectAttacker()
    {
        IEnemyParryable lockAttacker = GetOpenAttacker(_targeting.LockTarget);

        if(lockAttacker != null) return lockAttacker;

        IEnemyParryable aimAttacker = GetOpenAttacker(_targeting.FindTarget(_targeting.AttackRange));

        return aimAttacker != null ? aimAttacker : ParryWindow.FindLatestWithin(transform.position, _targeting.LockOnRange);
    }

    // 대상이 록온 사거리 안에서 패링 가능 시간을 연 적이면 그 적을, 아니면 null 을 돌려준다.
    private IEnemyParryable GetOpenAttacker(IEnemyTargetable target)
    {
        if(target == null) return null;

        if(!target.transform.TryGetComponent(out IEnemyParryable attacker) || !attacker.IsParryWindowOpen) return null;

        float lockOnRange = _targeting.LockOnRange;

        return (attacker.transform.position - transform.position).sqrMagnitude <= lockOnRange * lockOnRange ? attacker : null;
    }

    private void TryStartParry()
    {
        if(!_input.WasParryPressed) return;
        if(_parryState.IsActive || _controller.Status.IsAttackInvincible) return;

        // 패링 액션이 연결되지 않았으면 패링을 시작하지 않는다. 상태에 들어간 뒤 멈추면 준비 모션 없이 패링 판정만 켜져,
        // 준비 성공 → 반응 → 추격 순서가 깨지기 때문이다. 상태가 바뀌기 전에 막아 진행 중이던 동작도 끊지 않는다.
        if(Action == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[패링] {_controller.Character.name} 의 패링 액션이 비어 있어 패링하지 않는다");
#endif
            return;
        }

        _selectedAttacker = SelectAttacker();

        if(_selectedAttacker == null) return;

        _controller.EnterState(_parryState);
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
    }
}

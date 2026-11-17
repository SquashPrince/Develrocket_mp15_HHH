using System;
using UnityEngine;

// 적의 체력 · 경직 · 그로기 · 처치 상태. 조정값은 적 데이터(EnemyController.Data)에서 읽는다.
// 다른 기능(플레이어 등)은 이 컴포넌트를 직접 찾지 않고 EnemyController 의 인터페이스로 접근한다.
//
// 체력 : 플레이어 공격의 위력만큼 줄고, 체력이 0 인 적은 강타에 맞아야만 처치된다.
//   체력이 0 이어도 처치되기 전까지는 살아 있는 적이라 계속 공격하고, 대상으로 고를 수 있다.
//   처치되지 않게 설정한 적(시험용 허수아비 등)은 체력만 줄고 0 에서 멈춘다.
// 처치 : 처치된 적은 시체로 남아 있다가 정해진 시간 뒤 비활성화된다. 그동안 Collider 는 그대로 둔다.
//   파괴하지 않고 비활성화하는 이유 : 플레이어 기능이 적을 인터페이스로 들고 있어, 파괴하면 Unity 의 null 판정을 받지 못해
//   남아 있는 참조로 접근할 때 오류가 나기 때문이다. 비활성화한 적은 이후 웨이브에서 다시 쓸 수도 있다.
// 경직 : 경직을 거는 쪽의 경직 레벨이 지금의 경직 저항 레벨 이상일 때만 걸린다.
//   지금의 경직 저항 레벨은 평소 저항 레벨과, 공격 중이면 그 공격의 저항 레벨 중 큰 값이다.
// 그로기 게이지 : 플레이어 공격 적중 · 패링 성공으로 쌓이고, 최대치에 이르면 그로기가 된다. 시간이 지나도 줄지 않는다.
//   그로기 중에는 더 쌓이지 않고, 그로기가 끝나면 0 부터 다시 쌓는다.
// 그로기 : 그로기 중에는 경직이 걸려도 그로기를 유지한다. 그로기가 될 때마다 콤보 스킬 기회가 한 번 생긴다.
//   그로기 시간 정지 : 플레이어 쪽이 콤보 스킬 흐름에 맞춰 정해진 시간 동안 멈추거나(정지 시간), 캐릭터 선택 동안 멈춘다.
//   멈춘 동안 남은 그로기 시간은 그대로이고, 다시 흐를 때 이어서 줄어든다.
// 경직 · 그로기 · 시체 시간은 적 전용 시간으로 흘러 슬로모션 · 필살기 연출 동안 느려지거나 멈춘다.
// 적의 공격 기능은 알림을 받아 진행 중인 공격을 끊고, 경직 · 그로기 동안에는 공격하지 않는다.
// 실행 순서를 앞당기는 이유 : 적 체력 막대(EnemyGauge)는 켜질 때(OnEnable) 최대 체력 등을 읽는다.
//   Unity 는 오브젝트마다 Awake · OnEnable 을 이어서 부르므로, 자식인 막대가 먼저 돌면 이 컴포넌트가 데이터를 읽기 전이라 오류가 난다.
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(EnemyController))]
public class EnemyState : MonoBehaviour
{
    private enum Reaction
    {
        None,
        Stagger,
        Groggy
    }

    [Header("[확인용] 현재 값")]
    [Tooltip("현재 체력. 실행 중 확인용이며, 시작할 때 최대 체력으로 정한다")]
    [SerializeField] private float _currentHealth;
    [Tooltip("현재 그로기 게이지. 실행 중 확인용이며, 시작할 때 0 으로 정한다")]
    [SerializeField] private float _currentGroggy;

    private EnemyData _data;
    private Reaction _reaction;
    private float _corpseTimer;
    // 경직 · 그로기의 남은 시간
    private float _reactionTimer;
    // 그로기 시간이 줄지 않는 남은 시간. 플레이어 쪽이 콤보 스킬 흐름에 맞춰 건다.
    private float _groggyHoldTimer;
    // 진행 중인 공격의 경직 저항 레벨. 공격 기능이 공격을 시작할 때 정하고, 공격이 끝나거나 끊기면 0 으로 돌린다.
    private int _attackStaggerResistLevel;
    // 이번 그로기에서 남은 연계 수. 그로기가 될 때마다 최대 연계 수로 채우고, 콤보 스킬 시전 · 캔슬마다 1 씩 준다.
    private int _remainingComboChain;
    private bool _isDead;
    // 이번 그로기에서 콤보 스킬을 아직 발동하지 않았는지. 그로기가 될 때마다 한 번 생긴다.
    private bool _hasComboSkillChance;
    // 콤보 스킬 캐릭터 선택 동안 그로기 시간을 멈추는지
    private bool _isGroggyPaused;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _data.MaxHealth;
    public bool IsAlive => !_isDead;
    public bool IsGroggy => _reaction == Reaction.Groggy;
    public bool HasComboSkillChance => IsGroggy && _hasComboSkillChance;
    public bool HasRemainingComboChain => IsGroggy && _remainingComboChain > 0;
    // 경직 · 그로기 중인지. 이 동안 적은 공격하지 않는다.
    public bool IsRestricted => _reaction != Reaction.None;
    public float CurrentGroggy => _currentGroggy;
    public float MaxGroggy => _data.MaxGroggy;
    // 그로기 중 남은 그로기 시간의 비율(1 → 0). 정지 중에는 줄지 않는다. 그로기가 아니면 0 이다.
    public float GroggyTimeRatio => IsGroggy && _data.GroggyTime > 0f ? Mathf.Clamp01(_reactionTimer / _data.GroggyTime) : 0f;

    // 맞을 때마다 강타 여부와 함께 알린다. 처치되는 타격도 먼저 맞은 것으로 알린 뒤 처치를 알린다.
    public event Action<bool> Damaged;
    // 처치될 때 한 번 알린다.
    public event Action Died;
    public event Action Staggered;
    public event Action GroggyEntered;
    // 경직 · 그로기가 끝났을 때 알린다.
    public event Action Recovered;
    // 그로기 게이지가 바뀌면 알린다. 쌓일 때와, 그로기가 끝나 0 이 될 때다.
    public event Action GroggyGaugeChanged;

    // 현재값은 다른 오브젝트를 참조하지 않으므로 Awake 에서 정한다. 화면 등 다른 기능이 시작 단계에서 읽어도 값이 준비되어 있게 하기 위해서다.
    // 적 데이터는 Inspector 에서 연결한 값이라 EnemyController 의 Awake 보다 먼저 읽어도 준비되어 있다.
    private void Awake()
    {
        CacheComponent();
        Init();
    }

    private void Update()
    {
        UpdateCorpse();
        UpdateReactionTimer();
    }

    public void TakeDamage(float power, bool isImpact)
    {
        if(_isDead) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - power);

        Damaged?.Invoke(isImpact);

        // 이 타격으로 체력이 0 이 된 경우도 강타라면 바로 처치한다.
        if(_data.CanDie && isImpact && _currentHealth <= 0f) Die();
    }

    // 경직한다. 경직 중 다시 걸면 경직 시간을 처음부터 센다.
    // 경직 레벨이 지금의 경직 저항 레벨보다 낮으면 경직하지 않는다.
    // 그로기 중에는 이미 공격을 멈춘 상태이므로 경직하지 않고 그로기를 유지한다.
    public void Stagger(float duration, int staggerLevel)
    {
        if(IsGroggy || staggerLevel < Mathf.Max(_data.BaseStaggerResistLevel, _attackStaggerResistLevel)) return;

        _reactionTimer = duration;
        _reaction = Reaction.Stagger;

        Staggered?.Invoke();
    }

    // 공격 기능이 공격을 시작할 때 그 공격의 경직 저항 레벨로, 공격이 끝나거나 끊기면 0 으로 정한다.
    public void SetAttackStaggerResistLevel(int level) => _attackStaggerResistLevel = level;

    // 그로기 게이지를 쌓고, 최대치에 이르면 그로기가 된다. 그로기 중에는 쌓지 않는다.
    // 이번에 그로기가 되었으면 true 를 돌려준다. 플레이어 쪽이 그로기 진입 순간의 시간 정지를 걸 때 쓴다.
    public bool AddGroggy(float amount)
    {
        if(IsGroggy || amount <= 0f) return false;

        _currentGroggy = Mathf.Min(_currentGroggy + amount, _data.MaxGroggy);
        GroggyGaugeChanged?.Invoke();

        if(_currentGroggy < _data.MaxGroggy) return false;

        EnterGroggy();
        return true;
    }

    // 그로기 중일 때만 건다. 멈춰 있던 남은 시간은 버리고 duration 부터 다시 센다.
    public void HoldGroggy(float duration)
    {
        if(IsGroggy) _groggyHoldTimer = duration;
    }

    public void SetGroggyPaused(bool isPaused) => _isGroggyPaused = isPaused;

    public bool TryConsumeComboSkillChance()
    {
        if(!IsGroggy || !_hasComboSkillChance) return false;

        _hasComboSkillChance = false;
        return true;
    }

    // 그로기 중일 때만 남은 연계를 1 회 쓴다. 남은 연계가 없으면 그대로 둔다.
    public void UseComboChain()
    {
        if(HasRemainingComboChain) _remainingComboChain--;
    }

    // 이번 그로기의 콤보 스킬 기회와 남은 연계를 모두 없앤다.
    public void ClearComboChain()
    {
        _hasComboSkillChance = false;
        _remainingComboChain = 0;
    }

    private void Die()
    {
        _isDead = true;
        _corpseTimer = _data.CorpseDuration;

#if UNITY_EDITOR
        Debug.Log($"[적 처치] {name}");
#endif

        Died?.Invoke();
    }

    // 시체가 남아 있는 시간이 지나면 적 오브젝트를 비활성화한다.
    private void UpdateCorpse()
    {
        if(!_isDead) return;

        _corpseTimer -= EnemyTime.DeltaTime;

        if(_corpseTimer <= 0f) gameObject.SetActive(false);
    }

    // 그로기가 된다. 그로기마다 콤보 스킬 기회가 한 번 생기고, 남은 연계 수를 최대 연계 수로 채운다.
    private void EnterGroggy()
    {
        _reactionTimer = _data.GroggyTime;
        _reaction = Reaction.Groggy;
        _hasComboSkillChance = true;
        _remainingComboChain = _data.MaxComboChain;
        _groggyHoldTimer = 0f;
        _isGroggyPaused = false;

#if UNITY_EDITOR
        Debug.Log($"[적 그로기] {name} {_data.GroggyTime}초");
#endif

        GroggyEntered?.Invoke();
    }

    // 그로기는 선택 중 멈춤 → 정지 시간 → 남은 시간 순서로 줄인다. 정지 시간이 끝난 뒤에만 남은 그로기 시간이 줄어든다.
    private void UpdateReactionTimer()
    {
        if(_reaction == Reaction.None) return;

        if(IsGroggy && _isGroggyPaused) return;

        if(IsGroggy && _groggyHoldTimer > 0f)
        {
            _groggyHoldTimer -= EnemyTime.DeltaTime;
            return;
        }

        _reactionTimer -= EnemyTime.DeltaTime;

        if(_reactionTimer > 0f) return;

        // 그로기가 끝나면 게이지를 0 부터 다시 쌓는다.
        if(IsGroggy)
        {
            _currentGroggy = 0f;
            GroggyGaugeChanged?.Invoke();
        }

        _reaction = Reaction.None;
        Recovered?.Invoke();
    }

    private void CacheComponent()
    {
        _data = GetComponent<EnemyController>().Data;
    }

    private void Init()
    {
        _currentHealth = _data.MaxHealth;
        _currentGroggy = 0f;
    }
}

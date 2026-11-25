using System;
using UnityEngine;

// 적의 체력 · 경직 · 그로기 · 처치 상태와 이 적의 시간(Clock). 다른 기능은 EnemyController 의 인터페이스로 접근한다.
// 처치된 적은 파괴하지 않고 비활성화한다. 플레이어 기능이 인터페이스로 들고 있어 파괴하면 Unity 의 null 판정을 받지 못한다.
// 자식인 체력 막대가 OnEnable 에서 값을 읽기 전에 데이터를 읽도록 실행 순서를 앞당긴다.
// 자세한 흐름 : Docs/Technical/Enemy/EnemyCommon.md
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

    private readonly EnemyClock _clock = new EnemyClock();
    private EnemyData _data;
    private Reaction _reaction;
    private float _corpseTimer;
    private float _reactionTimer;
    // 콤보 스킬 흐름에 맞춰 플레이어 쪽이 건다.
    private float _groggyHoldTimer;
    // 막대 표시 전용 기준 시간. 실제 그로기 시간과 상관없다.
    private float _groggyRatioBase;
    // 공격이 끝나거나 끊기면 0
    private int _attackStaggerResistLevel;
    // 콤보 스킬 시전 · 캔슬마다 1 씩 준다.
    private int _remainingComboChain;
    private bool _isDead;
    private bool _hasComboSkillChance;
    private bool _isGroggyPaused;

    public EnemyClock Clock => _clock;
    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _data.MaxHealth;
    public bool IsAlive => !_isDead;
    public bool IsGroggy => _reaction == Reaction.Groggy;
    public bool HasComboSkillChance => IsGroggy && _hasComboSkillChance;
    public bool HasRemainingComboChain => IsGroggy && _remainingComboChain > 0;
    public bool IsRestricted => _reaction != Reaction.None;
    public float CurrentGroggy => _currentGroggy;
    public float MaxGroggy => _data.MaxGroggy;
    // 콤보 스킬 선택이 열릴 때마다 기준을 그 순간의 남은 시간으로 바꿔 막대가 다시 가득 찬 상태에서 줄어든다.
    public float GroggyTimeRatio => IsGroggy && _groggyRatioBase > 0f ? Mathf.Clamp01(_reactionTimer / _groggyRatioBase) : 0f;

    // 처치되는 타격도 먼저 맞은 것으로 알린 뒤 처치를 알린다.
    public event Action<bool> Damaged;
    public event Action Died;
    public event Action Staggered;
    public event Action GroggyEntered;
    public event Action Recovered;
    public event Action GroggyGaugeChanged;

    // 다른 기능이 Start 에서 읽어도 값이 준비되어 있게 Awake 에서 정한다.
    private void Awake()
    {
        CacheComponent();
        Init();
    }

    // 실행 순서가 앞당겨져 있어, 시간을 먼저 갱신하면 같은 프레임의 다른 기능이 갱신된 시간을 읽는다.
    private void Update()
    {
        _clock.Tick();
        UpdateCorpse();
        UpdateReactionTimer();
    }

    // 그로기 중이면 배율만큼 커진다. 돌려준 값은 대미지 숫자에 쓴다.
    public float TakeDamage(float power, bool isImpact)
    {
        if(_isDead) return 0f;

        float damage = IsGroggy ? power * _data.GroggyDamageMultiplier : power;

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);

        Damaged?.Invoke(isImpact);

        // 체력이 0 이어도 강타에 맞아야 처치된다.
        if(_data.CanDie && isImpact && _currentHealth <= 0f) Die();

        return damage;
    }

    // 그로기 중에는 이미 공격을 멈췄으므로 경직하지 않는다.
    public void Stagger(float duration, int staggerLevel)
    {
        if(IsGroggy || staggerLevel < Mathf.Max(_data.BaseStaggerResistLevel, _attackStaggerResistLevel)) return;

        _reactionTimer = duration;
        _reaction = Reaction.Stagger;

        Staggered?.Invoke();
    }

    public void SetAttackStaggerResistLevel(int level) => _attackStaggerResistLevel = level;

    // 이번에 그로기가 되었으면 true. 플레이어 쪽이 진입 순간의 정지를 걸 때 쓴다.
    public bool AddGroggy(float amount)
    {
        if(IsGroggy || amount <= 0f) return false;

        _currentGroggy = Mathf.Min(_currentGroggy + amount, _data.MaxGroggy);
        GroggyGaugeChanged?.Invoke();

        if(_currentGroggy < _data.MaxGroggy) return false;

        EnterGroggy();
        return true;
    }

    public void HoldGroggy(float duration)
    {
        if(IsGroggy) _groggyHoldTimer = duration;
    }

    // 멈출 때 막대 기준을 지금 남은 시간으로 바꿔 가득 찬 상태에서 다시 줄게 한다.
    public void SetGroggyPaused(bool isPaused)
    {
        _isGroggyPaused = isPaused;

        if(isPaused && IsGroggy) _groggyRatioBase = _reactionTimer;
    }

    public bool TryConsumeComboSkillChance()
    {
        if(!IsGroggy || !_hasComboSkillChance) return false;

        _hasComboSkillChance = false;
        return true;
    }

    public void UseComboChain()
    {
        if(HasRemainingComboChain) _remainingComboChain--;
    }

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
        BattleFeedback.Raise(BattleFeedback.Moment.EnemyKilled);
    }

    private void UpdateCorpse()
    {
        if(!_isDead) return;

        _corpseTimer -= _clock.DeltaTime;

        if(_corpseTimer <= 0f) gameObject.SetActive(false);
    }

    private void EnterGroggy()
    {
        _reactionTimer = _data.GroggyTime;
        _groggyRatioBase = _data.GroggyTime;
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
            _groggyHoldTimer -= _clock.DeltaTime;
            return;
        }

        _reactionTimer -= _clock.DeltaTime;

        if(_reactionTimer > 0f) return;

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

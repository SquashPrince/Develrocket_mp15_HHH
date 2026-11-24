using System;
using UnityEngine;

// 캐릭터마다의 체력 · 에너지 · 필살기 게이지 · 무적과 피격 판정(완벽 회피 · 패링 · 무적 · 피격 · 쓰러짐).
// 히트렉처럼 이 캐릭터만 멈추게 하려고 캐릭터 시간 배율을 가지며, 플레이어 쪽 기능 · 상태는 이 캐릭터 시간을 쓴다.
public class PlayerStatus : MonoBehaviour
{
    [Header("[확인용] 현재 값")]
    [Tooltip("현재 체력. 실행 중 확인용이며, 시작할 때 최대 체력으로 정한다")]
    [SerializeField] private float _currentHealth;
    [Tooltip("현재 에너지. 실행 중 확인용이며, 시작할 때 0 으로 정한다")]
    [SerializeField] private float _currentEnergy;
    [Tooltip("현재 필살기 게이지. 실행 중 확인용이며, 시작할 때 0 으로 정한다")]
    [SerializeField] private float _currentUltimateGauge;
    [Tooltip("캐릭터 시간 배율. 1 이면 원래 속도, 0 이면 멈춤(히트렉). 실행 중 확인용이며, 시작할 때 1 로 정한다")]
    [SerializeField] private float _timeScale = 1f;
    [Tooltip("남은 히트렉 시간(초, 실제 시간). 캐릭터 시간으로 세면 멈춘 동안 줄지 않으므로 실제 시간으로 센다. 실행 중 확인용이다")]
    [SerializeField] private float _hitLagTimer;

    private PlayerController _controller;
    private PlayerActionTrigger _actionTrigger;
    private float _invincibleTimer;
    private float _perfectDodgeTimer;
    private bool _isAttackInvincible;
    // 다른 캐릭터의 필살기 연출 동안 멈춤. 히트렉이 끝나며 배율을 되돌려도 멈춘 채로 남게 따로 둔다.
    private bool _isFrozen;

    public float MaxHealth => _controller.Character.MaxHealth;
    public float MaxEnergy => _controller.Character.MaxEnergy;
    public float MaxUltimateGauge => _controller.Character.MaxUltimateGauge;
    public float CurrentHealth
    {
        get => _currentHealth;
        private set
        {
            _currentHealth = Mathf.Clamp(value, 0f, MaxHealth);
            HealthChanged?.Invoke();
        }
    }
    // 매 프레임 바뀌므로 알림 없이 화면이 매 프레임 읽는다.
    public float CurrentEnergy
    {
        get => _currentEnergy;
        private set => _currentEnergy = Mathf.Clamp(value, 0f, _controller.Character.MaxEnergy);
    }
    public float CurrentUltimateGauge
    {
        get => _currentUltimateGauge;
        private set
        {
            _currentUltimateGauge = Mathf.Clamp(value, 0f, _controller.Character.MaxUltimateGauge);
            UltimateGaugeChanged?.Invoke();
        }
    }
    public bool IsDowned => _currentHealth <= 0f;
    public bool IsUltimateGaugeFull => _currentUltimateGauge >= _controller.Character.MaxUltimateGauge;
    // 시간으로 거는 무적(대시 · 넉백) 또는 시전 중 무적 공격
    public bool IsInvincible => _invincibleTimer > 0f || _isAttackInvincible;
    public bool IsAttackInvincible => _isAttackInvincible;
    public float TimeScale => _isFrozen ? 0f : _timeScale;
    public float DeltaTime => Time.deltaTime * TimeScale;
    // 콤보 입력 유지처럼 "언제까지" 를 재는 시계. 배율이 0 인 동안은 흐르지 않는다.
    public float LocalTime { get; private set; }
    public bool IsHitLag => _hitLagTimer > 0f;

    public event Action HealthChanged;
    public event Action UltimateGaugeChanged;

    // 다른 기능이 Start 에서 읽어도 값이 준비되어 있게 Awake 에서 정한다.
    private void Awake()
    {
        CacheComponent();
        Init();
    }
    private void Update()
    {
        UpdateHitLag();
        LocalTime += DeltaTime;
        UpdateInvincible();
        RegenerateEnergy();
    }

    public void BeginInvincible(float duration)
    {
        _invincibleTimer = Mathf.Max(_invincibleTimer, duration);
    }

    // 무적 시간과 완벽 회피 판정 시간은 같은 값을 쓴다.
    public void BeginDodgeInvincible(float duration)
    {
        BeginInvincible(duration);
        _perfectDodgeTimer = duration;
    }

    public void SetAttackInvincible(bool isInvincible) => _isAttackInvincible = isInvincible;

    public void EndInvincible()
    {
        _invincibleTimer = 0f;
        _perfectDodgeTimer = 0f;
    }

    // 다단 히트로 히트렉 중에 또 맞히면 남은 시간과 새 시간 중 긴 쪽으로 다시 건다.
    public void BeginHitLag(float duration)
    {
        if(duration <= 0f) return;

        _hitLagTimer = Mathf.Max(_hitLagTimer, duration);
        _timeScale = 0f;
    }

    public void SetFrozen(bool isFrozen) => _isFrozen = isFrozen;

    // 필살기처럼 히트렉 중에 바로 원래 속도로 시작해야 하는 행동도 부른다.
    public void EndHitLag()
    {
        _hitLagTimer = 0f;
        _timeScale = 1f;
    }

    public bool CanParry(EnemyAttackStep step) => !IsDowned && _actionTrigger.CanParry(step);

    // 연타 공격은 타격마다 부른다. 맞았으면 true.
    // 판정 순서 : 판 종료 → 쓰러짐 → 완벽 회피 → 패링 → 무적(슬로모션 중 포함) → 피격
    public bool TakeHit(EnemyAttackData attack, EnemyAttackStep step, EnemyAttackData.HitReaction reaction, IEnemyParryable attacker)
    {
        if(_controller.Party.IsBattleEnded || IsDowned) return false;
        if(TryPerfectDodge(attack)) return false;
        if(_actionTrigger.TryParry(attack, step, attacker)) return false;

        if(IsInvincible || _controller.Party.SlowMotion.IsActive)
        {
#if UNITY_EDITOR
            Debug.Log($"[피격 무시] {attack.name} : 무적");
#endif
            return false;
        }

        CurrentHealth = _currentHealth - attack.Power;

        // 쓰러지면 에너지도 모두 잃는다.
        if(IsDowned)
        {
            CurrentEnergy = 0f;
            _controller.StartDowned();
        }
        else _controller.StartHit(attack, reaction, attacker.transform);

        return true;
    }

    public void AddEnergy(float amount) => CurrentEnergy += amount;

    public bool TryConsumeEnergy(float amount)
    {
        if(_currentEnergy < amount) return false;

        CurrentEnergy -= amount;
        return true;
    }

    // 조작 중인 캐릭터가 얻으면 다른 파티원에게도 나눠 준다.
    public void AddUltimateGauge(float amount)
    {
        if(amount <= 0f) return;

        CurrentUltimateGauge += amount;

        if(_controller.Party.Controlled == _controller) _controller.Party.ShareUltimateGauge(_controller, amount);
    }

    // 받은 게이지는 다시 나눠 주지 않는다.
    public void ReceiveSharedUltimateGauge(float amount)
    {
        if(amount <= 0f) return;

        CurrentUltimateGauge += amount;
    }

    public void AddParryGauge() => AddUltimateGauge(_controller.Character.ParryGaugeGain);

    public void ConsumeUltimateGauge()
    {
        CurrentUltimateGauge = 0f;
    }

    // 한 번의 대시 · 회피에서 한 번만 발동하도록 판정을 바로 닫는다. 함께 닿은 공격은 남은 무적으로 무시된다.
    private bool TryPerfectDodge(EnemyAttackData attack)
    {
        if(_perfectDodgeTimer <= 0f || !attack.CanPerfectDodge) return false;

        _perfectDodgeTimer = 0f;

#if UNITY_EDITOR
        Debug.Log($"[완벽 회피] {attack.name}");
#endif

        _controller.Party.SlowMotion.Begin();
        AddUltimateGauge(_controller.Character.PerfectDodgeGaugeGain);
        return true;
    }

    private void UpdateHitLag()
    {
        if(!IsHitLag) return;

        _hitLagTimer -= Time.deltaTime;

        if(_hitLagTimer <= 0f) EndHitLag();
    }

    private void UpdateInvincible()
    {
        if(_invincibleTimer > 0f) _invincibleTimer -= DeltaTime;
        if(_perfectDodgeTimer > 0f) _perfectDodgeTimer -= DeltaTime;
    }

    private void RegenerateEnergy()
    {
        if(IsDowned) return;

        CurrentEnergy += _controller.Character.EnergyRegenPerSecond * DeltaTime;
    }

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _actionTrigger = GetComponent<PlayerActionTrigger>();
    }

    // 확인용 현재 값은 Inspector 에서 바꿔 두었더라도 다시 정한다.
    private void Init()
    {
        CurrentHealth = MaxHealth;
        _currentEnergy = 0f;
        CurrentUltimateGauge = 0f;
        _timeScale = 1f;
        _hitLagTimer = 0f;
        _isFrozen = false;
    }
}

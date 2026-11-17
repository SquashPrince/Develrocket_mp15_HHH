using System;
using UnityEngine;

// 플레이어의 체력 · 에너지 · 필살기 게이지와 무적 시간. 모두 캐릭터마다 따로 가지는 값이다.
// 적 공격에 맞으면 무적인지 확인하고, 무적이 아니면 체력을 줄인 뒤 피격 상태로 넘긴다.
// 대시 · 회피 무적 동안은 완벽 회피 판정도 함께 열려, 완벽 회피할 수 있는 공격이 닿으면 슬로모션을 시작한다.
// 에너지는 시간이 지나거나 공격이 적중하면 쌓이고, 특수스킬을 쓰면 줄어든다.
// 필살기 게이지는 에너지와 별개로 공격 적중 · 패링 성공 · 완벽 회피로 쌓이고, 필살기를 쓸 때 전부 소모한다. 그 외에는 줄어들지 않는다.
// 최대 체력 · 최대 에너지 · 초당 에너지 회복량 · 최대 게이지 · 패링 성공과 완벽 회피의 게이지 획득량은 캐릭터 데이터에서, 공격 적중 획득량은 액션 데이터에서 읽는다.
// 체력이 0이 되었을 때의 쓰러짐 · 실패는 게임 흐름 단계에서 만든다. 지금은 0에서 멈추고 로그만 남긴다.
// 현재 체력 · 에너지 · 게이지는 실행 중 Inspector 에서 바로 볼 수 있다. 확인용이라 실행 전에 넣은 값은 쓰지 않는다(시작할 때 다시 정한다).
// 캐릭터 시간 : 캐릭터마다 시간 배율을 가진다. 플레이어 쪽 기능 · 상태는 Unity 시간 대신 이 캐릭터 시간으로 타이머 · 이동을 계산한다.
//   히트렉처럼 공격한 캐릭터만 멈추게 하기 위해서다. 슬로모션처럼 적에게 거는 전투 효과는 실제 시간을 그대로 쓴다.
// 히트렉 : 공격이 적중하면 동작마다 정한 시간 동안 배율을 0 으로 두고, 끝나면 1 로 되돌린다.
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
    // 다른 캐릭터의 필살기 연출 동안 멈춰 있는지. 히트렉과 따로 두어, 히트렉이 끝나며 배율을 되돌려도 멈춘 채로 남게 한다.
    private bool _isFrozen;

    public float MaxHealth => _controller.Character.MaxHealth;
    public float MaxEnergy => _controller.Character.MaxEnergy;
    public float MaxUltimateGauge => _controller.Character.MaxUltimateGauge;
    // 값을 바꿀 때 항상 0 ~ 최대치 사이로 맞추고, 바뀌었음을 알린다.
    public float CurrentHealth
    {
        get => _currentHealth;
        private set
        {
            _currentHealth = Mathf.Clamp(value, 0f, MaxHealth);
            HealthChanged?.Invoke();
        }
    }
    // 값을 바꿀 때 항상 0 ~ 최대치 사이로 맞춘다. 시간이 지나며 매 프레임 바뀌므로 알림 없이, 화면은 매 프레임 읽는다.
    public float CurrentEnergy
    {
        get => _currentEnergy;
        private set => _currentEnergy = Mathf.Clamp(value, 0f, _controller.Character.MaxEnergy);
    }
    // 값을 바꿀 때 항상 0 ~ 최대치 사이로 맞추고, 바뀌었음을 알린다.
    public float CurrentUltimateGauge
    {
        get => _currentUltimateGauge;
        private set
        {
            _currentUltimateGauge = Mathf.Clamp(value, 0f, _controller.Character.MaxUltimateGauge);
            UltimateGaugeChanged?.Invoke();
        }
    }
    public bool IsUltimateGaugeFull => _currentUltimateGauge >= _controller.Character.MaxUltimateGauge;
    // 시간으로 거는 무적(대시 · 넉백)이나 시전 중 무적 공격의 무적 중인지
    public bool IsInvincible => _invincibleTimer > 0f || _isAttackInvincible;
    public bool IsAttackInvincible => _isAttackInvincible;
    // 캐릭터 시간 배율. 1 이면 원래 속도, 0 이면 멈춤 (히트렉 · 다른 캐릭터의 필살기 연출)
    public float TimeScale => _isFrozen ? 0f : _timeScale;
    // 이번 프레임의 캐릭터 시간(초). Unity 프레임 시간에 캐릭터 시간 배율을 곱한 값이다.
    public float DeltaTime => Time.deltaTime * TimeScale;
    // 캐릭터 시간으로 잰 시계(초). 콤보 입력 유지처럼 "언제까지" 를 재는 데 쓴다. 배율이 0 인 동안은 흐르지 않는다.
    public float LocalTime { get; private set; }
    public bool IsHitLag => _hitLagTimer > 0f;

    // 체력 · 필살기 게이지가 바뀌면 알린다. 화면 표시가 바뀔 때만 다시 그리는 데 쓴다.
    public event Action HealthChanged;
    public event Action UltimateGaugeChanged;

    // 처음 값은 다른 오브젝트를 참조하지 않으므로 Awake 에서 정한다. 화면 등 다른 기능이 시작 단계에서 읽어도 값이 준비되어 있게 하기 위해서다.
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

    // duration 동안 무적이 된다. 이미 더 긴 무적이 남아 있으면 그대로 둔다.
    public void BeginInvincible(float duration)
    {
        _invincibleTimer = Mathf.Max(_invincibleTimer, duration);
    }

    // 대시 · 회피 무적. 무적 시간과 완벽 회피 판정 시간은 같은 값을 쓴다.
    public void BeginDodgeInvincible(float duration)
    {
        BeginInvincible(duration);
        _perfectDodgeTimer = duration;
    }

    // 시전 중 무적인 공격이 휘두르는 동안 켜고, 후딜에 들어가거나 공격 상태를 나가면 끈다.
    public void SetAttackInvincible(bool isInvincible) => _isAttackInvincible = isInvincible;

    public void EndInvincible()
    {
        _invincibleTimer = 0f;
        _perfectDodgeTimer = 0f;
    }

    // duration 동안 이 캐릭터만 멈춘다(캐릭터 시간 배율 0). 적은 멈추지 않는다.
    // 다단 히트로 히트렉 중에 또 맞히면 남은 시간과 새 시간 중 긴 쪽으로 다시 건다.
    public void BeginHitLag(float duration)
    {
        if(duration <= 0f) return;

        _hitLagTimer = Mathf.Max(_hitLagTimer, duration);
        _timeScale = 0f;
    }

    // 다른 캐릭터의 필살기 연출 동안 이 캐릭터를 멈추거나 다시 움직인다. 파티가 시전자를 뺀 캐릭터에게 건다.
    public void SetFrozen(bool isFrozen) => _isFrozen = isFrozen;

    // 히트렉을 끝내고, 겹친 효과와 상관없이 캐릭터 시간 배율을 원래 속도(1)로 되돌린다.
    // 시간이 다 되었을 때와, 필살기처럼 히트렉 중에 바로 원래 속도로 시작해야 하는 행동이 부른다.
    public void EndHitLag()
    {
        _hitLagTimer = 0f;
        _timeScale = 1f;
    }

    // 맞았으면 true 를 돌려준다. 완벽 회피 · 패링했거나 무적이면 무시하고 false 를 돌려준다.
    // 판정 순서 : 완벽 회피 → 패링 → 무적 → 피격
    // step : 판정한 동작. 패링 가능 여부 · 패링 경직 시간은 동작마다 다르다.
    // attacker : 패링 성공을 알리고, 넉백 방향을 정할 때 위치를 쓰는 공격한 적
    public bool TakeHit(EnemyAttackData attack, EnemyAttackStep step, IEnemyParryable attacker)
    {
        if(TryPerfectDodge(attack)) return false;
        if(_actionTrigger.TryParry(attack, step, attacker)) return false;

        if(IsInvincible)
        {
#if UNITY_EDITOR
            Debug.Log($"[피격 무시] {attack.name} : 무적");
#endif
            return false;
        }

        CurrentHealth = _currentHealth - attack.Power;

        _controller.StartHit(attack, attacker.transform);
        return true;
    }

    public void AddEnergy(float amount) => CurrentEnergy += amount;

    // 에너지가 충분하면 소모하고 true 를 돌려준다. 모자라면 소모하지 않는다.
    public bool TryConsumeEnergy(float amount)
    {
        if(_currentEnergy < amount) return false;

        CurrentEnergy -= amount;
        return true;
    }

    public void AddUltimateGauge(float amount)
    {
        if(amount <= 0f) return;

        CurrentUltimateGauge += amount;
    }

    public void AddParryGauge() => AddUltimateGauge(_controller.Character.ParryGaugeGain);

    // 필살기를 쓸 때 게이지를 전부 소모한다.
    public void ConsumeUltimateGauge()
    {
        CurrentUltimateGauge = 0f;
    }

    // 판정 시간 안에 완벽 회피할 수 있는 공격이 닿으면 완벽 회피로 처리하고 슬로모션을 시작한다.
    // 한 번의 대시 · 회피에서 한 번만 발동하도록 판정을 바로 닫는다. 남은 무적 동안 함께 닿은 공격은 무적으로 무시된다.
    private bool TryPerfectDodge(EnemyAttackData attack)
    {
        if(_perfectDodgeTimer <= 0f || !attack.CanPerfectDodge) return false;

        _perfectDodgeTimer = 0f;

#if UNITY_EDITOR
        Debug.Log($"[완벽 회피] {attack.name}");
#endif

        _controller.Party.SlowMotion.Begin(transform.position);
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

    private void RegenerateEnergy() => CurrentEnergy += _controller.Character.EnergyRegenPerSecond * DeltaTime;

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _actionTrigger = GetComponent<PlayerActionTrigger>();
    }

    // 확인용으로 보이는 현재 값은 Inspector 에서 바꿔 두었더라도 시작할 때 다시 정한다.
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

using UnityEngine;

// 플레이어의 체력 · 에너지 · 필살기 게이지와 무적 시간. 모두 캐릭터마다 따로 가지는 값이다.
// 적 공격에 맞으면 무적인지 확인하고, 무적이 아니면 체력을 줄인 뒤 피격 상태로 넘긴다.
// 대시 · 회피 무적 동안은 완벽 회피 판정도 함께 열려, 완벽 회피할 수 있는 공격이 닿으면 슬로모션을 시작한다.
// 에너지는 시간이 지나거나 공격이 적중하면 쌓이고, 특수스킬을 쓰면 줄어든다.
// 필살기 게이지는 에너지와 별개로 공격 적중 · 패링 성공 · 완벽 회피로 쌓이고, 필살기를 쓸 때 전부 소모한다. 그 외에는 줄어들지 않는다.
// 최대 체력 · 최대 에너지 · 초당 에너지 회복량 · 최대 게이지 · 패링 성공과 완벽 회피의 게이지 획득량은 캐릭터 데이터에서, 공격 적중 획득량은 액션 데이터에서 읽는다.
// 체력이 0이 되었을 때의 쓰러짐 · 실패는 게임 흐름 단계에서 만든다. 지금은 0에서 멈추고 로그만 남긴다.
// 현재 체력 · 에너지 · 게이지는 Inspector 를 Debug 모드로 바꾸면 실시간으로 볼 수 있다.
public class PlayerStatus : MonoBehaviour
{
    private PlayerController _controller;
    private PlayerSlowMotion _slowMotion;
    private PlayerParry _parry;
    private float _currentHealth;
    private float _currentEnergy;
    private float _currentUltimateGauge;
    private float _invincibleTimer;
    private float _perfectDodgeTimer;
    private bool _isAttackInvincible;

    public float MaxHealth => _controller.Character.MaxHealth;
    public float CurrentHealth => _currentHealth;
    // 값을 바꿀 때 항상 0 ~ 최대치 사이로 맞춘다.
    public float CurrentEnergy
    {
        get => _currentEnergy;
        private set => _currentEnergy = Mathf.Clamp(value, 0f, _controller.Character.MaxEnergy);
    }
    // 값을 바꿀 때 항상 0 ~ 최대치 사이로 맞춘다.
    public float CurrentUltimateGauge
    {
        get => _currentUltimateGauge;
        private set => _currentUltimateGauge = Mathf.Clamp(value, 0f, _controller.Character.MaxUltimateGauge);
    }
    public bool IsUltimateGaugeFull => _currentUltimateGauge >= _controller.Character.MaxUltimateGauge;
    // 시간으로 거는 무적(대시 · 넉백)이나 시전 중 무적 공격의 무적 중인지
    public bool IsInvincible => _invincibleTimer > 0f || _isAttackInvincible;
    public bool IsAttackInvincible => _isAttackInvincible;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update()
    {
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

    // 맞았으면 true 를 돌려준다. 완벽 회피 · 패링했거나 무적이면 무시하고 false 를 돌려준다.
    // 판정 순서 : 완벽 회피 → 패링 → 무적 → 피격
    // attacker : 패링 성공을 알리고, 넉백 방향을 정할 때 위치를 쓰는 공격한 적
    public bool TakeHit(EnemyAttackSetting attack, IEnemyParryable attacker)
    {
        if(TryPerfectDodge(attack)) return false;
        if(_parry.TryParry(attack, attacker)) return false;

        if(IsInvincible)
        {
#if UNITY_EDITOR
            Debug.Log($"[피격 무시] {attack.Name} : 무적");
#endif
            return false;
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - attack.Power);

#if UNITY_EDITOR
        Debug.Log($"[피격] {attack.Name} ({attack.Reaction}) 위력 {attack.Power} → 체력 {_currentHealth:F0} / {MaxHealth:F0}");
#endif

        _controller.StartHit(attack, attacker.transform.position);
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

#if UNITY_EDITOR
        Debug.Log($"[필살기 게이지] +{amount} → {_currentUltimateGauge:F0} / {_controller.Character.MaxUltimateGauge:F0}");
#endif
    }

    public void AddParryGauge() => AddUltimateGauge(_controller.Character.ParryGaugeGain);

    // 필살기를 쓸 때 게이지를 전부 소모한다.
    public void ConsumeUltimateGauge()
    {
        CurrentUltimateGauge = 0f;

#if UNITY_EDITOR
        Debug.Log("[필살기] 게이지 전부 소모");
#endif
    }

    // 판정 시간 안에 완벽 회피할 수 있는 공격이 닿으면 완벽 회피로 처리하고 슬로모션을 시작한다.
    // 한 번의 대시 · 회피에서 한 번만 발동하도록 판정을 바로 닫는다. 남은 무적 동안 함께 닿은 공격은 무적으로 무시된다.
    private bool TryPerfectDodge(EnemyAttackSetting attack)
    {
        if(_perfectDodgeTimer <= 0f || !attack.CanPerfectDodge) return false;

        _perfectDodgeTimer = 0f;

#if UNITY_EDITOR
        Debug.Log($"[완벽 회피] {attack.Name}");
#endif

        _slowMotion.Begin();
        AddUltimateGauge(_controller.Character.PerfectDodgeGaugeGain);
        return true;
    }

    private void UpdateInvincible()
    {
        if(_invincibleTimer > 0f) _invincibleTimer -= Time.deltaTime;
        if(_perfectDodgeTimer > 0f) _perfectDodgeTimer -= Time.deltaTime;
    }

    private void RegenerateEnergy() => CurrentEnergy += _controller.Character.EnergyRegenPerSecond * Time.deltaTime;

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _slowMotion = GetComponent<PlayerSlowMotion>();
        _parry = GetComponent<PlayerParry>();
    }

    private void Init()
    {
        _currentHealth = MaxHealth;
    }
}

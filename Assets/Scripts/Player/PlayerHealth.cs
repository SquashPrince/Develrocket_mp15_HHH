using UnityEngine;

// 플레이어의 체력과 무적 시간. 적 공격에 맞으면 무적인지 확인하고, 무적이 아니면 체력을 줄인 뒤 피격 상태로 넘긴다.
// 대시 · 회피 무적 동안은 완벽 회피 판정도 함께 열려, 완벽 회피할 수 있는 공격이 닿으면 슬로모션을 시작한다.
// 체력이 0이 되었을 때의 쓰러짐 · 실패는 게임 흐름 단계에서 만든다. 지금은 0에서 멈추고 로그만 남긴다.
// 현재 체력은 Inspector 를 Debug 모드로 바꾸면 실시간으로 볼 수 있다.
public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] private float _maxHealth = 100f;

    private PlayerController _controller;
    private PlayerSlowMotion _slowMotion;
    private PlayerParry _parry;
    private PlayerUltimate _ultimate;
    private float _currentHealth;
    private float _invincibleTimer;
    private float _perfectDodgeTimer;
    private bool _isAttackInvincible;

    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;
    // 시간으로 거는 무적(대시 · 넉백)이나 시전 중 무적 공격의 무적 중인지
    public bool IsInvincible => _invincibleTimer > 0f || _isAttackInvincible;
    public bool IsAttackInvincible => _isAttackInvincible;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update() => UpdateInvincible();

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
    //            일반 적을 만드는 단계에서 적 공통 계약이 필요한지 다시 검토한다.
    public bool TakeHit(EnemyAttackSetting attack, DummyEnemyAttacker attacker)
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
        Debug.Log($"[피격] {attack.Name} ({attack.Reaction}) 위력 {attack.Power} → 체력 {_currentHealth:F0} / {_maxHealth:F0}");
#endif

        _controller.StartHit(attack, attacker.transform.position);
        return true;
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
        _ultimate.AddPerfectDodgeGauge();
        return true;
    }

    private void UpdateInvincible()
    {
        if(_invincibleTimer > 0f) _invincibleTimer -= Time.deltaTime;
        if(_perfectDodgeTimer > 0f) _perfectDodgeTimer -= Time.deltaTime;
    }

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _slowMotion = GetComponent<PlayerSlowMotion>();
        _parry = GetComponent<PlayerParry>();
        _ultimate = GetComponent<PlayerUltimate>();
    }

    private void Init()
    {
        _currentHealth = _maxHealth;
    }
}

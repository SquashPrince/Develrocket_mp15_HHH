using System;
using UnityEngine;

// 적 체력. 플레이어 공격의 위력만큼 체력이 줄고, 체력이 0 인 적은 강타에 맞아야만 처치된다.
// 체력이 0 이어도 처치되기 전까지는 살아 있는 적이라 계속 공격하고, 대상으로 고를 수 있다.
// 처치되지 않게 설정한 적(더미 적)은 체력만 줄고 0 에서 멈춘다.
// 처치된 적은 시체로 남아 있다가 정해진 시간 뒤 비활성화된다. 그동안 Collider 는 그대로 둔다.
// 파괴하지 않고 비활성화하는 이유 : 플레이어 기능이 적을 인터페이스로 들고 있어, 파괴하면 Unity 의 null 판정을 받지 못해
// 남아 있는 참조로 접근할 때 오류가 나기 때문이다. 비활성화한 적은 이후 웨이브에서 다시 쓸 수도 있다.
// 적 오브젝트의 맨 위(루트)에 붙인다. 플레이어는 이 오브젝트를 기준으로 적의 다른 기능을 찾는다.
public class EnemyHealth : MonoBehaviour, IEnemyTargetable, IEnemyDamageable
{
    [SerializeField, Min(1f)] private float _maxHealth = 100f;
    [Tooltip("끄면 체력이 0 이어도 강타에 처치되지 않는다 (더미 적)")]
    [SerializeField] private bool _canDie = true;
    [Tooltip("처치된 뒤 시체가 남아 있다가 사라지기까지의 시간(초, 적 전용 시간)")]
    [SerializeField, Min(0f)] private float _corpseDuration = 3f;

    private float _currentHealth;
    private float _corpseTimer;
    private bool _isDead;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public bool IsAlive => !_isDead;

    // 맞을 때마다 강타 여부와 함께 알린다. 처치되는 타격도 먼저 맞은 것으로 알린 뒤 처치를 알린다.
    public event Action<bool> Damaged;
    // 처치될 때 한 번 알린다.
    public event Action Died;

    private void Start() => Init();
    private void Update() => UpdateCorpse();

    public void TakeDamage(float power, bool isImpact)
    {
        if(_isDead) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - power);

#if UNITY_EDITOR
        Debug.Log($"[적 체력] {name} -{power} → {_currentHealth:F0} / {_maxHealth:F0}");
#endif

        Damaged?.Invoke(isImpact);

        // 이 타격으로 체력이 0 이 된 경우도 강타라면 바로 처치한다.
        if(_canDie && isImpact && _currentHealth <= 0f) Die();
    }

    private void Die()
    {
        _isDead = true;
        _corpseTimer = _corpseDuration;

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

    private void Init()
    {
        _currentHealth = _maxHealth;
    }
}

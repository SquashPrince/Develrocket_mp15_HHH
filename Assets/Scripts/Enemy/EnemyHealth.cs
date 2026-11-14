using System;
using UnityEngine;

// 적 체력. 플레이어 공격의 위력만큼 체력이 줄고, 체력이 0 인 적은 강타에 맞아야만 처치된다.
// 체력이 0 이어도 처치되기 전까지는 살아 있는 적이라 계속 공격하고, 대상으로 고를 수 있다.
// 처치되지 않게 설정한 적(시험용 허수아비 등)은 체력만 줄고 0 에서 멈춘다.
// 처치된 적은 시체로 남아 있다가 정해진 시간 뒤 비활성화된다. 그동안 Collider 는 그대로 둔다.
// 파괴하지 않고 비활성화하는 이유 : 플레이어 기능이 적을 인터페이스로 들고 있어, 파괴하면 Unity 의 null 판정을 받지 못해
// 남아 있는 참조로 접근할 때 오류가 나기 때문이다. 비활성화한 적은 이후 웨이브에서 다시 쓸 수도 있다.
// 적 오브젝트의 맨 위(루트)에 붙인다. 플레이어는 이 오브젝트를 기준으로 적의 다른 기능을 찾는다.
// 대상 정보 : 대상으로 고를 수 있는 적의 정보(생존 여부 · 몸 반지름 · 록온 표시 위치)도 이 컴포넌트가 알려준다.
//   몸 반지름은 시작할 때 같은 오브젝트의 콜라이더로 한 번 재 둔다. 연출 · 도약 중 콜라이더가 꺼져도 값이 유지된다.
public class EnemyHealth : MonoBehaviour, IEnemyTargetable, IEnemyDamageable
{
    [SerializeField, Min(1f)] private float _maxHealth = 100f;
    [Tooltip("끄면 체력이 0 이어도 강타에 처치되지 않는다 (시험용 허수아비 등)")]
    [SerializeField] private bool _canDie = true;
    [Tooltip("처치된 뒤 시체가 남아 있다가 사라지기까지의 시간(초, 적 전용 시간)")]
    [SerializeField, Min(0f)] private float _corpseDuration = 3f;
    [Tooltip("록온 표시를 띄울 위치 (머리 · 가슴 등에 둔 빈 자식 오브젝트). 비워 두면 타겟팅 기능의 기본 높이에 띄운다")]
    [SerializeField] private Transform _lockOnPoint;

    [Header("[확인용] 현재 값")]
    [Tooltip("현재 체력. 실행 중 확인용이며, 시작할 때 최대 체력으로 정한다")]
    [SerializeField] private float _currentHealth;

    private float _corpseTimer;
    private float _radius;
    private bool _isDead;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public bool IsAlive => !_isDead;
    public float Radius => _radius;
    public Transform LockOnPoint => _lockOnPoint;

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

        if(TryGetComponent(out Collider bodyCollider)) _radius = BodyRadius.Measure(bodyCollider);
    }
}

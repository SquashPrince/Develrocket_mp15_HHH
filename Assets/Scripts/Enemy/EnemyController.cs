using UnityEngine;

// 적의 대표 컴포넌트. 적 오브젝트의 맨 위(루트)에 붙인다.
// 적 데이터를 이 컴포넌트 한 곳에서 받고, 적의 다른 기능은 이 컴포넌트를 거쳐 데이터를 읽는다.
// 다른 기능(플레이어 등)이 적을 찾을 때 쓰는 인터페이스(대상 · 대미지 · 경직 · 그로기)도 이 컴포넌트가 모두 가진다.
//   실제 처리는 EnemyState 가 하고, 이 컴포넌트는 그대로 넘겨준다. 패링(IEnemyParryable)은 공격 흐름을 아는 EnemyAttacker 가 가진다.
// 대상 정보 : 몸 반지름은 시작할 때 같은 오브젝트의 콜라이더로 한 번 재 둔다. 연출 · 도약 중 콜라이더가 꺼져도 값이 유지된다.
[RequireComponent(typeof(EnemyState))]
public class EnemyController : MonoBehaviour, IEnemyTargetable, IEnemyDamageable, IEnemyStaggerable, IEnemyGroggyable
{
    [Tooltip("이 적 종류의 값")]
    [SerializeField] private EnemyData _data;
    [Tooltip("록온 표시를 띄울 위치 (머리 · 가슴 등에 둔 빈 자식 오브젝트). 비워 두면 타겟팅 기능의 기본 높이에 띄운다")]
    [SerializeField] private Transform _lockOnPoint;

    private EnemyState _state;
    private float _radius;

    public EnemyData Data => _data;
    public EnemyState State => _state;
    public bool IsAlive => _state.IsAlive;
    public float Radius => _radius;
    public Transform LockOnPoint => _lockOnPoint;
    public bool IsGroggy => _state.IsGroggy;
    public bool HasComboSkillChance => _state.HasComboSkillChance;
    public bool HasRemainingComboChain => _state.HasRemainingComboChain;

    private void Awake() => CacheComponent();
    // 몸 반지름은 콜라이더 경계가 잡힌 뒤에 재도록 Start 에서 잰다.
    private void Start() => Init();

    public void TakeDamage(float power, bool isImpact) => _state.TakeDamage(power, isImpact);
    public void Stagger(float duration, int staggerLevel) => _state.Stagger(duration, staggerLevel);
    public bool AddGroggy(float amount) => _state.AddGroggy(amount);
    public bool TryConsumeComboSkillChance() => _state.TryConsumeComboSkillChance();
    public void UseComboChain() => _state.UseComboChain();
    public void ClearComboChain() => _state.ClearComboChain();
    public void HoldGroggy(float duration) => _state.HoldGroggy(duration);
    public void SetGroggyPaused(bool isPaused) => _state.SetGroggyPaused(isPaused);

    private void CacheComponent()
    {
        _state = GetComponent<EnemyState>();
    }

    private void Init()
    {
        if(TryGetComponent(out Collider bodyCollider)) _radius = BodyRadius.Measure(bodyCollider);
    }
}

using UnityEngine;
using UnityEngine.Serialization;

// 적의 대표 컴포넌트. 적 오브젝트의 맨 위(루트)에 붙인다.
// 적 데이터를 이 컴포넌트 한 곳에서 받고, 적의 다른 기능은 이 컴포넌트를 거쳐 데이터를 읽는다.
// 다른 기능(플레이어 등)이 적을 찾을 때 쓰는 인터페이스(대상 · 대미지 · 경직 · 그로기)도 이 컴포넌트가 모두 가진다.
//   실제 처리는 EnemyState 가 하고, 이 컴포넌트는 그대로 넘겨준다. 패링(IEnemyParryable)은 공격 흐름을 아는 EnemyAttacker 가 가진다.
// 대상 정보 : 몸 반지름은 시작할 때 같은 오브젝트의 콜라이더로 한 번 재 둔다. 연출 · 도약 중 콜라이더가 꺼져도 값이 유지된다.
// 적 바 : 등장할 때 자기 적 바 Prefab 을 HUD 아래에 만들고 자신을 대상으로 넘겨준다. 등급으로 나누지 않고, 어떤 HUD 를 연결했는지에 따라 보이는 방식이 정해진다.
//   보스는 화면 Canvas(보스 바), 일반 · 엘리트는 자기 몸에 붙인 월드 공간 Canvas(머리 위 바)를 HUD 로 연결한다.
//   지금은 오브젝트가 켜지는 순간을 등장으로 본다. 다시 켜져도 이미 만든 바를 그대로 쓴다.
//   화면 Canvas 는 씬 오브젝트라, 씬에 놓은 보스면 Inspector 에서 연결한다. 실행 중 생성한 보스는 생성한 쪽이 SetHudRoot 로 넘겨준다.
//   HUD 가 비어 있으면 켜질 때 바를 만들지 않고, HUD 를 넘겨받는 순간 만든다. Prefab 에는 씬의 Canvas 를 연결할 수 없기 때문이다.
[RequireComponent(typeof(EnemyState))]
public class EnemyController : MonoBehaviour, IEnemyTargetable, IEnemyDamageable, IEnemyStaggerable, IEnemyGroggyable
{
    [Tooltip("이 적 종류의 값")]
    [SerializeField] private EnemyData _data;
    [Tooltip("록온 표시를 띄울 위치 (머리 · 가슴 등에 둔 빈 자식 오브젝트). 비워 두면 타겟팅 기능의 기본 높이에 띄운다")]
    [SerializeField] private Transform _lockOnPoint;

    [Header("적 바")]
    [Tooltip("등장할 때 만들 적 바 Prefab. 보스는 보스 바, 일반 · 엘리트는 머리 위 바를 연결한다")]
    [FormerlySerializedAs("_bossGaugePrefab")]
    [SerializeField] private EnemyGauge _gaugePrefab;
    [Tooltip("적 바를 띄울 HUD. 보스는 씬의 화면 Canvas, 일반 · 엘리트는 자기 몸에 붙인 월드 공간 Canvas 를 연결한다")]
    [SerializeField] private Transform _hudRoot;

    private EnemyState _state;
    // 이미 만든 적 바. 다시 켜질 때 또 만들지 않도록 들고 있는다.
    private EnemyGauge _gauge;
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
    private void OnEnable() => ShowGauge();
    // 몸 반지름은 콜라이더 경계가 잡힌 뒤에 재도록 Start 에서 잰다.
    private void Start() => Init();

    public float TakeDamage(float power, bool isImpact) => _state.TakeDamage(power, isImpact);
    public void BeginHitStop(float duration) => _state.Clock.BeginHitStop(duration);
    public void Stagger(float duration, int staggerLevel) => _state.Stagger(duration, staggerLevel);
    public bool AddGroggy(float amount) => _state.AddGroggy(amount);
    public bool TryConsumeComboSkillChance() => _state.TryConsumeComboSkillChance();
    public void UseComboChain() => _state.UseComboChain();
    public void ClearComboChain() => _state.ClearComboChain();
    public void HoldGroggy(float duration) => _state.HoldGroggy(duration);
    public void SetGroggyPaused(bool isPaused) => _state.SetGroggyPaused(isPaused);

    // 실행 중 생성한 보스에 생성한 쪽이 화면 Canvas 를 넘겨준다. 넘겨받는 순간 적 바를 만든다.
    public void SetHudRoot(Transform hudRoot)
    {
        _hudRoot = hudRoot;
        ShowGauge();
    }

    // 등장할 때 적 바를 HUD 아래에 한 번 만들고 자신을 대상으로 넘겨준다. HUD 를 아직 받지 않았으면 만들지 않는다.
    private void ShowGauge()
    {
        if(_gauge != null || _hudRoot == null) return;

        _gauge = Instantiate(_gaugePrefab, _hudRoot);
        _gauge.SetTarget(_state);
    }

    private void CacheComponent()
    {
        _state = GetComponent<EnemyState>();
    }

    private void Init()
    {
        if(TryGetComponent(out Collider bodyCollider)) _radius = BodyRadius.Measure(bodyCollider);
    }
}

using UnityEngine;
using UnityEngine.Serialization;

// 적 루트의 대표 컴포넌트. 적 데이터를 한 곳에서 받고, 다른 기능이 적에 접근하는 인터페이스를 모두 가진다.
// 실제 처리는 EnemyState 에 넘기고, 패링(IEnemyParryable)만 공격 흐름을 아는 EnemyAttacker 가 가진다.
// 적 바는 연결한 HUD 에 따라 보스 바(화면 Canvas) 또는 머리 위 바(월드 공간 Canvas)가 된다.
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
    // 다시 켜질 때 또 만들지 않도록 들고 있는다.
    private EnemyGauge _gauge;
    private float _radius;

    public EnemyData Data => _data;
    public EnemyState State => _state;
    // 공격 권한 · 판정 Layer · 다른 적 목록을 묻는 곳. 배틀 매니저가 등록할 때 넣어 준다.
    public BattleManager Battle { get; private set; }
    public bool IsAlive => _state.IsAlive;
    public float Radius => _radius;
    public Transform LockOnPoint => _lockOnPoint;
    public bool IsGroggy => _state.IsGroggy;
    public bool HasComboSkillChance => _state.HasComboSkillChance;
    public bool HasRemainingComboChain => _state.HasRemainingComboChain;

    private void Awake() => CacheComponent();
    private void OnEnable() => ShowGauge();
    // 몸 반지름은 콜라이더 경계가 잡힌 뒤 한 번 재 두어, 연출 · 도약 중 콜라이더가 꺼져도 값이 유지된다.
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

    public void JoinBattle(BattleManager battle) => Battle = battle;

    // Prefab 에는 씬의 Canvas 를 연결할 수 없어, 실행 중 생성한 보스는 생성한 쪽이 넘겨준다.
    public void SetHudRoot(Transform hudRoot)
    {
        _hudRoot = hudRoot;
        ShowGauge();
    }

    // HUD 를 아직 받지 않았으면 넘겨받는 순간 만든다.
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

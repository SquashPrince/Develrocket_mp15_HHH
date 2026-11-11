using UnityEngine;

// 공격하는 더미 적. 공격하지 않는 더미 적과 같은 오브젝트에 붙여, 타겟팅 · 적중은 그대로 두고 공격만 더한다.
// 흐름 : 대기(공격 간격) → 예고(대상 쪽으로 돌고 색 · 크기 변화) → 판정 대기 → 판정(앞쪽 범위) → 다음 공격으로 대기
// 패링 공격은 예고 시작부터 판정까지 패링 가능 시간을 연다.
// 경직 · 그로기는 적 반응 기능이 맡고, 이 기능은 알림을 받아 진행 중인 공격을 끊고 경직 · 그로기 색으로 멈춘다.
// 경직 · 그로기가 끝나면 공격 간격만큼 기다린 뒤 공격을 이어간다.
// 예고 · 경직 · 그로기 색은 상태 표시라 더미 적의 기본 색으로 정하고, 피격 깜빡임은 더미 적이 따로 처리한다.
// 공격 목록을 순서대로 반복한다. 기획서 기준 약공격 → 강공격 → 패링 공격 순서로 둔다.
// 5묶음에서 비헤이비어 트리 적으로 대체한다.
[RequireComponent(typeof(DummyEnemy), typeof(EnemyReaction))]
public class DummyEnemyAttacker : MonoBehaviour, IEnemyParryable
{
    // 한 번의 판정에서 검사할 수 있는 최대 콜라이더 수
    private const int MAX_HIT_COLLIDERS = 8;

    private enum Phase
    {
        Idle,
        Telegraph,
        HitDelay,
        // 경직 · 그로기로 멈춘 상태. 끝나는 시점은 적 반응 기능이 알린다.
        Restricted
    }

    [Tooltip("예고 때 크기를 바꿀 몸체")]
    [SerializeField] private Renderer _renderer;
    [Tooltip("공격 대상 (Player). 예고를 시작할 때 이 대상 쪽으로 돈다")]
    [SerializeField] private Transform _target;
    [Tooltip("판정에 걸릴 Layer (Player)")]
    [SerializeField] private LayerMask _targetLayers = ~0;
    [Tooltip("한 공격의 판정이 끝나고 다음 예고가 시작되기까지의 시간(초)")]
    [SerializeField, Min(0f)] private float _attackInterval = 2f;
    [Tooltip("경직하는 동안의 몸 색")]
    [SerializeField] private Color _staggerColor = Color.gray;
    [Tooltip("그로기 동안의 몸 색")]
    [SerializeField] private Color _groggyColor = new Color(0.6f, 0.2f, 0.9f);
    [Tooltip("순서대로 반복할 공격 목록")]
    [SerializeField] private EnemyAttackSetting[] _attacks = { new EnemyAttackSetting(), new EnemyAttackSetting(), new EnemyAttackSetting() };

    private readonly Collider[] _hitColliders = new Collider[MAX_HIT_COLLIDERS];
    private DummyEnemy _enemy;
    private EnemyReaction _reaction;
    private Vector3 _originalScale;
    private Phase _phase;
    private float _timer;
    private int _attackIndex;
    private bool _isParryWindowOpen;

    public bool IsParryWindowOpen => _isParryWindowOpen;

    private EnemyAttackSetting CurrentAttack => _attacks[_attackIndex];
    // 예고를 시작해 판정이 나기 전까지가 공격 중이다. 판정 순간 패링당해도 판정 대기 단계에서 경직이 들어온다.
    private bool IsAttacking => _phase == Phase.Telegraph || _phase == Phase.HitDelay;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void OnEnable() => BindReactionEvent();
    private void OnDisable()
    {
        CloseParryWindow();
        UnbindReactionEvent();
    }
    private void Update() => UpdateAttack();
    private void OnDrawGizmosSelected() => DrawHitRanges();

    private void UpdateAttack()
    {
        if(_attacks.Length == 0 || _phase == Phase.Restricted) return;

        // 완벽 회피 슬로모션 동안 느려지도록 적 전용 시간을 쓴다.
        _timer -= EnemyTime.DeltaTime;

        if(_timer > 0f) return;

        switch(_phase)
        {
            case Phase.Idle:
                BeginTelegraph();
                break;
            case Phase.Telegraph:
                EndTelegraph();
                break;
            case Phase.HitDelay:
                PerformAttack();
                break;
        }
    }

    // 경직하면 공격 중이었던 공격을 끊고, 경직이 끝나면 다음 공격으로 넘어간다. 공격 중이 아니었으면 원래 하려던 공격을 한다.
    private void OnStaggered() => Restrict(_staggerColor);

    // 그로기가 되면 경직과 같이 공격 중이었던 공격을 끊는다.
    private void OnGroggyEntered() => Restrict(_groggyColor);

    // 경직 · 그로기가 끝나면 원래 색으로 돌아가 공격 간격만큼 기다린 뒤 공격을 이어간다.
    private void OnRecovered()
    {
        _enemy.ResetBaseColor();
        _timer = _attackInterval;
        _phase = Phase.Idle;
    }

    private void Restrict(Color color)
    {
        InterruptAttack();
        _enemy.SetBaseColor(color);
        _phase = Phase.Restricted;
    }

    // 진행 중인 공격을 끊는다. 공격 중(예고 · 판정 대기)이었으면 끊긴 공격은 소모된 것으로 보고 다음 공격으로 넘긴다.
    // 공격 전 대기 · 경직 · 그로기 중이면 끊을 공격이 없으므로 공격 순서는 그대로 둔다.
    private void InterruptAttack()
    {
        CloseParryWindow();
        _renderer.transform.localScale = _originalScale;

        if(IsAttacking) _attackIndex = (_attackIndex + 1) % _attacks.Length;
    }

    private void BeginTelegraph()
    {
        FaceTarget();

        if(CurrentAttack.UseTelegraphColor) _enemy.SetBaseColor(CurrentAttack.TelegraphColor);
        if(CurrentAttack.IsParryable) OpenParryWindow();

        _renderer.transform.localScale = _originalScale * CurrentAttack.TelegraphScale;
        _timer = CurrentAttack.TelegraphTime;
        _phase = Phase.Telegraph;
    }

    // 예고 표시를 끄고 판정까지 기다린다. 패링 가능 시간은 판정 순간까지 열어 둔다.
    private void EndTelegraph()
    {
        _enemy.ResetBaseColor();
        _renderer.transform.localScale = _originalScale;
        _timer = CurrentAttack.HitDelay;
        _phase = Phase.HitDelay;
    }

    private void PerformAttack()
    {
        CloseParryWindow();
        HitTarget(CurrentAttack);

        // 판정 중 패링당해 경직했으면 경직 알림에서 다음 공격을 정했으므로 그대로 둔다.
        if(_phase == Phase.Restricted) return;

        _attackIndex = (_attackIndex + 1) % _attacks.Length;
        _timer = _attackInterval;
        _phase = Phase.Idle;
    }

    // 판정 구 안에 플레이어가 있으면 한 번 맞힌다. 콜라이더가 여러 개여도 플레이어는 하나이므로 처음 찾은 대상만 맞힌다.
    private void HitTarget(EnemyAttackSetting attack)
    {
        Vector3 center = transform.position + transform.rotation * attack.HitCenter;
        int count = Physics.OverlapSphereNonAlloc(center, attack.HitRadius, _hitColliders, _targetLayers, QueryTriggerInteraction.Ignore);

        for(int i = 0; i < count; i++)
        {
            PlayerHealth health = _hitColliders[i].GetComponentInParent<PlayerHealth>();

            if(health == null) continue;

            health.TakeHit(attack, this);
            return;
        }
    }

    private void OpenParryWindow()
    {
        if(_isParryWindowOpen) return;

        ParryWindow.Open(this);
        _isParryWindowOpen = true;
    }

    private void CloseParryWindow()
    {
        if(!_isParryWindowOpen) return;

        ParryWindow.Close(this);
        _isParryWindowOpen = false;
    }

    private void FaceTarget()
    {
        if(_target == null) return;

        Vector3 toTarget = _target.position - transform.position;
        toTarget.y = 0f;

        if(toTarget != Vector3.zero) transform.rotation = Quaternion.LookRotation(toTarget);
    }

    // Scene 창에서 더미 적을 선택하면 공격별 판정 범위를 보여준다. 패링 공격은 노란색, 나머지는 빨간색이다.
    private void DrawHitRanges()
    {
        foreach(EnemyAttackSetting attack in _attacks)
        {
            Gizmos.color = attack.IsParryable ? Color.yellow : Color.red;
            Gizmos.DrawWireSphere(transform.position + transform.rotation * attack.HitCenter, attack.HitRadius);
        }
    }

#if UNITY_EDITOR
    // [테스트] 플레이 중 컴포넌트 우클릭 메뉴에서 지금 공격의 패링 경직을 바로 건다.
    [ContextMenu("Test Stagger")]
    private void TestStagger() => _reaction.Stagger(CurrentAttack.ParryStaggerTime);
#endif

    private void CacheComponent()
    {
        _enemy = GetComponent<DummyEnemy>();
        _reaction = GetComponent<EnemyReaction>();
    }

    private void Init()
    {
        _originalScale = _renderer.transform.localScale;
        _timer = _attackInterval;
        _phase = Phase.Idle;
    }

    private void BindReactionEvent()
    {
        _reaction.Staggered += OnStaggered;
        _reaction.GroggyEntered += OnGroggyEntered;
        _reaction.Recovered += OnRecovered;
    }

    private void UnbindReactionEvent()
    {
        _reaction.Staggered -= OnStaggered;
        _reaction.GroggyEntered -= OnGroggyEntered;
        _reaction.Recovered -= OnRecovered;
    }
}

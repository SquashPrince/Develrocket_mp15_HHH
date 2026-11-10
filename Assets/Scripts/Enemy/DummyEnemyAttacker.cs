using UnityEngine;

// 공격하는 더미 적. 공격하지 않는 더미 적과 같은 오브젝트에 붙여, 타겟팅 · 적중은 그대로 두고 공격만 더한다.
// 흐름 : 대기(공격 간격) → 예고(대상 쪽으로 돌고 색 · 크기 변화) → 판정 대기 → 판정(앞쪽 범위) → 다음 공격으로 대기
// 패링 공격은 예고 시작부터 판정까지 패링 가능 시간을 연다. 패링당하면 경직 시간 동안 회색으로 멈췄다가 다음 공격으로 넘어간다.
// 그로기가 되면 그로기 시간 동안 그로기 색으로 공격을 멈춘다. 그로기 중에는 경직하지 않는다.
// 예고 · 경직 · 그로기 색은 상태 표시라 더미 적의 기본 색으로 정하고, 피격 깜빡임은 더미 적이 따로 처리한다.
// 공격 목록을 순서대로 반복한다. 기획서 기준 약공격 → 강공격 → 패링 공격 순서로 둔다.
// 5묶음에서 비헤이비어 트리 적으로 대체한다.
[RequireComponent(typeof(DummyEnemy))]
public class DummyEnemyAttacker : MonoBehaviour
{
    // 한 번의 판정에서 검사할 수 있는 최대 콜라이더 수
    private const int MAX_HIT_COLLIDERS = 8;

    private enum Phase
    {
        Idle,
        Telegraph,
        HitDelay,
        Stagger,
        Groggy
    }

    [Tooltip("예고 때 크기를 바꿀 몸체")]
    [SerializeField] private Renderer _renderer;
    [Tooltip("공격 대상 (Player). 예고를 시작할 때 이 대상 쪽으로 돈다")]
    [SerializeField] private Transform _target;
    [Tooltip("판정에 걸릴 Layer (Player)")]
    [SerializeField] private LayerMask _targetLayers = ~0;
    [Tooltip("한 공격의 판정이 끝나고 다음 예고가 시작되기까지의 시간(초)")]
    [SerializeField, Min(0f)] private float _attackInterval = 2f;
    [Tooltip("패링당해 경직하는 동안의 몸 색")]
    [SerializeField] private Color _staggerColor = Color.gray;
    [Tooltip("그로기 동안의 몸 색")]
    [SerializeField] private Color _groggyColor = new Color(0.6f, 0.2f, 0.9f);
    [Tooltip("그로기가 유지되는 시간(초, 적 전용 시간)")]
    [SerializeField, Min(0f)] private float _groggyTime = 12f;
    [Tooltip("순서대로 반복할 공격 목록")]
    [SerializeField] private EnemyAttackSetting[] _attacks = { new EnemyAttackSetting(), new EnemyAttackSetting(), new EnemyAttackSetting() };

    private readonly Collider[] _hitColliders = new Collider[MAX_HIT_COLLIDERS];
    private DummyEnemy _enemy;
    private Vector3 _originalScale;
    private Phase _phase;
    private float _timer;
    private int _attackIndex;
    private bool _isParryWindowOpen;
    // 이번 그로기에서 콤보 스킬을 아직 발동하지 않았는지. 그로기가 될 때마다 한 번 생긴다.
    private bool _hasComboSkillChance;

    public bool IsGroggy => _phase == Phase.Groggy;

    private EnemyAttackSetting CurrentAttack => _attacks[_attackIndex];
    // 예고를 시작해 판정이 나기 전까지가 공격 중이다. 판정 순간 패링당해도 판정 대기 단계에서 경직이 들어온다.
    private bool IsAttacking => _phase == Phase.Telegraph || _phase == Phase.HitDelay;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update() => UpdateAttack();
    private void OnDisable() => CloseParryWindow();
    private void OnDrawGizmosSelected() => DrawHitRanges();

    // 경직한다. 공격 중이었으면 그 공격을 끊고 경직이 끝나면 다음 공격으로, 아니면 원래 하려던 공격을 한다.
    // 그로기 중에는 이미 공격을 멈춘 상태이므로 경직하지 않고 그로기를 유지한다. 피격 깜빡임은 그대로 나온다.
    public void Stagger(float duration)
    {
        if(IsGroggy) return;

        InterruptAttack();
        _enemy.SetBaseColor(_staggerColor);
        _timer = duration;
        _phase = Phase.Stagger;

#if UNITY_EDITOR
        Debug.Log($"[적 경직] {name} {duration}초");
#endif
    }

    // 그로기가 된다. 공격 중이었으면 경직과 같이 그 공격을 끊는다. 그로기 중 다시 걸면 그로기 시간을 처음부터 센다.
    public void EnterGroggy()
    {
        InterruptAttack();
        _enemy.SetBaseColor(_groggyColor);
        _timer = _groggyTime;
        _phase = Phase.Groggy;
        _hasComboSkillChance = true;

#if UNITY_EDITOR
        Debug.Log($"[적 그로기] {name} {_groggyTime}초");
#endif
    }

    // 그로기 중이고 이번 그로기에서 콤보 스킬을 아직 발동하지 않았으면 기회를 소모하고 true 를 돌려준다.
    public bool TryConsumeComboSkillChance()
    {
        if(!IsGroggy || !_hasComboSkillChance) return false;

        _hasComboSkillChance = false;
        return true;
    }

    private void UpdateAttack()
    {
        if(_attacks.Length == 0) return;

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
            case Phase.Stagger:
            case Phase.Groggy:
                EndRestriction();
                break;
        }
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

        // 판정 중 패링당해 경직했으면 경직 쪽에서 다음 공격과 시간을 정했으므로 그대로 둔다.
        if(_phase == Phase.Stagger) return;

        _attackIndex = (_attackIndex + 1) % _attacks.Length;
        _timer = _attackInterval;
        _phase = Phase.Idle;
    }

    // 경직 · 그로기가 끝나면 원래 색으로 돌아가 공격 간격만큼 기다린 뒤 공격을 이어간다.
    private void EndRestriction()
    {
        _enemy.ResetBaseColor();
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
    private void TestStagger() => Stagger(CurrentAttack.ParryStaggerTime);
#endif

    private void CacheComponent()
    {
        _enemy = GetComponent<DummyEnemy>();
    }

    private void Init()
    {
        _originalScale = _renderer.transform.localScale;
        _timer = _attackInterval;
        _phase = Phase.Idle;
    }
}

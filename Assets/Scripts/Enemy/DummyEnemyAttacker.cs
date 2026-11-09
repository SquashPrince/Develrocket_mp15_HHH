using UnityEngine;

// 공격하는 더미 적. 공격하지 않는 더미 적과 같은 오브젝트에 붙여, 타겟팅 · 적중은 그대로 두고 공격만 더한다.
// 흐름 : 대기(공격 간격) → 예고(대상 쪽으로 돌고 색 · 크기 변화) → 판정(예고가 끝나는 순간 앞쪽 범위) → 다음 공격으로 대기
// 공격 목록을 순서대로 반복한다. 기획서 기준 약공격 → 강공격 → 패링 공격 순서로 둔다.
// 5묶음에서 비헤이비어 트리 적으로 대체한다.
[RequireComponent(typeof(DummyEnemy))]
public class DummyEnemyAttacker : MonoBehaviour
{
    // 한 번의 판정에서 검사할 수 있는 최대 콜라이더 수
    private const int MAX_HIT_COLLIDERS = 8;

    [Tooltip("예고 때 색 · 크기를 바꿀 몸체")]
    [SerializeField] private Renderer _renderer;
    [Tooltip("공격 대상 (Player). 예고를 시작할 때 이 대상 쪽으로 돈다")]
    [SerializeField] private Transform _target;
    [Tooltip("판정에 걸릴 Layer (Player)")]
    [SerializeField] private LayerMask _targetLayers = ~0;
    [Tooltip("한 공격의 판정이 끝나고 다음 예고가 시작되기까지의 시간(초)")]
    [SerializeField, Min(0f)] private float _attackInterval = 2f;
    [Tooltip("순서대로 반복할 공격 목록")]
    [SerializeField] private EnemyAttackSetting[] _attacks = { new EnemyAttackSetting(), new EnemyAttackSetting(), new EnemyAttackSetting() };

    private readonly Collider[] _hitColliders = new Collider[MAX_HIT_COLLIDERS];
    private Color _originalColor;
    private Vector3 _originalScale;
    private float _timer;
    private int _attackIndex;
    private bool _isTelegraphing;

    private EnemyAttackSetting CurrentAttack => _attacks[_attackIndex];

    private void Start() => Init();
    private void Update() => UpdateAttack();
    private void OnDrawGizmosSelected() => DrawHitRanges();

    private void UpdateAttack()
    {
        if(_attacks.Length == 0) return;

        // 완벽 회피 슬로모션 동안 느려지도록 적 전용 시간을 쓴다.
        _timer -= EnemyTime.DeltaTime;

        if(_timer > 0f) return;

        if(_isTelegraphing) PerformAttack();
        else BeginTelegraph();
    }

    private void BeginTelegraph()
    {
        FaceTarget();

        if(CurrentAttack.UseTelegraphColor) _renderer.material.color = CurrentAttack.TelegraphColor;

        _renderer.transform.localScale = _originalScale * CurrentAttack.TelegraphScale;
        _timer = CurrentAttack.TelegraphTime;
        _isTelegraphing = true;
    }

    private void PerformAttack()
    {
        _renderer.material.color = _originalColor;
        _renderer.transform.localScale = _originalScale;

        HitTarget(CurrentAttack);

        _attackIndex = (_attackIndex + 1) % _attacks.Length;
        _timer = _attackInterval;
        _isTelegraphing = false;
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

            health.TakeHit(attack, transform.position);
            return;
        }
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

    private void Init()
    {
        _originalColor = _renderer.material.color;
        _originalScale = _renderer.transform.localScale;
        _timer = _attackInterval;
    }
}

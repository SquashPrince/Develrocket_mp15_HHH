using UnityEngine;

// 이 캐릭터 기준으로 공격 · 록온 대상을 찾는다. 캐릭터 위치에 따라 달라 캐릭터마다 둔다.
// 록온 상태는 플레이어 전체에 하나라 파티의 록온 기능이 들고, 여기서는 읽기만 한다.
// 자세한 흐름 : Docs/Technical/Player/PlayerTargeting.md
[RequireComponent(typeof(PlayerController))]
public class PlayerTargeting : MonoBehaviour
{
    private const int MAX_CANDIDATE_COLLIDERS = 32;

    private readonly Collider[] _candidateColliders = new Collider[MAX_CANDIDATE_COLLIDERS];
    private PlayerController _controller;

    // 교체해도 같은 대상이다.
    public IEnemyTargetable LockTarget => _controller.Party.LockOn.LockTarget;
    public float AttackRange => Common.AttackRange;
    public float LockOnRange => Common.LockOnRange;
    private CommonCharacterSettings Common => _controller.Common;
    private Camera Camera => _controller.Party.MainCamera;

    private void Awake() => CacheComponent();
    private void OnDrawGizmosSelected() => DrawRange();

    // "누구를 노릴지"는 모두 이 순서를 따른다 : 록온 대상 → 우선 대상 → range 안의 자동 조준 대상
    public IEnemyTargetable FindAttackTarget(float range, IEnemyTargetable priorityTarget = null)
    {
        IEnemyTargetable lockTarget = LockTarget;

        if(lockTarget != null) return lockTarget;
        if(priorityTarget != null) return priorityTarget;

        return FindTarget(range);
    }

    // 앞쪽 적 중 카메라 중앙 레이와 가장 가까운 적을 우선하고, 앞쪽에 없을 때만 등 뒤의 가장 가까운 적을 고른다.
    // 등 뒤의 적은 레이와의 거리가 거의 0 이라 같은 기준으로 비교하면 앞의 적보다 먼저 뽑히기 때문이다.
    // 벽에 막힌 적은 다가가면 벽에 박히므로 고르지 않는다.
    public IEnemyTargetable FindTarget(float range, bool onlyOnScreen = false)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, range, _candidateColliders, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        Vector3 rayOrigin = Camera.transform.position;
        Vector3 rayDirection = Camera.transform.forward;
        IEnemyTargetable frontEnemy = null;
        IEnemyTargetable backEnemy = null;
        float frontSqrDistance = float.MaxValue;
        float backSqrDistance = float.MaxValue;

        for(int i = 0; i < count; i++)
        {
            IEnemyTargetable enemy = _candidateColliders[i].GetComponentInParent<IEnemyTargetable>();

            if(enemy == null || !enemy.IsAlive) continue;

            Vector3 enemyCenter = _candidateColliders[i].bounds.center;

            if(onlyOnScreen && !IsOnScreen(enemyCenter)) continue;

            if(IsBlocked(enemyCenter)) continue;

            // 크기 비교만 하므로 제곱근 없이 거리의 제곱으로 비교한다.
            if(Vector3.Dot(rayDirection, enemyCenter - transform.position) < 0f)
            {
                float sqrDistanceToPlayer = (enemyCenter - transform.position).sqrMagnitude;

                if(sqrDistanceToPlayer >= backSqrDistance) continue;

                backSqrDistance = sqrDistanceToPlayer;
                backEnemy = enemy;
                continue;
            }

            // 레이 방향(길이 1)과의 외적 크기가 레이까지의 수직 거리다.
            float sqrDistanceToRay = Vector3.Cross(rayDirection, enemyCenter - rayOrigin).sqrMagnitude;

            if(sqrDistanceToRay >= frontSqrDistance) continue;

            frontSqrDistance = sqrDistanceToRay;
            frontEnemy = enemy;
        }

        return frontEnemy != null ? frontEnemy : backEnemy;
    }

    // 플레이어 가슴 높이에서 선을 그어 장애물 Layer 에 닿으면 막힌 것으로 본다.
    public bool IsBlocked(Vector3 position)
    {
        Vector3 sightOrigin = transform.position + Vector3.up * Common.SightHeight;

        return Physics.Linecast(sightOrigin, position, Common.ObstacleLayers, QueryTriggerInteraction.Ignore);
    }

    // Viewport z 가 0 이하면 카메라 뒤에 있다.
    private bool IsOnScreen(Vector3 position)
    {
        Vector3 viewport = Camera.WorldToViewportPoint(position);

        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    // 공격 사거리(노랑) · 록온 사거리(하늘)와, 플레이 중이면 지금 공격할 대상까지 선을 그린다.
    // 실행 중이 아니면 파티가 아직 자신을 알려 주지 않았으므로 씬의 파티를 직접 찾는다.
    private void DrawRange()
    {
        PlayerParty party = Application.isPlaying ? GetComponent<PlayerController>().Party : FindAnyObjectByType<PlayerParty>();

        if(party == null || party.Common == null) return;

        CommonCharacterSettings common = party.Common;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, common.AttackRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, common.LockOnRange);

        if(!Application.isPlaying) return;

        IEnemyTargetable target = FindTarget(common.AttackRange);

        if(target == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.transform.position);
    }

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
    }
}

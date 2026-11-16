using UnityEngine;

// 이 캐릭터를 기준으로 공격 · 록온 대상을 찾는다. 대상 찾기는 캐릭터 위치에 따라 달라 캐릭터마다 둔다.
// 록온 상태(대상 · 표시 · 록온 입력)는 플레이어가 고른 대상 하나라 파티의 록온 기능(PlayerLockOn)이 들고, 여기서는 읽기만 한다.
// 범위 안의 플레이어 앞쪽 적 중 카메라 중앙 레이와의 수직 거리가 가장 짧은 적을 대상으로 고른다.
// 같은 화면 위치라면 가까운 적일수록 레이와의 거리가 짧아 근접 공격 대상으로 우선된다.
// 사거리 · 가림 판정 값은 공통 설정에서, 카메라는 파티에서 읽는다.
[RequireComponent(typeof(PlayerController))]
public class PlayerTargeting : MonoBehaviour
{
    // 한 번의 대상 찾기에서 검사할 수 있는 최대 콜라이더 수
    private const int MAX_CANDIDATE_COLLIDERS = 32;

    private readonly Collider[] _candidateColliders = new Collider[MAX_CANDIDATE_COLLIDERS];
    private PlayerController _controller;

    // 파티의 록온 대상. 교체해도 같은 대상이다.
    public IEnemyTargetable LockTarget => _controller.Party.LockOn.LockTarget;
    public float AttackRange => Common.AttackRange;
    public float LockOnRange => Common.LockOnRange;
    private CommonCharacterSettings Common => _controller.Common;
    private Camera Camera => _controller.Party.MainCamera;

    private void Awake() => CacheComponent();
    private void OnDrawGizmosSelected() => DrawRange();

    // 공격 대상을 고른다. 록온 대상 → 우선 대상 → range 안의 자동 조준 대상 순서다. 없으면 null 이다.
    // 공격 · 필살기 · 그로기 테스트처럼 "누구를 노릴지"는 모두 이 순서를 따른다.
    // priorityTarget : 록온 중이 아닐 때 자동 조준보다 먼저 노릴 대상. 패링 추격은 패링한 적을 넘긴다.
    public IEnemyTargetable FindAttackTarget(float range, IEnemyTargetable priorityTarget = null)
    {
        IEnemyTargetable lockTarget = LockTarget;

        if(lockTarget != null) return lockTarget;
        if(priorityTarget != null) return priorityTarget;

        return FindTarget(range);
    }

    // 플레이어 기준 range 안의 적을 찾아 돌려준다. 없으면 null 이다.
    // 플레이어 앞쪽(카메라가 보는 방향 기준)의 적 중 카메라 중앙 레이와 가장 가까운 적을 우선한다.
    // 앞쪽에 적이 없을 때만 등 뒤의 적 중 플레이어와 가장 가까운 적을 고른다.
    // 등 뒤의 적은 카메라와 플레이어 사이에 있어 레이와의 거리가 거의 0 이 되므로, 같은 기준으로 비교하면 앞의 적보다 먼저 뽑히기 때문이다.
    // 플레이어와 적 사이가 벽으로 막힌 적은 고르지 않는다. 막힌 적에게 다가가면 벽에 박히기 때문이다.
    // onlyOnScreen : true 면 중심이 화면 안에 보이는 적만 고른다. 록온에 쓴다.
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

    // 플레이어 가슴 높이에서 적 중심까지 선을 그어 장애물 Layer 에 닿으면 막힌 것으로 본다.
    // 파티의 록온 기능도 록온 대상이 가려졌는지 이 캐릭터 기준으로 확인할 때 쓴다.
    public bool IsBlocked(Vector3 position)
    {
        Vector3 sightOrigin = transform.position + Vector3.up * Common.SightHeight;

        return Physics.Linecast(sightOrigin, position, Common.ObstacleLayers, QueryTriggerInteraction.Ignore);
    }

    // 화면 좌표(Viewport)는 화면 왼쪽 아래가 (0, 0), 오른쪽 위가 (1, 1) 이고, z 는 카메라 앞쪽 거리다.
    // z 가 0 이하면 카메라 뒤에 있다는 뜻이다.
    private bool IsOnScreen(Vector3 position)
    {
        Vector3 viewport = Camera.WorldToViewportPoint(position);

        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    // Scene 창에서 플레이어를 선택하면 공격 사거리(노랑)와 록온 사거리(하늘)를 보여준다.
    // 플레이 중에는 지금 공격하면 선택될 대상까지 선을 긋는다.
    // 에디터에서는 파티가 캐릭터에 자신을 알려 주기 전이므로, 실행 중이 아니면 씬의 파티를 직접 찾는다. 파티나 공통 설정이 없으면 그리지 않는다.
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
